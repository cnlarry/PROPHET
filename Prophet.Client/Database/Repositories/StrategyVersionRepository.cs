using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Prophet.Client.Models;

namespace Prophet.Client.Database.Repositories;

/// <summary>
/// 策略版本数据访问实现
/// 管理 strategy_dsl_versions 表的CRUD操作
/// </summary>
public class StrategyVersionRepository
{
    /// <summary>
    /// 创建策略的初始版本（v1.0.0）
    /// </summary>
    public async Task<long> CreateInitialVersionAsync(
        string strategyId, 
        string dsl, 
        string createdBy = "system")
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var dslHash = ComputeDslHash(dsl);

        const string sql = @"
            INSERT INTO strategy_versions (
                strategy_id, version_string, major_version, minor_version, patch_version,
                change_type, dsl, parameters, dsl_hash,
                version_status, validation_status,
                created_by, created_at
            ) VALUES (
                @strategyId, 'v1.0.0', 1, 0, 0,
                'initial', @dsl, @parameters, @dslHash,
                'draft', 'pending',
                @createdBy, @createdAt
            );
            SELECT last_insert_rowid();";

        var versionId = await DBHelper.ExecuteScalarAsync<long>(sql, new
        {
            strategyId,
            dsl,
            parameters = "{}",  // 空的JSON对象
            dslHash,
            createdBy,
            createdAt = now
        });

