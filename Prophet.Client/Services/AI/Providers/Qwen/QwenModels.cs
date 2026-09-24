using System.Collections.Generic;
using Newtonsoft.Json;

namespace Prophet.Client.Services.AI.Providers.Qwen;

internal class QwenMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
}

internal class QwenInput
{
    [JsonProperty("messages")]
    public List<QwenMessage> Messages { get; set; } = new();
}

internal class QwenParameters
{
    [JsonProperty("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonProperty("top_p")]
    public double TopP { get; set; } = 0.8;
    
    [JsonProperty("incremental_output")]
    public bool IncrementalOutput { get; set; } = false;
}

internal class QwenChatRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = "qwen-plus";

    [JsonProperty("input")]
    public QwenInput Input { get; set; } = new();

    [JsonProperty("parameters")]
    public QwenParameters Parameters { get; set; } = new();
}

internal class QwenChoice
{
    [JsonProperty("message")]
    public QwenMessage Message { get; set; } = new();

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class QwenOutput
{
    [JsonProperty("choices")]
    public List<QwenChoice> Choices { get; set; } = new();
}

internal class QwenUsage
{
    [JsonProperty("input_tokens")]
    public int InputTokens { get; set; }

    [JsonProperty("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

internal class QwenChatResponse
{
    [JsonProperty("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonProperty("output")]
    public QwenOutput Output { get; set; } = new();

    [JsonProperty("usage")]
    public QwenUsage? Usage { get; set; }
}

internal class QwenErrorResponse
{
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Qwen 流式响应
/// </summary>
internal class QwenStreamResponse
{
    [JsonProperty("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonProperty("output")]
    public QwenOutput Output { get; set; } = new();

    [JsonProperty("usage")]
    public QwenUsage? Usage { get; set; }
}
