using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Network;

/// <summary>
/// 代理类型
/// </summary>
public enum ProxyType
{
    /// <summary>
    /// 不使用代理
    /// </summary>
    None,
    
    /// <summary>
    /// HTTP/HTTPS 代理
    /// </summary>
    Http,
    
    /// <summary>
    /// SOCKS5 代理
    /// </summary>
    Socks5,
    
    /// <summary>
    /// 使用系统代理设置
    /// </summary>
    System
}

/// <summary>
/// 代理健康状态
/// </summary>
public enum ProxyHealthStatus
{
    /// <summary>
    /// 未检查
    /// </summary>
    Unknown,
    
    /// <summary>
    /// 健康
    /// </summary>
    Healthy,
    
    /// <summary>
    /// 不健康
    /// </summary>
    Unhealthy,
    
    /// <summary>
    /// 检查中
    /// </summary>
    Checking
}

/// <summary>
/// 代理配置模型（支持多配置）
/// </summary>
public class ProxyConfig
{
    /// <summary>
    /// 配置ID（用于多配置管理）
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 配置名称
    /// </summary>
    public string Name { get; set; } = "默认代理";
    
    /// <summary>
    /// 是否启用此配置
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// 代理类型
    /// </summary>
    public ProxyType Type { get; set; } = ProxyType.System;
    
    /// <summary>
    /// 代理服务器地址（例如：127.0.0.1 或 proxy.example.com）
    /// </summary>
    public string? Address { get; set; }
    
    /// <summary>
    /// 代理服务器端口
    /// </summary>
    public int Port { get; set; } = 7890;
    
    /// <summary>
    /// 用户名（如果需要认证）
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// 密码（如果需要认证）
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// 是否跳过SSL证书验证
    /// </summary>
    public bool SkipSslValidation { get; set; } = false;
    
    /// <summary>
    /// 代理超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// 绕过代理的域名列表（直连）
    /// 例如：["localhost", "127.0.0.1", "*.local"]
    /// </summary>
    public List<string> BypassDomains { get; set; } = new();
    
    /// <summary>
    /// 仅对指定域名使用代理（为空则所有流量都走代理）
    /// 例如：["api.binance.com", "*.openai.com"]
    /// </summary>
    public List<string> OnlyForDomains { get; set; } = new();
    
    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;
    
    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 60;
    
    /// <summary>
    /// 健康检查URL（用于测试代理连通性）
    /// </summary>
    public string HealthCheckUrl { get; set; } = "https://www.google.com/generate_204";
    
    /// <summary>
    /// 当前健康状态
    /// </summary>
    public ProxyHealthStatus HealthStatus { get; set; } = ProxyHealthStatus.Unknown;
    
    /// <summary>
    /// 最后一次健康检查时间
    /// </summary>
    public DateTime? LastHealthCheckTime { get; set; }
    
    /// <summary>
    /// 最后一次健康检查延迟（毫秒）
    /// </summary>
    public long? LastHealthCheckLatencyMs { get; set; }
    
    /// <summary>
    /// 最后一次健康检查错误信息
    /// </summary>
    public string? LastHealthCheckError { get; set; }
    
    /// <summary>
    /// 获取完整的代理地址（包含协议）
    /// </summary>
    public string GetProxyUri()
    {
        if (Type == ProxyType.None || string.IsNullOrWhiteSpace(Address))
            return string.Empty;
        
        var protocol = Type switch
        {
            ProxyType.Http => "http",
            ProxyType.Socks5 => "socks5",
            _ => "http"
        };
        
        return $"{protocol}://{Address}:{Port}";
    }
    
    /// <summary>
    /// 检查是否应该对指定域名使用代理
    /// </summary>
    public bool ShouldUseProxyForDomain(string domain)
    {
        if (!IsEnabled || Type == ProxyType.None)
            return false;
        
        // 检查是否在绕过列表中
        if (BypassDomains.Any(pattern => MatchesDomainPattern(domain, pattern)))
            return false;
        
        // 如果指定了仅对特定域名使用代理
        if (OnlyForDomains.Any())
        {
            return OnlyForDomains.Any(pattern => MatchesDomainPattern(domain, pattern));
        }
        
        return true;
    }
    
    /// <summary>
    /// 域名匹配（支持通配符）
    /// </summary>
    private bool MatchesDomainPattern(string domain, string pattern)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(pattern))
            return false;
        
        // 完全匹配
        if (domain.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            return true;
        
        // 通配符匹配（*.example.com）
        if (pattern.StartsWith("*."))
        {
            var suffix = pattern.Substring(1); // 去掉 *
            return domain.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }
    
    /// <summary>
    /// 克隆配置
    /// </summary>
    public ProxyConfig Clone()
    {
        return new ProxyConfig
        {
            Id = Id,
            Name = Name,
            IsEnabled = IsEnabled,
            Type = Type,
            Address = Address,
            Port = Port,
            Username = Username,
            Password = Password,
            SkipSslValidation = SkipSslValidation,
            TimeoutSeconds = TimeoutSeconds,
            BypassDomains = new List<string>(BypassDomains),
            OnlyForDomains = new List<string>(OnlyForDomains),
            EnableHealthCheck = EnableHealthCheck,
            HealthCheckIntervalSeconds = HealthCheckIntervalSeconds,
            HealthCheckUrl = HealthCheckUrl,
            HealthStatus = HealthStatus,
            LastHealthCheckTime = LastHealthCheckTime,
            LastHealthCheckLatencyMs = LastHealthCheckLatencyMs,
            LastHealthCheckError = LastHealthCheckError
        };
    }
}

/// <summary>
/// 代理配置集合（支持多配置）
/// </summary>
public class ProxySettings
{
    /// <summary>
    /// 所有代理配置
    /// </summary>
    public List<ProxyConfig> Configs { get; set; } = new();
    
    /// <summary>
    /// 当前激活的配置ID
    /// </summary>
    public string? ActiveConfigId { get; set; }
    
    /// <summary>
    /// 是否启用自动故障切换
    /// </summary>
    public bool EnableAutoFailover { get; set; } = false;
    
    /// <summary>
    /// 获取当前激活的配置
    /// </summary>
    public ProxyConfig? GetActiveConfig()
    {
        if (string.IsNullOrWhiteSpace(ActiveConfigId))
            return Configs.FirstOrDefault(c => c.IsEnabled);
        
        return Configs.FirstOrDefault(c => c.Id == ActiveConfigId && c.IsEnabled);
    }
    
    /// <summary>
    /// 添加或更新配置
    /// </summary>
    public void AddOrUpdateConfig(ProxyConfig config)
    {
        var existing = Configs.FirstOrDefault(c => c.Id == config.Id);
        if (existing != null)
        {
            var index = Configs.IndexOf(existing);
            Configs[index] = config;
        }
        else
        {
            Configs.Add(config);
        }
    }
    
    /// <summary>
    /// 删除配置
    /// </summary>
    public void RemoveConfig(string configId)
    {
        var config = Configs.FirstOrDefault(c => c.Id == configId);
        if (config != null)
        {
            Configs.Remove(config);
            if (ActiveConfigId == configId)
                ActiveConfigId = null;
        }
    }
}

