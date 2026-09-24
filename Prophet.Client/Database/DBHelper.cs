using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Prophet.Client.Database.Migrations;

namespace Prophet.Client.Database;

/// <summary>
/// SQLite数据库辅助类
/// 提供连接管理、事务支持、初始化等核心功能
/// </summary>
public static class DBHelper
{
    private static string? _connectionString;
    private static readonly object _lock = new object();

    /// <summary>
    /// 获取数据库连接字符串
    /// </summary>
    public static string ConnectionString
    {
        get
        {
            if (_connectionString == null)
            {
                lock (_lock)
                {
                    if (_connectionString == null)
                    {
                        var dbPath = DatabasePath;  // 使用统一的 DatabasePath 属性
                        _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
                    }
                }
            }
            return _connectionString;
        }
    }

    /// <summary>
    /// 获取数据库文件路径
    /// 数据库路径统一由 AppConfig 管理
    /// </summary>
    public static string DatabasePath => Core.AppConfig.Database.LocalDatabasePath;

    /// <summary>
    /// 设置自定义连接字符串（用于测试）
    /// </summary>
    public static void SetConnectionString(string connectionString)
    {
        lock (_lock)
        {
            _connectionString = connectionString;
        }
    }

    /// <summary>
    /// 创建新的数据库连接
    /// </summary>
    public static IDbConnection CreateConnection()
    {
        return CreateConnection(DatabasePath);
    }

    /// <summary>
    /// 创建新的数据库连接（指定数据库文件路径）
    /// </summary>
    public static IDbConnection CreateConnection(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径不能为空", nameof(databasePath));
        }

        var connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        // 启用外键约束
        connection.Execute("PRAGMA foreign_keys = ON;");

        // 使用WAL模式提升并发性能
        connection.Execute("PRAGMA journal_mode = WAL;");

