# Prophet Client AI 集成文档

**最后更新**: 2025-01-27  
**版本**: 1.0

---

## 📋 目录

1. [概述](#概述)
2. [架构设计](#架构设计)
3. [核心组件](#核心组件)
4. [功能特性](#功能特性)
5. [使用指南](#使用指南)
6. [配置说明](#配置说明)
7. [API集成](#api集成)
8. [交互场景](#交互场景)
9. [开发指南](#开发指南)
10. [故障排除](#故障排除)

---

## 🎯 概述

Prophet 集成了 DeepSeek AI 服务，为量化交易策略开发提供智能辅助功能。AI助手可以帮助用户：

- 🤖 **生成策略代码**：根据自然语言描述生成符合 Prophet DSL 语法的交易策略
- 📊 **分析回测结果**：分析回测数据并提供优化建议
- 🔮 **预测市场走势**：基于市场数据提供交易建议
- ✨ **智能交互操作**：直接操作应用程序，实现无缝的工作流

### 技术栈

- **AI服务提供商**: DeepSeek API
- **集成方式**: RESTful API
- **提示词管理**: 动态生成，基于 DSL 规则系统
- **UI框架**: Avalonia UI

---

## 🏗️ 架构设计

### 整体架构

```
┌─────────────────────────────────────────────────────────────┐
│                    Prophet.Client (C#)                       │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  UI层                                                │   │
│  │  - AiAssistantPanel (AI助手面板)                     │   │
│  │  - SettingsView (AI配置界面)                         │   │
│  └────────────────┬─────────────────────────────────────┘   │
│                   │                                           │
│  ┌────────────────┴─────────────────────────────────────┐   │
│  │  ViewModel层                                         │   │
│  │  - AiAssistantViewModel                              │   │
│  └────────────────┬─────────────────────────────────────┘   │
│                   │                                           │
│  ┌────────────────┴─────────────────────────────────────┐   │
│  │  服务层                                              │   │
│  │  ┌──────────────────────────────────────────────┐   │   │
│  │  │  AiServiceManager (AI服务管理器)             │   │   │
│  │  │  - 配置管理                                    │   │   │
│  │  │  - API调用协调                                 │   │   │
│  │  └────────────────┬───────────────────────────────┘   │   │
│  │  ┌────────────────┴───────────────────────────────┐   │   │
│  │  │  DeepSeekApiClient (API客户端)                 │   │   │
│  │  │  - HTTP请求处理                                 │   │   │
│  │  │  - 错误处理                                     │   │   │
│  │  └────────────────┬───────────────────────────────┘   │   │
│  │  ┌────────────────┴───────────────────────────────┐   │   │
│  │  │  PromptTemplates (提示词模板)                   │   │   │
│  │  │  - 动态提示词生成                                │   │   │
│  │  │  - DSL规则集成                                  │   │   │
│  │  └─────────────────────────────────────────────────┘   │   │
│  │                                                          │   │
│  │  ┌─────────────────────────────────────────────────┐   │   │
│  │  │  AiOperationService (AI操作服务)                │   │   │
│  │  │  - 创建新Tab                                     │   │   │
│  │  │  - 替换代码                                     │   │   │
│  │  │  - 保存版本并回测                               │   │   │
│  │  └─────────────────────────────────────────────────┘   │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌─────────────────────┐
                    │   DeepSeek API      │
                    │   (外部服务)        │
                    └─────────────────────┘
```

### 数据流

```
用户输入
   │
   ▼
AiAssistantPanel (UI)
   │
   ▼
AiAssistantViewModel
   │
   ▼
AiServiceManager
   │
   ├─► PromptTemplates (生成提示词)
   │       │
   │       └─► DSLRulesService (获取DSL规则)
   │
   └─► DeepSeekApiClient (调用API)
           │
           ▼
       DeepSeek API
           │
           ▼
       AI响应
           │
           ▼
   AiOperationService (执行操作)
           │
           ├─► 创建新Tab
           ├─► 替换代码
           └─► 保存版本并回测
```

---

## 🔧 核心组件

### 1. AiServiceManager

**位置**: `Prophet.Client/Services/DeepSeek/AiServiceManager.cs`

**职责**:
- AI服务的统一入口点（单例模式）
- 管理API客户端生命周期
- 提供高级AI功能接口

**主要方法**:

```csharp
// 初始化服务
public void Initialize(string apiKey, string baseUrl = "https://api.deepseek.com")
public void LoadConfig()  // 从AppSettings加载配置

// AI功能调用
public async Task<string> GenerateStrategyAsync(string userQuery, string? contextCode = null)
public async Task<string> AnalyzeBacktestResultAsync(BacktestResult backtestResult, string currentStrategyCode)
public async Task<string> PredictMarketTrendAsync(string symbol, string timeframe, string marketDataSummary)
```

**使用示例**:

```csharp
// 加载配置
AiServiceManager.Instance.LoadConfig();

// 生成策略
var code = await AiServiceManager.Instance.GenerateStrategyAsync(
    "帮我生成一个基于MACD的趋势跟踪策略",
    currentStrategyCode
);
```

### 2. DeepSeekApiClient

**位置**: `Prophet.Client/Services/DeepSeek/DeepSeekApiClient.cs`

**职责**:
- 封装 DeepSeek API 的 HTTP 请求
- 处理请求/响应序列化
- 错误处理和重试逻辑

**主要特性**:
- ✅ 自动设置 Authorization Header
- ✅ 友好的错误消息（中文）
- ✅ 支持流式响应（未来扩展）
- ✅ 超时控制（默认30秒）

**错误处理**:

```csharp
// 自动识别常见错误并转换为友好的中文消息
- "Insufficient Balance" → "账户余额不足"
- "invalid_api_key" → "API密钥无效"
- "rate_limit_exceeded" → "请求频率超限"
```

### 3. PromptTemplates

**位置**: `Prophet.Client/Services/DeepSeek/PromptTemplates.cs`

**职责**:
- 动态生成AI提示词
- 集成DSL规则系统
- 提供不同场景的提示词模板

**核心特性**:

1. **动态提示词生成**
   - 基于 `DSLRulesService` 动态生成包含所有DSL规则的提示词
   - 包含62个指标、18个数学函数、8个信号函数等完整信息

2. **提示词缓存**
   - 首次生成后缓存，提高性能

3. **多场景支持**
   - 策略生成提示词
   - 回测分析提示词
   - 市场预测提示词

**提示词结构**:

```
系统角色定义
├─ 核心任务说明
├─ DSL语法规则
│   ├─ 信号函数（8个）
│   ├─ 技术指标（62个）
│   ├─ 数据函数（30个）
│   ├─ 数学函数（18个）
│   ├─ 时间序列函数（2个）
│   ├─ 环境变量
│   └─ 枚举值
├─ 语法规则详解
├─ 最佳实践
└─ 输出格式要求
```

### 4. AiOperationService

**位置**: `Prophet.Client/Services/AiOperationService.cs`

**职责**:
- 协调AI与应用程序的交互操作
- 实现三个核心交互场景

**主要方法**:

```csharp
// 场景1：创建新Tab并插入代码
public async Task<bool> CreateNewTabWithCodeAsync(string dslCode, string strategyName = "AI生成策略")

// 场景2：替换当前编辑器代码
public async Task<bool> ReplaceCurrentCodeAsync(string newDslCode)

// 场景3：保存新版本并启动回测
public async Task<(bool success, string? versionString, string? message)> SaveVersionAndBacktestAsync(
    string strategyId, 
    string dslCode, 
    string changeDescription = "AI优化建议")
```

### 5. AiAssistantViewModel

**位置**: `Prophet.Client/ViewModels/AiAssistantViewModel.cs`

**职责**:
- 管理AI对话状态
- 消息列表管理
- 操作类型判断

**核心属性**:

```csharp
public ObservableCollection<AiMessageModel> Messages { get; }
public string InputText { get; set; }
public bool IsLoading { get; set; }
public bool HasMessages => Messages.Count > 0;
```

**消息模型** (`AiMessageModel`):

```csharp
public class AiMessageModel
{
    public string Content { get; set; }        // 消息内容
    public string Code { get; set; }          // DSL代码（如果有）
    public bool IsUser { get; set; }          // 是否为用户消息
    public DateTime Timestamp { get; set; }   // 时间戳
    public AiOperationType OperationType { get; set; }  // 操作类型
    public string? CurrentStrategyId { get; set; }      // 当前策略ID
}
```

**操作类型枚举**:

```csharp
public enum AiOperationType
{
    None,           // 无操作
    CreateNewTab,   // 场景1：创建新Tab并插入代码
    ReplaceCode,    // 场景2：替换当前编辑器代码
    SaveAndBacktest // 场景3：保存新版本并回测
}
```

### 6. AiAssistantPanel

**位置**: `Prophet.Client/Views/ContextMenus/AiAssistantPanel.axaml`

**职责**:
- AI助手的UI界面
- 消息显示和交互
- 操作按钮处理

**UI特性**:
- 💬 微信风格的气泡消息
- 🎯 指向头像的小箭头
- 📝 代码预览区域
- 🔘 智能操作按钮（根据操作类型显示）

---

## ✨ 功能特性

### 1. 策略生成

**功能描述**: 根据自然语言描述生成符合 Prophet DSL 语法的交易策略代码。

**使用场景**:
- 用户说："帮我生成一个基于EMA均线的趋势跟踪策略"
- AI返回完整的DSL代码，包含注释和说明

**实现流程**:

```
用户输入 → AiServiceManager.GenerateStrategyAsync()
         → PromptTemplates.GetStrategyGenerationPrompt()
         → DeepSeekApiClient.ChatCompletionAsync()
         → 解析响应，提取代码
         → 显示在AI助手面板
```

### 2. 回测结果分析

**功能描述**: 分析回测结果，提供优化建议和改进方案。

**使用场景**:
- 用户完成回测后，要求AI分析结果
- AI提供详细的性能分析和优化建议

**实现流程**:

```
回测结果 → PromptTemplates.GenerateBacktestSummary()
         → PromptTemplates.GetBacktestAnalysisPrompt()
         → DeepSeekApiClient.ChatCompletionAsync()
         → 返回分析报告
```

### 3. 市场预测

**功能描述**: 基于市场数据提供交易建议和走势预测。

**使用场景**:
- 用户提供市场数据摘要
- AI分析并给出交易建议

### 4. 智能交互操作

**功能描述**: AI可以直接操作应用程序，实现无缝的工作流。

**三个核心场景**:

#### 场景1：创建新Tab并插入代码

**触发条件**: 用户要求生成新策略

**操作流程**:
1. AI生成策略代码
2. 显示"创建新Tab"按钮
3. 用户点击后，自动创建新Tab并插入代码

**实现**:
```csharp
AiOperationService.Instance.CreateNewTabWithCodeAsync(dslCode, "AI生成策略");
```

#### 场景2：替换当前编辑器代码

**触发条件**: 用户要求审查/优化当前策略

**操作流程**:
1. AI分析当前策略并生成优化版本
2. 显示"替换当前代码"按钮
3. 用户点击后，直接替换编辑器中的代码

**实现**:
```csharp
AiOperationService.Instance.ReplaceCurrentCodeAsync(newDslCode);
```

#### 场景3：保存新版本并回测

**触发条件**: 用户要求分析回测结果并改进

**操作流程**:
1. AI分析回测结果并生成改进代码
2. 显示"保存并回测"按钮
3. 用户点击后，弹出确认对话框
4. 确认后保存为新版本并自动启动回测

**实现**:
```csharp
var (success, version, message) = await AiOperationService.Instance
    .SaveVersionAndBacktestAsync(strategyId, dslCode, "AI优化建议");
```

---

## 📖 使用指南

### 初始配置

1. **获取 DeepSeek API 密钥**
   - 访问 [DeepSeek官网](https://www.deepseek.com/)
   - 注册账号并获取API密钥

2. **配置API密钥**
   - 打开Prophet应用
   - 进入"设置" → "AI助手"
   - 输入API密钥
   - 保存配置

3. **验证配置**
   - 打开AI助手面板（策略页面右侧）
   - 发送测试消息
   - 如果配置正确，AI会正常响应

### 基本使用

#### 生成新策略

1. 打开AI助手面板
2. 输入："帮我生成一个趋势跟踪策略"
3. AI返回代码后，点击"创建新Tab"按钮
4. 代码会自动插入到新Tab中

#### 优化现有策略

1. 打开一个策略文件
2. 在AI助手面板输入："优化这个策略"
3. AI分析当前代码并返回优化版本
4. 点击"替换当前代码"按钮
5. 代码会自动替换

#### 分析回测结果

1. 完成一次回测
2. 在AI助手面板输入："分析回测结果"
3. AI分析回测数据并提供建议
4. 如果AI提供了改进代码，点击"保存并回测"
5. 确认后自动保存为新版本并启动回测

### 快捷操作

AI助手面板提供了快捷操作按钮：

- 📈 **生成趋势跟踪策略**: 一键生成基于EMA的趋势跟踪策略
- 🛡️ **添加止损止盈**: 为当前策略添加止损止盈逻辑
- ⚡ **优化入场条件**: 优化策略的入场条件
- 📖 **解释代码**: 详细解释当前策略的逻辑

---

## ⚙️ 配置说明

### AppSettings 配置项

**位置**: `Prophet.Client/Models/AppSettings.cs`

```csharp
public class AppSettings
{
    public string DeepSeekApiKey { get; set; } = string.Empty;      // DeepSeek API密钥
    public string AiApiBaseUrl { get; set; } = "https://api.deepseek.com";  // API基础地址
    public string AiModel { get; set; } = "deepseek-chat";         // 使用的模型
    public double AiTemperature { get; set; } = 0.7;               // 温度参数（0-1）
    public int AiMaxTokens { get; set; } = 4096;                   // 最大token数
}
```

### 配置参数说明

| 参数 | 说明 | 默认值 | 推荐值 |
|------|------|--------|--------|
| `DeepSeekApiKey` | API密钥 | - | 必填 |
| `AiApiBaseUrl` | API基础地址 | `https://api.deepseek.com` | 一般不需要修改 |
| `AiModel` | 使用的模型 | `deepseek-chat` | `deepseek-chat` 或 `deepseek-coder` |
| `AiTemperature` | 温度参数 | `0.7` | `0.3-0.9`（越低越确定，越高越创造性） |
| `AiMaxTokens` | 最大token数 | `4096` | `2048-8192`（根据需求调整） |

### 模型选择建议

- **deepseek-chat**: 通用对话模型，适合策略生成和分析
- **deepseek-coder**: 代码专用模型，适合代码生成和优化（如果可用）

---

## 🔌 API集成

### DeepSeek API 端点

**基础URL**: `https://api.deepseek.com`

**主要端点**:
- `POST /v1/chat/completions` - 聊天补全（用于策略生成和分析）

### 请求格式

```json
{
  "model": "deepseek-chat",
  "messages": [
    {
      "role": "system",
      "content": "系统提示词..."
    },
    {
      "role": "user",
      "content": "用户消息..."
    }
  ],
  "temperature": 0.7,
  "max_tokens": 4096
}
```

### 响应格式

```json
{
  "id": "chatcmpl-xxx",
  "object": "chat.completion",
  "created": 1234567890,
  "model": "deepseek-chat",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "AI响应内容..."
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 100,
    "completion_tokens": 200,
    "total_tokens": 300
  }
}
```

### 错误处理

常见错误码和处理：

| 错误码 | 说明 | 处理方式 |
|--------|------|----------|
| `invalid_api_key` | API密钥无效 | 检查密钥是否正确 |
| `insufficient_quota` | 余额不足 | 充值账户 |
| `rate_limit_exceeded` | 请求频率超限 | 降低请求频率 |
| `invalid_request_error` | 请求参数错误 | 检查请求格式 |

---

## 🎬 交互场景

### 场景1：生成新策略

**用户输入**: "帮我生成一个基于MACD的趋势跟踪策略"

**AI响应**:
```
好的，我来为你生成一个基于MACD的趋势跟踪策略。

这个策略的核心思路是：
1. 使用MACD指标判断趋势方向
2. 当MACD金叉且MACD值大于0时买入
3. 当MACD死叉或MACD值小于0时卖出

以下是完整的DSL代码：

\`\`\`dsl
// MACD趋势跟踪策略
ALL {
    $(5m).MACD().trend = BULLISH;
    $(5m).MACD().macd > 0;
} = BUY;

ALL {
    $(5m).MACD().trend = BEARISH;
    $(5m).MACD().macd < 0;
} = SELL;
\`\`\`
```

**操作**: 点击"创建新Tab"按钮 → 代码自动插入到新Tab

### 场景2：优化现有策略

**用户输入**: "优化这个策略，增加RSI过滤"

**AI响应**:
```
好的，我来为你的策略添加RSI过滤条件，避免在超买超卖区域交易。

优化后的策略：
1. 保留原有的MACD趋势判断
2. 增加RSI过滤：买入时RSI < 70，卖出时RSI > 30
3. 这样可以避免在极端市场条件下交易

\`\`\`dsl
// 优化后的MACD+RSI策略
ALL {
    $(5m).MACD().trend = BULLISH;
    $(5m).MACD().macd > 0;
    $(5m).RSI().value < 70;
} = BUY;

ALL {
    $(5m).MACD().trend = BEARISH;
    $(5m).MACD().macd < 0;
    $(5m).RSI().value > 30;
} = SELL;
\`\`\`
```

**操作**: 点击"替换当前代码"按钮 → 代码自动替换

### 场景3：分析回测并改进

**用户输入**: "分析回测结果，给出改进建议"

**AI响应**:
```
根据回测结果分析：

**性能指标**:
- 总收益率: 15.3%
- 最大回撤: 8.2%
- 夏普比率: 1.2
- 胜率: 52%

**问题诊断**:
1. 胜率偏低，可能是入场条件不够严格
2. 回撤较大，建议增加止损机制
3. 交易频率较高，可以增加过滤条件

**改进建议**:
1. 增加成交量过滤，只在成交量放大时交易
2. 添加止损逻辑，限制单笔亏损
3. 优化入场条件，提高信号质量

以下是改进后的代码：

\`\`\`dsl
// 改进后的策略
ALL {
    $(5m).MACD().trend = BULLISH;
    $(5m).MACD().macd > 0;
    $(5m).RSI().value < 70;
    $(5m).VOLUME().value > $(5m).VOLUME().ma(20);
} = BUY(TP=5%, SL=3%);

ALL {
    $(5m).MACD().trend = BEARISH;
    $(5m).MACD().macd < 0;
    $(5m).RSI().value > 30;
} = SELL;
\`\`\`
```

**操作**: 点击"保存并回测"按钮 → 确认 → 自动保存为新版本并启动回测

---

## 🛠️ 开发指南

### 添加新的AI功能

1. **在 AiServiceManager 中添加方法**:

```csharp
public async Task<string> YourNewFeatureAsync(string input)
{
    EnsureInitialized();
    var settings = AppSettingsService.Instance.Settings;
    
    var messages = PromptTemplates.Instance.GetYourPrompt(input);
    var response = await _apiClient!.ChatCompletionAsync(
        messages, 
        settings.AiModel, 
        settings.AiTemperature, 
        settings.AiMaxTokens
    );
    return response.Choices[0].Message.Content;
}
```

2. **在 PromptTemplates 中添加提示词模板**:

```csharp
public List<ChatMessage> GetYourPrompt(string input)
{
    return new List<ChatMessage>
    {
        new ChatMessage { Role = "system", Content = "系统提示词..." },
        new ChatMessage { Role = "user", Content = input }
    };
}
```

3. **在 UI 中调用**:

```csharp
var result = await AiServiceManager.Instance.YourNewFeatureAsync(userInput);
```

### 扩展操作类型

1. **添加新的操作类型枚举值**:

```csharp
public enum AiOperationType
{
    // ... 现有类型
    YourNewOperation  // 新操作类型
}
```

2. **在 AiOperationService 中实现操作**:

```csharp
public async Task<bool> YourNewOperationAsync(string param)
{
    // 实现操作逻辑
}
```

3. **在 AiAssistantPanel 中添加按钮和处理**:

```csharp
private async void OnYourNewOperation(object? sender, RoutedEventArgs e)
{
    // 处理逻辑
}
```

### 自定义提示词

提示词模板基于 `DSLRulesService` 动态生成，包含完整的DSL规则信息。如果需要自定义：

1. 修改 `PromptTemplates.cs` 中的生成逻辑
2. 调整提示词结构和内容
3. 清除缓存以重新生成：`_cachedStrategyPrompt = null;`

---

## 🔍 故障排除

### 常见问题

#### 1. AI服务未初始化

**症状**: 提示"请先在设置中配置DeepSeek API密钥"

**解决方案**:
1. 检查 `AppSettings.DeepSeekApiKey` 是否已设置
2. 确认 `AiServiceManager.Instance.LoadConfig()` 已调用
3. 检查设置页面中的API密钥是否正确保存

#### 2. API调用失败

**症状**: 提示"API调用失败"或"账户余额不足"

**解决方案**:
1. 检查API密钥是否正确
2. 检查账户余额是否充足
3. 检查网络连接是否正常
4. 查看控制台输出的详细错误信息

#### 3. 操作类型判断错误

**症状**: 显示错误的操作按钮

**解决方案**:
1. 检查 `DetermineOperationType` 方法的逻辑
2. 调整关键词匹配规则
3. 手动指定操作类型（开发时）

#### 4. 代码解析失败

**症状**: AI返回了代码，但无法正确提取

**解决方案**:
1. 检查 `ParseAiResponse` 方法的正则表达式
2. 确认AI返回的代码块格式正确（```dsl ... ```）
3. 查看控制台输出的原始响应

### 调试技巧

1. **启用详细日志**:
   ```csharp
   Console.WriteLine($"AI响应: {response}");
   ```

2. **检查提示词**:
   ```csharp
   var prompt = PromptTemplates.Instance.GetStrategyGenerationPrompt(userQuery, contextCode);
   Console.WriteLine($"提示词: {string.Join("\n", prompt.Select(m => $"{m.Role}: {m.Content}"))}");
   ```

3. **验证API响应**:
   ```csharp
   var response = await _apiClient.ChatCompletionAsync(...);
   Console.WriteLine($"响应: {JsonConvert.SerializeObject(response, Formatting.Indented)}");
   ```

---

## 📚 相关文档

- [Prophet DSL 规范](../doc/Prophet%20DSL%20规范/)
- [DSL规则系统](../Prophet.Client/Resources/Rules/DSLRules.yaml)
- [DeepSeek API文档](https://platform.deepseek.com/api-docs/)

---

## 🤝 贡献指南

如果你想要改进AI集成功能：

1. **改进提示词**: 修改 `PromptTemplates.cs`
2. **添加新功能**: 扩展 `AiServiceManager`
3. **优化UI**: 改进 `AiAssistantPanel`
4. **增强交互**: 扩展 `AiOperationService`

---

## 📝 更新日志

### v1.0 (2025-01-27)
- ✅ 初始版本发布
- ✅ 集成 DeepSeek API
- ✅ 实现策略生成功能
- ✅ 实现回测分析功能
- ✅ 实现三个交互场景
- ✅ 动态提示词生成
- ✅ 微信风格UI设计

---

## 📧 联系方式

如有问题或建议，请通过以下方式联系：

- GitHub Issues: [项目地址]
- Email: [联系邮箱]

---

**文档维护者**: Prophet开发团队  
**最后更新**: 2025-01-27
