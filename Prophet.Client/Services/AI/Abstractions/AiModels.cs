using System.Collections.Generic;

namespace Prophet.Client.Services.AI.Abstractions;

/// <summary>
/// AI 消息
/// </summary>
public class AiMessage
{
    /// <summary>
    /// 消息角色 (system, user, assistant)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// AI 请求
/// </summary>
public class AiRequest
{
    /// <summary>
    /// 对话消息列表
    /// </summary>
    public List<AiMessage> Messages { get; set; } = new();

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 温度参数 (0.0 - 2.0)
    /// 控制输出的随机性：0=确定性，2=最大随机性
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 最大Token数
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// 是否启用流式响应
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// Top P 参数 (nucleus sampling)
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// 扩展参数（用于不同 Provider 的特殊需求）
    /// </summary>
    public Dictionary<string, object>? ExtraParameters { get; set; }
}

/// <summary>
/// AI 响应
/// </summary>
public class AiResponse
{
    /// <summary>
    /// 响应ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 响应内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 使用的模型
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 提示词 Token 数
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// 补全 Token 数
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 结束原因 (stop, length, etc.)
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// AI 流式响应块
/// </summary>
public class AiStreamChunk
{
    /// <summary>
    /// 响应ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 内容块（增量内容）
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 是否是最后一块
    /// </summary>
    public bool IsLast { get; set; } = false;

    /// <summary>
    /// 结束原因
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// 使用情况（只在最后一块中提供）
    /// </summary>
    public AiUsage? Usage { get; set; }
}

/// <summary>
/// Token 使用情况
/// </summary>
public class AiUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// AI Provider 信息
/// </summary>
public class AiProviderInfo
{
    /// <summary>
    /// Provider 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 支持的模型列表
    /// </summary>
    public List<string> SupportedModels { get; set; } = new();

    /// <summary>
    /// 默认模型
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// 是否支持流式响应
    /// </summary>
    public bool SupportsStreaming { get; set; } = false;

    /// <summary>
    /// API 基础地址
    /// </summary>
    public string DefaultApiBaseUrl { get; set; } = string.Empty;
}