        return connection;
    }

    /// <summary>
    /// 初始化数据库（仅验证连接，不创建或修改数据）
    /// 注意：PROPHET.db 由用户手动维护，此方法仅验证数据库是否可访问
    /// </summary>
    public static Task InitializeDatabaseAsync()
    {
        try
        {
            
            // 检查数据库文件是否存在
            var dbPath = DatabasePath;
            var dbExists = File.Exists(dbPath);
            
            if (!dbExists)
            {
                // 不自动创建数据库，让SQLite在首次连接时创建空文件
                // 用户应该手动放置 PROPHET.db 文件
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 数据库验证失败: {ex.Message}");
            Console.WriteLine($"   请检查 PROPHET.db 文件是否正确");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// 应用数据库迁移（幂等）
    /// </summary>
    public static async Task ApplyMigrationsAsync(IEnumerable<DbMigration> migrations)
    {
        if (migrations == null) throw new ArgumentNullException(nameof(migrations));

        using var connection = CreateConnection();

        await EnsureSchemaMigrationsTableAsync(connection);

        foreach (var migration in migrations)
        {
            if (string.IsNullOrWhiteSpace(migration.Id))
            {
                throw new InvalidOperationException("迁移 Id 不能为空");
            }

            var alreadyApplied = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(1) FROM schema_migrations WHERE id = @id",
                new { id = migration.Id }
            );

            if (alreadyApplied > 0)
            {
                continue;
            }

            Console.WriteLine($"🔧 [DB] Applying migration: {migration.Id} - {migration.Description}");

            try
            {
                if (migration.DisableForeignKeys)
                {
                    await connection.ExecuteAsync("PRAGMA foreign_keys = OFF;");
                }

                IDbTransaction? tx = null;
                try
                {
                    tx = connection.BeginTransaction();

                    // 执行迁移SQL（允许多语句）
                    await connection.ExecuteAsync(migration.Sql, transaction: tx);

                    await connection.ExecuteAsync(
                        "INSERT INTO schema_migrations (id, description) VALUES (@id, @description)",
                        new { id = migration.Id, description = migration.Description },
                        tx
                    );

                    tx.Commit();
                }
                catch
                {
                    try { tx?.Rollback(); } catch { }
                    throw;
                }
                finally
                {
                    tx?.Dispose();
                }

                if (migration.DisableForeignKeys)
                {
                    await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
                }

                Console.WriteLine($"✅ [DB] Migration applied: {migration.Id}");
            }
            catch (Exception ex)
            {
                if (migration.DisableForeignKeys)
                {
                    try { await connection.ExecuteAsync("PRAGMA foreign_keys = ON;"); } catch { }
                }

                Console.WriteLine($"❌ [DB] Migration failed: {migration.Id} - {ex.Message}");
                throw;
            }
        }
    }

    private static async Task EnsureSchemaMigrationsTableAsync(IDbConnection connection)
    {
        // 1) schema_migrations 不存在：直接创建
        var exists = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='schema_migrations'"
        );

        if (exists == 0)
        {
            await connection.ExecuteAsync(@"
CREATE TABLE schema_migrations (
    id TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    applied_at TEXT NOT NULL DEFAULT (DATETIME('now', 'localtime'))
);");
            return;
        }

        // 2) 存在但结构不符合：重命名旧表并重建
        var columns = (await connection.QueryAsync<string>(
                "SELECT name FROM pragma_table_info('schema_migrations')"
            ))
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (columns.Contains("id"))
        {
            return;
        }

        var legacyName = $"schema_migrations_legacy_{DateTime.UtcNow:yyyyMMddHHmmss}";
        Console.WriteLine($"⚠️ [DB] schema_migrations 结构异常（缺少 id 列），将重命名为 {legacyName} 并重建");

        await connection.ExecuteAsync($"ALTER TABLE schema_migrations RENAME TO {legacyName};");

        await connection.ExecuteAsync(@"
CREATE TABLE schema_migrations (
    id TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    applied_at TEXT NOT NULL DEFAULT (DATETIME('now', 'localtime'))
);");
    }

    /// <summary>
    /// 获取数据库版本
    /// </summary>
    public static async Task<string> GetDatabaseVersionAsync()
    {
        try
        {
            using var connection = CreateConnection();
            
            // 检查 app_settings 表是否存在
            var tableExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='app_settings'");
            
            if (tableExists == 0)
                return "0.0.0";
            
            var version = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT value FROM app_settings WHERE key = 'db_version'");
            
            return version ?? "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }

    /// <summary>
    /// 执行查询（返回多行）
    /// </summary>
    public static async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, param);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询失败: {ex.Message}");
            Console.WriteLine($"   SQL: {sql}");
            throw;
        }
    }

    /// <summary>
    /// 执行查询（返回单行）
    /// </summary>
    public static async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询失败: {ex.Message}");
            Console.WriteLine($"   SQL: {sql}");
            throw;
        }
    }

    /// <summary>
    /// 执行命令（INSERT/UPDATE/DELETE）
    /// </summary>
    public static async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, param);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 执行命令失败: {ex.Message}");
            Console.WriteLine($"   SQL: {sql}");
            throw;
        }
    }

    /// <summary>
    /// 执行标量查询（返回单个值）
    /// </summary>
    public static async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<T>(sql, param);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 执行标量查询失败: {ex.Message}");
            Console.WriteLine($"   SQL: {sql}");
            throw;
        }
    }

    /// <summary>
    /// 在事务中执行操作
    /// </summary>
    public static async Task<T> ExecuteInTransactionAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> action)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var result = await action(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 事务执行失败，回滚: {ex.Message}");
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 在事务中执行操作（无返回值）
    /// </summary>
    public static async Task ExecuteInTransactionAsync(Func<IDbConnection, IDbTransaction, Task> action)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            await action(connection, transaction);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 事务执行失败，回滚: {ex.Message}");
            transaction.Rollback();
            throw;
        }
    }

    // 表名/列名只能是标识符（字母/数字/下划线，数字不开头），拼 SQL 前强制校验，
    // 调用方传外部输入（如品种名、策略名）时必须先过此关，防注入。
    private static readonly Regex SafeIdentifierPattern =
        new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static string ValidateIdentifier(string identifier, string paramName = "tableName")
    {
        if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
        {
            throw new ArgumentException($"非法的数据表/列标识符: {identifier}", paramName);
        }

        return identifier;
    }

    /// <summary>
    /// 批量插入数据
    /// </summary>
    public static async Task<int> BulkInsertAsync<T>(string tableName, IEnumerable<T> items)
    {
        ValidateIdentifier(tableName);

        if (!items.Any())
            return 0;

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var type = typeof(T);
            var properties = type.GetProperties()
                .Where(p => p.CanRead && p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute), true).Length == 0)
                .ToList();
            
            var columnNames = string.Join(", ", properties.Select(p => $"\"{ValidateIdentifier(p.Name, "columnName")}\""));
            var paramNames = string.Join(", ", properties.Select(p => $"@{p.Name}"));
            
            var sql = $"INSERT INTO \"{tableName}\" ({columnNames}) VALUES ({paramNames})";
            
            var count = await connection.ExecuteAsync(sql, items, transaction);
            
            transaction.Commit();
            
            Console.WriteLine($"✅ 批量插入 {count} 条记录到 {tableName}");
            
            return count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 批量插入失败: {ex.Message}");
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 清空表数据
    /// </summary>
    public static async Task<int> TruncateTableAsync(string tableName)
    {
        ValidateIdentifier(tableName);

        try
        {
            Console.WriteLine($"🗑️ 清空表: {tableName}");
            using var connection = CreateConnection();
            return await connection.ExecuteAsync($"DELETE FROM \"{tableName}\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 清空表失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    public static async Task<bool> TableExistsAsync(string tableName)
    {
        using var connection = CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName",
            new { tableName });
        return count > 0;
    }

    /// <summary>
    /// 获取表的行数
    /// </summary>
    public static async Task<long> GetTableRowCountAsync(string tableName)
    {
        ValidateIdentifier(tableName);

        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM \"{tableName}\"");
    }

    /// <summary>
    /// 备份数据库
    /// </summary>
    public static async Task BackupDatabaseAsync(string backupPath)
    {
        try
        {
            Console.WriteLine($"💾 备份数据库到: {backupPath}");
            
            var sourcePath = DatabasePath;
            
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("数据库文件不存在", sourcePath);
            }
            
            // 确保备份目录存在
            var backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }
            
            // 复制文件
            await Task.Run(() => File.Copy(sourcePath, backupPath, true));
            
            Console.WriteLine($"✅ 数据库备份成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 数据库备份失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 恢复数据库
    /// </summary>
    public static async Task RestoreDatabaseAsync(string backupPath)
    {
        try
        {
            Console.WriteLine($"📂 从备份恢复数据库: {backupPath}");
            
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("备份文件不存在", backupPath);
            }
            
            var targetPath = DatabasePath;
            
            // 复制文件
            await Task.Run(() => File.Copy(backupPath, targetPath, true));
            
            Console.WriteLine($"✅ 数据库恢复成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 数据库恢复失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 优化数据库（VACUUM）
    /// </summary>
    public static async Task VacuumDatabaseAsync()
    {
        try
        {
            Console.WriteLine("🔧 优化数据库...");
            using var connection = CreateConnection();
            await connection.ExecuteAsync("VACUUM");
            Console.WriteLine("✅ 数据库优化完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 数据库优化失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取数据库文件大小（字节）
    /// </summary>
    public static long GetDatabaseSize()
    {
        var dbPath = DatabasePath;
        if (File.Exists(dbPath))
        {
            return new FileInfo(dbPath).Length;
        }
        return 0;
    }

    /// <summary>
    /// 获取数据库文件大小（格式化字符串）
    /// </summary>
    public static string GetDatabaseSizeFormatted()
    {
        var bytes = GetDatabaseSize();
        
        if (bytes < 1024)
            return $"{bytes} B";
        
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F2} KB";
        
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

