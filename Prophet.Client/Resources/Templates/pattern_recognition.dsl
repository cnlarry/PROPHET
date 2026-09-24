// K线形态识别策略 - 使用 Prophet DSL
// 识别经典K线形态并结合趋势
// 反转形态和持续形态

// ========== 看涨反转形态 + 趋势确认 ==========
ANY {
    // 锤子线（看涨反转）
    PATTERN(5m).hammer() == true AND $(5m).RSI().level == OVERSOLD AND KLINE(5m).close(0) > KLINE(5m).open(0) AND KLINE(5m).close(0) < $(5m).BOLL().lower;
    
    // 看涨吞没形态
    PATTERN(5m).engulfing() == BULLISH AND $(5m).RSI().value < 40 AND KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.5;
    
    // 启明星形态
    PATTERN(5m).morningstar() == true AND $(5m).MACD().histogram > 0 AND KLINE(5m).close(0) > $(5m).EMA().value;
    
    // 三白兵
    PATTERN(5m).three_white_soldiers() == true AND $(5m).ADX().value > 20 AND CONSECUTIVE(5m).close(3).rising() >= 3;
    
    // 上升三法
    PATTERN(5m).rising_three_methods() == true AND $(5m).MACD().trend == BULLISH;
} = BUY;

// ========== 看跌反转形态出场 ==========
ANY {
    // 射击之星
    PATTERN(5m).shooting_star() == true;
    
    // 黄昏之星
    PATTERN(5m).eveningstar() == true;
    
    // 看跌吞没
    PATTERN(5m).engulfing() == BEARISH;
    
    // 乌云盖顶
    PATTERN(5m).dark_cloud_cover() == true;
    
    // 三只乌鸦
    PATTERN(5m).three_black_crows() == true;
} = SELL;

// ========== 看跌反转形态 + 趋势确认 ==========
ANY {
    // 倒锤子线（看跌反转）
    PATTERN(5m).inverted_hammer() == true AND $(5m).RSI().level == OVERBOUGHT AND KLINE(5m).close(0) < KLINE(5m).open(0) AND KLINE(5m).close(0) > $(5m).BOLL().upper;
    
    // 看跌吞没形态
    PATTERN(5m).engulfing() == BEARISH AND $(5m).RSI().value > 60 AND KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.5;
    
    // 黄昏之星形态
    PATTERN(5m).eveningstar() == true AND $(5m).MACD().histogram < 0 AND KLINE(5m).close(0) < $(5m).EMA().value;
    
    // 三只乌鸦
    PATTERN(5m).three_black_crows() == true AND $(5m).ADX().value > 20 AND CONSECUTIVE(5m).close(3).falling() >= 3;
    
    // 下降三法
    PATTERN(5m).falling_three_methods() == true AND $(5m).MACD().trend == BEARISH;
} = SELL;

// ========== 看涨反转形态出场 ==========
ANY {
    // 锤子线
    PATTERN(5m).hammer() == true;
    
    // 启明星
    PATTERN(5m).morningstar() == true;
    
    // 看涨吞没
    PATTERN(5m).engulfing() == BULLISH;
    
    // 曙光初现
    PATTERN(5m).piercing() == true;
    
    // 三白兵
    PATTERN(5m).three_white_soldiers() == true;
} = BUY;
