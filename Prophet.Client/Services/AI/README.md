# Prophet AI Integration Architecture

## 📁 目录结构

```
Services/AI/
├── Abstractions/           # 抽象接口层
│   ├── IAiProvider.cs     # AI Provider 核心接口
│   ├── AiModels.cs        # 通用数据模型（AiRequest, AiResponse等）
│   └── AiException.cs     # AI 异常定义
│
├── Providers/             # 具体 Provider 实现
│   ├── DeepSeek/
│   │   ├── DeepSeekProvider.cs    # DeepSeek 实现
│   │   └── DeepSeekModels.cs      # DeepSeek 专用模型
│   ├── OpenAI/
│   │   ├── OpenAIProvider.cs      # OpenAI 实现
│   │   └── OpenAIModels.cs        # OpenAI 专用模型
│   └── ProviderFactory.cs         # Provider 工厂类
│
├── Core/
│   ├── AiServiceManager.cs        # 统一的服务管理器
│   └── PromptTemplates.cs         # 提示词模板（业务逻辑）
│
└── Configuration/
    └── AiProviderConfig.cs        # Provider 配置模型
```

## 🎯 架构设计原则

### 1. **接口抽象** - 解耦业务与实现
- `IAiProvider` 定义了所有 AI Provider 必须实现的标准接口
- 业务代码只依赖接口，不依赖具体实现
- 便于切换和扩展不同的 AI 服务商

### 2. **工厂模式** - 简化对象创建
- `ProviderFactory` 统一管理所有 Provider 的创建
- 添加新 Provider 只需：
  1. 实现 `IAiProvider` 接口
  2. 在 `ProviderFactory` 中注册

### 3. **配置统一** - 标准化参数管理
- `AiProviderConfig` 定义通用配置参数
- 所有 Provider 共享标准参数（ApiKey, Model, Temperature等）
- 通过 `ExtraParameters` 支持 Provider 特有配置

### 4. **业务分离** - 领域逻辑独立
- `PromptTemplates` 管理所有提示词模板
- `AiServiceManager` 提供高级业务方法
- 与具体 Provider 完全解耦

## 🔧 使用方式

### 初始化 AI Service

```csharp
// 从配置加载（自动使用 AppSettings 中配置的 Provider）
await AiServiceManager.Instance.LoadFromSettingsAsync();

// 或手动初始化特定 Provider
var config = new AiProviderConfig
{
    ProviderType = "OpenAI",
    ApiKey = "sk-xxxxx",
    Model = "gpt-4",
    Temperature = 0.7,
    MaxTokens = 4096
};
await AiServiceManager.Instance.InitializeProviderAsync(config);
```

### 调用业务功能

```csharp
// 生成策略代码
var strategy = await AiServiceManager.Instance.GenerateStrategyAsync(
    "创建一个基于RSI和MACD的策略",
    contextCode: currentStrategyCode
);

// 分析回测结果
var analysis = await AiServiceManager.Instance.AnalyzeBacktestResultAsync(
    backtestResult,
    currentStrategyCode
);

// 预测市场走势
var prediction = await AiServiceManager.Instance.PredictMarketTrendAsync(
    "BTCUSDT",
    "1h",
    marketDataSummary
);
```

### 切换 Provider

```csharp
// 获取所有可用的 Provider
var providers = AiServiceManager.Instance.AvailableProviders;

// 切换到已初始化的 Provider
AiServiceManager.Instance.SwitchProvider("OpenAI");

// 验证连接
bool isConnected = await AiServiceManager.Instance.ValidateConnectionAsync();
```

## 🚀 添加新的 AI Provider

### 步骤 1: 创建 Provider 类

在 `Providers/YourProvider/` 目录下创建：

```csharp
public class YourProvider : IAiProvider
{
    public AiProviderInfo ProviderInfo { get; } = new AiProviderInfo
    {
        Name = "YourProvider",
        DisplayName = "Your AI Provider",
        SupportedModels = new List<string> { "model-1", "model-2" },
        DefaultModel = "model-1",
        DefaultApiBaseUrl = "https://api.yourprovider.com"
    };

    public bool IsInitialized { get; private set; }

    public async Task<bool> InitializeAsync(AiProviderConfig config)
    {
        // 初始化逻辑
    }

    public async Task<AiResponse> ChatCompletionAsync(AiRequest request)
    {
        // 实现聊天补全
    }

    public async Task<bool> ValidateConnectionAsync()
    {
        // 验证连接
    }

    public AiProviderConfig? GetCurrentConfig() { /* ... */ }
    public void Dispose() { /* ... */ }
}
```

### 步骤 2: 注册到工厂

在 `ProviderFactory.cs` 中添加：

