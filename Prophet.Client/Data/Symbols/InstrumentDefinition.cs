using System;

namespace Prophet.Client.Data.Symbols;

public sealed class InstrumentDefinition
{
    public string SymbolKey { get; set; } = string.Empty;
    public string BaseSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;     // BINANCE / OKX ...
    public string MarketType { get; set; } = string.Empty;   // SWAP / SPOT ...
    public string VenueInstrumentId { get; set; } = string.Empty;
    public int IsEnabled { get; set; } = 1;

    public static InstrumentDefinition CreateBinanceSwap(string baseSymbol)
    {
        var upper = baseSymbol.ToUpperInvariant();
        return new InstrumentDefinition
        {
            SymbolKey = $"{upper}-BINANCE-SWAP",
            BaseSymbol = upper,
            Exchange = "BINANCE",
            MarketType = "SWAP",
            VenueInstrumentId = upper,
            IsEnabled = 1
        };
    }

    public static InstrumentDefinition CreateOkxSwap(string baseSymbol)
    {
        var upper = baseSymbol.ToUpperInvariant();
        return new InstrumentDefinition
        {
            SymbolKey = $"{upper}-OKX-SWAP",
            BaseSymbol = upper,
            Exchange = "OKX",
            MarketType = "SWAP",
            VenueInstrumentId = ToOkxSwapInstrumentId(upper),
            IsEnabled = 1
        };
    }

    private static string ToOkxSwapInstrumentId(string baseSymbolUpper)
    {
        // 目前仅覆盖 USDT 线性永续：BTCUSDT -> BTC-USDT-SWAP
        if (!baseSymbolUpper.EndsWith("USDT", StringComparison.Ordinal))
        {
            return baseSymbolUpper;
        }

        var baseAsset = baseSymbolUpper.Substring(0, baseSymbolUpper.Length - 4);
        return $"{baseAsset}-USDT-SWAP";
    }
}


