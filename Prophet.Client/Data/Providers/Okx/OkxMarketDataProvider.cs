using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Core;
using Prophet.Client.Data.Abstractions;
using Prophet.Client.Data.Models;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Services.Network;
using Prophet.Client.Services.Settings;

namespace Prophet.Client.Data.Providers.Okx;

/// <summary>
/// OKX 行情 Provider（最小实现：Ticker）
/// API: GET https://www.okx.com/api/v5/market/ticker?instId=BTC-USDT
/// </summary>
public sealed class OkxMarketDataProvider : IMarketDataProvider, IFundingRateProvider, IOpenInterestProvider, ILongShortRatioProvider, ITakerLongShortRatioProvider
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public OkxMarketDataProvider(HttpClient? httpClient = null)
    {
        if (httpClient == null)
        {
            _httpClient = CreateHttpClientFromSettings();
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public ExchangeId ExchangeId => ExchangeId.Okx;
    public string Name => "OKX";

    public Task<IReadOnlyList<Kline>> FetchKlinesAsync(
        string symbol,
        string interval,
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        // 主页目前不需要K线；后续可实现 OKX candlesticks 接口并做时间框架映射
        return Task.FromResult<IReadOnlyList<Kline>>(Array.Empty<Kline>());
    }

    public async Task<Ticker?> FetchTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var instId = MapSymbolToOkxInstId(symbol);
            if (string.IsNullOrWhiteSpace(instId))
            {
                return null;
            }

            var url = $"https://www.okx.com/api/v5/market/ticker?instId={Uri.EscapeDataString(instId)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                var shortText = text.Length > 200 ? text.Substring(0, 200) + "..." : text;
                Console.WriteLine($"❌ [OKX] 获取Ticker失败: HTTP {(int)response.StatusCode} {response.StatusCode} - {shortText}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OkxTickerResponse>(stream, cancellationToken: cancellationToken);
            if (payload == null || payload.Code != "0" || payload.Data == null || payload.Data.Count == 0)
            {
                return null;
            }

            var item = payload.Data[0];
            var last = ParseDecimal(item.Last);
            var open24h = ParseDecimal(item.Open24h);
            var high24h = ParseDecimal(item.High24h);
            var low24h = ParseDecimal(item.Low24h);
            var vol24h = ParseDecimal(item.Vol24h);
            var volCcy24h = ParseDecimal(item.VolCcy24h);

            decimal changePct = 0m;
            if (open24h > 0m)
            {
                changePct = (last - open24h) / open24h * 100m;
            }

            return new Ticker
            {
                Symbol = symbol, // 对上层保持内部标准symbol
                LastPrice = last,
                PriceChangePercent24h = changePct,
                Volume24h = vol24h,
                QuoteVolume24h = volCcy24h,
                HighPrice24h = high24h,
                LowPrice24h = low24h,
                UpdateTime = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [OKX] 获取Ticker异常: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public Task<OrderBook?> FetchOrderBookAsync(string symbol, int depth = 20, CancellationToken cancellationToken = default)
    {
        // 后续实现 OKX order book 接口
        return Task.FromResult<OrderBook?>(null);
    }

    public async Task<LongShortRatio?> FetchLongShortRatioAsync(string symbol, string period, CancellationToken cancellationToken = default)
    {
        try
        {
            // OKX Rubik 多空比接口通常以币种维度统计（ccy=BTC），period 形如 5m/1H/1D
            var baseAsset = SymbolParser.TryGetBaseAsset(symbol);
            if (string.IsNullOrWhiteSpace(baseAsset))
            {
                return null;
            }

            var okxPeriod = NormalizeOkxRubikPeriod(period);
            var url = $"https://www.okx.com/api/v5/rubik/stat/contracts/long-short-account-ratio?ccy={Uri.EscapeDataString(baseAsset)}&period={Uri.EscapeDataString(okxPeriod)}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("code", out var codeEl) || codeEl.GetString() != "0")
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array || dataEl.GetArrayLength() == 0)
            {
                return null;
            }

            // 取最新一条（Rubik 通常按时间升序/降序不保证，直接找最大 ts）
            long bestTs = -1;
            JsonElement? best = null;
            foreach (var item in dataEl.EnumerateArray())
            {
                var ts = TryReadLong(item, "ts") ?? TryReadLong(item, "timestamp");
                if (ts.HasValue && ts.Value > bestTs)
                {
                    bestTs = ts.Value;
                    best = item;
                }
            }

            if (best == null)
            {
                return null;
            }

            var element = best.Value;

            // Rubik 返回可能是对象数组，也可能是“数组行”。两种都兼容：
            // 数组行常见格式（经验）：[ts, longShortRatio, longAccount, shortAccount]
            string? longShortRatioStr;
            string? longAccountStr;
            string? shortAccountStr;
            long? tsMs;
            if (element.ValueKind == JsonValueKind.Array)
            {
                tsMs = TryReadArrayLong(element, 0);
                longShortRatioStr = TryReadArrayString(element, 1);
                longAccountStr = TryReadArrayString(element, 2);
                shortAccountStr = TryReadArrayString(element, 3);
            }
            else
            {
                tsMs = TryReadLong(element, "ts") ?? TryReadLong(element, "timestamp");
                longShortRatioStr = TryReadString(element, "longShortRatio") ?? TryReadString(element, "ratio");
                longAccountStr = TryReadString(element, "longAccount") ?? TryReadString(element, "longRatio");
                shortAccountStr = TryReadString(element, "shortAccount") ?? TryReadString(element, "shortRatio");
            }

            var longShortRatio = ParseDecimal(longShortRatioStr);

            // 将账户多空比转换为“多头占比”形式：longAccount/(longAccount+shortAccount)
            decimal longAccountRatio = 0.5m;
            var longAcc = ParseDecimal(longAccountStr);
            var shortAcc = ParseDecimal(shortAccountStr);
            if (longAcc > 0m || shortAcc > 0m)
            {
                var total = longAcc + shortAcc;
                if (total > 0m)
                {
                    longAccountRatio = longAcc / total;
                }
            }

            // OKX 此接口未必提供持仓多空比，这里用 longShortRatio 推一个近似占比
            decimal longPositionRatio = 0.5m;
            if (longShortRatio > 0m)
            {
                longPositionRatio = longShortRatio / (longShortRatio + 1m);
            }

            return new LongShortRatio
            {
                Symbol = symbol,
                Period = period,
                LongAccountRatio = longAccountRatio,
                LongPositionRatio = longPositionRatio,
                LongShortRatioValue = longShortRatio,
                UpdateTime = (tsMs ?? bestTs) > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(tsMs ?? bestTs).UtcDateTime : DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [OKX] 获取LongShortRatio异常: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public async Task<TakerLongShortRatio?> FetchTakerLongShortRatioAsync(string symbol, string period, CancellationToken cancellationToken = default)
    {
        try
        {
            var baseAsset = SymbolParser.TryGetBaseAsset(symbol);
            if (string.IsNullOrWhiteSpace(baseAsset))
            {
                return null;
            }

            var okxPeriod = NormalizeOkxRubikPeriod(period);
            var url = $"https://www.okx.com/api/v5/rubik/stat/contracts/taker-volume?ccy={Uri.EscapeDataString(baseAsset)}&period={Uri.EscapeDataString(okxPeriod)}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("code", out var codeEl) || codeEl.GetString() != "0")
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array || dataEl.GetArrayLength() == 0)
            {
                return null;
            }

            long bestTs = -1;
            JsonElement? best = null;
            foreach (var item in dataEl.EnumerateArray())
            {
                var ts = TryReadLong(item, "ts") ?? TryReadLong(item, "timestamp");
                if (ts.HasValue && ts.Value > bestTs)
                {
                    bestTs = ts.Value;
                    best = item;
                }
            }

            if (best == null)
            {
                return null;
            }

            var element = best.Value;
            decimal buyVol;
            decimal sellVol;
            long? tsMs;
            if (element.ValueKind == JsonValueKind.Array)
            {
                // 数组行常见格式（经验）：[ts, buyVol, sellVol]
                tsMs = TryReadArrayLong(element, 0);
                buyVol = ParseDecimal(TryReadArrayString(element, 1));
                sellVol = ParseDecimal(TryReadArrayString(element, 2));
            }
            else
            {
                tsMs = TryReadLong(element, "ts") ?? TryReadLong(element, "timestamp");
                buyVol = ParseDecimal(TryReadString(element, "buyVol") ?? TryReadString(element, "takerBuyVol") ?? TryReadString(element, "buy"));
                sellVol = ParseDecimal(TryReadString(element, "sellVol") ?? TryReadString(element, "takerSellVol") ?? TryReadString(element, "sell"));
            }
            var ratio = sellVol > 0m ? buyVol / sellVol : 0m;

            return new TakerLongShortRatio
            {
                Symbol = symbol,
                Period = period,
                BuyVol = buyVol,
                SellVol = sellVol,
                BuySellRatio = ratio,
                UpdateTime = (tsMs ?? bestTs) > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(tsMs ?? bestTs).UtcDateTime : DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [OKX] 获取TakerVolume异常: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    private static string NormalizeOkxRubikPeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return "5m";
        }

        var p = period.Trim();
        // OKX 常见取值：5m/15m/30m/1H/2H/4H/1D 等；这里做最小兼容转换
        if (p.EndsWith("h", StringComparison.OrdinalIgnoreCase))
        {
            return p.Substring(0, p.Length - 1) + "H";
        }

        if (p.EndsWith("d", StringComparison.OrdinalIgnoreCase))
        {
            return p.Substring(0, p.Length - 1) + "D";
        }

        return p;
    }

    public async Task<FundingRateSnapshot?> FetchFundingRateSnapshotAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var instId = MapSymbolToOkxInstId(symbol);
            if (string.IsNullOrWhiteSpace(instId))
            {
                return null;
            }

            var url = $"https://www.okx.com/api/v5/public/funding-rate?instId={Uri.EscapeDataString(instId)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                var shortText = text.Length > 200 ? text.Substring(0, 200) + "..." : text;
                Console.WriteLine($"❌ [OKX] 获取FundingRate失败: HTTP {(int)response.StatusCode} {response.StatusCode} - {shortText}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OkxFundingRateResponse>(stream, cancellationToken: cancellationToken);
            if (payload == null || payload.Code != "0" || payload.Data == null || payload.Data.Count == 0)
            {
                return null;
            }

            var item = payload.Data[0];
            var rate = ParseDecimal(item.FundingRate);

            var change24hPercent = await TryCalculateFundingRateChange24hPercentAsync(instId, rate, cancellationToken);

            return new FundingRateSnapshot
            {
                Symbol = symbol,
                Rate = rate,
                Change24hPercent = change24hPercent,
                UpdateTime = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [OKX] 获取FundingRate异常: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public async Task<OpenInterest?> FetchOpenInterestAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var instId = MapSymbolToOkxInstId(symbol);
            if (string.IsNullOrWhiteSpace(instId))
            {
                return null;
            }

            // OKX Open Interest：使用 SWAP + instId
            var url = $"https://www.okx.com/api/v5/public/open-interest?instType=SWAP&instId={Uri.EscapeDataString(instId)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                var shortText = text.Length > 200 ? text.Substring(0, 200) + "..." : text;
                Console.WriteLine($"❌ [OKX] 获取OpenInterest失败: HTTP {(int)response.StatusCode} {response.StatusCode} - {shortText}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OkxOpenInterestResponse>(stream, cancellationToken: cancellationToken);
            if (payload == null || payload.Code != "0" || payload.Data == null || payload.Data.Count == 0)
            {
                return null;
            }

            var item = payload.Data[0];
            var oi = ParseDecimal(item.Oi);

            return new OpenInterest
            {
                Symbol = symbol,
                Value = oi,
                UpdateTime = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ [OKX] 获取OpenInterest异常: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    private static string MapSymbolToOkxInstId(string symbol)
    {
        // 最小映射（永续）：把 BTCUSDT -> BTC-USDT-SWAP
        // 后续可扩展：支持现货 BTC-USDT、交割合约等，并基于配置决定优先级
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return string.Empty;
        }

        var baseAsset = SymbolParser.TryGetBaseAsset(symbol);
        if (!string.IsNullOrWhiteSpace(baseAsset))
        {
            return $"{baseAsset}-USDT-SWAP";
        }

        return string.Empty;
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, out var d) ? d : 0m;

    private async Task<decimal?> TryCalculateFundingRateChange24hPercentAsync(
        string instId,
        decimal currentFundingRate,
        CancellationToken cancellationToken)
    {
        try
        {
            // funding-rate-history：取最近若干条，找最接近 24h 前的那条
            var url = $"https://www.okx.com/api/v5/public/funding-rate-history?instId={Uri.EscapeDataString(instId)}&limit=20";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("code", out var codeEl) || codeEl.GetString() != "0")
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var target = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds();

            long? bestTime = null;
            decimal? bestRate = null;
            foreach (var item in dataEl.EnumerateArray())
            {
                var timeMs = TryReadLong(item, "fundingTime") ?? TryReadLong(item, "ts");
                if (!timeMs.HasValue)
                {
                    continue;
                }

                // OKX 有的字段叫 realizedRate，有的叫 fundingRate；两者取其一
                var rateStr = TryReadString(item, "fundingRate") ?? TryReadString(item, "realizedRate");
                if (string.IsNullOrWhiteSpace(rateStr))
                {
                    continue;
                }

                if (!decimal.TryParse(rateStr, out var rate))
                {
                    continue;
                }

                // 选取最接近 target 的历史点
                var diff = Math.Abs(timeMs.Value - target);
                if (!bestTime.HasValue || diff < Math.Abs(bestTime.Value - target))
                {
                    bestTime = timeMs.Value;
                    bestRate = rate;
                }
            }

            if (!bestRate.HasValue)
            {
                return null;
            }

            // 与 Binance 保持一致：变化值= (now-old) * 100，UI 显示为 “x%”
            return (currentFundingRate - bestRate.Value) * 100m;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static long? TryReadLong(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(name, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
        {
            return n;
        }

        if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out var s))
        {
            return s;
        }

        return null;
    }

    private static string? TryReadArrayString(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        if (index < 0 || index >= element.GetArrayLength())
        {
            return null;
        }

        var item = element[index];
        if (item.ValueKind == JsonValueKind.String)
        {
            return item.GetString();
        }

        if (item.ValueKind == JsonValueKind.Number)
        {
            return item.GetRawText();
        }

        return null;
    }

    private static long? TryReadArrayLong(JsonElement element, int index)
    {
        var s = TryReadArrayString(element, index);
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return long.TryParse(s, out var v) ? v : null;
    }

    private static HttpClient CreateHttpClientFromSettings()
    {
        // ✅ 使用ProxyManagementService统一管理代理
        var proxyService = ServiceContainer.GetService<ProxyManagementService>();
        var handler = proxyService.CreateHttpClientHandler();

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.Add("User-Agent", "ProphetClient/1.0");
        return client;
    }

    private sealed class OkxTickerResponse
    {
        public string? Code { get; set; }
        public List<OkxTickerItem>? Data { get; set; }
    }

    private sealed class OkxTickerItem
    {
        public string? InstId { get; set; }
        public string? Last { get; set; }
        public string? Open24h { get; set; }
        public string? High24h { get; set; }
        public string? Low24h { get; set; }
        public string? Vol24h { get; set; }
        public string? VolCcy24h { get; set; }
        public string? Ts { get; set; }
    }

    private sealed class OkxFundingRateResponse
    {
        public string? Code { get; set; }
        public List<OkxFundingRateItem>? Data { get; set; }
    }

    private sealed class OkxFundingRateItem
    {
        public string? InstId { get; set; }
        public string? FundingRate { get; set; }
        public string? NextFundingRate { get; set; }
        public string? FundingTime { get; set; }
        public string? NextFundingTime { get; set; }
    }

    private sealed class OkxOpenInterestResponse
    {
        public string? Code { get; set; }
        public List<OkxOpenInterestItem>? Data { get; set; }
    }

    private sealed class OkxOpenInterestItem
    {
        public string? InstId { get; set; }
        public string? Oi { get; set; }
        public string? Ts { get; set; }
    }
}


