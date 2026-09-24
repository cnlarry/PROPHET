using System.Collections.Generic;
using Newtonsoft.Json;

namespace Prophet.Client.Services.AI.Providers.DeepSeek;

/// <summary>
/// DeepSeek 消息
/// </summary>
internal class DeepSeekMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// DeepSeek 聊天请求
/// </summary>
internal class DeepSeekChatRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = "deepseek-chat";

    [JsonProperty("messages")]
    public List<DeepSeekMessage> Messages { get; set; } = new();

    [JsonProperty("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonProperty("stream")]
    public bool Stream { get; set; } = false;
}

/// <summary>
/// DeepSeek 聊天响应
/// </summary>
internal class DeepSeekChatResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("object")]
    public string Object { get; set; } = string.Empty;

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("choices")]
    public List<DeepSeekChoice> Choices { get; set; } = new();

    [JsonProperty("usage")]
    public DeepSeekUsage Usage { get; set; } = new();
}

/// <summary>
/// DeepSeek 选择项
/// </summary>
internal class DeepSeekChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("message")]
    public DeepSeekMessage Message { get; set; } = new();

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// DeepSeek 用量统计
/// </summary>
internal class DeepSeekUsage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// DeepSeek 错误响应
/// </summary>
internal class DeepSeekErrorResponse
{
    [JsonProperty("error")]
    public DeepSeekError Error { get; set; } = new();
}

/// <summary>
/// DeepSeek 错误详情
/// </summary>
internal class DeepSeekError
{
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// DeepSeek 流式响应
/// </summary>
internal class DeepSeekStreamResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("object")]
    public string Object { get; set; } = string.Empty;

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("choices")]
    public List<DeepSeekStreamChoice> Choices { get; set; } = new();

    [JsonProperty("usage")]
    public DeepSeekUsage? Usage { get; set; }
}

/// <summary>
/// DeepSeek 流式选择项
/// </summary>
internal class DeepSeekStreamChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("delta")]
    public DeepSeekDelta? Delta { get; set; }

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// DeepSeek 增量内容
/// </summary>
internal class DeepSeekDelta
{
    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("content")]
    public string? Content { get; set; }
}
