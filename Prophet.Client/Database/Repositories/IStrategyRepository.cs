using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Models;

namespace Prophet.Client.Database.Repositories;

/// <summary>
/// 策略数据访问接口
/// </summary>
public interface IStrategyRepository
{
    /// <summary>
    /// 获取所有策略
    /// </summary>
    Task<List<StrategyInfo>> GetAllStrategiesAsync();

    /// <summary>
    /// 根据ID获取策略
    /// </summary>
    Task<StrategyInfo?> GetStrategyByIdAsync(string id);

    /// <summary>
    /// 根据状态获取策略
    /// </summary>
    Task<List<StrategyInfo>> GetStrategiesByStatusAsync(string status);

    /// <summary>
    /// 创建新策略
    /// </summary>
    Task<string> CreateStrategyAsync(StrategyInfo strategy);

    /// <summary>
    /// 更新策略
    /// </summary>
    Task<bool> UpdateStrategyAsync(StrategyInfo strategy);

    /// <summary>
    /// 删除策略
    /// </summary>
    Task<bool> DeleteStrategyAsync(string id);

    /// <summary>
    /// 更新策略状态
    /// </summary>
    Task<bool> UpdateStrategyStatusAsync(string id, string status, string? reason = null);

    /// <summary>
    /// 更新策略回测统计
    /// </summary>
    Task<bool> UpdateBacktestStatsAsync(string id, DateTime lastBacktestAt);

    /// <summary>
    /// 检查策略名称是否存在
    /// </summary>
    Task<bool> StrategyNameExistsAsync(string name, string? excludeId = null);

    /// <summary>
    /// 获取策略数量统计
    /// </summary>
    Task<Dictionary<string, int>> GetStrategyCountByStatusAsync();
}

