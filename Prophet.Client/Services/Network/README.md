# 代理管理系统使用指南

## 概述

Prophet 客户端现在拥有一个统一的、专业的代理管理系统，用于管理所有HTTP请求的代理配置。

## 核心组件

### 1. ProxyConfig - 代理配置模型
支持以下代理类型：
- **System**: 使用系统代理
- **Http/Https**: 标准HTTP代理
- **Socks5**: SOCKS5代理（适用于高级场景）

### 2. ProxyManagementService - 代理管理服务
核心服务，提供：
- ✅ 统一的HttpClient创建
- ✅ 代理健康检查
- ✅ 智能代理选择（基于域名规则）
- ✅ 自动故障切换

### 3. ProxyValidator - 代理验证器
用于测试代理连接、获取延迟、检查外网IP等。

## 使用方式

### 方式一：通过ProxyManagementService（推荐）

```csharp
// 1. 获取代理管理服务
var proxyService = ServiceContainer.GetService<ProxyManagementService>();

// 2. 创建HttpClient（自动应用代理配置）
var httpClient = proxyService.CreateHttpClient();

// 3. 针对特定URI创建（智能代理选择）
var targetUri = new Uri("https://api.binance.com/api/v3/ticker/24hr");
var httpClient = proxyService.CreateHttpClient(targetUri);

// 4. 强制使用指定的代理配置
var httpClient = proxyService.CreateHttpClient(
    forceProxyConfigId: "my-proxy-id",
    timeoutSeconds: 30
);
```

### 方式二：创建HttpClientHandler（更灵活）

```csharp
var proxyService = ServiceContainer.GetService<ProxyManagementService>();

// 创建Handler
var handler = proxyService.CreateHttpClientHandler();

// 自定义配置
var client = new HttpClient(handler)
{
    BaseAddress = new Uri("https://api.example.com"),
    Timeout = TimeSpan.FromSeconds(30)
};
```

### 方式三：测试代理

```csharp
var proxyService = ServiceContainer.GetService<ProxyManagementService>();

// 获取当前激活的代理
var proxyConfig = proxyService.GetActiveProxyConfig();

// 测试代理
var result = await proxyService.TestProxyAsync(proxyConfig);

if (result.IsSuccess)
{
    Console.WriteLine($"✅ 代理连接成功，延迟: {result.LatencyMs}ms");
    Console.WriteLine($"外网IP: {result.ExternalIp}");
}
else
{
    Console.WriteLine($"❌ 代理失败: {result.ErrorMessage}");
}
```

## 迁移现有代码

### 旧代码（重复的代理配置）

```csharp
// ❌ 旧方式 - 代码重复
var handler = new HttpClientHandler();

if (settings.EnableProxy && !string.IsNullOrWhiteSpace(settings.ProxyAddress))
{
    handler.UseProxy = true;
    handler.Proxy = new WebProxy(settings.ProxyAddress)
    {
        Credentials = BuildProxyCredential(settings)
    };
}

if (settings.SkipSslCertificateValidation)
{
    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
}

var client = new HttpClient(handler);
```

### 新代码（简洁统一）

```csharp
// ✅ 新方式 - 统一管理
var proxyService = ServiceContainer.GetService<ProxyManagementService>();
var client = proxyService.CreateHttpClient();
```

## 需要迁移的服务

以下服务需要更新以使用新的代理管理系统：

### 高优先级（核心服务）
1. ✅ `BinanceExchangeGateway` - 市场数据网关
2. ✅ `BinanceExchange` - 交易服务
3. ✅ `DeepSeekProvider` - AI服务
4. ✅ `OpenAIProvider` - AI服务
5. ✅ `ChatGLMProvider` - AI服务
6. ✅ `QwenProvider` - AI服务

### 中优先级
7. `OkxMarketDataProvider` - OKX市场数据
8. `BinanceRestClient` - Binance REST客户端

## 迁移示例

### 示例：迁移AI Provider

