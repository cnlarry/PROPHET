using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges.Binance.Models;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Trading.Exchanges.Binance;

/// <summary>
/// 币安REST API客户端
/// </summary>
public class BinanceRestClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private string _apiKey = string.Empty;
    private string _apiSecret = string.Empty;
    private string _baseUrl = string.Empty;
    private bool _disposed;
    
    public BinanceRestClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }
    
    /// <summary>
    /// 初始化客户端
    /// </summary>
    public void Initialize(string baseUrl, string apiKey, string apiSecret)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        
        // 设置默认请求头
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);
    }
    
    // ========== 账户相关 ==========
    
    /// <summary>
    /// 获取账户信息
    /// </summary>
    public async Task<BinanceAccountInfoResponse> GetAccountInfoAsync()
    {
        var endpoint = "/fapi/v2/account";
        var parameters = new Dictionary<string, string>();
        
        return await SendSignedRequestAsync<BinanceAccountInfoResponse>(
            HttpMethod.Get, endpoint, parameters);
    }
    
    /// <summary>
    /// 获取持仓信息
    /// </summary>
    public async Task<List<BinancePosition>> GetPositionsAsync(string? symbol = null)
    {
        var endpoint = "/fapi/v2/positionRisk";
        var parameters = new Dictionary<string, string>();
        
        if (!string.IsNullOrEmpty(symbol))
        {
            parameters["symbol"] = symbol;
        }
        
        var positions = await SendSignedRequestAsync<List<BinancePosition>>(
            HttpMethod.Get, endpoint, parameters);
        
        // 过滤掉数量为0的持仓
        return positions.Where(p => decimal.Parse(p.PositionAmt) != 0).ToList();
    }
    
    // ========== 市场数据 ==========
    
    /// <summary>
    /// 获取历史K线数据
    /// </summary>
    public async Task<List<List<object>>> GetKlinesAsync(
        string symbol, 
        string interval, 
        long? startTime = null, 
        long? endTime = null, 
        int limit = 500)
    {
        var endpoint = "/fapi/v1/klines";
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["interval"] = interval,
            ["limit"] = limit.ToString()
        };
        
        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToString();
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToString();
        
        var queryString = BinanceAuthHelper.BuildQueryString(parameters);
        var url = $"{_baseUrl}{endpoint}?{queryString}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<List<object>>>(content) ?? new();
    }
    
    /// <summary>
    /// 获取订单簿
    /// </summary>
    public async Task<BinanceOrderBookResponse> GetOrderBookAsync(string symbol, int limit = 20)
    {
        var endpoint = "/fapi/v1/depth";
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["limit"] = limit.ToString()
        };
        
        var queryString = BinanceAuthHelper.BuildQueryString(parameters);
        var url = $"{_baseUrl}{endpoint}?{queryString}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<BinanceOrderBookResponse>(content) 
            ?? new BinanceOrderBookResponse();
    }
    
    /// <summary>
    /// 获取Ticker信息
    /// </summary>
    public async Task<BinanceTickerResponse> GetTickerAsync(string symbol)
    {
        var endpoint = "/fapi/v1/ticker/24hr";
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol
        };
        
        var queryString = BinanceAuthHelper.BuildQueryString(parameters);
        var url = $"{_baseUrl}{endpoint}?{queryString}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<BinanceTickerResponse>(content) 
            ?? new BinanceTickerResponse();
    }
    
    // ========== 订单相关 ==========
    
    /// <summary>
    /// 下单
    /// </summary>
    public async Task<BinanceOrderResponse> PlaceOrderAsync(
        string symbol,
        string side,
        string type,
        decimal? quantity = null,
        decimal? price = null,
        string? timeInForce = null,
        string? clientOrderId = null)
    {
        var endpoint = "/fapi/v1/order";
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = side,
            ["type"] = type
        };
        
        if (quantity.HasValue)
            parameters["quantity"] = quantity.Value.ToString();
        if (price.HasValue)
            parameters["price"] = price.Value.ToString();
        if (!string.IsNullOrEmpty(timeInForce))
            parameters["timeInForce"] = timeInForce;
        if (!string.IsNullOrEmpty(clientOrderId))
            parameters["newClientOrderId"] = clientOrderId;
        
        return await SendSignedRequestAsync<BinanceOrderResponse>(
            HttpMethod.Post, endpoint, parameters);
    }
    
    /// <summary>
    /// 撤销订单
    /// </summary>
    public async Task<BinanceOrderResponse> CancelOrderAsync(string symbol, long orderId)
    {
        var endpoint = "/fapi/v1/order";
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["orderId"] = orderId.ToString()
        };
        
        return await SendSignedRequestAsync<BinanceOrderResponse>(
            HttpMethod.Delete, endpoint, parameters);
    }
    
    /// <summary>
    /// 查询订单
    /// </summary>
    public async Task<BinanceOrderResponse> GetOrderAsync(string symbol, long orderId)
    {
        var endpoint = "/fapi/v1/order";
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["orderId"] = orderId.ToString()
        };
        
        return await SendSignedRequestAsync<BinanceOrderResponse>(
            HttpMethod.Get, endpoint, parameters);
    }
    
    /// <summary>
    /// 获取未完成订单
    /// </summary>
    public async Task<List<BinanceOrderResponse>> GetOpenOrdersAsync(string? symbol = null)
    {
        var endpoint = "/fapi/v1/openOrders";
        var parameters = new Dictionary<string, string>();
        
        if (!string.IsNullOrEmpty(symbol))
        {
            parameters["symbol"] = symbol;
        }
        
        return await SendSignedRequestAsync<List<BinanceOrderResponse>>(
            HttpMethod.Get, endpoint, parameters);
    }
    
    // ========== 杠杆相关 ==========
    
    /// <summary>
    /// 设置杠杆倍数
    /// </summary>
    public async Task<bool> SetLeverageAsync(string symbol, int leverage)
    {
        var endpoint = "/fapi/v1/leverage";
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["leverage"] = leverage.ToString()
        };
        
        try
        {
            await SendSignedRequestAsync<object>(HttpMethod.Post, endpoint, parameters);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    // ========== WebSocket Listen Key ==========
    
    /// <summary>
    /// 创建Listen Key（用于用户数据流）
    /// </summary>
    public async Task<string> CreateListenKeyAsync()
    {
        var endpoint = "/fapi/v1/listenKey";
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{endpoint}");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<BinanceListenKeyResponse>(content);
        
        return result?.ListenKey ?? string.Empty;
    }
    
    /// <summary>
    /// 保持Listen Key活跃
    /// </summary>
    public async Task<bool> KeepAliveListenKeyAsync()
    {
        var endpoint = "/fapi/v1/listenKey";
        
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}{endpoint}");
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    // ========== 私有方法 ==========
    
    /// <summary>
    /// 发送签名请求
    /// </summary>
    private async Task<T> SendSignedRequestAsync<T>(
        HttpMethod method, 
        string endpoint, 
        Dictionary<string, string> parameters)
    {
        // 添加签名
        var queryString = BinanceAuthHelper.BuildSignedQueryString(parameters, _apiSecret);
        var url = $"{_baseUrl}{endpoint}?{queryString}";
        
        var request = new HttpRequestMessage(method, url);
        var response = await _httpClient.SendAsync(request);
        
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            // 尝试解析错误响应
            try
            {
                var error = JsonSerializer.Deserialize<BinanceErrorResponse>(content);
                throw new Exception($"Binance API Error: {error?.Code} - {error?.Message}");
            }
            catch
            {
                throw new Exception($"HTTP Error: {response.StatusCode} - {content}");
            }
        }
        
        return JsonSerializer.Deserialize<T>(content) 
            ?? throw new Exception("Failed to deserialize response");
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

