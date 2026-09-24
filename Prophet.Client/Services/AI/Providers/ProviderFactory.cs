using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Services.AI.Abstractions;
using Prophet.Client.Services.AI.Providers.DeepSeek;
using Prophet.Client.Services.AI.Providers.OpenAI;
using Prophet.Client.Services.AI.Providers.Qwen;
using Prophet.Client.Services.AI.Providers.ChatGLM;

namespace Prophet.Client.Services.AI.Providers;

/// <summary>
/// AI Provider 工厂
/// 负责创建和管理所有可用的 AI Provider
/// </summary>
public static class ProviderFactory
{
    private static readonly Dictionary<string, Func<IAiProvider>> _providerCreators = new()
    {
        { "DeepSeek", () => new DeepSeekProvider() },
        { "OpenAI", () => new OpenAIProvider() },
        { "Qwen", () => new QwenProvider() },
        { "ChatGLM", () => new ChatGLMProvider() },
        // 在这里添加新的 Provider
        // { "Claude", () => new ClaudeProvider() },
        // { "Gemini", () => new GeminiProvider() },
    };

    /// <summary>
    /// 获取所有可用的 Provider 名称
    /// </summary>
    public static IReadOnlyList<string> GetAvailableProviders()
    {
        return _providerCreators.Keys.ToList();
    }

    /// <summary>
    /// 创建指定的 Provider 实例
    /// </summary>
    /// <param name="providerName">Provider 名称（不区分大小写）</param>
    /// <returns>Provider 实例</returns>
    /// <exception cref="ArgumentException">当 Provider 不存在时抛出</exception>
    public static IAiProvider CreateProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentNullException(nameof(providerName));

        // 不区分大小写查找
        var key = _providerCreators.Keys.FirstOrDefault(k => 
            k.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (key == null)
        {
            var available = string.Join(", ", _providerCreators.Keys);
            throw new ArgumentException(
                $"不支持的 AI Provider: '{providerName}'。可用的 Provider: {available}",
                nameof(providerName));
        }

        return _providerCreators[key]();
    }

    /// <summary>
    /// 获取所有 Provider 的信息
    /// </summary>
    public static IReadOnlyList<AiProviderInfo> GetAllProviderInfo()
    {
        var infos = new List<AiProviderInfo>();

        foreach (var providerName in _providerCreators.Keys)
        {
            try
            {
                using var provider = CreateProvider(providerName);
                infos.Add(provider.ProviderInfo);
            }
            catch
            {
                // 忽略创建失败的 Provider
            }
        }

        return infos;
    }

    /// <summary>
    /// 检查 Provider 是否可用
    /// </summary>
    public static bool IsProviderAvailable(string providerName)
    {
        return _providerCreators.Keys.Any(k => 
            k.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }
}
