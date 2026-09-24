using System.Collections.Generic;

namespace Prophet.Client.Services.AI.Configuration;

/// <summary>
/// AI Provider 配置
/// </summary>
public class AiProviderConfig
{
    /// <summary>
    /// Provider 类型 (DeepSeek, OpenAI, Claude, Gemini等)
    /// </summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>
    /// API 密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API 基础地址
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 温度参数 (0.0 - 2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 最大 Token 数
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// 是否启用流式响应
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// 扩展参数（用于特定 Provider 的自定义配置）
    /// </summary>
    public Dictionary<string, object>? ExtraParameters { get; set; }

    /// <summary>
    /// 克隆配置
    /// </summary>
    public AiProviderConfig Clone()
    {
        return new AiProviderConfig
        {
            ProviderType = ProviderType,
            ApiKey = ApiKey,
            ApiBaseUrl = ApiBaseUrl,
            Model = Model,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            EnableStreaming = EnableStreaming,
            TimeoutSeconds = TimeoutSeconds,
            ExtraParameters = ExtraParameters != null 
                ? new Dictionary<string, object>(ExtraParameters) 
                : null
        };
    }
}
