using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Prophet.Client.Database.Repositories;

/// <summary>
/// Repository 基类
/// 提供通用的 CRUD 操作，减少代码重复
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TId">主键类型</typeparam>
public abstract class BaseRepository<TEntity, TId> where TEntity : class
{
    /// <summary>
    /// 表名（子类必须实现）
    /// </summary>
    protected abstract string TableName { get; }
    
    /// <summary>
    /// 主键列名（默认为 "id"）
    /// </summary>
    protected virtual string IdColumnName => "id";

    // 表名/主键列在首次使用时校验并加引号：子类常量若不合法直接抛错，
    // 避免标识符拼进 SQL 造成注入或关键字冲突。
    private string? _quotedTable;
    private string QuotedTable => _quotedTable ??= $"\"{DBHelper.ValidateIdentifier(TableName)}\"";
    private string? _quotedIdColumn;
    private string QuotedIdColumn => _quotedIdColumn ??= $"\"{DBHelper.ValidateIdentifier(IdColumnName, "idColumnName")}\"";
    
    /// <summary>
    /// 创建数据库连接
    /// </summary>
    protected IDbConnection CreateConnection()
    {
        return DBHelper.CreateConnection();
    }

    /// <summary>
    /// 根据ID查询单条记录
    /// </summary>
    public virtual async Task<TEntity?> GetByIdAsync(TId id)
    {
        using var connection = CreateConnection();
        var sql = $"SELECT * FROM {QuotedTable} WHERE {QuotedIdColumn} = @id";
        return await connection.QueryFirstOrDefaultAsync<TEntity>(sql, new { id });
    }

