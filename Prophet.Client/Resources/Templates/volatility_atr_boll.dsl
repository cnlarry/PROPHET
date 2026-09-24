// 波动率突破策略 - 使用 Prophet DSL
// 利用ATR和布林带宽度捕捉波动率扩张
// 适用于波动较大的市场

// ========== 波动率扩张向上突破 ==========
ALL {
    // ATR显示高波动
    $(5m).ATR().volatility == HIGH;
    $(5m).ATR().value > $(5m).ATR().value(-5);  // ATR上升
    
    // 布林带宽度扩张
    $(5m).BOLL().width > $(15m).BOLL().width;
    $(5m).BOLL().isexpanding == true;
    
    // 价格突破上轨
    KLINE(5m).close(0) > $(5m).BOLL().upper;
    $(5m).BOLL().position == UPPER;
    
    // RSI显示强势但未超买
    $(5m).RSI().value > 55;
    $(5m).RSI().value < 75;
    
    // 成交量大幅放大
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 2.0;
    
    // 价格动量强劲
    CHANGE(5m).close(1).pct > 0.005;  // 涨幅超过0.5%
    
    // ADX确认趋势强度
    $(5m).ADX().value > 20;
    $(5m).ADX().trend == STRONG;
} = BUY;

// ========== 波动率收缩或回归 ==========
ANY {
    // ATR降低（波动率收缩）
    $(5m).ATR().volatility == LOW;
    
    // 布林带宽度收缩
    $(5m).BOLL().issqueezing == true;
    
    // 价格回归中轨
    KLINE(5m).close(0) < $(5m).BOLL().middle;
    
    // RSI背离
    $(5m).RSI().divergence == BEARISH;
    
    // 成交量萎缩
    KLINE(5m).volume(0) < AVERAGE(5m).volume(10);
} = SELL;

// ========== 波动率扩张向下突破 ==========
ALL {
    // ATR显示高波动
    $(5m).ATR().volatility == HIGH;
    $(5m).ATR().value > $(5m).ATR().value(-5);
    
    // 布林带宽度扩张
    $(5m).BOLL().width > $(15m).BOLL().width;
    $(5m).BOLL().isexpanding == true;
    
    // 价格跌破下轨
    KLINE(5m).close(0) < $(5m).BOLL().lower;
    $(5m).BOLL().position == LOWER;
    
    // RSI显示弱势但未超卖
    $(5m).RSI().value < 45;
    $(5m).RSI().value > 25;
    
    // 成交量大幅放大
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 2.0;
    
    // 价格动量强劲
    CHANGE(5m).close(1).pct < -0.005;  // 跌幅超过0.5%
    
    // ADX确认趋势强度
    $(5m).ADX().value > 20;
    $(5m).ADX().trend == STRONG;
} = SELL;

// ========== 波动率收缩或回归 ==========
ANY {
    // ATR降低
    $(5m).ATR().volatility == LOW;
    
    // 布林带宽度收缩
    $(5m).BOLL().issqueezing == true;
    
    // 价格回归中轨
    KLINE(5m).close(0) > $(5m).BOLL().middle;
    
    // RSI背离
    $(5m).RSI().divergence == BULLISH;
    
    // 成交量萎缩
    KLINE(5m).volume(0) < AVERAGE(5m).volume(10);
} = BUY;
