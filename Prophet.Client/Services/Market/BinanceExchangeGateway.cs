using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Core;
using Prophet.Client.Models;
using Prophet.Client.Services.Network;
using Prophet.Client.Services.Settings;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 统一的币安期货数据访问网关
/// </summary>
public sealed class BinanceExchangeGateway : IBinanceExchangeGateway
{
    private readonly HttpClient _httpClient;
    private readonly BinanceGatewayOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly bool _ownsHttpClient;

    public BinanceExchangeGateway(HttpClient? httpClient = null, BinanceGatewayOptions? options = null)
    {
        _options = options ?? BinanceGatewayOptions.CreateFromConfig();
        if (httpClient == null)
        {
            _httpClient = CreateHttpClient(_options);
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public HttpClient HttpClient => _httpClient;

    public async Task<IReadOnlyList<Candlestick>> FetchKlinesAsync(
        string symbol,
        string interval,
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUrl = BuildKlineUrl(symbol, interval, limit, startTime, endTime);
            Console.WriteLine($"🔗 [BinanceGateway] 请求URL: {requestUrl}");
            Console.WriteLine($"   HttpClient超时: {_httpClient.Timeout.TotalSeconds}秒");
            Console.WriteLine($"   代理配置: EnableProxy={_options.EnableProxy}, ProxyAddress={_options.ProxyAddress ?? "系统默认"}");
            
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"❌ [BinanceGateway] 获取K线失败: HTTP {response.StatusCode} - {errorContent}");
                return Array.Empty<Candlestick>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var rawData = await JsonSerializer.DeserializeAsync<List<List<JsonElement>>>(stream, _serializerOptions, cancellationToken);

            if (rawData == null || rawData.Count == 0)
            {
                return Array.Empty<Candlestick>();
            }

            var candles = new List<Candlestick>(rawData.Count);
            foreach (var raw in rawData)
            {
                var candle = KlineParser.ParseRestKline(raw);
                if (candle != null)
                {
                    candles.Add(candle);
                }
            }

            return candles;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取K线超时: 请求在 {_options.HttpTimeout.TotalSeconds} 秒内未完成");
            Console.WriteLine($"   可能原因：");
            Console.WriteLine($"   1. 网络连接慢或不稳定");
            Console.WriteLine($"   2. 需要配置代理（在设置页面启用代理）");
            Console.WriteLine($"   3. 币安API访问受限");
            return Array.Empty<Candlestick>();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"❌ [BinanceGateway] HTTP请求异常: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
            
            // SSL/TLS 相关错误的特殊处理
            if (ex.Message.Contains("443") || ex.Message.Contains("SSL") || ex.Message.Contains("证书") || 
                ex.Message.Contains("certificate") || ex.Message.Contains("TLS"))
            {
                Console.WriteLine($"   🔍 SSL/TLS连接问题诊断：");
                Console.WriteLine($"   1. 检查网络连接是否正常");
                Console.WriteLine($"   2. 检查防火墙/代理设置");
                Console.WriteLine($"   3. 如果使用代理，确保在设置页面启用代理");
                Console.WriteLine($"   4. 临时调试：在设置页面启用\"跳过SSL证书验证\"（仅用于测试）");
            }
            
            return Array.Empty<Candlestick>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取K线异常: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
            return Array.Empty<Candlestick>();
        }
    }

    public async Task<IReadOnlyList<FundingRateData>> FetchFundingRatesAsync(
        string symbol,
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUrl = BuildFundingRateUrl(symbol, limit, startTime, endTime);
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"❌ [BinanceGateway] 获取资金费率失败: HTTP {response.StatusCode} - {errorContent}");
                return Array.Empty<FundingRateData>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiItems = await JsonSerializer.DeserializeAsync<List<FundingRateApiItem>>(stream, _serializerOptions, cancellationToken);

            if (apiItems == null || apiItems.Count == 0)
            {
                return Array.Empty<FundingRateData>();
            }

