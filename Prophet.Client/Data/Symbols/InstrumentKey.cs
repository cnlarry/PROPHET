using System;

namespace Prophet.Client.Data.Symbols;

/// <summary>
/// 交易标的键（AICoin风格的“Symbol选择”落地形态）
/// 例：BTCUSDT-OKX-SWAP / ETHUSDT-BINANCE-SWAP
/// </summary>
public readonly record struct InstrumentKey(string BaseSymbol, string Exchange, string MarketType)
{
    public static bool TryParse(string? value, out InstrumentKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        var baseSymbol = parts[0].ToUpperInvariant();
        var exchange = parts[1].ToUpperInvariant();
        var marketType = parts[2].ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(baseSymbol) || string.IsNullOrWhiteSpace(exchange) || string.IsNullOrWhiteSpace(marketType))
        {
            return false;
        }

        key = new InstrumentKey(baseSymbol, exchange, marketType);
        return true;
    }

    /// <summary>
    /// 规范字符串表示（与 symbol_key 格式一致）：BASE-EXCHANGE-MARKETTYPE，如 BTCUSDT-BINANCE-SWAP
    /// </summary>
    public override string ToString()
    {
        return $"{BaseSymbol}-{Exchange}-{MarketType}";
    }
}


