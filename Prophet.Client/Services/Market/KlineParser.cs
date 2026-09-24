using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Prophet.Client.Models;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 统一的币安K线解析器
/// </summary>
internal static class KlineParser
{
    public static Candlestick? ParseRestKline(IReadOnlyList<JsonElement> raw)
    {
        try
        {
            if (raw.Count < 6)
                return null;

            var openTimeMs = raw[0].GetInt64();
            var candle = new Candlestick
            {
                Time = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime,
                Open = ParseDouble(raw[1]),
                High = ParseDouble(raw[2]),
                Low = ParseDouble(raw[3]),
                Close = ParseDouble(raw[4]),
                Volume = ParseDouble(raw[5])
            };

            if (raw.Count > 7 && raw[7].ValueKind != JsonValueKind.Null)
            {
                candle.QuoteVolume = ParseDouble(raw[7]);
            }

            if (raw.Count > 8 && raw[8].ValueKind != JsonValueKind.Null)
            {
                candle.TradeCount = raw[8].GetInt64();
            }

            if (raw.Count > 9 && raw[9].ValueKind != JsonValueKind.Null)
            {
                candle.TakerBuyVolume = ParseDouble(raw[9]);
            }

            if (raw.Count > 10 && raw[10].ValueKind != JsonValueKind.Null)
            {
                candle.TakerBuyQuoteVolume = ParseDouble(raw[10]);
            }

            return candle;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [KlineParser] 解析REST K线失败: {ex.Message}");
            return null;
        }
    }

    public static Candlestick? ParseWebSocketKline(JsonElement payload)
    {
        try
        {
            if (!payload.TryGetProperty("t", out var openTimeProperty))
                return null;

            var openTimeMs = openTimeProperty.GetInt64();

            // 检查K线是否已闭合（x字段：true=已闭合，false=进行中）
            var isClosed = payload.TryGetProperty("x", out var xProperty) && xProperty.GetBoolean();

            var candle = new Candlestick
            {
                Time = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime,
                Open = ParseDouble(payload.GetProperty("o")),
                High = ParseDouble(payload.GetProperty("h")),
                Low = ParseDouble(payload.GetProperty("l")),
                Close = ParseDouble(payload.GetProperty("c")),
                Volume = ParseDouble(payload.GetProperty("v")),
                IsClosed = isClosed // 标记K线是否已闭合
            };

            if (payload.TryGetProperty("q", out var quoteVolume) && quoteVolume.ValueKind != JsonValueKind.Null)
            {
                candle.QuoteVolume = ParseDouble(quoteVolume);
            }

            if (payload.TryGetProperty("n", out var tradeCount) && tradeCount.ValueKind != JsonValueKind.Null)
            {
                candle.TradeCount = tradeCount.GetInt64();
            }

            if (payload.TryGetProperty("V", out var takerBuyVolume) && takerBuyVolume.ValueKind != JsonValueKind.Null)
            {
                candle.TakerBuyVolume = ParseDouble(takerBuyVolume);
            }

            if (payload.TryGetProperty("Q", out var takerBuyQuoteVolume) && takerBuyQuoteVolume.ValueKind != JsonValueKind.Null)
            {
                candle.TakerBuyQuoteVolume = ParseDouble(takerBuyQuoteVolume);
            }

            return candle;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [KlineParser] 解析WebSocket K线失败: {ex.Message}");
            return null;
        }
    }

    private static double ParseDouble(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => double.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
            JsonValueKind.Number => element.GetDouble(),
            _ => 0d
        };
    }
}
