// MACD+RSI动量策略 - 使用 Prophet DSL
// 双重动量确认，结合成交量过滤
// 适用于趋势启动阶段

// ========== 多头动量入场 ==========
ALL {
    // MACD金叉或柱状图扩大
    $(5m).MACD().histogram > 0;
    $(5m).MACD().histogram > $(5m).MACD().histogram(-1);
    $(5m).MACD().trend == BULLISH;
    
    // RSI进入多头区域
    $(5m).RSI().value > 50;
    $(5m).RSI().value < 75;
    $(5m).RSI().trend == BULLISH;
    
    // RSI相对前一根上升（动量增强）
    $(5m).RSI().value > $(5m).RSI().value(-1);
    
    // 价格在EMA上方
    KLINE(5m).close(0) > $(5m).EMA().value;
    
    // 成交量放大
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.5;
    
    // 连续上涨确认
    CONSECUTIVE(5m).close(3).rising() >= 2;
} = BUY;

// ========== 多头动量减弱出场 ==========
ANY {
    // MACD柱状图收缩
    $(5m).MACD().histogram < 0;
    
    // MACD死叉
    $(5m).MACD().crossover_type == DEATH_CROSS;
    
    // RSI背离（价格新高但RSI未新高）
    $(5m).RSI().divergence == BEARISH;
    
    // RSI进入超买区
    $(5m).RSI().value > 80;
    
    // 跌破EMA
    KLINE(5m).close(0) < $(5m).EMA().value;
} = SELL;

// ========== 空头动量入场 ==========
ALL {
    // MACD死叉或柱状图扩大
    $(5m).MACD().histogram < 0;
    $(5m).MACD().histogram < $(5m).MACD().histogram(-1);
    $(5m).MACD().trend == BEARISH;
    
    // RSI进入空头区域
    $(5m).RSI().value < 50;
    $(5m).RSI().value > 25;
    $(5m).RSI().trend == BEARISH;
    
    // RSI相对前一根下降（动量增强）
    $(5m).RSI().value < $(5m).RSI().value(-1);
    
    // 价格在EMA下方
    KLINE(5m).close(0) < $(5m).EMA().value;
    
    // 成交量放大
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.5;
    
    // 连续下跌确认
    CONSECUTIVE(5m).close(3).falling() >= 2;
} = SELL;

// ========== 空头动量减弱出场 ==========
ANY {
    // MACD柱状图收缩
    $(5m).MACD().histogram > 0;
    
    // MACD金叉
    $(5m).MACD().crossover_type == GOLDEN_CROSS;
    
    // RSI背离（价格新低但RSI未新低）
    $(5m).RSI().divergence == BULLISH;
    
    // RSI进入超卖区
    $(5m).RSI().value < 20;
    
    // 突破EMA
    KLINE(5m).close(0) > $(5m).EMA().value;
} = BUY;

