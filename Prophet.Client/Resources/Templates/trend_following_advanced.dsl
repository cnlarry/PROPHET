// 高级趋势跟踪策略 - 使用 Prophet DSL
// 结合Ichimoku、SuperTrend、ADX等高级指标
// 适用于中长期趋势行情

// ========== 多头入场：多指标共振 ==========
ALL {
    // Ichimoku云图看涨
    $(5m).Ichimoku().trend == BULLISH;
    KLINE(5m).close(0) > $(5m).Ichimoku().senkou_span_a;
    
    // SuperTrend确认上升趋势
    $(5m).Supertrend().trend == BULLISH;
    $(5m).Supertrend().islong == true;
    
    // ADX显示强趋势
    $(5m).ADX().value > 25;
    $(5m).ADX().trend == STRONG;
    
    // MACD金叉且在零轴上方
    $(5m).MACD().trend == BULLISH;
    $(5m).MACD().histogram > 0;
    
    // 成交量确认
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.2;
} = BUY;

// ========== 多头出场：趋势减弱信号 ==========
ANY {
    // SuperTrend转空
    $(5m).Supertrend().isshort == true;
    
    // 跌破Ichimoku云图
    KLINE(5m).close(0) < $(5m).Ichimoku().senkou_span_b;
    
    // ADX下降且低于20（趋势减弱）
    $(5m).ADX().value < 20;
    $(5m).ADX().trend == WEAK;
    
    // MACD死叉
    $(5m).MACD().crossover_type == DEATH_CROSS;
} = SELL;

// ========== 空头入场：多指标共振 ==========
ALL {
    // Ichimoku云图看跌
    $(5m).Ichimoku().trend == BEARISH;
    KLINE(5m).close(0) < $(5m).Ichimoku().senkou_span_b;
    
    // SuperTrend确认下降趋势
    $(5m).Supertrend().trend == BEARISH;
    $(5m).Supertrend().isshort == true;
    
    // ADX显示强趋势
    $(5m).ADX().value > 25;
    $(5m).ADX().trend == STRONG;
    
    // MACD死叉且在零轴下方
    $(5m).MACD().trend == BEARISH;
    $(5m).MACD().histogram < 0;
    
    // 成交量确认
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.2;
} = SELL;

// ========== 空头出场：趋势减弱信号 ==========
ANY {
    // SuperTrend转多
    $(5m).Supertrend().islong == true;
    
    // 突破Ichimoku云图
    KLINE(5m).close(0) > $(5m).Ichimoku().senkou_span_a;
    
    // ADX下降且低于20
    $(5m).ADX().value < 20;
    $(5m).ADX().trend == WEAK;
    
    // MACD金叉
    $(5m).MACD().crossover_type == GOLDEN_CROSS;
} = BUY;

