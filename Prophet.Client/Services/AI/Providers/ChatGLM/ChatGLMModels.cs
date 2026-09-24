using System.Collections.Generic;
using Newtonsoft.Json;

namespace Prophet.Client.Services.AI.Providers.ChatGLM;

internal class ChatGLMMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
}

internal class ChatGLMRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = "glm-4";

    [JsonProperty("messages")]
    public List<ChatGLMMessage> Messages { get; set; } = new();

    [JsonProperty("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonProperty("top_p")]
    public double TopP { get; set; } = 0.7;
    
    [JsonProperty("stream")]
    public bool Stream { get; set; } = false;
}

internal class ChatGLMChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("message")]
    public ChatGLMMessage Message { get; set; } = new();

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class ChatGLMUsage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

internal class ChatGLMResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("choices")]
    public List<ChatGLMChoice> Choices { get; set; } = new();

    [JsonProperty("usage")]
    public ChatGLMUsage Usage { get; set; } = new();
}

internal class ChatGLMError
{
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}

internal class ChatGLMErrorResponse
{
    [JsonProperty("error")]
    public ChatGLMError? Error { get; set; }
}

/// <summary>
/// ChatGLM 流式响应
/// </summary>
internal class ChatGLMStreamResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("choices")]
    public List<ChatGLMStreamChoice> Choices { get; set; } = new();

    [JsonProperty("usage")]
    public ChatGLMUsage? Usage { get; set; }
}

/// <summary>
/// ChatGLM 流式选择项
/// </summary>
internal class ChatGLMStreamChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("delta")]
    public ChatGLMDelta? Delta { get; set; }

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// ChatGLM 增量内容
/// </summary>
internal class ChatGLMDelta
{
    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("content")]
    public string? Content { get; set; }
}
