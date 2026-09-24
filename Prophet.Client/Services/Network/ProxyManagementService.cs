using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Core;
using Prophet.Client.Services.Security;
using Prophet.Client.Services.Settings;

namespace Prophet.Client.Services.Network;

/// <summary>
/// 代理管理服务 - 统一管理HTTP代理配置和HttpClient创建
/// </summary>
public class ProxyManagementService : IDisposable
{
    private readonly AppSettingsService _settingsService;
    private readonly ProxyValidator _validator;
    private readonly PasswordEncryptionService _encryptionService;
    private ProxySettings _proxySettings;
    private Timer? _healthCheckTimer;
    private bool _disposed;
    
    /// <summary>
    /// 代理健康状态变化事件
    /// </summary>
    public event EventHandler<ProxyHealthChangedEventArgs>? ProxyHealthChanged;
    
    public ProxyManagementService(AppSettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _validator = new ProxyValidator();
        _encryptionService = new PasswordEncryptionService();
        
        // 加载或创建代理设置
        _proxySettings = LoadProxySettings();
        
        // 启动健康检查
        StartHealthCheckTimer();
        
        Console.WriteLine("[ProxyManagementService] 服务已初始化");
    }
    
    /// <summary>
    /// 获取当前激活的代理配置
    /// </summary>
    public ProxyConfig? GetActiveProxyConfig()
    {
        return _proxySettings.GetActiveConfig();
    }
    
    /// <summary>
    /// 获取所有代理配置
    /// </summary>
    public List<ProxyConfig> GetAllProxyConfigs()
    {
        return _proxySettings.Configs.ToList();
    }
    
    /// <summary>
    /// 添加或更新代理配置
    /// </summary>
    public void AddOrUpdateProxyConfig(ProxyConfig config)
    {
        Console.WriteLine($"[ProxyManagementService] 添加/更新代理配置: {config.Name}");
        _proxySettings.AddOrUpdateConfig(config);
        SaveProxySettings();
    }
    
    /// <summary>
    /// 删除代理配置
    /// </summary>
    public void RemoveProxyConfig(string configId)
    {
        Console.WriteLine($"[ProxyManagementService] 删除代理配置: {configId}");
        _proxySettings.RemoveConfig(configId);
        SaveProxySettings();
    }
    
    /// <summary>
    /// 设置激活的代理配置
    /// </summary>
    public void SetActiveProxyConfig(string? configId)
    {
        Console.WriteLine($"[ProxyManagementService] 设置激活代理: {configId}");
        _proxySettings.ActiveConfigId = configId;
        SaveProxySettings();
    }
    
    /// <summary>
    /// 测试代理连接
    /// </summary>
    public async Task<ProxyValidationResult> TestProxyAsync(ProxyConfig config)
    {
        Console.WriteLine($"[ProxyManagementService] 测试代理: {config.Name}");
        return await _validator.ValidateProxyWithDetailsAsync(config);
    }
    
    /// <summary>
    /// 测试所有代理
    /// </summary>
    public async Task<Dictionary<string, ProxyValidationResult>> TestAllProxiesAsync()
    {
        Console.WriteLine("[ProxyManagementService] 测试所有代理配置");
        var configs = _proxySettings.Configs.Where(c => c.IsEnabled).ToList();
        return await _validator.ValidateMultipleProxiesAsync(configs);
    }
    
    /// <summary>
    /// 创建HttpClient（应用代理配置）
    /// </summary>
    /// <param name="targetUri">目标URI（用于智能代理选择）</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="forceProxyConfigId">强制使用指定的代理配置ID</param>
    public HttpClient CreateHttpClient(
        Uri? targetUri = null,
        int? timeoutSeconds = null,
        string? forceProxyConfigId = null)
    {
        ProxyConfig? config = null;
        
        // 强制使用指定配置
        if (!string.IsNullOrWhiteSpace(forceProxyConfigId))
        {
            config = _proxySettings.Configs.FirstOrDefault(c => c.Id == forceProxyConfigId);
        }
        // 智能选择代理
        else if (targetUri != null)
        {
            config = SelectProxyForUri(targetUri);
        }
        // 使用激活的配置
        else
        {
            config = GetActiveProxyConfig();
        }
        
        var handler = CreateHttpClientHandler(config);
        var client = new HttpClient(handler, disposeHandler: true);
        
        // 设置超时
        if (timeoutSeconds.HasValue)
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
        }
        else if (config != null && config.TimeoutSeconds > 0)
        {
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        }
        
