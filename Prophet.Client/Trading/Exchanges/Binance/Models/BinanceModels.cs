using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Prophet.Client.Trading.Exchanges.Binance.Models;

/// <summary>
/// 币安账户信息响应
/// </summary>
public class BinanceAccountInfoResponse
{
    [JsonPropertyName("totalWalletBalance")]
    public string TotalWalletBalance { get; set; } = "0";
    
    [JsonPropertyName("availableBalance")]
    public string AvailableBalance { get; set; } = "0";
    
    [JsonPropertyName("totalMarginBalance")]
    public string TotalMarginBalance { get; set; } = "0";
    
    [JsonPropertyName("totalUnrealizedProfit")]
    public string TotalUnrealizedProfit { get; set; } = "0";
    
    [JsonPropertyName("assets")]
    public List<BinanceAsset> Assets { get; set; } = new();
    
    [JsonPropertyName("positions")]
    public List<BinancePosition> Positions { get; set; } = new();
}

/// <summary>
/// 币安资产信息
/// </summary>
public class BinanceAsset
{
    [JsonPropertyName("asset")]
    public string Asset { get; set; } = string.Empty;
    
    [JsonPropertyName("walletBalance")]
    public string WalletBalance { get; set; } = "0";
    
    [JsonPropertyName("availableBalance")]
    public string AvailableBalance { get; set; } = "0";
    
    [JsonPropertyName("crossUnPnl")]
    public string CrossUnPnl { get; set; } = "0";
}

/// <summary>
/// 币安持仓信息
/// </summary>
public class BinancePosition
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
    
    [JsonPropertyName("positionAmt")]
    public string PositionAmt { get; set; } = "0";
    
    [JsonPropertyName("entryPrice")]
    public string EntryPrice { get; set; } = "0";
    
    [JsonPropertyName("markPrice")]
    public string MarkPrice { get; set; } = "0";
    
    [JsonPropertyName("unRealizedProfit")]
    public string UnRealizedProfit { get; set; } = "0";
    
    [JsonPropertyName("liquidationPrice")]
    public string LiquidationPrice { get; set; } = "0";
    
    [JsonPropertyName("leverage")]
    public string Leverage { get; set; } = "0";
    
    [JsonPropertyName("positionSide")]
    public string PositionSide { get; set; } = "BOTH";
    
    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; set; }
}

/// <summary>
/// 币安订单响应
/// </summary>
public class BinanceOrderResponse
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }
    
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("clientOrderId")]
    public string ClientOrderId { get; set; } = string.Empty;
    
    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";
    
    [JsonPropertyName("avgPrice")]
    public string AvgPrice { get; set; } = "0";
    
    [JsonPropertyName("origQty")]
    public string OrigQty { get; set; } = "0";
    
    [JsonPropertyName("executedQty")]
    public string ExecutedQty { get; set; } = "0";
    
    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; set; }
}

/// <summary>
/// 币安K线数据
/// </summary>
public class BinanceKlineData
{
    [JsonPropertyName("t")]
    public long OpenTime { get; set; }
    
    [JsonPropertyName("o")]
    public string Open { get; set; } = "0";
    
    [JsonPropertyName("h")]
    public string High { get; set; } = "0";
    
    [JsonPropertyName("l")]
    public string Low { get; set; } = "0";
    
    [JsonPropertyName("c")]
    public string Close { get; set; } = "0";
    
    [JsonPropertyName("v")]
    public string Volume { get; set; } = "0";
    
    [JsonPropertyName("T")]
    public long CloseTime { get; set; }
    
    [JsonPropertyName("x")]
    public bool IsClosed { get; set; }
}

/// <summary>
/// 币安WebSocket K线消息
/// </summary>
public class BinanceKlineMessage
{
    [JsonPropertyName("e")]
    public string EventType { get; set; } = string.Empty;
    
    [JsonPropertyName("E")]
    public long EventTime { get; set; }
    
    [JsonPropertyName("s")]
    public string Symbol { get; set; } = string.Empty;
    
    [JsonPropertyName("k")]
    public BinanceKlineData Kline { get; set; } = new();
}

/// <summary>
/// 币安错误响应
/// </summary>
public class BinanceErrorResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 币安订单簿响应
/// </summary>
public class BinanceOrderBookResponse
{
    [JsonPropertyName("lastUpdateId")]
    public long LastUpdateId { get; set; }
    
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; set; } = new();
    
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; set; } = new();
}

