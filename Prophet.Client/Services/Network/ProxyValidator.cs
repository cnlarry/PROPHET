using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prophet.Client.Services.Network;

/// <summary>
/// 代理验证结果
/// </summary>
public class ProxyValidationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long LatencyMs { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// HTTP状态码
    /// </summary>
    public HttpStatusCode? StatusCode { get; set; }
    
    /// <summary>
    /// 测试时间
    /// </summary>
    public DateTime TestTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 外网IP（通过代理获取的）
    /// </summary>
    public string? ExternalIp { get; set; }
    
    /// <summary>
    /// 额外信息
    /// </summary>
    public string? AdditionalInfo { get; set; }
}

/// <summary>
/// 代理验证器 - 用于测试代理连接和性能
/// </summary>
public class ProxyValidator
{
    private const string DefaultTestUrl = "https://www.google.com/generate_204";
    private const string IpCheckUrl = "https://api.ipify.org?format=text";
    private const int DefaultTimeoutSeconds = 10;
    
    /// <summary>
    /// 测试代理连接
    /// </summary>
    public async Task<ProxyValidationResult> ValidateProxyAsync(
        ProxyConfig config, 
        string? testUrl = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProxyValidationResult();
        var url = testUrl ?? config.HealthCheckUrl ?? DefaultTestUrl;
        
        Console.WriteLine($"[ProxyValidator] 开始测试代理: {config.Name} ({config.GetProxyUri()})");
        Console.WriteLine($"[ProxyValidator] 测试URL: {url}");
        
        HttpClient? client = null;
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 创建测试用的 HttpClient
            var handler = CreateHttpClientHandler(config);
            client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 
                    ? config.TimeoutSeconds 
                    : DefaultTimeoutSeconds)
            };
            
            // 发送测试请求
            var response = await client.GetAsync(url, cancellationToken);
            stopwatch.Stop();
            
            result.IsSuccess = response.IsSuccessStatusCode;
            result.StatusCode = response.StatusCode;
            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            
            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase}";
                Console.WriteLine($"[ProxyValidator] 测试失败: {result.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"[ProxyValidator] 测试成功，延迟: {result.LatencyMs}ms");
            }
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            result.IsSuccess = false;
            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            result.ErrorMessage = "连接超时";
            Console.WriteLine($"[ProxyValidator] 测试超时: {result.LatencyMs}ms");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            result.IsSuccess = false;
            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            result.ErrorMessage = $"连接错误: {ex.Message}";
            Console.WriteLine($"[ProxyValidator] 连接错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.IsSuccess = false;
            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            result.ErrorMessage = $"未知错误: {ex.Message}";
            Console.WriteLine($"[ProxyValidator] 未知错误: {ex.Message}");
        }
        finally
        {
            client?.Dispose();
        }
        
        return result;
    }
    
    /// <summary>
    /// 获取通过代理的外网IP
    /// </summary>
    public async Task<string?> GetExternalIpAsync(
        ProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ProxyValidator] 获取外网IP: {config.Name}");
        
        HttpClient? client = null;
        try
        {
            var handler = CreateHttpClientHandler(config);
            client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds)
            };
            
            var response = await client.GetAsync(IpCheckUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var ip = await response.Content.ReadAsStringAsync(cancellationToken);
                ip = ip.Trim();
                Console.WriteLine($"[ProxyValidator] 外网IP: {ip}");
                return ip;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProxyValidator] 获取外网IP失败: {ex.Message}");
        }
        finally
        {
            client?.Dispose();
        }
        
        return null;
    }
    
    /// <summary>
    /// 测试代理并获取详细信息
    /// </summary>
    public async Task<ProxyValidationResult> ValidateProxyWithDetailsAsync(
        ProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        // 先测试基本连接
        var result = await ValidateProxyAsync(config, null, cancellationToken);
        
        // 如果连接成功，尝试获取外网IP
        if (result.IsSuccess)
        {
            result.ExternalIp = await GetExternalIpAsync(config, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(result.ExternalIp))
            {
                result.AdditionalInfo = $"代理生效，外网IP: {result.ExternalIp}";
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// 批量测试多个代理配置
    /// </summary>
    public async Task<Dictionary<string, ProxyValidationResult>> ValidateMultipleProxiesAsync(
        IEnumerable<ProxyConfig> configs,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, ProxyValidationResult>();
        
        foreach (var config in configs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var result = await ValidateProxyAsync(config, null, cancellationToken);
            results[config.Id] = result;
        }
        
        return results;
    }
    
    /// <summary>
    /// 创建HttpClientHandler（应用代理配置）
    /// </summary>
    private HttpClientHandler CreateHttpClientHandler(ProxyConfig config)
    {
        var handler = new HttpClientHandler();
        
        // 配置代理
        if (config.Type == ProxyType.System)
        {
            // 使用系统代理
            handler.UseProxy = true;
            // 不设置 Proxy，让系统自动检测
        }
        else if (config.Type != ProxyType.None && !string.IsNullOrWhiteSpace(config.Address))
        {
            // 使用显式代理
            handler.UseProxy = true;
            handler.Proxy = new WebProxy(config.GetProxyUri())
            {
                Credentials = !string.IsNullOrWhiteSpace(config.Username)
                    ? new NetworkCredential(config.Username, config.Password)
                    : null
            };
        }
        else
        {
            handler.UseProxy = false;
        }
        
        // SSL证书验证
        if (config.SkipSslValidation)
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }
        
        return handler;
    }
}

