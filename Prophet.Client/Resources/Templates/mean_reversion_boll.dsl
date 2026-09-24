// 布林带均值回归策略 - 使用 Prophet DSL
// 基于布林带的均值回归，结合RSI和CCI确认
// 适用于震荡行情

// ========== 超卖反弹入场 ==========
ALL {
    // 价格触及或跌破下轨
    KLINE(5m).close(0) <= $(5m).BOLL().lower;
    $(5m).BOLL().position == LOWER;
    
    // RSI超卖
    $(5m).RSI().level == OVERSOLD;
    $(5m).RSI().value < 30;
    
    // CCI超卖确认
    $(5m).CCI().level == OVERSOLD;
    $(5m).CCI().value < -100;
    
    // 出现看涨反转K线
    KLINE(5m).close(0) > KLINE(5m).open(0);
    
    // 布林带宽度适中（避免趋势市场）
    $(5m).BOLL().width > 0.01;
    $(5m).BOLL().width < 0.05;
} = BUY;

// ========== 回归中轨平仓 ==========
ANY {
    // 价格回到中轨附近
    KLINE(5m).close(0) >= $(5m).BOLL().middle;
    
    // RSI回到50以上
    $(5m).RSI().value > 50;
    
    // CCI回到零轴以上
    $(5m).CCI().value > 0;
    
    // 或者RSI进入超买区
    $(5m).RSI().overbought == true;
} = SELL;

// ========== 超买回落入场 ==========
ALL {
    // 价格触及或突破上轨
    KLINE(5m).close(0) >= $(5m).BOLL().upper;
    $(5m).BOLL().position == UPPER;
    
    // RSI超买
    $(5m).RSI().level == OVERBOUGHT;
    $(5m).RSI().value > 70;
    
    // CCI超买确认
    $(5m).CCI().level == OVERBOUGHT;
    $(5m).CCI().value > 100;
    
    // 出现看跌反转K线
    KLINE(5m).close(0) < KLINE(5m).open(0);
    
    // 布林带宽度适中
    $(5m).BOLL().width > 0.01;
    $(5m).BOLL().width < 0.05;
} = SELL;

// ========== 回归中轨平仓 ==========
ANY {
    // 价格回到中轨附近
    KLINE(5m).close(0) <= $(5m).BOLL().middle;
    
    // RSI回到50以下
    $(5m).RSI().value < 50;
    
    // CCI回到零轴以下
    $(5m).CCI().value < 0;
    
    // 或者RSI进入超卖区
    $(5m).RSI().oversold == true;
} = BUY;