        Console.WriteLine($"✅ 创建初始版本: StrategyId={strategyId}, VersionId={versionId}");
        return versionId;
    }

    /// <summary>
    /// 创建新版本（patch版本递增）
    /// </summary>
    public async Task<long> CreatePatchVersionAsync(
        string strategyId, 
        string dsl, 
        string changeDescription = "",
        string createdBy = "system")
    {
        // 1. 获取当前最新版本号
        const string getVersionSql = @"
            SELECT major_version, minor_version, patch_version
            FROM strategy_versions
            WHERE strategy_id = @strategyId
            ORDER BY major_version DESC, minor_version DESC, patch_version DESC
            LIMIT 1";

        var currentVersion = await DBHelper.QueryFirstOrDefaultAsync<(int major, int minor, int patch)>(
            getVersionSql, 
            new { strategyId });

        // 如果没有找到版本，说明这是第一次保存，返回0表示需要创建初始版本
        if (currentVersion.major == 0 && currentVersion.minor == 0 && currentVersion.patch == 0)
        {
            Console.WriteLine($"⚠️ 未找到现有版本，请先创建初始版本: StrategyId={strategyId}");
            return 0;
        }

        int newMajor = currentVersion.major;
        int newMinor = currentVersion.minor;
        int newPatch = currentVersion.patch + 1;
        string newVersionString = $"v{newMajor}.{newMinor}.{newPatch}";

        // 2. 检查DSL是否真的变化（通过哈希值比较）
        var dslHash = ComputeDslHash(dsl);
        const string checkHashSql = @"
            SELECT COUNT(*) FROM strategy_versions
            WHERE strategy_id = @strategyId AND dsl_hash = @dslHash";

        var hashExists = await DBHelper.ExecuteScalarAsync<int>(checkHashSql, new { strategyId, dslHash });
        if (hashExists > 0)
        {
            Console.WriteLine($"⚠️ DSL代码未变化，跳过版本创建: StrategyId={strategyId}");
            return 0; // DSL没有变化，不创建新版本
        }

        // 3. 创建新版本
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        const string insertSql = @"
            INSERT INTO strategy_versions (
                strategy_id, version_string, major_version, minor_version, patch_version,
                change_type, dsl, parameters, dsl_hash,
                version_status, validation_status,
                change_description, created_by, created_at
            ) VALUES (
                @strategyId, @versionString, @major, @minor, @patch,
                'patch', @dsl, @parameters, @dslHash,
                'draft', 'pending',
                @changeDescription, @createdBy, @createdAt
            );
            SELECT last_insert_rowid();";

        var versionId = await DBHelper.ExecuteScalarAsync<long>(insertSql, new
        {
            strategyId,
            versionString = newVersionString,
            major = newMajor,
            minor = newMinor,
            patch = newPatch,
            dsl,
            parameters = "{}",
            dslHash,
            changeDescription,
            createdBy,
            createdAt = now
        });

        Console.WriteLine($"✅ 创建新版本: StrategyId={strategyId}, Version={newVersionString}, VersionId={versionId}");
        return versionId;
    }

    /// <summary>
    /// 获取策略的所有版本
    /// </summary>
    public async Task<List<StrategyVersion>> GetVersionsByStrategyIdAsync(string strategyId)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                strategy_id AS StrategyId,
                major_version AS MajorVersion,
                minor_version AS MinorVersion,
                patch_version AS PatchVersion,
                version_string AS VersionString,
                change_type AS ChangeType,
                dsl AS Dsl,
                parameters AS Parameters,
                dsl_hash AS DslHash,
                version_status AS Status,
                status_changed_at AS StatusChangedAt,
                validation_status AS ValidationStatus,
                validation_message AS ValidationMessage,
                validated_at AS ValidatedAt,
                backtest_data AS BacktestData,
                backtest_completed_at AS BacktestCompletedAt,
                change_description AS ChangeDescription,
                breaking_changes AS BreakingChanges,
                risk_disclosure AS RiskDisclosure,
                tags AS Tags,
                deprecation_reason AS DeprecationReason,
                force_upgrade AS ForceUpgrade,
                upgrade_deadline AS UpgradeDeadline,
                created_by AS CreatedBy,
                created_at AS CreatedAt,
                activated_at AS ActivatedAt
            FROM strategy_versions
            WHERE strategy_id = @strategyId
            ORDER BY major_version DESC, minor_version DESC, patch_version DESC";

        var versions = await DBHelper.QueryAsync<StrategyVersion>(sql, new { strategyId });
        return versions.ToList();
    }

    /// <summary>
    /// 获取策略的最新版本
    /// </summary>
    public async Task<StrategyVersion?> GetLatestVersionAsync(string strategyId)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                strategy_id AS StrategyId,
                major_version AS MajorVersion,
                minor_version AS MinorVersion,
                patch_version AS PatchVersion,
                version_string AS VersionString,
                change_type AS ChangeType,
                dsl AS Dsl,
                parameters AS Parameters,
                dsl_hash AS DslHash,
                version_status AS Status,
                status_changed_at AS StatusChangedAt,
                validation_status AS ValidationStatus,
                validation_message AS ValidationMessage,
                validated_at AS ValidatedAt,
                backtest_data AS BacktestData,
                backtest_completed_at AS BacktestCompletedAt,
                change_description AS ChangeDescription,
                breaking_changes AS BreakingChanges,
                risk_disclosure AS RiskDisclosure,
                tags AS Tags,
                deprecation_reason AS DeprecationReason,
                force_upgrade AS ForceUpgrade,
                upgrade_deadline AS UpgradeDeadline,
                created_by AS CreatedBy,
                created_at AS CreatedAt,
                activated_at AS ActivatedAt
            FROM strategy_versions
            WHERE strategy_id = @strategyId
            ORDER BY major_version DESC, minor_version DESC, patch_version DESC
            LIMIT 1";

        return await DBHelper.QueryFirstOrDefaultAsync<StrategyVersion>(sql, new { strategyId });
    }

    /// <summary>
    /// 获取指定版本的策略
    /// </summary>
    public async Task<StrategyVersion?> GetVersionAsync(string strategyId, string versionString)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                strategy_id AS StrategyId,
                major_version AS MajorVersion,
                minor_version AS MinorVersion,
                patch_version AS PatchVersion,
                version_string AS VersionString,
                change_type AS ChangeType,
                dsl AS Dsl,
                parameters AS Parameters,
                dsl_hash AS DslHash,
                version_status AS Status,
                status_changed_at AS StatusChangedAt,
                validation_status AS ValidationStatus,
                validation_message AS ValidationMessage,
                validated_at AS ValidatedAt,
                backtest_data AS BacktestData,
                backtest_completed_at AS BacktestCompletedAt,
                change_description AS ChangeDescription,
                breaking_changes AS BreakingChanges,
                risk_disclosure AS RiskDisclosure,
                tags AS Tags,
                deprecation_reason AS DeprecationReason,
                force_upgrade AS ForceUpgrade,
                upgrade_deadline AS UpgradeDeadline,
                created_by AS CreatedBy,
                created_at AS CreatedAt,
                activated_at AS ActivatedAt
            FROM strategy_versions
            WHERE strategy_id = @strategyId AND version_string = @versionString";

        return await DBHelper.QueryFirstOrDefaultAsync<StrategyVersion>(sql, new { strategyId, versionString });
    }

    /// <summary>
    /// 删除策略的所有版本（级联删除时使用）
    /// </summary>
    public async Task<int> DeleteVersionsByStrategyIdAsync(string strategyId)
    {
        const string sql = "DELETE FROM strategy_versions WHERE strategy_id = @strategyId";
        return await DBHelper.ExecuteAsync(sql, new { strategyId });
    }

    /// <summary>
    /// 计算DSL代码的SHA-256哈希值
    /// </summary>
    private string ComputeDslHash(string dsl)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(dsl);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

