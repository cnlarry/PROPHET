using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Services.AI.Abstractions;
using Prophet.Client.Services.AI.Configuration;
using Prophet.Client.Services.AI.Providers;
using Prophet.Client.Services.Settings;

namespace Prophet.Client.Services.AI.Core;

/// <summary>
/// AI 服务管理器
/// 统一管理所有 AI Provider，提供策略生成、回测分析、行情预测等业务功能
/// </summary>
public class AiServiceManager : IDisposable
{
    private readonly Dictionary<string, IAiProvider> _providers = new();
    private readonly AppSettingsService _appSettingsService;
    private IAiProvider? _currentProvider;
    private string _currentProviderName = string.Empty;
    private bool _disposed = false;

    /// <summary>
    /// 当前使用的 Provider 名称
    /// </summary>
    public string CurrentProviderName => _currentProviderName;

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized => _currentProvider?.IsInitialized ?? false;

    /// <summary>
    /// 所有可用的 Provider 名称
    /// </summary>
    public IReadOnlyList<string> AvailableProviders => ProviderFactory.GetAvailableProviders();

    public AiServiceManager(AppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    #region Provider 管理

    /// <summary>
    /// 从 AppSettings 加载并初始化默认 Provider
    /// </summary>
    public async Task<bool> LoadFromSettingsAsync()
    {
        var settings = _appSettingsService.Settings;
        
        // 使用用户选择的 Provider，默认为 DeepSeek
        var providerType = settings.CurrentAiProvider ?? "DeepSeek";
        
        // 根据 Provider 类型选择对应的 API Key
        var apiKey = providerType switch
        {
            "DeepSeek" => settings.DeepSeekApiKey ?? string.Empty,
            "OpenAI" => settings.OpenAiApiKey ?? string.Empty,
            "Qwen" => settings.QwenApiKey ?? string.Empty,
            "ChatGLM" => settings.ChatGLMApiKey ?? string.Empty,
            _ => settings.DeepSeekApiKey ?? string.Empty
        };
        
        var config = new AiProviderConfig
        {
            ProviderType = providerType,
            ApiKey = apiKey,
            ApiBaseUrl = settings.AiApiBaseUrl,
            Model = settings.AiModel,
            Temperature = settings.AiTemperature,
            MaxTokens = settings.AiMaxTokens,
            EnableStreaming = settings.AiEnableStreaming
        };

        return await InitializeProviderAsync(config);
    }

    /// <summary>
    /// 初始化指定的 Provider
    /// </summary>
    public async Task<bool> InitializeProviderAsync(AiProviderConfig config)
    {
        try
        {
            // 如果已经存在该 Provider，先释放
            if (_providers.TryGetValue(config.ProviderType, out var existingProvider))
            {
                existingProvider.Dispose();
                _providers.Remove(config.ProviderType);
            }

            // 创建新的 Provider
            var provider = ProviderFactory.CreateProvider(config.ProviderType);
            
            // 初始化
            var success = await provider.InitializeAsync(config);
            
            if (success)
            {
                _providers[config.ProviderType] = provider;
                _currentProvider = provider;
                _currentProviderName = config.ProviderType;
                
                Console.WriteLine($"✅ [AiServiceManager] Provider '{config.ProviderType}' 初始化成功");
                return true;
            }
            else
            {
                provider.Dispose();
                Console.WriteLine($"❌ [AiServiceManager] Provider '{config.ProviderType}' 初始化失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AiServiceManager] 初始化失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 切换当前使用的 Provider
    /// </summary>
    public bool SwitchProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
        {
            _currentProvider = provider;
            _currentProviderName = providerName;
            Console.WriteLine($"✅ [AiServiceManager] 已切换到 Provider '{providerName}'");
            return true;
        }

        Console.WriteLine($"❌ [AiServiceManager] Provider '{providerName}' 未初始化");
        return false;
    }

    /// <summary>
    /// 获取所有 Provider 信息
    /// </summary>
    public IReadOnlyList<AiProviderInfo> GetAllProviderInfo()
    {
        return ProviderFactory.GetAllProviderInfo();
    }

    /// <summary>
    /// 验证当前 Provider 的连接
    /// </summary>
    public async Task<bool> ValidateConnectionAsync()
    {
        if (_currentProvider == null)
            return false;

        try
        {
            return await _currentProvider.ValidateConnectionAsync();
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 业务功能

    /// <summary>
    /// 生成策略代码
    /// </summary>
    /// <param name="userQuery">用户需求</param>
    /// <param name="contextCode">上下文代码（当前策略）</param>
    /// <returns>AI 生成的策略代码</returns>
    public async Task<string> GenerateStrategyAsync(string userQuery, string? contextCode = null)
    {
        EnsureInitialized();

        var messages = PromptTemplates.Instance.GetStrategyGenerationPrompt(userQuery, contextCode);
        
        var request = new AiRequest
        {
            Messages = messages.Select(m => new AiMessage 
            { 
                Role = m.Role, 
                Content = m.Content 
            }).ToList(),
            Model = _currentProvider!.GetCurrentConfig()?.Model ?? string.Empty,
            Temperature = _currentProvider.GetCurrentConfig()?.Temperature ?? 0.7,
            MaxTokens = _currentProvider.GetCurrentConfig()?.MaxTokens ?? 4096
        };

        var response = await _currentProvider.ChatCompletionAsync(request);
        
        if (!response.Success)
            throw new AiException(response.ErrorMessage ?? "AI 请求失败");

        return response.Content;
    }

    /// <summary>
    /// 流式生成策略代码
    /// </summary>
    /// <param name="userQuery">用户需求</param>
    /// <param name="contextCode">上下文代码（当前策略）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步枚举流式响应块</returns>
    public async IAsyncEnumerable<AiStreamChunk> GenerateStrategyStreamAsync(
        string userQuery, 
        string? contextCode = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var messages = PromptTemplates.Instance.GetStrategyGenerationPrompt(userQuery, contextCode);
        
        var request = new AiRequest
        {
            Messages = messages.Select(m => new AiMessage 
            { 
                Role = m.Role, 
                Content = m.Content 
            }).ToList(),
            Model = _currentProvider!.GetCurrentConfig()?.Model ?? string.Empty,
            Temperature = _currentProvider.GetCurrentConfig()?.Temperature ?? 0.7,
            MaxTokens = _currentProvider.GetCurrentConfig()?.MaxTokens ?? 4096
        };

        await foreach (var chunk in _currentProvider.ChatCompletionStreamAsync(request, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// 分析回测结果并提供优化建议
    /// </summary>
    /// <param name="backtestResult">回测结果</param>
    /// <param name="currentStrategyCode">当前策略代码</param>
    /// <returns>优化建议</returns>
    public async Task<string> AnalyzeBacktestResultAsync(
        Backtest.Models.BacktestResult backtestResult, 
        string currentStrategyCode)
    {
        EnsureInitialized();

        var summary = PromptTemplates.Instance.GenerateBacktestSummary(backtestResult);
        var messages = PromptTemplates.Instance.GetBacktestAnalysisPrompt(summary, currentStrategyCode);
        
        var request = new AiRequest
        {
            Messages = messages.Select(m => new AiMessage 
            { 
                Role = m.Role, 
                Content = m.Content 
            }).ToList(),
            Model = _currentProvider!.GetCurrentConfig()?.Model ?? string.Empty,
            Temperature = _currentProvider.GetCurrentConfig()?.Temperature ?? 0.7,
            MaxTokens = _currentProvider.GetCurrentConfig()?.MaxTokens ?? 4096
        };

        var response = await _currentProvider.ChatCompletionAsync(request);
        
        if (!response.Success)
            throw new AiException(response.ErrorMessage ?? "AI 请求失败");

        return response.Content;
    }

    /// <summary>
    /// 预测市场走势
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="timeframe">时间框架</param>
    /// <param name="marketDataSummary">市场数据摘要</param>
    /// <returns>市场预测</returns>
    public async Task<string> PredictMarketTrendAsync(
        string symbol, 
        string timeframe, 
        string marketDataSummary)
    {
        EnsureInitialized();

        var messages = PromptTemplates.Instance.GetMarketPredictionPrompt(symbol, timeframe, marketDataSummary);
        
        var request = new AiRequest
        {
            Messages = messages.Select(m => new AiMessage 
            { 
                Role = m.Role, 
                Content = m.Content 
            }).ToList(),
            Model = _currentProvider!.GetCurrentConfig()?.Model ?? string.Empty,
            Temperature = _currentProvider.GetCurrentConfig()?.Temperature ?? 0.7,
            MaxTokens = _currentProvider.GetCurrentConfig()?.MaxTokens ?? 4096
        };

        var response = await _currentProvider.ChatCompletionAsync(request);
        
        if (!response.Success)
            throw new AiException(response.ErrorMessage ?? "AI 请求失败");

        return response.Content;
    }

    private void EnsureInitialized()
    {
        if (_currentProvider == null || !_currentProvider.IsInitialized)
        {
            throw new AiProviderNotInitializedException(_currentProviderName);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var provider in _providers.Values)
            {
                provider.Dispose();
            }
            _providers.Clear();
            _currentProvider = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}
