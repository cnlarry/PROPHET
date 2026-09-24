using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Storage;

/// <summary>
/// 回测存储接口
/// </summary>
public interface IBacktestStorage
{
    // ========== 回测记录 ==========
    Task<string> SaveBacktestResultAsync(
        BacktestResult result, 
        CancellationToken cancellationToken = default
    );
    
    Task<BacktestResult?> GetBacktestResultAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<BacktestResult>> GetRecentBacktestsAsync(
        int count = 10, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<BacktestResult>> GetBacktestsByStrategyAsync(
        string strategyId, 
        CancellationToken cancellationToken = default
    );
    
    Task DeleteBacktestAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    // ========== 订单记录 ==========
    Task SaveOrdersAsync(
        string backtestId, 
        List<Order> orders, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<Order>> GetOrdersAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    // ========== 权益曲线 ==========
    Task SaveEquityCurveAsync(
        string backtestId, 
        List<EquityPoint> equityCurve, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<EquityPoint>> GetEquityCurveAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    // ========== 数据清理 ==========
    Task CleanupOldBacktestsAsync(
        int keepCount = 50, 
        CancellationToken cancellationToken = default
    );
}

