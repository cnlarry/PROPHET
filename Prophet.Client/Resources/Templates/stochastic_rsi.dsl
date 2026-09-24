// 随机RSI策略 - 使用 Prophet DSL
// 使用StochRSI捕捉超买超卖的精确时机
// StochRSI比RSI更敏感

// ========== StochRSI超卖反弹 ==========
ALL {
    // StochRSI在超卖区
    $(5m).STOCHRSI().k < 20;
    $(5m).STOCHRSI().d < 20;
    
    // StochRSI金叉（K线上穿D线）
    $(5m).STOCHRSI().crossover == GOLDEN_CROSS;
    
    // K线开始上升
    $(5m).STOCHRSI().k > $(5m).STOCHRSI().k(-1);
    
    // RSI也在低位确认
    $(5m).RSI().value < 40;
    $(5m).RSI().level == OVERSOLD;
    
    // 价格在布林带下轨附近
    KLINE(5m).close(0) <= $(5m).BOLL().lower;
    
    // 出现看涨K线
    KLINE(5m).close(0) > KLINE(5m).open(0);
    
    // MACD柱状图开始转正
    $(5m).MACD().histogram > $(5m).MACD().histogram(-1);
} = BUY;

// ========== StochRSI超买或背离 ==========
ANY {
    // StochRSI进入超买区并死叉
    $(5m).STOCHRSI().k > 80 AND $(5m).STOCHRSI().crossover == DEATH_CROSS;
    
    // StochRSI背离（价格新高但StochRSI未新高）
    $(5m).STOCHRSI().divergence == BEARISH;
    
    // 价格突破布林带上轨并回落
    KLINE(5m).close(0) < $(5m).BOLL().upper AND $(5m).STOCHRSI().k < $(5m).STOCHRSI().k(-1);
    
    // RSI超买
    $(5m).RSI().value > 75;
} = SELL;

// ========== StochRSI超买回落 ==========
ALL {
    // StochRSI在超买区
    $(5m).STOCHRSI().k > 80;
    $(5m).STOCHRSI().d > 80;
    
    // StochRSI死叉（K线下穿D线）
    $(5m).STOCHRSI().crossover == DEATH_CROSS;
    
    // K线开始下降
    $(5m).STOCHRSI().k < $(5m).STOCHRSI().k(-1);
    
    // RSI也在高位确认
    $(5m).RSI().value > 60;
    $(5m).RSI().level == OVERBOUGHT;
    
    // 价格在布林带上轨附近
    KLINE(5m).close(0) >= $(5m).BOLL().upper;
    
    // 出现看跌K线
    KLINE(5m).close(0) < KLINE(5m).open(0);
    
    // MACD柱状图开始转负
    $(5m).MACD().histogram < $(5m).MACD().histogram(-1);
} = SELL;

// ========== StochRSI超卖或背离 ==========
ANY {
    // StochRSI进入超卖区并金叉
    $(5m).STOCHRSI().k < 20 AND $(5m).STOCHRSI().crossover == GOLDEN_CROSS;
    
    // StochRSI背离（价格新低但StochRSI未新低）
    $(5m).STOCHRSI().divergence == BULLISH;
    
    // 价格跌破布林带下轨并反弹
    KLINE(5m).close(0) > $(5m).BOLL().lower AND $(5m).STOCHRSI().k > $(5m).STOCHRSI().k(-1);
    
    // RSI超卖
    $(5m).RSI().value < 25;
} = BUY;
