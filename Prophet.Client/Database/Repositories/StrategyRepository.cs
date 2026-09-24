using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Prophet.Client.Models;

namespace Prophet.Client.Database.Repositories;

/// <summary>
/// 策略数据访问实现
/// </summary>
public class StrategyRepository : IStrategyRepository
{
    private readonly StrategyVersionRepository _versionRepository;

    public StrategyRepository() : this(null)
    {
    }

    /// <summary>
    /// 依赖注入构造函数（避免仓库内嵌套 new 仓库，形成多实例）
    /// </summary>
    public StrategyRepository(StrategyVersionRepository? versionRepository = null)
    {
        _versionRepository = versionRepository ?? new StrategyVersionRepository();
    }
    /// <summary>
    /// 获取所有策略
    /// </summary>
    public async Task<List<StrategyInfo>> GetAllStrategiesAsync()
    {
        const string sql = @"
            SELECT 
                id AS Id,
                name AS Name,
                dsl AS Dsl,
                remarks AS Remarks,
                description AS Description,
                strategy_type AS StrategyType,
                risk_level AS RiskLevel,
                quality_score AS QualityScore,
                status AS Status,
                status_changed_at AS StatusChangedAt,
                anomaly_reason AS AnomalyReason,
                enabled AS Enabled,
                public AS Public,
                total_subscribers AS TotalSubscribers,
                total_backtest_count AS TotalBacktestCount,
                last_backtest_at AS LastBacktestAt,
                current_version_string AS Version,
                last_compiled_at AS LastCompiledAt,
                updated_at AS UpdatedAt,
                created_at
            FROM strategies
            ORDER BY updated_at DESC";

        var strategies = await DBHelper.QueryAsync<StrategyInfo>(sql);
        return strategies.ToList();
    }

    /// <summary>
    /// 根据ID获取策略
    /// </summary>
    public async Task<StrategyInfo?> GetStrategyByIdAsync(string id)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                name AS Name,
                dsl AS Dsl,
                remarks AS Remarks,
                description AS Description,
                strategy_type AS StrategyType,
                risk_level AS RiskLevel,
                quality_score AS QualityScore,
                status AS Status,
                status_changed_at AS StatusChangedAt,
                anomaly_reason AS AnomalyReason,
                enabled AS Enabled,
                public AS Public,
                total_subscribers AS TotalSubscribers,
                total_backtest_count AS TotalBacktestCount,
                last_backtest_at AS LastBacktestAt,
                current_version_string AS Version,
                last_compiled_at AS LastCompiledAt,
                updated_at AS UpdatedAt,
                created_at
            FROM strategies
            WHERE id = @id";

