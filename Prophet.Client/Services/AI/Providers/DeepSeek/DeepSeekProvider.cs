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

namespace Prophet.Client.Services.AI.Providers.DeepSeek;

/// <summary>
/// DeepSeek AI Provider 实现
/// </summary>
public class DeepSeekProvider : IAiProvider
{
    private HttpClient? _httpClient;
    private AiProviderConfig? _config;
    private bool _disposed = false;

    public AiProviderInfo ProviderInfo { get; } = new AiProviderInfo
    {
        Name = "DeepSeek",
        DisplayName = "DeepSeek AI",
        Description = "DeepSeek 深度求索 - 中国领先的 AI 大模型",
        SupportedModels = new List<string> { "deepseek-chat", "deepseek-coder" },
        DefaultModel = "deepseek-chat",
        SupportsStreaming = true,
        DefaultApiBaseUrl = "https://api.deepseek.com"
    };

    public bool IsInitialized => _httpClient != null && _config != null;

    public async Task<bool> InitializeAsync(AiProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            Console.WriteLine("❌ [DeepSeekProvider] API 密钥为空，初始化失败");
            return false;
        }

        _config = config.Clone();

        // 设置默认值
        if (string.IsNullOrWhiteSpace(_config.ApiBaseUrl))
            _config.ApiBaseUrl = ProviderInfo.DefaultApiBaseUrl;
        
        if (string.IsNullOrWhiteSpace(_config.Model))
            _config.Model = ProviderInfo.DefaultModel;

        // 创建 HttpClient
        _httpClient = CreateHttpClient(_config);

        Console.WriteLine($"✅ [DeepSeekProvider] 初始化成功");
        Console.WriteLine($"   API Base: {_config.ApiBaseUrl}");
        Console.WriteLine($"   Model: {_config.Model}");
        Console.WriteLine($"   Timeout: {_config.TimeoutSeconds}s");

        return await Task.FromResult(true);
    }

    public async Task<AiResponse> ChatCompletionAsync(AiRequest request)
    {
        if (!IsInitialized || _httpClient == null || _config == null)
            throw new AiProviderNotInitializedException(ProviderInfo.Name);

        try
        {
            // 转换为 DeepSeek 格式
            var deepseekRequest = new DeepSeekChatRequest
            {
                Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
                Messages = request.Messages.Select(m => new DeepSeekMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList(),
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = request.EnableStreaming
            };

            var jsonBody = JsonConvert.SerializeObject(deepseekRequest);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/chat/completions", content);

            // 处理错误
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return HandleErrorResponse(response.StatusCode, errorContent);
            }

            // 解析响应
            var responseJson = await response.Content.ReadAsStringAsync();
            var deepseekResponse = JsonConvert.DeserializeObject<DeepSeekChatResponse>(responseJson);

            if (deepseekResponse == null || deepseekResponse.Choices.Count == 0)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = "DeepSeek API 返回空响应"
                };
            }

            return new AiResponse
            {
                Id = deepseekResponse.Id,
                Content = deepseekResponse.Choices[0].Message.Content,
                Model = deepseekResponse.Model,
                PromptTokens = deepseekResponse.Usage.PromptTokens,
                CompletionTokens = deepseekResponse.Usage.CompletionTokens,
                TotalTokens = deepseekResponse.Usage.TotalTokens,
                FinishReason = deepseekResponse.Choices[0].FinishReason,
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
            throw new AiException($"DeepSeek API 调用失败: {ex.Message}", ex);
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

        // 转换为 DeepSeek 格式
        var deepseekRequest = new DeepSeekChatRequest
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
            Messages = request.Messages.Select(m => new DeepSeekMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = true  // 强制启用流式
        };

        var jsonBody = JsonConvert.SerializeObject(deepseekRequest);
        var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = httpContent
        };
        
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new AiException($"DeepSeek API 错误: {errorContent}");
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
                
                DeepSeekStreamResponse? streamResponse = null;
                try
                {
                    streamResponse = JsonConvert.DeserializeObject<DeepSeekStreamResponse>(data);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"⚠️ [DeepSeekProvider] 解析流式响应失败: {ex.Message}");
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
            var errorResponse = JsonConvert.DeserializeObject<DeepSeekErrorResponse>(errorContent);
            var errorCode = errorResponse?.Error.Code ?? "unknown";
            var errorMessage = errorResponse?.Error.Message ?? "未知错误";

            string friendlyMessage = errorCode switch
            {
                "invalid_request_error" when errorMessage.Contains("Insufficient Balance", StringComparison.OrdinalIgnoreCase)
                    => "账户余额不足，请前往 DeepSeek 官网充值后再试",
                "invalid_api_key" or "authentication_error"
                    => "API 密钥无效，请检查设置中的 API 密钥是否正确",
                "rate_limit_exceeded"
                    => "请求频率过高，请稍后再试",
                "insufficient_quota"
                    => "配额不足，请检查 DeepSeek 账户配额",
                _ => $"DeepSeek API 错误: {errorCode} - {errorMessage}"
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
            _httpClient?.Dispose();
            _httpClient = null;
            _config = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