            var result = new List<FundingRateData>(apiItems.Count);
            foreach (var item in apiItems)
            {
                result.Add(new FundingRateData
                {
                    Symbol = item.Symbol ?? symbol.ToUpperInvariant(),
                    CalcTime = item.FundingTime,
                    CalcTimeStr = DateTimeOffset.FromUnixTimeMilliseconds(item.FundingTime).UtcDateTime,
                    FundingIntervalHours = 8,
                    LastFundingRate = decimal.TryParse(item.FundingRate, out var rate) ? rate : 0m
                });
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取资金费率异常: {ex.Message}");
            return Array.Empty<FundingRateData>();
        }
    }

    public ClientWebSocket CreateWebSocketClient()
    {
        var socket = new ClientWebSocket();

        if (_options.EnableProxy && !string.IsNullOrWhiteSpace(_options.ProxyAddress))
        {
            var proxyService = ServiceContainer.GetService<ProxyManagementService>();
            var activeConfig = proxyService.GetActiveProxyConfig();
            
            if (activeConfig != null && activeConfig.IsEnabled)
            {
                socket.Options.Proxy = new WebProxy(_options.ProxyAddress!);
            }
        }

        socket.Options.SetRequestHeader("User-Agent", _options.UserAgent);
        return socket;
    }

    public Uri BuildKlineStreamUri(string symbol, string interval)
    {
        var normalizedSymbol = NormalizeSymbol(symbol).ToLowerInvariant();
        return new Uri($"{_options.WebSocketBaseUrl.TrimEnd('/')}/{normalizedSymbol}@kline_{interval}");
    }

    public async Task<Ticker24hData?> FetchTicker24hAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            var url = $"{_options.RestBaseUrl}/ticker/24hr?symbol={normalizedSymbol}";
            
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"❌ [BinanceGateway] 获取Ticker24h失败: HTTP {response.StatusCode} - {errorContent}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiData = await JsonSerializer.DeserializeAsync<Ticker24hApiItem>(stream, _serializerOptions, cancellationToken);

            if (apiData == null)
                return null;

            return new Ticker24hData
            {
                Symbol = apiData.Symbol ?? normalizedSymbol,
                Price = decimal.TryParse(apiData.LastPrice, out var price) ? price : 0m,
                PriceChangePercent = decimal.TryParse(apiData.PriceChangePercent, out var change) ? change : 0m,
                Volume24h = decimal.TryParse(apiData.Volume, out var volume) ? volume : 0m,
                QuoteVolume24h = decimal.TryParse(apiData.QuoteVolume, out var quoteVolume) ? quoteVolume : 0m,
                BuyVolume = decimal.TryParse(apiData.BuyVolume, out var buyVol) ? buyVol : 0m,
                SellVolume = decimal.TryParse(apiData.SellVolume, out var sellVol) ? sellVol : 0m,
                OpenTime = apiData.OpenTime,
                CloseTime = apiData.CloseTime,
                LastUpdateTime = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取Ticker24h异常: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<Ticker24hData>> FetchAllTickers24hAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_options.RestBaseUrl}/ticker/24hr";
            
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"❌ [BinanceGateway] 获取所有Ticker24h失败: HTTP {response.StatusCode} - {errorContent}");
                return Array.Empty<Ticker24hData>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiItems = await JsonSerializer.DeserializeAsync<List<Ticker24hApiItem>>(stream, _serializerOptions, cancellationToken);

            if (apiItems == null || apiItems.Count == 0)
                return Array.Empty<Ticker24hData>();

            var result = new List<Ticker24hData>(apiItems.Count);
            foreach (var item in apiItems)
            {
                if (string.IsNullOrEmpty(item.Symbol))
                    continue;

                result.Add(new Ticker24hData
                {
                    Symbol = item.Symbol,
                    Price = decimal.TryParse(item.LastPrice, out var price) ? price : 0m,
                    PriceChangePercent = decimal.TryParse(item.PriceChangePercent, out var change) ? change : 0m,
                    Volume24h = decimal.TryParse(item.Volume, out var volume) ? volume : 0m,
                    QuoteVolume24h = decimal.TryParse(item.QuoteVolume, out var quoteVolume) ? quoteVolume : 0m,
                    BuyVolume = decimal.TryParse(item.BuyVolume, out var buyVol) ? buyVol : 0m,
                    SellVolume = decimal.TryParse(item.SellVolume, out var sellVol) ? sellVol : 0m,
                    OpenTime = item.OpenTime,
                    CloseTime = item.CloseTime,
                    LastUpdateTime = DateTime.UtcNow
                });
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取所有Ticker24h异常: {ex.Message}");
            return Array.Empty<Ticker24hData>();
        }
    }

    public async Task<OpenInterestData?> FetchOpenInterestAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            var url = $"{_options.RestBaseUrl}/openInterest?symbol={normalizedSymbol}";
            
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"❌ [BinanceGateway] 获取持仓量失败: HTTP {response.StatusCode} - {errorContent}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiData = await JsonSerializer.DeserializeAsync<OpenInterestApiItem>(stream, _serializerOptions, cancellationToken);

            if (apiData == null)
                return null;

            return new OpenInterestData
            {
                Symbol = apiData.Symbol ?? normalizedSymbol,
                OpenInterest = decimal.TryParse(apiData.OpenInterest, out var oi) ? oi : 0m,
                UpdateTime = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取持仓量异常: {ex.Message}");
            return null;
        }
    }

    public async Task<LongShortRatioData?> FetchLongShortRatioAsync(
        string symbol, 
        string period = "5m", 
        string ratioType = "global", 
        CancellationToken cancellationToken = default)
    {
        // 调用历史数据查询方法，limit=1获取最新数据
        var history = await FetchLongShortRatioHistoryAsync(symbol, period, 1, null, null, ratioType, cancellationToken);
        return history.Count > 0 ? history[0] : null;
    }
    
    public async Task<IReadOnlyList<LongShortRatioData>> FetchLongShortRatioHistoryAsync(
        string symbol,
        string period = "5m",
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string ratioType = "global",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            var requestUrl = BuildLongShortRatioUrl(normalizedSymbol, period, limit, startTime, endTime, ratioType);
            
            Console.WriteLine($"🔗 [BinanceGateway] 请求多空比历史数据URL: {requestUrl}");
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var shortError = errorContent.Length > 200 ? errorContent.Substring(0, 200) + "..." : errorContent;
                Console.WriteLine($"❌ [BinanceGateway] 获取多空比历史数据失败: HTTP {response.StatusCode} - {shortError}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"⚠️ [BinanceGateway] 多空比API端点不存在或不可用");
                }
                
                return Array.Empty<LongShortRatioData>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiItems = await JsonSerializer.DeserializeAsync<List<LongShortRatioApiItem>>(stream, _serializerOptions, cancellationToken);

            if (apiItems == null || apiItems.Count == 0)
            {
                Console.WriteLine($"⚠️ [BinanceGateway] 多空比历史数据为空");
                return Array.Empty<LongShortRatioData>();
            }

            var result = new List<LongShortRatioData>(apiItems.Count);
            foreach (var item in apiItems)
            {
                var longShortRatio = decimal.TryParse(item.LongShortRatio, out var ratio) ? ratio : 0m;
                var longAccount = decimal.TryParse(item.LongAccount, out var longAcc) ? longAcc : 0m;
                var shortAccount = decimal.TryParse(item.ShortAccount, out var shortAcc) ? shortAcc : 0m;
                var longPosition = decimal.TryParse(item.LongPosition, out var longPos) ? longPos : 0m;
                var shortPosition = decimal.TryParse(item.ShortPosition, out var shortPos) ? shortPos : 0m;
                
                // 计算多头和空头比例
                // 如果有多头账户数据，使用账户比例；否则使用持仓比例；最后使用多空比计算
                decimal longRatio = 0.5m;
                decimal shortRatio = 0.5m;
                
                if (longAccount > 0 && shortAccount > 0)
                {
                    // 使用账户比例
                    var total = longAccount + shortAccount;
                    longRatio = longAccount / total;
                    shortRatio = shortAccount / total;
                }
                else if (longPosition > 0 && shortPosition > 0)
                {
                    // 使用持仓比例
                    var total = longPosition + shortPosition;
                    longRatio = longPosition / total;
                    shortRatio = shortPosition / total;
                }
                else if (longShortRatio > 0)
                {
                    // 从多空比计算：long_ratio = ratio / (ratio + 1)
                    longRatio = longShortRatio / (longShortRatio + 1.0m);
                    shortRatio = 1.0m - longRatio;
                }
                
                result.Add(new LongShortRatioData
                {
                    Symbol = normalizedSymbol,
                    Period = period,
                    LongAccountRatio = longAccount > 0 ? longAccount : longRatio,
                    LongPositionRatio = longPosition > 0 ? longPosition : longRatio,
                    LongShortRatio = longShortRatio,
                    UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(item.Timestamp).UtcDateTime
                });
            }
            
            Console.WriteLine($"✅ [BinanceGateway] 获取多空比历史数据成功: {normalizedSymbol} - 共 {result.Count} 条");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取多空比历史数据异常: {ex.Message}");
            return Array.Empty<LongShortRatioData>();
        }
    }
    
    /// <summary>
    /// 构建多空比API请求URL
    /// </summary>
    private string BuildLongShortRatioUrl(
        string symbol, 
        string period, 
        int limit, 
        DateTime? startTime, 
        DateTime? endTime, 
        string ratioType)
    {
        var baseUrl = _options.RestBaseUrl.Replace("/fapi/v1", ""); // 移除 /fapi/v1，使用基础URL
        
        // 根据ratioType选择不同的API端点
        string endpoint;
        switch (ratioType.ToLower())
        {
            case "global":
                endpoint = "globalLongShortAccountRatio";
                break;
            case "top":
                endpoint = "topLongShortAccountRatio";
                break;
            case "position":
                endpoint = "topLongShortPositionRatio";
                break;
            default:
                endpoint = "globalLongShortAccountRatio";
                break;
        }
        
        var url = $"{baseUrl}/futures/data/{endpoint}?symbol={symbol}&period={period}&limit={limit}";
        
        if (startTime.HasValue)
        {
            var startTimestamp = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds();
            url += $"&startTime={startTimestamp}";
        }
        
        if (endTime.HasValue)
        {
            var endTimestamp = new DateTimeOffset(endTime.Value).ToUnixTimeMilliseconds();
            url += $"&endTime={endTimestamp}";
        }
        
        return url;
    }

    public async Task<LiquidationData?> FetchLiquidationDataAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            // 币安期货API没有直接的liquidationOrders端点
            // 需要使用forceOrders端点，但这个端点需要认证
            // 或者使用其他数据源
            // 暂时返回空数据，后续可以通过其他方式获取
            
            Console.WriteLine($"⚠️ [BinanceGateway] 爆仓数据API暂不可用（币安期货API无公开端点）");
            
            // 返回空数据，避免UI显示错误
            return await Task.FromResult(new LiquidationData
            {
                Symbol = normalizedSymbol,
                LongLiquidation = 0m,
                ShortLiquidation = 0m,
                TotalLiquidation = 0m,
                UpdateTime = DateTime.UtcNow
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取爆仓数据异常: {ex.Message}");
            return new LiquidationData
            {
                Symbol = NormalizeSymbol(symbol),
                LongLiquidation = 0m,
                ShortLiquidation = 0m,
                TotalLiquidation = 0m,
                UpdateTime = DateTime.UtcNow
            };
        }
    }

    public async Task<TakerLongShortRatioData?> FetchTakerLongShortRatioAsync(
        string symbol, 
        string period = "5m", 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            var baseUrl = _options.RestBaseUrl.Replace("/fapi/v1", "");
            var url = $"{baseUrl}/futures/data/takerlongshortRatio?symbol={normalizedSymbol}&period={period}&limit=1";
            
            Console.WriteLine($"🔗 [BinanceGateway] 请求主动买卖量URL: {url}");
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var shortError = errorContent.Length > 200 ? errorContent.Substring(0, 200) + "..." : errorContent;
                Console.WriteLine($"❌ [BinanceGateway] 获取主动买卖量失败: HTTP {response.StatusCode}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiItems = await JsonSerializer.DeserializeAsync<List<TakerLongShortRatioApiItem>>(stream, _serializerOptions, cancellationToken);

            if (apiItems == null || apiItems.Count == 0)
            {
                Console.WriteLine($"⚠️ [BinanceGateway] 主动买卖量数据为空");
                return null;
            }

            var item = apiItems[0];
            
            var buyVol = decimal.TryParse(item.BuyVol, out var buy) ? buy : 0m;
            var sellVol = decimal.TryParse(item.SellVol, out var sell) ? sell : 0m;
            var buySellRatio = sellVol > 0 ? (buyVol / sellVol) : 0m;
            
            Console.WriteLine($"✅ [BinanceGateway] 获取主动买卖量成功: {normalizedSymbol} - 买入={buyVol}, 卖出={sellVol}, 买卖比={buySellRatio:F2}");
            
            return new TakerLongShortRatioData
            {
                Symbol = normalizedSymbol,
                Period = period,
                BuyVol = buyVol,
                SellVol = sellVol,
                BuySellRatio = buySellRatio,
                UpdateTime = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [BinanceGateway] 获取主动买卖量异常: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private string BuildKlineUrl(string symbol, string interval, int limit, DateTime? startTime, DateTime? endTime)
    {
        limit = Math.Clamp(limit, 1, 1500);
        var normalizedSymbol = NormalizeSymbol(symbol);
        var url = $"{_options.RestBaseUrl}/klines?symbol={normalizedSymbol}&interval={interval}&limit={limit}";

        if (startTime.HasValue)
        {
            var ms = new DateTimeOffset(startTime.Value.ToUniversalTime()).ToUnixTimeMilliseconds();
            url += $"&startTime={ms}";
        }

        if (endTime.HasValue)
        {
            var ms = new DateTimeOffset(endTime.Value.ToUniversalTime()).ToUnixTimeMilliseconds();
            url += $"&endTime={ms}";
        }

        return url;
    }

    private string BuildFundingRateUrl(string symbol, int limit, DateTime? startTime, DateTime? endTime)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var normalizedSymbol = NormalizeSymbol(symbol);
        var url = $"{_options.RestBaseUrl}/fundingRate?symbol={normalizedSymbol}&limit={limit}";

        if (startTime.HasValue)
        {
            var ms = new DateTimeOffset(startTime.Value.ToUniversalTime()).ToUnixTimeMilliseconds();
            url += $"&startTime={ms}";
        }

        if (endTime.HasValue)
        {
            var ms = new DateTimeOffset(endTime.Value.ToUniversalTime()).ToUnixTimeMilliseconds();
            url += $"&endTime={ms}";
        }

        return url;
    }

    private static HttpClient CreateHttpClient(BinanceGatewayOptions options)
    {
        // ✅ 新方式：使用ProxyManagementService统一管理代理
        var proxyService = ServiceContainer.GetService<ProxyManagementService>();
        var handler = proxyService.CreateHttpClientHandler();

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = options.HttpTimeout
        };

        client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
        return client;
    }

    private static string NormalizeSymbol(string symbol)
    {
        return string.IsNullOrWhiteSpace(symbol) ? ServiceContainer.GetService<AppSettingsService>().Settings.DefaultSymbol : symbol.ToUpperInvariant();
    }

    private sealed class FundingRateApiItem
    {
        public string? Symbol { get; set; }
        public long FundingTime { get; set; }
        public string FundingRate { get; set; } = "0";
    }

    /// <summary>
    /// 24h Ticker API响应模型
    /// </summary>
    private sealed class Ticker24hApiItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("symbol")]
        public string? Symbol { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("lastPrice")]
        public string? LastPrice { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("priceChangePercent")]
        public string? PriceChangePercent { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("volume")]
        public string? Volume { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("quoteVolume")]
        public string? QuoteVolume { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("buyVol")]
        public string? BuyVolume { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("sellVol")]
        public string? SellVolume { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("openTime")]
        public long OpenTime { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("closeTime")]
        public long CloseTime { get; set; }
    }

    /// <summary>
    /// 持仓量API响应模型
    /// </summary>
    private sealed class OpenInterestApiItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("symbol")]
        public string? Symbol { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("openInterest")]
        public string? OpenInterest { get; set; }
    }

    /// <summary>
    /// 多空比API响应模型
    /// 根据币安API文档：https://binance-docs.github.io/apidocs/futures/cn/#v1-0-0
    /// 
    /// 不同端点的返回字段：
    /// - globalLongShortAccountRatio: symbol, longAccount, shortAccount, longShortRatio, timestamp
    /// - topLongShortAccountRatio: symbol, longAccount, shortAccount, longShortRatio, timestamp
    /// - topLongShortPositionRatio: symbol, longPosition, shortPosition, longShortRatio, timestamp
    /// </summary>
    private sealed class LongShortRatioApiItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("symbol")]
        public string? Symbol { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("longAccount")]
        public string? LongAccount { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("longPosition")]
        public string? LongPosition { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("shortAccount")]
        public string? ShortAccount { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("shortPosition")]
        public string? ShortPosition { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("longShortRatio")]
        public string? LongShortRatio { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// 爆仓订单API响应模型
    /// </summary>
    private sealed class LiquidationApiItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("symbol")]
        public string? Symbol { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("side")]
        public string? Side { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("qty")]
        public string? Qty { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("price")]
        public string? Price { get; set; }
    }

    /// <summary>
    /// 主动买卖量API响应模型
    /// </summary>
    private sealed class TakerLongShortRatioApiItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("symbol")]
        public string? Symbol { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("buyVol")]
        public string? BuyVol { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("sellVol")]
        public string? SellVol { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }
}

