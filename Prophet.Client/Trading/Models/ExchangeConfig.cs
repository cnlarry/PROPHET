namespace Prophet.Client.Trading.Models;

/// <summary>
/// 交易所配置
/// </summary>
public class ExchangeConfig
{
    /// <summary>
    /// 交易所名称
    /// </summary>
    public string ExchangeName { get; set; } = "Binance";
    
    /// <summary>
    /// API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// API Secret
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否使用测试网
    /// </summary>
    public bool UseTestnet { get; set; } = true;
    
    /// <summary>
    /// API基础URL（可选，默认根据UseTestnet自动设置）
    /// </summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>
    /// WebSocket URL（可选，默认根据UseTestnet自动设置）
    /// </summary>
    public string? WebSocketUrl { get; set; }
    
    /// <summary>
    /// 是否启用代理
    /// </summary>
    public bool EnableProxy { get; set; } = false;
    
    /// <summary>
    /// 代理地址
    /// </summary>
    public string? ProxyAddress { get; set; }
    
    /// <summary>
    /// 代理用户名
    /// </summary>
    public string? ProxyUsername { get; set; }
    
    /// <summary>
    /// 代理密码
    /// </summary>
    public string? ProxyPassword { get; set; }
    
    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

