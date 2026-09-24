namespace Prophet.Client.Backtest.Models;

/// <summary>
/// 回测状态
/// </summary>
public enum BacktestStatus
{
    RUNNING = 1,    // 运行中
    COMPLETED = 2,  // 已完成
    CANCELLED = 3,  // 已取消
    FAILED = 4      // 失败
}

/// <summary>
/// 滑点模式（P1.3）
/// </summary>
public enum SlippageMode
{
    /// <summary>按基点固定滑点（1 bps = 0.01%）</summary>
    FixedBps = 1,
    
    /// <summary>固定价格滑点（绝对价差）</summary>
    FixedPrice = 2,
    
    /// <summary>按当前K线价差百分比</summary>
    PctOfSpread = 3,
    
    /// <summary>基于订单大小的市场冲击模型</summary>
    MarketImpact = 4
}

