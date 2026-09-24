using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Prophet.Client.Core;
using Prophet.Client.Services.AI.Abstractions;
using Prophet.Client.Services.AI.Configuration;
using Prophet.Client.Services.Network;
using Prophet.Client.Services.Settings;

namespace Prophet.Client.Services.AI.Providers.OpenAI;

/// <summary>
/// OpenAI Provider 实现
/// </summary>
public class OpenAIProvider : IAiProvider
{
    private HttpClient? _httpClient;
    private AiProviderConfig? _config;
    private bool _disposed = false;
    private bool _ownsHttpClient = true;  // 标记是否由当前实例创建（需要释放）

    public AiProviderInfo ProviderInfo { get; } = new AiProviderInfo
    {
        Name = "OpenAI",
        DisplayName = "OpenAI GPT",
        Description = "OpenAI GPT 系列模型（包括 GPT-3.5、GPT-4）",
        SupportedModels = new List<string> 
        { 
            "gpt-3.5-turbo", 
            "gpt-4", 
            "gpt-4-turbo-preview",
            "gpt-4o",
            "gpt-4o-mini"
        },
        DefaultModel = "gpt-3.5-turbo",
        SupportsStreaming = true,
        DefaultApiBaseUrl = "https://api.openai.com/v1"
    };

    public bool IsInitialized => _httpClient != null && _config != null;

    public async Task<bool> InitializeAsync(AiProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            Logger.Error("API 密钥为空，初始化失败");
            return false;
        }

        _config = config.Clone();

        // 设置默认值
        if (string.IsNullOrWhiteSpace(_config.ApiBaseUrl))
            _config.ApiBaseUrl = ProviderInfo.DefaultApiBaseUrl;
        
        if (string.IsNullOrWhiteSpace(_config.Model))
            _config.Model = ProviderInfo.DefaultModel;

        // 释放旧的 HttpClient（如果存在）
        if (_httpClient != null && _ownsHttpClient)
        {
            _httpClient.Dispose();
            Logger.Debug("已释放旧的 HttpClient");
        }

        // 创建新的 HttpClient
        _httpClient = CreateHttpClient(_config);
        _ownsHttpClient = true;

        Logger.Info($"OpenAI Provider 初始化成功");
        Logger.Debug($"API Base: {_config.ApiBaseUrl}, Model: {_config.Model}");

