using Prophet.Client.Data.Providers.Binance;
using Prophet.Client.Data.Providers.Okx;
using Prophet.Client.Models;

namespace Prophet.Client.Data;

public static class DataPlaneFactory
{
    public static DataPlaneService CreateFromSettings(AppSettings settings)
    {
        var dataPlane = new DataPlaneService();

        // 默认路由
        dataPlane.SetDefaults(
            marketDataExchange: ExchangeIdParser.ParseOrDefault(settings.DefaultMarketDataExchange, ExchangeId.Binance),
            tradingExchange: ExchangeIdParser.ParseOrDefault(settings.DefaultTradingExchange, ExchangeId.Binance));

        // 注册 Binance Provider（后续添加 OKX/Bybit 只需 Register 新 Provider）
        dataPlane.Register(new BinanceExchangeProvider());
        dataPlane.Register(new OkxExchangeProvider());

        return dataPlane;
    }
}