    /// <summary>
    /// 查询所有记录
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = $"SELECT * FROM {QuotedTable}";
        return await connection.QueryAsync<TEntity>(sql);
    }

    /// <summary>
    /// 查询记录数量
    /// </summary>
    public virtual async Task<int> CountAsync()
    {
        using var connection = CreateConnection();
        var sql = $"SELECT COUNT(*) FROM {QuotedTable}";
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    /// <summary>
    /// 条件查询
    /// </summary>
    /// <param name="whereClause">WHERE 子句（不含 WHERE 关键字；必须是代码内常量，值一律走 parameters 参数化，禁止拼外部输入）</param>
    /// <param name="parameters">参数对象</param>
    public virtual async Task<IEnumerable<TEntity>> QueryAsync(string whereClause, object? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            throw new ArgumentException("WHERE 子句不能为空", nameof(whereClause));

        using var connection = CreateConnection();
        var sql = $"SELECT * FROM {QuotedTable} WHERE {whereClause}";
        return await connection.QueryAsync<TEntity>(sql, parameters);
    }

    /// <summary>
    /// 条件查询单条记录
    /// </summary>
    public virtual async Task<TEntity?> QueryFirstOrDefaultAsync(string whereClause, object? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            throw new ArgumentException("WHERE 子句不能为空", nameof(whereClause));

        using var connection = CreateConnection();
        var sql = $"SELECT * FROM {QuotedTable} WHERE {whereClause}";
        return await connection.QueryFirstOrDefaultAsync<TEntity>(sql, parameters);
    }

    /// <summary>
    /// 插入记录
    /// </summary>
    /// <param name="entity">实体对象</param>
    /// <returns>影响的行数</returns>
    public virtual async Task<int> InsertAsync(TEntity entity)
    {
        using var connection = CreateConnection();
        
        // 获取实体的所有属性（除了 ID 如果是自增的话）
        var properties = typeof(TEntity).GetProperties();
        var columns = new List<string>();
        var values = new List<string>();
        
        foreach (var prop in properties)
        {
            columns.Add($"\"{DBHelper.ValidateIdentifier(prop.Name, "columnName")}\"");
            values.Add($"@{prop.Name}");
        }
        
        var sql = $"INSERT INTO {QuotedTable} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        return await connection.ExecuteAsync(sql, entity);
    }

    /// <summary>
    /// 更新记录
    /// </summary>
    public virtual async Task<int> UpdateAsync(TEntity entity, TId id)
    {
        using var connection = CreateConnection();
        
        var properties = typeof(TEntity).GetProperties();
        var setClauses = new List<string>();
        
        foreach (var prop in properties)
        {
            if (prop.Name != IdColumnName)
            {
                setClauses.Add($"\"{DBHelper.ValidateIdentifier(prop.Name, "columnName")}\" = @{prop.Name}");
            }
        }
        
        var sql = $"UPDATE {QuotedTable} SET {string.Join(", ", setClauses)} WHERE {QuotedIdColumn} = @id";
        
        // 创建参数对象（包含实体属性 + id）
        var parameters = new DynamicParameters(entity);
        parameters.Add("id", id);
        
        return await connection.ExecuteAsync(sql, parameters);
    }

    /// <summary>
    /// 删除记录
    /// </summary>
    public virtual async Task<int> DeleteAsync(TId id)
    {
        using var connection = CreateConnection();
        var sql = $"DELETE FROM {QuotedTable} WHERE {QuotedIdColumn} = @id";
        return await connection.ExecuteAsync(sql, new { id });
    }

    /// <summary>
    /// 条件删除
    /// </summary>
    public virtual async Task<int> DeleteWhereAsync(string whereClause, object? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            throw new ArgumentException("WHERE 子句不能为空（禁止无条件全表删除）", nameof(whereClause));

        using var connection = CreateConnection();
        var sql = $"DELETE FROM {QuotedTable} WHERE {whereClause}";
        return await connection.ExecuteAsync(sql, parameters);
    }

    /// <summary>
    /// 检查记录是否存在
    /// </summary>
    public virtual async Task<bool> ExistsAsync(TId id)
    {
        using var connection = CreateConnection();
        var sql = $"SELECT COUNT(*) FROM {QuotedTable} WHERE {QuotedIdColumn} = @id";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { id });
        return count > 0;
    }

    /// <summary>
    /// 批量插入（事务）
    /// </summary>
    public virtual async Task<int> BulkInsertAsync(IEnumerable<TEntity> entities)
    {
        using var connection = CreateConnection();
        
        var properties = typeof(TEntity).GetProperties();
        var columns = new List<string>();
        var values = new List<string>();
        
        foreach (var prop in properties)
        {
            columns.Add($"\"{DBHelper.ValidateIdentifier(prop.Name, "columnName")}\"");
            values.Add($"@{prop.Name}");
        }
        
        var sql = $"INSERT INTO {QuotedTable} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        
        using var transaction = connection.BeginTransaction();
        try
        {
            var count = 0;
            foreach (var entity in entities)
            {
                count += await connection.ExecuteAsync(sql, entity, transaction);
            }
            transaction.Commit();
            return count;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 执行自定义 SQL（查询）
    /// </summary>
    protected async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sql, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<T>(sql, parameters);
    }

    /// <summary>
    /// 执行自定义 SQL（非查询）
    /// </summary>
    protected async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, parameters);
    }

    /// <summary>
    /// 执行自定义 SQL（返回标量值）
    /// </summary>
    protected async Task<T> ExecuteScalarAsync<T>(string sql, object? parameters = null)
    {
        using var connection = CreateConnection();
        var result = await connection.ExecuteScalarAsync<T>(sql, parameters);
        return result ?? throw new InvalidOperationException("查询返回 null 值");
    }
}

/// <summary>
/// 只读 Repository 基类（不提供增删改操作）
/// </summary>
public abstract class ReadOnlyRepository<TEntity, TId> : BaseRepository<TEntity, TId> where TEntity : class
{
    public new Task<int> InsertAsync(TEntity entity)
    {
        throw new NotSupportedException("此 Repository 不支持插入操作");
    }

    public new Task<int> UpdateAsync(TEntity entity, TId id)
    {
        throw new NotSupportedException("此 Repository 不支持更新操作");
    }

    public new Task<int> DeleteAsync(TId id)
    {
        throw new NotSupportedException("此 Repository 不支持删除操作");
    }
}

