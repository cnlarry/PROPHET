using Prophet.Client.Data.Abstractions;

namespace Prophet.Client.Data.Providers.Okx;

/// <summary>
/// OKX 交易所 Provider（最小可用：先实现行情Ticker；交易能力先占位）
/// </summary>
public sealed class OkxExchangeProvider : IExchangeProvider
{
    private readonly OkxMarketDataProvider _marketData;
    private readonly OkxTradingProvider _trading;

    public OkxExchangeProvider()
    {
        _marketData = new OkxMarketDataProvider();
        _trading = new OkxTradingProvider();
    }

    public ExchangeId ExchangeId => ExchangeId.Okx;
    public string Name => "OKX";

    public IMarketDataProvider MarketData => _marketData;
    public ITradingProvider Trading => _trading;

    public void Dispose()
    {
        _marketData.Dispose();
        _trading.Dispose();
    }
}