```csharp
private static readonly Dictionary<string, Func<IAiProvider>> _providerCreators = new()
{
    { "DeepSeek", () => new DeepSeekProvider() },
    { "OpenAI", () => new OpenAIProvider() },
    { "YourProvider", () => new YourProvider() },  // ← 添加这一行
};
```

完成！新 Provider 已可用。

## 📊 当前支持的 Provider

| Provider | 状态 | 模型 | 说明 |
|----------|------|------|------|
| **DeepSeek** | ✅ 完整实现 | deepseek-chat, deepseek-coder | 价格优惠，中文能力优秀 |
| **OpenAI** | ✅ 完整实现 | gpt-3.5-turbo, gpt-4, gpt-4o | 行业标准，能力全面 |
| **Qwen** | ✅ 完整实现 | qwen-turbo, qwen-plus, qwen-max | 阿里云旗下，中文能力优秀 |
| **ChatGLM** | ✅ 完整实现 | glm-4, glm-4-flash, glm-3-turbo | 清华系，中文能力优秀 |
| **Claude** | ⏳ 待实现 | - | 可按需添加 |
| **Gemini** | ⏳ 待实现 | - | 可按需添加 |

## 🔄 迁移指南

### 从旧代码迁移

#### 旧代码
```csharp
using Prophet.Client.Services.DeepSeek;

// 初始化
AiServiceManager.Instance.Initialize(apiKey, baseUrl);

// 调用
var result = await AiServiceManager.Instance.GenerateStrategyAsync(query);
```

#### 新代码
```csharp
using Prophet.Client.Services.AI.Core;
using Prophet.Client.Services.AI.Configuration;

// 初始化（从配置）
await AiServiceManager.Instance.LoadFromSettingsAsync();

// 或手动初始化
var config = new AiProviderConfig
{
    ProviderType = "DeepSeek",
    ApiKey = apiKey,
    ApiBaseUrl = baseUrl
};
await AiServiceManager.Instance.InitializeProviderAsync(config);

// 调用（API 不变）
var result = await AiServiceManager.Instance.GenerateStrategyAsync(query);
```

## 🛡️ 错误处理

架构提供了统一的异常类型：

```csharp
try
{
    await AiServiceManager.Instance.GenerateStrategyAsync(query);
}
catch (AiProviderNotInitializedException)
{
    // Provider 未初始化
}
catch (AiInvalidApiKeyException)
{
    // API 密钥无效
}
catch (AiQuotaExceededException)
{
    // 配额不足
}
catch (AiRequestTimeoutException)
{
    // 请求超时
}
catch (AiNetworkException)
{
    // 网络错误
}
catch (AiException ex)
{
    // 通用 AI 异常
}
```

## 📝 配置说明

### AppSettings 配置项

```csharp
// AI Provider 选择
CurrentAiProvider = "DeepSeek"  // 或 "OpenAI", "Qwen", "ChatGLM" 等

// DeepSeek 配置
DeepSeekApiKey = "sk-xxxxx"

// OpenAI 配置
OpenAiApiKey = "sk-xxxxx"

// Qwen 配置
QwenApiKey = "sk-xxxxx"

// ChatGLM 配置
ChatGLMApiKey = "xxxxx"

// 通用配置
AiApiBaseUrl = "https://api.deepseek.com"
AiModel = "deepseek-chat"
AiTemperature = 0.7
AiMaxTokens = 4096
AiEnableStreaming = true
```

## 🎯 最佳实践

1. **优先使用 LoadFromSettingsAsync()**
   - 自动从 AppSettings 加载配置
   - 简化初始化流程

2. **错误处理**
   - 始终捕获特定的 AI 异常类型
   - 提供用户友好的错误提示

3. **Provider 切换**
   - 先初始化多个 Provider
   - 运行时动态切换无需重启

4. **配置管理**
   - 敏感信息（API Key）应加密存储
   - 使用 Clone() 方法复制配置避免修改原始对象

## 🔗 相关文件

- **旧代码位置**: `Services/DeepSeek/` （已废弃，可删除）
- **新代码位置**: `Services/AI/`
- **配置文件**: `Models/AppSettings.cs`
- **UI设置**: `Views/Dialogs/SettingsDialog.axaml.cs`

## ⚠️ 注意事项

1. **旧代码兼容性**
   - 旧的 `Services/DeepSeek/` 目录可以删除
   - 所有引用已迁移到新架构

2. **API 兼容性**
   - 业务方法签名保持不变
   - 现有调用代码无需修改

3. **性能考虑**
   - Provider 实例可复用
   - 避免频繁创建和销毁

---

**更新时间**: 2025-12-09  
**架构版本**: v2.0  
**维护者**: Prophet Team
