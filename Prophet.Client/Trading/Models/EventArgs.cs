using System;
using System.Collections.Generic;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 订单更新事件参数
/// </summary>
public class OrderUpdateEventArgs : EventArgs
{
    /// <summary>
    /// 订单信息
    /// </summary>
    public OrderInfo Order { get; set; } = new();
}

/// <summary>
/// 仓位更新事件参数
/// </summary>
public class PositionUpdateEventArgs : EventArgs
{
    /// <summary>
    /// 仓位信息
    /// </summary>
    public Position Position { get; set; } = new();
}

/// <summary>
/// 账户更新事件参数
/// </summary>
public class AccountUpdateEventArgs : EventArgs
{
    /// <summary>
    /// 账户信息
    /// </summary>
    public AccountInfo Account { get; set; } = new();
    
    /// <summary>
    /// 更新的仓位列表
    /// </summary>
    public List<Position> Positions { get; set; } = new();
    
    /// <summary>
    /// 更新类型
    /// </summary>
    public string UpdateType { get; set; } = string.Empty;
}

/// <summary>
/// 错误事件参数
/// </summary>
public class ExchangeErrorEventArgs : EventArgs
{
    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