/// <summary>
/// 币安Ticker响应
/// </summary>
public class BinanceTickerResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
    
    [JsonPropertyName("lastPrice")]
    public string LastPrice { get; set; } = "0";
    
    [JsonPropertyName("highPrice")]
    public string HighPrice { get; set; } = "0";
    
    [JsonPropertyName("lowPrice")]
    public string LowPrice { get; set; } = "0";
    
    [JsonPropertyName("volume")]
    public string Volume { get; set; } = "0";
    
    [JsonPropertyName("quoteVolume")]
    public string QuoteVolume { get; set; } = "0";
    
    [JsonPropertyName("priceChange")]
    public string PriceChange { get; set; } = "0";
    
    [JsonPropertyName("priceChangePercent")]
    public string PriceChangePercent { get; set; } = "0";
}

/// <summary>
/// 币安Listen Key响应
/// </summary>
public class BinanceListenKeyResponse
{
    [JsonPropertyName("listenKey")]
    public string ListenKey { get; set; } = string.Empty;
}

/// <summary>
/// 币安用户数据流 - 账户更新消息（ACCOUNT_UPDATE）
/// </summary>
public class BinanceAccountUpdateMessage
{
    [JsonPropertyName("e")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("E")]
    public long EventTime { get; set; }

    [JsonPropertyName("T")]
    public long TransactionTime { get; set; }

    [JsonPropertyName("a")]
    public BinanceAccountUpdateData UpdateData { get; set; } = new();
}

/// <summary>
/// 币安用户数据流 - 账户更新数据
/// </summary>
public class BinanceAccountUpdateData
{
    [JsonPropertyName("m")]
    public string MarginType { get; set; } = string.Empty;

    [JsonPropertyName("B")]
    public List<BinanceBalanceUpdate> Balances { get; set; } = new();

    [JsonPropertyName("P")]
    public List<BinancePositionUpdate> Positions { get; set; } = new();
}

/// <summary>
/// 币安用户数据流 - 余额更新
/// </summary>
public class BinanceBalanceUpdate
{
    [JsonPropertyName("a")]
    public string Asset { get; set; } = string.Empty;

    [JsonPropertyName("wb")]
    public string WalletBalance { get; set; } = "0";

    [JsonPropertyName("cw")]
    public string CrossWalletBalance { get; set; } = "0";
}

/// <summary>
/// 币安用户数据流 - 仓位更新
/// </summary>
public class BinancePositionUpdate
{
    [JsonPropertyName("s")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("pa")]
    public string PositionAmt { get; set; } = "0";

    [JsonPropertyName("ep")]
    public string EntryPrice { get; set; } = "0";

    [JsonPropertyName("up")]
    public string UnrealizedProfit { get; set; } = "0";

    [JsonPropertyName("mt")]
    public string MarginType { get; set; } = string.Empty;

    [JsonPropertyName("pp")]
    public string PositionSide { get; set; } = "BOTH";
}

/// <summary>
/// 币安用户数据流 - 订单更新消息（ORDER_TRADE_UPDATE）
/// </summary>
public class BinanceOrderTradeUpdateMessage
{
    [JsonPropertyName("e")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("E")]
    public long EventTime { get; set; }

    [JsonPropertyName("o")]
    public BinanceOrderUpdateData Order { get; set; } = new();
}

/// <summary>
/// 币安用户数据流 - 订单更新数据
/// </summary>
public class BinanceOrderUpdateData
{
    [JsonPropertyName("s")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("c")]
    public string ClientOrderId { get; set; } = string.Empty;

    [JsonPropertyName("i")]
    public long OrderId { get; set; }

    [JsonPropertyName("S")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("o")]
    public string OrderType { get; set; } = string.Empty;

    [JsonPropertyName("X")]
    public string CurrentStatus { get; set; } = string.Empty;

    [JsonPropertyName("p")]
    public string Price { get; set; } = "0";

    [JsonPropertyName("ap")]
    public string AveragePrice { get; set; } = "0";

    [JsonPropertyName("q")]
    public string OriginalQuantity { get; set; } = "0";

    [JsonPropertyName("z")]
    public string AccumulatedFilledQuantity { get; set; } = "0";

    [JsonPropertyName("T")]
    public long OrderTradeTime { get; set; }
}

