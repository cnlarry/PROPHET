using System.Collections.Generic;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 账户信息
/// </summary>
public class AccountInfo
{
    /// <summary>
    /// 总余额
    /// </summary>
    public decimal TotalBalance { get; set; }
    
    /// <summary>
    /// 可用余额
    /// </summary>
    public decimal AvailableBalance { get; set; }
    
    /// <summary>
    /// 总保证金
    /// </summary>
    public decimal TotalMargin { get; set; }
    
    /// <summary>
    /// 未实现盈亏
    /// </summary>
    public decimal TotalUnrealizedPnl { get; set; }
    
    /// <summary>
    /// 已实现盈亏
    /// </summary>
    public decimal TotalRealizedPnl { get; set; }
    
    /// <summary>
    /// 资产列表
    /// </summary>
    public List<AssetBalance> Assets { get; set; } = new();
}

/// <summary>
/// 资产余额
/// </summary>
public class AssetBalance
{
    /// <summary>
    /// 资产名称（如USDT）
    /// </summary>
    public string Asset { get; set; } = string.Empty;
    
    /// <summary>
    /// 总余额
    /// </summary>
    public decimal Balance { get; set; }
    
    /// <summary>
    /// 可用余额
    /// </summary>
    public decimal AvailableBalance { get; set; }
    
    /// <summary>
    /// 冻结余额
    /// </summary>
    public decimal LockedBalance { get; set; }
}

