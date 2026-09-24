namespace Prophet.Client.Trading.Exchanges;

/// <summary>
/// 交易所连接状态
/// </summary>
public enum ExchangeStatus
{
    /// <summary>
    /// 未连接
    /// </summary>
    Disconnected,
    
    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,
    
    /// <summary>
    /// 已连接
    /// </summary>
    Connected,
    
    /// <summary>
    /// 已认证
    /// </summary>
    Authenticated,
    
    /// <summary>
    /// 连接错误
    /// </summary>
    Error,
    
    /// <summary>
    /// 重连中
    /// </summary>
    Reconnecting
}

/// <summary>
/// 持仓方向
/// </summary>
public enum PositionSide
{
    /// <summary>
    /// 多头
    /// </summary>
    Long,
    
    /// <summary>
    /// 空头
    /// </summary>
    Short,
    
    /// <summary>
    /// 双向持仓模式
    /// </summary>
    Both
}

/// <summary>
/// 订单方向
/// </summary>
public enum OrderSide
{
    /// <summary>
    /// 买入
    /// </summary>
    BUY,
    
    /// <summary>
    /// 卖出
    /// </summary>
    SELL
}

/// <summary>
/// 订单类型
/// </summary>
public enum OrderType
{
    /// <summary>
    /// 市价单
    /// </summary>
    MARKET,
    
    /// <summary>
    /// 限价单
    /// </summary>
    LIMIT,
    
    /// <summary>
    /// 止损单
    /// </summary>
    STOP_MARKET,
    
    /// <summary>
    /// 止盈单
    /// </summary>
    TAKE_PROFIT_MARKET
}

/// <summary>
/// 有效期类型
/// </summary>
public enum TimeInForce
{
    /// <summary>
    /// 成交为止
    /// </summary>
    GTC,
    
    /// <summary>
    /// 立即成交或取消
    /// </summary>
    IOC,
    
    /// <summary>
    /// 全部成交或取消
    /// </summary>
    FOK
}

