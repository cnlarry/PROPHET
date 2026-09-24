using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;

namespace Prophet.Client.Services.Strategy;

/// <summary>
/// 本地策略服务（使用SQLite数据库）
/// 完全离线，不依赖API
/// </summary>
public class LocalStrategyService
{
    private readonly StrategyRepository _repository;
    private readonly StrategyVersionRepository _versionRepository;

    public LocalStrategyService() : this(null, null)
    {
    }

    /// <summary>
    /// 依赖注入构造函数（DI 单例注册时注入单例仓库，避免与 DI 中注册的 StrategyVersionRepository 形成多个实例）
    /// </summary>
    public LocalStrategyService(
        StrategyRepository? repository = null,
        StrategyVersionRepository? versionRepository = null)
    {
        _repository = repository ?? new StrategyRepository();
        _versionRepository = versionRepository ?? new StrategyVersionRepository();
    }

    /// <summary>
    /// 获取我的所有策略
    /// </summary>
    public async Task<List<StrategyInfo>> GetMyStrategiesAsync()
    {
        try
        {
            var strategies = await _repository.GetAllStrategiesAsync();
            return strategies;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 加载策略列表失败: {ex.Message}");
            return new List<StrategyInfo>();
        }
    }

    /// <summary>
    /// 根据ID获取策略
    /// </summary>
    public async Task<StrategyInfo?> GetStrategyByIdAsync(string id)
    {
        return await _repository.GetStrategyByIdAsync(id);
    }

    /// <summary>
    /// 根据状态获取策略
    /// </summary>
    public async Task<List<StrategyInfo>> GetStrategiesByStatusAsync(string status)
    {
        return await _repository.GetStrategiesByStatusAsync(status);
    }

    /// <summary>
    /// 创建新策略
    /// </summary>
    public async Task<string> CreateStrategyAsync(StrategyInfo strategy)
    {
        // 检查名称是否重复
        var exists = await _repository.StrategyNameExistsAsync(strategy.Name);
        if (exists)
        {
            throw new Exception($"策略名称 '{strategy.Name}' 已存在");
        }

        return await _repository.CreateStrategyAsync(strategy);
    }

    /// <summary>
    /// 更新策略
    /// </summary>
    public async Task<bool> UpdateStrategyAsync(StrategyInfo strategy)
    {
        // 检查名称是否与其他策略重复
        var exists = await _repository.StrategyNameExistsAsync(strategy.Name, strategy.Id);
        if (exists)
        {
            throw new Exception($"策略名称 '{strategy.Name}' 已被其他策略使用");
        }

        return await _repository.UpdateStrategyAsync(strategy);
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    public async Task<bool> DeleteStrategyAsync(string id)
    {
        return await _repository.DeleteStrategyAsync(id);
    }

    /// <summary>
    /// 激活策略
    /// </summary>
    public async Task<bool> ActivateStrategyAsync(string id)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        return await _repository.UpdateStrategyStatusAsync(id, "active");
    }

    /// <summary>
    /// 废弃策略
    /// </summary>
    public async Task<bool> DeprecateStrategyAsync(string id, string reason)
    {
        return await _repository.UpdateStrategyStatusAsync(id, "deprecated", reason);
    }

    /// <summary>
    /// 归档策略
    /// </summary>
    public async Task<bool> ArchiveStrategyAsync(string id, string reason)
    {
        return await _repository.UpdateStrategyStatusAsync(id, "archived", reason);
    }

    /// <summary>
    /// 标记策略为异常
    /// </summary>
    public async Task<bool> MarkStrategyAsAnomalyAsync(string id, string reason)
    {
        return await _repository.UpdateStrategyStatusAsync(id, "anomaly", reason);
    }

    /// <summary>
    /// 从异常状态恢复策略
    /// </summary>
    public async Task<bool> RecoverStrategyFromAnomalyAsync(string id)
    {
        return await _repository.UpdateStrategyStatusAsync(id, "draft");
    }

    /// <summary>
    /// 重命名策略（仅草稿状态可重命名）
    /// </summary>
    public async Task<bool> RenameStrategyAsync(string id, string newName)
    {
        var strategy = await _repository.GetStrategyByIdAsync(id);
        if (strategy == null)
        {
            throw new Exception("策略不存在");
        }

        if (strategy.Status != "draft")
        {
            throw new Exception("只有草稿状态的策略可以重命名");
        }

        // 检查新名称是否重复
        var exists = await _repository.StrategyNameExistsAsync(newName, id);
        if (exists)
        {
            throw new Exception($"策略名称 '{newName}' 已存在");
        }

        strategy.Name = newName;
        return await _repository.UpdateStrategyAsync(strategy);
    }

    /// <summary>
    /// 更新策略回测统计
    /// </summary>
    public async Task<bool> UpdateBacktestStatsAsync(string id, DateTime lastBacktestAt)
    {
        return await _repository.UpdateBacktestStatsAsync(id, lastBacktestAt);
    }

    /// <summary>
    /// 获取策略数量统计
    /// </summary>
    public async Task<Dictionary<string, int>> GetStrategyCountByStatusAsync()
    {
        return await _repository.GetStrategyCountByStatusAsync();
    }

    /// <summary>
    /// 获取按状态分组的策略列表
    /// </summary>
    public async Task<Dictionary<string, List<StrategyInfo>>> GetStrategiesGroupedByStatusAsync()
    {
        var allStrategies = await _repository.GetAllStrategiesAsync();
        
        var grouped = allStrategies
            .GroupBy(s => s.Status)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        return grouped;
    }

    /// <summary>
    /// 获取策略的所有版本
    /// </summary>
    public async Task<List<VersionListItem>> GetStrategyVersionsAsync(string strategyId)
    {
        try
        {
            var versions = await _versionRepository.GetVersionsByStrategyIdAsync(strategyId);
            
            // 转换为 VersionListItem
            var versionList = versions.Select(v => new VersionListItem
            {
                VersionString = v.VersionString,
                ChangeType = v.ChangeType,
                Status = v.Status,
                ChangeDescription = v.ChangeDescription,
                CreatedAt = v.CreatedAt,
                ActiveUsers = 0 // 本地版本，无用户统计
            }).ToList();
            
            return versionList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 加载版本列表失败: {ex.Message}");
            return new List<VersionListItem>();
        }
    }

    /// <summary>
    /// 获取指定版本的策略
    /// </summary>
    public async Task<StrategyVersion?> GetStrategyVersionAsync(string strategyId, string versionString)
    {
        try
        {
            var version = await _versionRepository.GetVersionAsync(strategyId, versionString);
            
            if (version != null)
            {
                return version;
            }
            
            Console.WriteLine($"⚠️ 未找到版本: {versionString}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 加载版本失败: {ex.Message}");
            return null;
        }
    }
}