/// <summary>
/// Binance网关配置
/// </summary>
public sealed class BinanceGatewayOptions
{
    public string RestBaseUrl { get; set; } = "https://fapi.binance.com/fapi/v1";
    public string WebSocketBaseUrl { get; set; } = "wss://fstream.binance.com/ws";
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public bool EnableProxy { get; set; }
    public string? ProxyAddress { get; set; }
    public string? ProxyUsername { get; set; }
    public string? ProxyPassword { get; set; }
    public string UserAgent { get; set; } = "ProphetClient/1.0";

    public static BinanceGatewayOptions CreateFromConfig()
    {
        var settings = ServiceContainer.GetService<AppSettingsService>().Settings;
        return new BinanceGatewayOptions
        {
            RestBaseUrl = Core.AppConfig.Trading.BinanceApi.RestBaseUrl,
            WebSocketBaseUrl = Core.AppConfig.Trading.BinanceApi.WebSocketBaseUrl,
            HttpTimeout = TimeSpan.FromSeconds(Core.AppConfig.Trading.BinanceApi.RestTimeoutSeconds),
            EnableProxy = settings.EnableProxy,
            ProxyAddress = settings.ProxyAddress,
            ProxyUsername = settings.ProxyUsername,
            ProxyPassword = settings.ProxyPassword
        };
    }
}