**Before:**
```csharp
private HttpClient CreateHttpClient(AiProviderConfig config)
{
    var settings = ServiceContainer.GetService<AppSettingsService>().Settings;
    var handler = new HttpClientHandler();

    // 配置代理
    if (settings.EnableProxy && !string.IsNullOrWhiteSpace(settings.ProxyAddress))
    {
        handler.UseProxy = true;
        handler.Proxy = new WebProxy(settings.ProxyAddress)
        {
            Credentials = BuildProxyCredential(settings)
        };
    }
    
    var client = new HttpClient(handler, disposeHandler: true)
    {
        BaseAddress = new Uri(config.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
    };

    return client;
}
```

**After:**
```csharp
private HttpClient CreateHttpClient(AiProviderConfig config)
{
    var proxyService = ServiceContainer.GetService<ProxyManagementService>();
    var handler = proxyService.CreateHttpClientHandler();
    
    var client = new HttpClient(handler, disposeHandler: true)
    {
        BaseAddress = new Uri(config.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
    };

    return client;
}
```

## 高级功能

### 智能代理选择（基于域名）

```csharp
var proxyConfig = new ProxyConfig
{
    // 仅对指定域名使用代理
    OnlyForDomains = new List<string> 
    { 
        "api.binance.com",
        "*.openai.com",
        "api.deepseek.com"
    },
    
    // 绕过代理的域名（直连）
    BypassDomains = new List<string>
    {
        "localhost",
        "127.0.0.1",
        "*.local"
    }
};

proxyService.AddOrUpdateProxyConfig(proxyConfig);
```

### 健康检查与监控

```csharp
// 订阅健康状态变化事件
proxyService.ProxyHealthChanged += (sender, args) =>
{
    var config = args.Config;
    Console.WriteLine($"代理 {config.Name} 状态: {config.HealthStatus}");
    
    if (config.HealthStatus == ProxyHealthStatus.Unhealthy)
    {
        // 处理代理失败
    }
};
```

## UI使用

在设置对话框中：
1. 选择代理类型（系统代理/HTTP/SOCKS5）
2. 输入代理地址和端口
3. 可选：输入认证信息
4. 点击"测试代理连接"按钮验证配置
5. 查看实时状态（延迟、外网IP）

## 注意事项

1. **线程安全**: ProxyManagementService 是线程安全的单例服务
2. **资源释放**: 通过ProxyManagementService创建的HttpClient需要手动释放
3. **健康检查**: 默认每60秒自动检查代理健康状态
4. **向后兼容**: 自动迁移旧的代理配置到新系统

## 性能优化

### 连接池复用
- 通过统一的ProxyManagementService创建HttpClient
- 避免频繁创建/销毁HttpClient
- 复用连接池，提升性能

### 示例：服务级HttpClient缓存

```csharp
public class MyService : IDisposable
{
    private readonly ProxyManagementService _proxyService;
    private HttpClient? _httpClient;
    
    public MyService()
    {
        _proxyService = ServiceContainer.GetService<ProxyManagementService>();
    }
    
    private HttpClient GetOrCreateHttpClient()
    {
        if (_httpClient == null)
        {
            _httpClient = _proxyService.CreateHttpClient(
                timeoutSeconds: 30
            );
        }
        return _httpClient;
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
```

## 故障排查

### 问题：代理测试失败

**检查清单：**
1. 代理服务器是否正在运行？
2. 地址和端口是否正确？
3. 防火墙是否阻止连接？
4. 是否需要认证信息？
5. 目标URL是否可访问？

### 问题：SSL证书错误

**解决方案：**
- 仅在调试环境启用"跳过SSL证书验证"
- 生产环境应使用有效证书

## 未来计划

- [ ] 支持PAC（代理自动配置）脚本
- [ ] 多代理配置与自动故障切换
- [ ] 代理性能统计与分析
- [ ] 代理密码加密存储
- [ ] WebSocket代理支持