        return await DBHelper.QueryFirstOrDefaultAsync<StrategyInfo>(sql, new { id });
    }

    /// <summary>
    /// 根据状态获取策略
    /// </summary>
    public async Task<List<StrategyInfo>> GetStrategiesByStatusAsync(string status)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                name AS Name,
                dsl AS Dsl,
                remarks AS Remarks,
                description AS Description,
                strategy_type AS StrategyType,
                risk_level AS RiskLevel,
                quality_score AS QualityScore,
                status AS Status,
                status_changed_at AS StatusChangedAt,
                anomaly_reason AS AnomalyReason,
                enabled AS Enabled,
                public AS Public,
                total_subscribers AS TotalSubscribers,
                total_backtest_count AS TotalBacktestCount,
                last_backtest_at AS LastBacktestAt,
                current_version_string AS Version,
                last_compiled_at AS LastCompiledAt,
                updated_at AS UpdatedAt,
                created_at
            FROM strategies
            WHERE status = @status
            ORDER BY updated_at DESC";

        var strategies = await DBHelper.QueryAsync<StrategyInfo>(sql, new { status });
        return strategies.ToList();
    }

    /// <summary>
    /// 创建新策略
    /// </summary>
    public async Task<string> CreateStrategyAsync(StrategyInfo strategy)
    {
        // 生成新ID
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        const string sql = @"
            INSERT INTO strategies (
                id, name, dsl, remarks, description,
                strategy_type, risk_level, quality_score,
                status, status_changed_at, anomaly_reason,
                enabled, public, total_subscribers, total_backtest_count,
                last_backtest_at, current_version_string, last_compiled_at,
                created_at, updated_at
            ) VALUES (
                @id, @name, @dsl, @remarks, @description,
                @strategyType, @riskLevel, @qualityScore,
                @status, @statusChangedAt, @anomalyReason,
                @enabled, @public, @totalSubscribers, @totalBacktestCount,
                @lastBacktestAt, @version, @lastCompiledAt,
                @createdAt, @updatedAt
            )";

        await DBHelper.ExecuteAsync(sql, new
        {
            id,
            name = strategy.Name,
            dsl = strategy.Dsl,
            remarks = strategy.Remarks,
            description = strategy.Description,
            strategyType = strategy.StrategyType,
            riskLevel = strategy.RiskLevel,
            qualityScore = strategy.QualityScore,
            status = strategy.Status,
            statusChangedAt = strategy.StatusChangedAt,
            anomalyReason = strategy.AnomalyReason,
            enabled = strategy.Enabled ? 1 : 0,
            @public = strategy.Public ? 1 : 0,
            totalSubscribers = strategy.TotalSubscribers,
            totalBacktestCount = strategy.TotalBacktestCount,
            lastBacktestAt = strategy.LastBacktestAt,
            version = "v1.0.0",  // 固定为 v1.0.0
            lastCompiledAt = now,
            createdAt = now,
            updatedAt = now
        });

        Console.WriteLine($"✅ 创建策略成功: ID={id}, Name={strategy.Name}");

        // 创建初始版本记录（v1.0.0）
        try
        {
            await _versionRepository.CreateInitialVersionAsync(id, strategy.Dsl, "user");
            Console.WriteLine($"✅ 创建初始版本成功: StrategyId={id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建初始版本失败: {ex.Message}");
            // 即使版本创建失败，策略也已经创建成功，所以不抛出异常
        }

        return id;
    }

    /// <summary>
    /// 更新策略
    /// </summary>
    public async Task<bool> UpdateStrategyAsync(StrategyInfo strategy)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        // 先获取最新版本号
        var latestVersion = await _versionRepository.GetLatestVersionAsync(strategy.Id);
        string newVersionString;
        
        if (latestVersion != null)
        {
            // 版本号递增（patch版本+1）
            int newPatch = latestVersion.PatchVersion + 1;
            newVersionString = $"v{latestVersion.MajorVersion}.{latestVersion.MinorVersion}.{newPatch}";
        }
        else
        {
            // 如果没有版本记录，使用 v1.0.0
            newVersionString = "v1.0.0";
        }

        const string sql = @"
            UPDATE strategies SET
                name = @name,
                dsl = @dsl,
                remarks = @remarks,
                description = @description,
                strategy_type = @strategyType,
                risk_level = @riskLevel,
                quality_score = @qualityScore,
                status = @status,
                status_changed_at = @statusChangedAt,
                anomaly_reason = @anomalyReason,
                enabled = @enabled,
                public = @public,
                total_subscribers = @totalSubscribers,
                total_backtest_count = @totalBacktestCount,
                last_backtest_at = @lastBacktestAt,
                current_version_string = @version,
                last_compiled_at = @lastCompiledAt,
                updated_at = @updatedAt
            WHERE id = @id";

        var affected = await DBHelper.ExecuteAsync(sql, new
        {
            id = strategy.Id,
            name = strategy.Name,
            dsl = strategy.Dsl,
            remarks = strategy.Remarks,
            description = strategy.Description,
            strategyType = strategy.StrategyType,
            riskLevel = strategy.RiskLevel,
            qualityScore = strategy.QualityScore,
            status = strategy.Status,
            statusChangedAt = strategy.StatusChangedAt,
            anomalyReason = strategy.AnomalyReason,
            enabled = strategy.Enabled ? 1 : 0,
            @public = strategy.Public ? 1 : 0,
            totalSubscribers = strategy.TotalSubscribers,
            totalBacktestCount = strategy.TotalBacktestCount,
            lastBacktestAt = strategy.LastBacktestAt,
            version = newVersionString,
            lastCompiledAt = now,
            updatedAt = now
        });

        if (affected > 0)
        {
            Console.WriteLine($"✅ 更新策略成功: ID={strategy.Id}");

            // 创建新的版本记录
            try
            {
                if (latestVersion != null)
                {
                    // 如果已有版本，创建patch版本
                    var versionId = await _versionRepository.CreatePatchVersionAsync(
                        strategy.Id, 
                        strategy.Dsl, 
                        "策略更新",
                        "user");
                    
                    if (versionId > 0)
                    {
                        Console.WriteLine($"✅ 创建新版本成功: StrategyId={strategy.Id}, Version={newVersionString}");
                    }
                }
                else
                {
                    // 如果没有版本记录，创建初始版本
                    await _versionRepository.CreateInitialVersionAsync(strategy.Id, strategy.Dsl, "user");
                    Console.WriteLine($"✅ 创建初始版本成功: StrategyId={strategy.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 创建版本记录失败: {ex.Message}");
                // 版本创建失败不影响策略更新的成功
            }

            return true;
        }

        Console.WriteLine($"⚠️ 更新策略失败，策略不存在: ID={strategy.Id}");
        return false;
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    public async Task<bool> DeleteStrategyAsync(string id)
    {
        const string sql = "DELETE FROM strategies WHERE id = @id";
        
        var affected = await DBHelper.ExecuteAsync(sql, new { id });
        
        if (affected > 0)
        {
            Console.WriteLine($"✅ 删除策略成功: ID={id}");
            return true;
        }

        Console.WriteLine($"⚠️ 删除策略失败，策略不存在: ID={id}");
        return false;
    }

    /// <summary>
    /// 更新策略状态
    /// </summary>
    public async Task<bool> UpdateStrategyStatusAsync(string id, string status, string? reason = null)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        const string sql = @"
            UPDATE strategies SET
                status = @status,
                status_changed_at = @statusChangedAt,
                anomaly_reason = @anomalyReason,
                updated_at = @updatedAt
            WHERE id = @id";

        var affected = await DBHelper.ExecuteAsync(sql, new
        {
            id,
            status,
            statusChangedAt = now,
            anomalyReason = reason,
            updatedAt = now
        });

        if (affected > 0)
        {
            Console.WriteLine($"✅ 更新策略状态成功: ID={id}, Status={status}");
            return true;
        }

        Console.WriteLine($"⚠️ 更新策略状态失败，策略不存在: ID={id}");
        return false;
    }

    /// <summary>
    /// 更新策略回测统计
    /// </summary>
    public async Task<bool> UpdateBacktestStatsAsync(string id, DateTime lastBacktestAt)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var lastBacktestStr = lastBacktestAt.ToString("yyyy-MM-dd HH:mm:ss");

        const string sql = @"
            UPDATE strategies SET
                total_backtest_count = total_backtest_count + 1,
                last_backtest_at = @lastBacktestAt,
                updated_at = @updatedAt
            WHERE id = @id";

        var affected = await DBHelper.ExecuteAsync(sql, new
        {
            id,
            lastBacktestAt = lastBacktestStr,
            updatedAt = now
        });

        return affected > 0;
    }

    /// <summary>
    /// 检查策略名称是否存在
    /// </summary>
    public async Task<bool> StrategyNameExistsAsync(string name, string? excludeId = null)
    {
        string sql;
        object param;

        if (string.IsNullOrEmpty(excludeId))
        {
            sql = "SELECT COUNT(*) FROM strategies WHERE name = @name";
            param = new { name };
        }
        else
        {
            sql = "SELECT COUNT(*) FROM strategies WHERE name = @name AND id != @excludeId";
            param = new { name, excludeId };
        }

        var count = await DBHelper.ExecuteScalarAsync<int>(sql, param);
        return count > 0;
    }

    /// <summary>
    /// 获取策略数量统计
    /// </summary>
    public async Task<Dictionary<string, int>> GetStrategyCountByStatusAsync()
    {
        const string sql = @"
            SELECT status, COUNT(*) as count
            FROM strategies
            GROUP BY status";

        using var connection = DBHelper.CreateConnection();
        var results = await connection.QueryAsync<(string status, int count)>(sql);

        return results.ToDictionary(r => r.status, r => r.count);
    }
}

