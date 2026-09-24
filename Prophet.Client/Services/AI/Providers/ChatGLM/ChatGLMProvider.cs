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

namespace Prophet.Client.Services.AI.Providers.ChatGLM;

/// <summary>
/// 智谱 ChatGLM Provider 实现
/// </summary>
public class ChatGLMProvider : IAiProvider
{
    private HttpClient? _httpClient;
    private AiProviderConfig? _config;
    private bool _disposed = false;

    public AiProviderInfo ProviderInfo { get; } = new AiProviderInfo
    {
        Name = "ChatGLM",
        DisplayName = "智谱 ChatGLM",
        Description = "清华系大语言模型，中文能力优秀，开源友好",
        SupportedModels = new List<string> 
        { 
            "glm-4",
            "glm-4-flash",
            "glm-3-turbo"
        },
        DefaultModel = "glm-4",
        SupportsStreaming = true,
        DefaultApiBaseUrl = "https://open.bigmodel.cn/api/paas/v4"
    };

    public bool IsInitialized => _httpClient != null && _config != null;

    public async Task<bool> InitializeAsync(AiProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            Console.WriteLine("❌ [ChatGLMProvider] API 密钥为空，初始化失败");
            return false;
        }

        _config = config.Clone();

        if (string.IsNullOrWhiteSpace(_config.ApiBaseUrl))
            _config.ApiBaseUrl = ProviderInfo.DefaultApiBaseUrl;
        
        if (string.IsNullOrWhiteSpace(_config.Model))
            _config.Model = ProviderInfo.DefaultModel;

        _httpClient = CreateHttpClient(_config);

        Console.WriteLine($"✅ [ChatGLMProvider] 初始化成功");
        Console.WriteLine($"   API Base: {_config.ApiBaseUrl}");
        Console.WriteLine($"   Model: {_config.Model}");

        return await Task.FromResult(true);
    }

    public async Task<AiResponse> ChatCompletionAsync(AiRequest request)
    {
        if (!IsInitialized || _httpClient == null || _config == null)
            throw new AiProviderNotInitializedException(ProviderInfo.Name);

        try
        {
            var glmRequest = new ChatGLMRequest
            {
                Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
                Messages = request.Messages.Select(m => new ChatGLMMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList(),
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                TopP = request.TopP ?? 0.7
            };

            var jsonBody = JsonConvert.SerializeObject(glmRequest);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return HandleErrorResponse(response.StatusCode, errorContent);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var glmResponse = JsonConvert.DeserializeObject<ChatGLMResponse>(responseJson);

            if (glmResponse == null || glmResponse.Choices.Count == 0)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = "ChatGLM API 返回空响应"
                };
            }

            return new AiResponse
            {
                Id = glmResponse.Id,
                Content = glmResponse.Choices[0].Message.Content,
                Model = glmResponse.Model,
                PromptTokens = glmResponse.Usage.PromptTokens,
                CompletionTokens = glmResponse.Usage.CompletionTokens,
                TotalTokens = glmResponse.Usage.TotalTokens,
                FinishReason = glmResponse.Choices[0].FinishReason,
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
            throw new AiException($"ChatGLM API 调用失败: {ex.Message}", ex);
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
                    new AiMessage { Role = "user", Content = "你好" }
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

        var glmRequest = new ChatGLMRequest
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
            Messages = request.Messages.Select(m => new ChatGLMMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP ?? 0.7,
            Stream = true  // 强制启用流式
        };

        var jsonBody = JsonConvert.SerializeObject(glmRequest);
        var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = httpContent
        };
        
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new AiException($"ChatGLM API 错误: {errorContent}");
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
                
                ChatGLMStreamResponse? streamResponse = null;
                try
                {
                    streamResponse = JsonConvert.DeserializeObject<ChatGLMStreamResponse>(data);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"⚠️ [ChatGLMProvider] 解析流式响应失败: {ex.Message}");
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
            var errorResponse = JsonConvert.DeserializeObject<ChatGLMErrorResponse>(errorContent);
            var errorMessage = errorResponse?.Error?.Message ?? "未知错误";
            var errorCode = errorResponse?.Error?.Code ?? "unknown";

            string friendlyMessage = errorCode switch
            {
                "invalid_api_key" => "API 密钥无效，请检查配置",
                "insufficient_quota" => "配额不足，请充值后再试",
                "rate_limit_exceeded" => "请求频率过高，请稍后再试",
                _ => $"ChatGLM API 错误: {errorMessage}"
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
