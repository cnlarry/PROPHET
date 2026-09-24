using System;

namespace Prophet.Client.Data.Symbols;

/// <summary>
/// 内部统一 symbol 的解析工具（目前仅覆盖 *USDT 的常见形态）
/// </summary>
public static class SymbolParser
{
    public static string? TryGetBaseAsset(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        // 兼容 InstrumentKey：BTCUSDT-OKX-SWAP -> BTCUSDT
        var core = symbol;
        var dash = symbol.IndexOf('-', StringComparison.Ordinal);
        if (dash > 0)
        {
            core = symbol.Substring(0, dash);
        }

        var upper = core.ToUpperInvariant();
        if (upper.EndsWith("USDT", StringComparison.Ordinal))
        {
            var baseAsset = upper.Substring(0, upper.Length - 4);
            return string.IsNullOrWhiteSpace(baseAsset) ? null : baseAsset;
        }

        return null;
    }
}


