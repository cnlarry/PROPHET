using System;
using System.Threading.Tasks;
using Dapper;

namespace Prophet.Client.Database;

/// <summary>
/// 数据库版本管理
/// 记录和跟踪数据库 schema 版本
/// </summary>
public static class DatabaseVersionManager
{
    /// <summary>
    /// 当前 Schema 版本号
    /// 格式: Major.Minor.Patch
    /// 
    /// 版本升级规则:
    /// - Major: 不兼容的重大结构变更（删除表、删除列）
    /// - Minor: 向下兼容的功能增强（新增表、新增列）
    /// - Patch: 向下兼容的Bug修复（修改索引、优化查询）
    /// </summary>
    public const string CurrentSchemaVersion = "1.4.0";
    
    /// <summary>
    /// Schema 版本历史
    /// </summary>
    private static readonly (string Version, string Description, DateTime ReleaseDate)[] VersionHistory =
    {
        ("1.0.0", "初始版本", new DateTime(2024, 12, 1)),
        ("1.1.0", "添加策略版本管理", new DateTime(2024, 12, 15)),
        ("1.2.0", "添加回测信号表", new DateTime(2024, 12, 20)),
        ("1.3.0", "Symbol 升级为 symbol_key（instruments 外键）", new DateTime(2024, 12, 31)),
        ("1.4.0", "补全所有缺失表（集中式迁移完成）", new DateTime(2026, 1, 4)),
    };
    
    /// <summary>
    /// App Settings 中的版本键
    /// </summary>
    private const string VersionKey = "db_schema_version";
    
    /// <summary>
    /// 获取数据库中记录的 Schema 版本
    /// </summary>
    public static async Task<string> GetDatabaseVersionAsync()
    {
        try
        {
            using var connection = DBHelper.CreateConnection();
            
            // 检查 app_settings 表是否存在
            var tableExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='app_settings'"
            );
            
            if (tableExists == 0)
            {
                return "0.0.0"; // 表不存在，说明是全新数据库
            }
            
            var version = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT value FROM app_settings WHERE key = @key",
                new { key = VersionKey }
            );
            
            return version ?? "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }
    
    /// <summary>
    /// 设置数据库 Schema 版本
    /// </summary>
    public static async Task SetDatabaseVersionAsync(string version)
    {
        using var connection = DBHelper.CreateConnection();
        
        await connection.ExecuteAsync(@"
            INSERT OR REPLACE INTO app_settings (key, value, description, updated_at)
            VALUES (@key, @value, @description, datetime('now', 'localtime'))",
            new
            {
                key = VersionKey,
                value = version,
                description = "数据库 Schema 版本号"
            }
        );
    }
    
    /// <summary>
    /// 更新数据库版本到当前版本
    /// </summary>
    public static async Task UpdateToCurrentVersionAsync()
    {
        await SetDatabaseVersionAsync(CurrentSchemaVersion);
    }
    
    /// <summary>
    /// 检查是否需要升级
    /// </summary>
    public static async Task<bool> RequiresUpgradeAsync()
    {
        var dbVersion = await GetDatabaseVersionAsync();
        
        // 解析版本号
        if (!TryParseVersion(dbVersion, out var dbVer) ||
            !TryParseVersion(CurrentSchemaVersion, out var currentVer))
        {
            return true; // 无法解析版本号，假定需要升级
        }
        
        return dbVer < currentVer;
    }
    
    /// <summary>
    /// 获取版本信息报告
    /// </summary>
    public static async Task<string> GetVersionReportAsync()
    {
        var dbVersion = await GetDatabaseVersionAsync();
        var requiresUpgrade = await RequiresUpgradeAsync();
        
        var report = new System.Text.StringBuilder();
        report.AppendLine("=".PadRight(60, '='));
        report.AppendLine("数据库版本信息");
        report.AppendLine("=".PadRight(60, '='));
        report.AppendLine();
        report.AppendLine($"当前 Schema 版本: {CurrentSchemaVersion}");
        report.AppendLine($"数据库记录版本: {dbVersion}");
        report.AppendLine($"需要升级: {(requiresUpgrade ? "是 ⚠️" : "否 ✅")}");
        report.AppendLine();
        
        if (requiresUpgrade)
        {
            report.AppendLine("⚠️ 数据库版本落后，建议执行迁移升级");
            report.AppendLine();
        }
        
        report.AppendLine("【版本历史】");
        foreach (var (version, description, releaseDate) in VersionHistory)
        {
            var marker = version == CurrentSchemaVersion ? " ← 当前" : "";
            report.AppendLine($"  v{version} - {description} ({releaseDate:yyyy-MM-dd}){marker}");
        }
        
        report.AppendLine("=".PadRight(60, '='));
        
        return report.ToString();
    }
    
    /// <summary>
    /// 解析版本号字符串
    /// </summary>
    private static bool TryParseVersion(string versionString, out Version version)
    {
        version = new Version(0, 0, 0);
        
        if (string.IsNullOrWhiteSpace(versionString))
            return false;
        
        try
        {
            version = new Version(versionString);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 获取版本变更日志
    /// </summary>
    public static string GetChangeLog(string fromVersion, string toVersion)
    {
        if (!TryParseVersion(fromVersion, out var from) ||
            !TryParseVersion(toVersion, out var to))
        {
            return "无法解析版本号";
        }
        
        var changes = new System.Text.StringBuilder();
        changes.AppendLine($"从 v{fromVersion} 升级到 v{toVersion}:");
        changes.AppendLine();
        
        foreach (var (version, description, _) in VersionHistory)
        {
            if (TryParseVersion(version, out var ver) && ver > from && ver <= to)
            {
                changes.AppendLine($"  - v{version}: {description}");
            }
        }
        
        return changes.ToString();
    }
}

