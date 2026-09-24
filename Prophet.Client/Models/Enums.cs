namespace Prophet.Client.Models;

/// <summary>
/// 信号动作类型
/// 注意：核心引擎只返回 BUY、SELL、HOLD 三种信号
/// </summary>
public enum SignalAction
{
    BUY = 1,         // 买入（做多）
    SELL = 2,        // 卖出（做空）
    HOLD = 3         // 持有（不交易）
}

/// <summary>
/// 订单方向
/// </summary>
public enum OrderSide
{
    BUY = 1,    // 买入
    SELL = 2    // 卖出
}

/// <summary>
/// 订单类型
/// </summary>
public enum OrderType
{
    MARKET = 1,  // 市价单
    LIMIT = 2    // 限价单
}

/// <summary>
/// 订单状态
/// </summary>
public enum OrderStatus
{
    PENDING = 1,   // 待成交
    FILLED = 2,    // 已成交
    OPEN = 3,      // 持仓中
    CLOSED = 4,    // 已平仓
    CANCELLED = 5  // 已取消
}