        return await Task.FromResult(true);
    }

    public async Task<AiResponse> ChatCompletionAsync(AiRequest request)
    {
        if (!IsInitialized || _httpClient == null || _config == null)
            throw new AiProviderNotInitializedException(ProviderInfo.Name);

        try
        {
            var openaiRequest = new OpenAIChatRequest
            {
                Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
                Messages = request.Messages.Select(m => new OpenAIMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList(),
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = request.EnableStreaming
            };

            var jsonBody = JsonConvert.SerializeObject(openaiRequest);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return HandleErrorResponse(response.StatusCode, errorContent);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var openaiResponse = JsonConvert.DeserializeObject<OpenAIChatResponse>(responseJson);

            if (openaiResponse == null || openaiResponse.Choices.Count == 0)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = "OpenAI API 返回空响应"
                };
            }

            return new AiResponse
            {
                Id = openaiResponse.Id,
                Content = openaiResponse.Choices[0].Message.Content,
                Model = openaiResponse.Model,
                PromptTokens = openaiResponse.Usage.PromptTokens,
                CompletionTokens = openaiResponse.Usage.CompletionTokens,
                TotalTokens = openaiResponse.Usage.TotalTokens,
                FinishReason = openaiResponse.Choices[0].FinishReason,
                Success = true
            };
        }
        catch (TaskCanceledException)
        {
            throw new AiRequestTimeoutException(ProviderInfo.Name, _config.TimeoutSeconds);
        }
        catch (HttpRequestException ex)
        {
            throw new AiNetworkException(ProviderInfo.Name, ex.Message);
        }
        catch (Exception ex)
        {
            throw new AiException($"OpenAI API 调用失败: {ex.Message}", ex);
        }
    }

    public async Task<bool> ValidateConnectionAsync()
    {
        try
        {
            var testRequest = new AiRequest
            {
                Messages = new List<AiMessage>
                {
                    new AiMessage { Role = "user", Content = "Hi" }
                },
                MaxTokens = 10
            };

            var response = await ChatCompletionAsync(testRequest);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }
    
    public async IAsyncEnumerable<AiStreamChunk> ChatCompletionStreamAsync(AiRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsInitialized || _httpClient == null || _config == null)
            throw new AiProviderNotInitializedException(ProviderInfo.Name);

        var openaiRequest = new OpenAIChatRequest
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
            Messages = request.Messages.Select(m => new OpenAIMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = true  // 强制启用流式
        };

        var jsonBody = JsonConvert.SerializeObject(openaiRequest);
        var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = httpContent
        };
        
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new AiException($"OpenAI API 错误: {errorContent}");
        }

        var stream = await response.Content.ReadAsStreamAsync();
        var reader = new StreamReader(stream);
        
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                if (!line.StartsWith("data: "))
                    continue;
                
                var data = line.Substring(6).Trim();
                
                if (data == "[DONE]")
                    break;
                
                OpenAIStreamResponse? streamResponse = null;
                try
                {
                    streamResponse = JsonConvert.DeserializeObject<OpenAIStreamResponse>(data);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"⚠️ [OpenAIProvider] 解析流式响应失败: {ex.Message}");
                    continue;
                }
                
                if (streamResponse?.Choices != null && streamResponse.Choices.Count > 0)
                {
                    var choice = streamResponse.Choices[0];
                    var deltaContent = choice.Delta?.Content ?? string.Empty;
                    
                    yield return new AiStreamChunk
                    {
                        Id = streamResponse.Id,
                        Content = deltaContent,
                        IsLast = choice.FinishReason != null,
                        FinishReason = choice.FinishReason,
                        Usage = streamResponse.Usage != null ? new AiUsage
                        {
                            PromptTokens = streamResponse.Usage.PromptTokens,
                            CompletionTokens = streamResponse.Usage.CompletionTokens,
                            TotalTokens = streamResponse.Usage.TotalTokens
                        } : null
                    };
                }
            }
        }
        finally
        {
            reader.Dispose();
            stream.Dispose();
            response.Dispose();
        }
    }

    public AiProviderConfig? GetCurrentConfig()
    {
        return _config?.Clone();
    }

    private HttpClient CreateHttpClient(AiProviderConfig config)
    {
        // ✅ 使用ProxyManagementService统一管理代理
        var proxyService = ServiceContainer.GetService<ProxyManagementService>();
        var handler = proxyService.CreateHttpClientHandler();

        var client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(config.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

        return client;
    }

    private AiResponse HandleErrorResponse(HttpStatusCode statusCode, string errorContent)
    {
        try
        {
            var errorResponse = JsonConvert.DeserializeObject<OpenAIErrorResponse>(errorContent);
            var errorMessage = errorResponse?.Error.Message ?? "未知错误";
            var errorType = errorResponse?.Error.Type ?? "unknown";

            string friendlyMessage = errorType switch
            {
                "insufficient_quota" => "OpenAI 配额不足，请充值后再试",
                "invalid_api_key" => "OpenAI API 密钥无效，请检查配置",
                "rate_limit_exceeded" => "请求频率过高，请稍后再试",
                _ => $"OpenAI API 错误: {errorMessage}"
            };

            return new AiResponse
            {
                Success = false,
                ErrorMessage = friendlyMessage
            };
        }
        catch
        {
            return new AiResponse
            {
                Success = false,
                ErrorMessage = $"HTTP {(int)statusCode}: {errorContent}"
            };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_httpClient != null && _ownsHttpClient)
            {
                _httpClient.Dispose();
                Logger.Debug("OpenAI HttpClient 已释放");
            }
            _httpClient = null;
            _config = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
