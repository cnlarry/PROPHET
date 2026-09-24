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

namespace Prophet.Client.Services.AI.Providers.Qwen;

/// <summary>
/// 阿里通义千问 Provider 实现
/// </summary>
public class QwenProvider : IAiProvider
{
    private HttpClient? _httpClient;
    private AiProviderConfig? _config;
    private bool _disposed = false;

    public AiProviderInfo ProviderInfo { get; } = new AiProviderInfo
    {
        Name = "Qwen",
        DisplayName = "阿里通义千问",
        Description = "阿里云旗下大语言模型，中文能力优秀",
        SupportedModels = new List<string> 
        { 
            "qwen-turbo", 
            "qwen-plus", 
            "qwen-max",
            "qwen-max-longcontext"
        },
        DefaultModel = "qwen-plus",
        SupportsStreaming = true,
        DefaultApiBaseUrl = "https://dashscope.aliyuncs.com/api/v1"
    };

    public bool IsInitialized => _httpClient != null && _config != null;

    public async Task<bool> InitializeAsync(AiProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            Console.WriteLine("❌ [QwenProvider] API 密钥为空，初始化失败");
            return false;
        }

        _config = config.Clone();

        if (string.IsNullOrWhiteSpace(_config.ApiBaseUrl))
            _config.ApiBaseUrl = ProviderInfo.DefaultApiBaseUrl;
        
        if (string.IsNullOrWhiteSpace(_config.Model))
            _config.Model = ProviderInfo.DefaultModel;

        _httpClient = CreateHttpClient(_config);

        Console.WriteLine($"✅ [QwenProvider] 初始化成功");
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
            var qwenRequest = new QwenChatRequest
            {
                Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
                Input = new QwenInput
                {
                    Messages = request.Messages.Select(m => new QwenMessage
                    {
                        Role = m.Role,
                        Content = m.Content
                    }).ToList()
                },
                Parameters = new QwenParameters
                {
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    TopP = request.TopP ?? 0.8
                }
            };

            var jsonBody = JsonConvert.SerializeObject(qwenRequest);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/applications/generation", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return HandleErrorResponse(response.StatusCode, errorContent);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var qwenResponse = JsonConvert.DeserializeObject<QwenChatResponse>(responseJson);

            if (qwenResponse == null || qwenResponse.Output?.Choices == null || qwenResponse.Output.Choices.Count == 0)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = "通义千问 API 返回空响应"
                };
            }

            return new AiResponse
            {
                Id = qwenResponse.RequestId,
                Content = qwenResponse.Output.Choices[0].Message.Content,
                Model = _config.Model,
                PromptTokens = qwenResponse.Usage?.InputTokens ?? 0,
                CompletionTokens = qwenResponse.Usage?.OutputTokens ?? 0,
                TotalTokens = qwenResponse.Usage?.TotalTokens ?? 0,
                FinishReason = qwenResponse.Output.Choices[0].FinishReason,
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
            throw new AiException($"通义千问 API 调用失败: {ex.Message}", ex);
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

        var qwenRequest = new QwenChatRequest
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? _config.Model : request.Model,
            Input = new QwenInput
            {
                Messages = request.Messages.Select(m => new QwenMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList()
            },
            Parameters = new QwenParameters
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                TopP = request.TopP ?? 0.8,
                IncrementalOutput = true  // 启用增量输出
            }
        };

        var jsonBody = JsonConvert.SerializeObject(qwenRequest);
        var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/services/aigc/text-generation/generation")
        {
            Content = httpContent
        };
        
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new AiException($"通义千问 API 错误: {errorContent}");
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
                
                if (!line.StartsWith("data:"))
                    continue;
                
                var data = line.Substring(5).Trim();
                
                QwenStreamResponse? streamResponse = null;
                try
                {
                    streamResponse = JsonConvert.DeserializeObject<QwenStreamResponse>(data);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"⚠️ [QwenProvider] 解析流式响应失败: {ex.Message}");
                    continue;
                }
                
                if (streamResponse?.Output?.Choices != null && streamResponse.Output.Choices.Count > 0)
                {
                    var choice = streamResponse.Output.Choices[0];
                    var deltaContent = choice.Message?.Content ?? string.Empty;
                    var finishReason = choice.FinishReason;
                    
                    yield return new AiStreamChunk
                    {
                        Id = streamResponse.RequestId,
                        Content = deltaContent,
                        IsLast = finishReason != null && finishReason != "null",
                        FinishReason = finishReason,
                        Usage = streamResponse.Usage != null ? new AiUsage
                        {
                            PromptTokens = streamResponse.Usage.InputTokens,
                            CompletionTokens = streamResponse.Usage.OutputTokens,
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

        // 通义千问使用 X-DashScope-API-Key 作为认证头
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

        return client;
    }

    private AiResponse HandleErrorResponse(HttpStatusCode statusCode, string errorContent)
    {
        try
        {
            var errorResponse = JsonConvert.DeserializeObject<QwenErrorResponse>(errorContent);
            var errorMessage = errorResponse?.Message ?? "未知错误";
            var errorCode = errorResponse?.Code ?? "unknown";

            string friendlyMessage = errorCode switch
            {
                "InvalidApiKey" => "API 密钥无效，请检查配置",
                "InsufficientBalance" => "账户余额不足，请充值后再试",
                "RateLimitExceeded" => "请求频率过高，请稍后再试",
                "InvalidParameter" => $"参数错误: {errorMessage}",
                _ => $"通义千问 API 错误: {errorMessage}"
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