        var proxyInfo = config?.IsEnabled == true ? config.GetProxyUri() : "直连";
        Console.WriteLine($"[ProxyManagementService] 创建HttpClient: {proxyInfo}, 超时: {client.Timeout.TotalSeconds}秒");
        
        return client;
    }
    
    /// <summary>
    /// 创建HttpClientHandler（应用代理配置）
    /// </summary>
    public HttpClientHandler CreateHttpClientHandler(ProxyConfig? config = null)
    {
        config ??= GetActiveProxyConfig();
        
        var handler = new HttpClientHandler();
        
        // 配置代理
        if (config?.IsEnabled == true)
        {
            if (config.Type == ProxyType.System)
            {
                handler.UseProxy = true;
                Console.WriteLine("[ProxyManagementService] 使用系统代理");
            }
            else if (config.Type != ProxyType.None && !string.IsNullOrWhiteSpace(config.Address))
            {
            handler.UseProxy = true;
            handler.Proxy = new WebProxy(config.GetProxyUri())
            {
                Credentials = !string.IsNullOrWhiteSpace(config.Username)
                    ? new NetworkCredential(config.Username, DecryptPassword(config.Password))
                    : null
            };
            Console.WriteLine($"[ProxyManagementService] 使用显式代理: {config.GetProxyUri()}");
        }
            else
            {
                handler.UseProxy = false;
                Console.WriteLine("[ProxyManagementService] 不使用代理");
            }
            
            // SSL证书验证
            if (config.SkipSslValidation)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                Console.WriteLine("[ProxyManagementService] ⚠️ SSL证书验证已跳过（调试模式）");
            }
        }
        else
        {
            handler.UseProxy = false;
        }
        
        return handler;
    }
    
    /// <summary>
    /// 根据URI智能选择代理配置
    /// </summary>
    private ProxyConfig? SelectProxyForUri(Uri uri)
    {
        var domain = uri.Host;
        
        // 检查所有启用的配置
        foreach (var config in _proxySettings.Configs.Where(c => c.IsEnabled))
        {
            if (config.ShouldUseProxyForDomain(domain))
            {
                Console.WriteLine($"[ProxyManagementService] 域名 {domain} 使用代理: {config.Name}");
                return config;
            }
        }
        
        Console.WriteLine($"[ProxyManagementService] 域名 {domain} 不使用代理");
        return null;
    }
    
    /// <summary>
    /// 启动健康检查定时器
    /// </summary>
    private void StartHealthCheckTimer()
    {
        // 每60秒检查一次
        _healthCheckTimer = new Timer(
            async _ => await PerformHealthCheckAsync(),
            null,
            TimeSpan.FromSeconds(10), // 启动后10秒开始第一次检查
            TimeSpan.FromSeconds(60)  // 每60秒检查一次
        );
        
        Console.WriteLine("[ProxyManagementService] 健康检查定时器已启动");
    }
    
    /// <summary>
    /// 执行健康检查
    /// </summary>
    private async Task PerformHealthCheckAsync()
    {
        var configs = _proxySettings.Configs
            .Where(c => c.IsEnabled && c.EnableHealthCheck)
            .ToList();
        
        if (!configs.Any())
            return;
        
        Console.WriteLine($"[ProxyManagementService] 开始健康检查，共 {configs.Count} 个配置");
        
        foreach (var config in configs)
        {
            try
            {
                config.HealthStatus = ProxyHealthStatus.Checking;
                
                var result = await _validator.ValidateProxyAsync(config);
                
                var previousStatus = config.HealthStatus;
                config.HealthStatus = result.IsSuccess 
                    ? ProxyHealthStatus.Healthy 
                    : ProxyHealthStatus.Unhealthy;
                config.LastHealthCheckTime = DateTime.Now;
                config.LastHealthCheckLatencyMs = result.LatencyMs;
                config.LastHealthCheckError = result.ErrorMessage;
                
                // 如果状态变化，触发事件
                if (previousStatus != config.HealthStatus)
                {
                    Console.WriteLine($"[ProxyManagementService] 代理 {config.Name} 状态变化: {previousStatus} -> {config.HealthStatus}");
                    OnProxyHealthChanged(config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProxyManagementService] 健康检查异常: {config.Name} - {ex.Message}");
                config.HealthStatus = ProxyHealthStatus.Unhealthy;
                config.LastHealthCheckError = ex.Message;
            }
        }
        
        SaveProxySettings();
    }
    
    /// <summary>
    /// 触发代理健康状态变化事件
    /// </summary>
    private void OnProxyHealthChanged(ProxyConfig config)
    {
        ProxyHealthChanged?.Invoke(this, new ProxyHealthChangedEventArgs(config));
    }
    
    /// <summary>
    /// 加载代理设置
    /// </summary>
    private ProxySettings LoadProxySettings()
    {
        // 尝试从AppSettings加载旧的代理配置
        var settings = _settingsService.Settings;
        
        var proxySettings = new ProxySettings();
        
        // 迁移旧配置
        if (settings.EnableProxy || !string.IsNullOrWhiteSpace(settings.ProxyAddress))
        {
            var legacyConfig = new ProxyConfig
            {
                Id = "legacy",
                Name = "默认代理（从旧配置迁移）",
                IsEnabled = settings.EnableProxy,
                Type = string.IsNullOrWhiteSpace(settings.ProxyAddress) 
                    ? ProxyType.System 
                    : ProxyType.Http,
                Address = ParseProxyAddress(settings.ProxyAddress),
                Port = ParseProxyPort(settings.ProxyAddress),
                Username = settings.ProxyUsername,
                Password = EncryptPassword(settings.ProxyPassword), // 加密存储
                SkipSslValidation = settings.SkipSslCertificateValidation
            };
            
            proxySettings.Configs.Add(legacyConfig);
            proxySettings.ActiveConfigId = legacyConfig.Id;
            
            Console.WriteLine("[ProxyManagementService] 已迁移旧的代理配置（密码已加密）");
        }
        else
        {
            // 创建默认配置
            proxySettings.Configs.Add(new ProxyConfig
            {
                Id = "default",
                Name = "系统代理",
                IsEnabled = false,
                Type = ProxyType.System
            });
        }
        
        return proxySettings;
    }
    
    /// <summary>
    /// 保存代理设置
    /// </summary>
    private void SaveProxySettings()
    {
        // 目前仍然保存到AppSettings以保持兼容性
        var activeConfig = GetActiveProxyConfig();
        
        if (activeConfig != null)
        {
            var settings = _settingsService.Settings;
            settings.EnableProxy = activeConfig.IsEnabled;
            settings.ProxyAddress = activeConfig.Type == ProxyType.System 
                ? null 
                : activeConfig.GetProxyUri();
            settings.ProxyUsername = activeConfig.Username;
            settings.ProxyPassword = activeConfig.Password; // 已加密，直接保存
            settings.SkipSslCertificateValidation = activeConfig.SkipSslValidation;
            
            _settingsService.UpdateSettings(settings);
            Console.WriteLine("[ProxyManagementService] 代理设置已保存（密码已加密）");
        }
    }
    
    /// <summary>
    /// 加密密码
    /// </summary>
    private string? EncryptPassword(string? plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            return null;
        
        return _encryptionService.EncryptIfNeeded(plainPassword);
    }
    
    /// <summary>
    /// 解密密码
    /// </summary>
    private string? DecryptPassword(string? encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(encryptedPassword))
            return null;
        
        return _encryptionService.Decrypt(encryptedPassword);
    }
    
    /// <summary>
    /// 解析代理地址（从完整URI中提取）
    /// </summary>
    private string? ParseProxyAddress(string? proxyUri)
    {
        if (string.IsNullOrWhiteSpace(proxyUri))
            return null;
        
        try
        {
            var uri = new Uri(proxyUri);
            return uri.Host;
        }
        catch
        {
            // 如果不是完整URI，直接返回
            return proxyUri.Split(':')[0];
        }
    }
    
    /// <summary>
    /// 解析代理端口（从完整URI中提取）
    /// </summary>
    private int ParseProxyPort(string? proxyUri)
    {
        if (string.IsNullOrWhiteSpace(proxyUri))
            return 7890;
        
        try
        {
            var uri = new Uri(proxyUri);
            return uri.Port > 0 ? uri.Port : 7890;
        }
        catch
        {
            // 尝试解析 host:port 格式
            var parts = proxyUri.Split(':');
            if (parts.Length > 1 && int.TryParse(parts[1], out var port))
                return port;
            
            return 7890;
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _disposed = true;
            Console.WriteLine("[ProxyManagementService] 服务已释放");
        }
    }
}

/// <summary>
/// 代理健康状态变化事件参数
/// </summary>
public class ProxyHealthChangedEventArgs : EventArgs
{
    public ProxyConfig Config { get; }
    
    public ProxyHealthChangedEventArgs(ProxyConfig config)
    {
        Config = config;
    }
}

