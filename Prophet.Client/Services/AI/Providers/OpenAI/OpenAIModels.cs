using System.Collections.Generic;
using Newtonsoft.Json;

namespace Prophet.Client.Services.AI.Providers.OpenAI;

/// <summary>
/// OpenAI 消息
/// </summary>
internal class OpenAIMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// OpenAI 聊天请求
/// </summary>
internal class OpenAIChatRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = "gpt-3.5-turbo";

    [JsonProperty("messages")]
    public List<OpenAIMessage> Messages { get; set; } = new();

    [JsonProperty("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonProperty("stream")]
    public bool Stream { get; set; } = false;
}

/// <summary>
/// OpenAI 聊天响应
/// </summary>
internal class OpenAIChatResponse
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
    public List<OpenAIChoice> Choices { get; set; } = new();

    [JsonProperty("usage")]
    public OpenAIUsage Usage { get; set; } = new();
}

/// <summary>
/// OpenAI 选择项
/// </summary>
internal class OpenAIChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("message")]
    public OpenAIMessage Message { get; set; } = new();

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// OpenAI 用量统计
/// </summary>
internal class OpenAIUsage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// OpenAI 错误响应
/// </summary>
internal class OpenAIErrorResponse
{
    [JsonProperty("error")]
    public OpenAIError Error { get; set; } = new();
}

/// <summary>
/// OpenAI 错误详情
/// </summary>
internal class OpenAIError
{
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("code")]
    public string? Code { get; set; }
}

/// <summary>
/// OpenAI 流式响应
/// </summary>
internal class OpenAIStreamResponse
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
    public List<OpenAIStreamChoice> Choices { get; set; } = new();

    [JsonProperty("usage")]
    public OpenAIUsage? Usage { get; set; }
}

/// <summary>
/// OpenAI 流式选择项
/// </summary>
internal class OpenAIStreamChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("delta")]
    public OpenAIDelta? Delta { get; set; }

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// OpenAI 增量内容
/// </summary>
internal class OpenAIDelta
{
    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("content")]
    public string? Content { get; set; }
}
