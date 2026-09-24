using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Services.AI.Configuration;

namespace Prophet.Client.Services.AI.Abstractions;

/// <summary>
/// AI Provider 核心接口
/// 所有 AI 服务提供商（DeepSeek、OpenAI、Claude等）必须实现此接口
/// </summary>
public interface IAiProvider : IDisposable
{
    /// <summary>
    /// Provider 信息
    /// </summary>
    AiProviderInfo ProviderInfo { get; }

    /// <summary>
    /// 是否已初始化
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 初始化 Provider
    /// </summary>
    /// <param name="config">Provider 配置</param>
    /// <returns>是否初始化成功</returns>
    Task<bool> InitializeAsync(AiProviderConfig config);

    /// <summary>
    /// 聊天补全（核心方法）
    /// </summary>
    /// <param name="request">AI 请求</param>
    /// <returns>AI 响应</returns>
    Task<AiResponse> ChatCompletionAsync(AiRequest request);
    
    /// <summary>
    /// 流式聊天补全（支持实时输出）
    /// </summary>
    /// <param name="request">AI 请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步枚举流式响应块</returns>
    IAsyncEnumerable<AiStreamChunk> ChatCompletionStreamAsync(AiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证连接（测试 API 密钥是否有效）
    /// </summary>
    /// <returns>是否连接成功</returns>
    Task<bool> ValidateConnectionAsync();

    /// <summary>
    /// 获取当前配置
    /// </summary>
    AiProviderConfig? GetCurrentConfig();
}
