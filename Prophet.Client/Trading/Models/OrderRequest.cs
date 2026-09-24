using Prophet.Client.Trading.Exchanges;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 市价单请求
/// </summary>
public class MarketOrderRequest
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 买卖方向
    /// </summary>
    public OrderSide Side { get; set; }
    
    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// 客户端订单ID（可选）
    /// </summary>
    public string? ClientOrderId { get; set; }
}

/// <summary>
/// 限价单请求
/// </summary>
public class LimitOrderRequest
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 买卖方向
    /// </summary>
    public OrderSide Side { get; set; }
    
    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// 价格
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// 有效期
    /// </summary>
    public TimeInForce TimeInForce { get; set; } = TimeInForce.GTC;
    
    /// <summary>
    /// 客户端订单ID（可选）
    /// </summary>
    public string? ClientOrderId { get; set; }
}

/// <summary>
/// 订单结果
/// </summary>
public class OrderResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 订单ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// 客户端订单ID
    /// </summary>
    public string? ClientOrderId { get; set; }
    
    /// <summary>
    /// 成交价格
    /// </summary>
    public decimal FilledPrice { get; set; }
    
    /// <summary>
    /// 成交数量
    /// </summary>
    public decimal FilledQuantity { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 订单信息
/// </summary>
public class OrderInfo
{
    /// <summary>
    /// 订单ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// 客户端订单ID
    /// </summary>
    public string? ClientOrderId { get; set; }
    
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 订单方向
    /// </summary>
    public OrderSide Side { get; set; }
    
    /// <summary>
    /// 订单类型
    /// </summary>
    public OrderType Type { get; set; }
    
    /// <summary>
    /// 订单状态
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// 价格
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// 已成交数量
    /// </summary>
    public decimal FilledQuantity { get; set; }
    
    /// <summary>
    /// 平均成交价
    /// </summary>
    public decimal AvgPrice { get; set; }
}

