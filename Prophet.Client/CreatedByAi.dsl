// ============================================
// EMA趋势跟踪策略
// 版本：1.0
// 作者：Prophet量化策略专家
// 描述：基于EMA双均线交叉的趋势跟踪策略
// ============================================

// 自定义函数：计算趋势强度评分
calcTrendScore(ema_fast: Double, ema_slow: Double, adx_value: Double): Double {
    @score: Double = 0.0;
    
    // EMA快慢线关系评分
    if (ema_fast > ema_slow) {
        @score += 40.0;  // 快线在慢线上方，看涨
    } else {
        @score -= 40.0;  // 快线在慢线下方，看跌
    }
    
    // ADX趋势强度评分
    if (adx_value > 25) {
        @score += 30.0;  // 趋势较强
    } else if (adx_value < 20) {
        @score -= 20.0;  // 趋势较弱，可能震荡
    }
    
    // EMA斜率评分
    @ema_fast_slope: Double = $(5m).EMA(12).slope;
    if (@ema_fast_slope > 0.05) {
        @score += 15.0;  // 快线斜率向上
    } else if (@ema_fast_slope < -0.05) {
        @score -= 15.0;  // 快线斜率向下
    }
    
    
    return @score;
}

// 自定义函数：计算动态止损价格
calcStopLoss(entry_price: Double, atr_value: Double, is_long: Boolean): Double {
    @stop_loss: Double = 0.0;
    
    if (is_long) {
        // 多头止损：入场价 - 2倍ATR
        @stop_loss = entry_price - (atr_value * 2.0);
    } else {
        // 空头止损：入场价 + 2倍ATR
        @stop_loss = entry_price + (atr_value * 2.0);
    }
    
    return @stop_loss;
}

// 自定义函数：计算动态止盈价格
calcTakeProfit(entry_price: Double, atr_value: Double, is_long: Boolean): Double {
    @take_profit: Double = 0.0;
    
    if (is_long) {
        // 多头止盈：入场价 + 3倍ATR（风险回报比1:1.5）
        @take_profit = entry_price + (atr_value * 3.0);
    } else {
        // 空头止盈：入场价 - 3倍ATR
        @take_profit = entry_price - (atr_value * 3.0);
    }
    
    return @take_profit;
}


@CURRENT_PRICE:Double = CURRENT().price;

// ============================================
// 主要交易信号规则
// ============================================

// 规则1：EMA金叉买入信号（多时间框架共振）
ALL{
    // 5分钟时间框架：EMA(12)上穿EMA(26)
    $(5m).EMA(12).value > $(5m).EMA(26).value;
    $(5m).EMA(12).value(-1) <= $(5m).EMA(26).value(-1);  // 前一根K线还未上穿
    
    // 1小时时间框架确认：EMA趋势一致
    $(1h).EMA(12).value > $(1h).EMA(26).value;
    
    // 趋势强度确认：ADX > 20，避免震荡市
    $(5m).ADX(14).value > 20;
    
    // 成交量确认：OBV趋势向上
    $(5m).OBV().trend == BULLISH;
    
    // 价格位置：当前价格在EMA快线上方
    @CURRENT_PRICE > $(5m).EMA(12).value;
} = BUY;

// 规则2：EMA死叉卖出信号（多时间框架共振）
ALL{
    // 5分钟时间框架：EMA(12)下穿EMA(26)
    $(5m).EMA(12).value < $(5m).EMA(26).value;
    $(5m).EMA(12).value(-1) >= $(5m).EMA(26).value(-1);  // 前一根K线还未下穿
    
    // 1小时时间框架确认：EMA趋势一致
    $(1h).EMA(12).value < $(1h).EMA(26).value;
    
    // 趋势强度确认：ADX > 20，避免震荡市
    $(5m).ADX(14).value > 20;
    
    // 成交量确认：OBV趋势向下
    $(5m).OBV().trend == BEARISH;
    
    // 价格位置：当前价格在EMA快线下方
    @CURRENT_PRICE < $(5m).EMA(12).value;
} = SELL;

// 规则3：趋势反转预警信号（使用加权评分）
WEIGHTED(0.6){
    // EMA趋势评分（权重40%）
    WEIGHT(calcTrendScore($(5m).EMA(12).value, $(5m).EMA(26).value, $(5m).ADX(14).value) > 50) = 0.4;
    
    // 多时间框架一致性（权重30%）
    WEIGHT($(5m).EMA(12).value > $(5m).EMA(26).value AND $(1h).EMA(12).value > $(1h).EMA(26).value) = 0.3;
    
    // 成交量配合（权重20%）
    WEIGHT($(5m).OBV().trend == BULLISH) = 0.2;
    
    // 波动率适中（权重10%）
    WEIGHT($(5m).ATR(14).value BETWEEN (@CURRENT_PRICE * 0.005, @CURRENT_PRICE * 0.02)) = 0.1;
} = BUY;

WEIGHTED(0.6){
    // EMA趋势评分（权重40%）
    WEIGHT(calcTrendScore($(5m).EMA(12).value, $(5m).EMA(26).value, $(5m).ADX(14).value) < -50) = 0.4;
    
    // 多时间框架一致性（权重30%）
    WEIGHT($(5m).EMA(12).value < $(5m).EMA(26).value AND $(1h).EMA(12).value < $(1h).EMA(26).value) = 0.3;
    
    // 成交量配合（权重20%）
    WEIGHT($(5m).OBV().trend == BEARISH) = 0.2;
    
    // 波动率适中（权重10%）
    WEIGHT($(5m).ATR(14).value BETWEEN (@CURRENT_PRICE * 0.005, @CURRENT_PRICE * 0.02)) = 0.1;
} = SELL;

// 规则4：趋势减弱平仓信号
ANY{
    // 情况1：ADX趋势强度大幅减弱（从强趋势转为弱趋势）
    $(5m).ADX(14).value < 20;
    $(5m).ADX(14).value(-5) > 25;  // 5根K线前还是强趋势
    
    // 情况2：价格大幅偏离均线（可能回调）
    ABS(@CURRENT_PRICE - $(5m).EMA(12).value) > $(5m).ATR(14).value * 2.5;
    
    // 情况3：EMA快线斜率趋平（趋势动能减弱）
    ABS($(5m).EMA(12).slope) < 0.02;
} = HOLD;

// 规则5：异常波动过滤（避免在极端行情中交易）
NONE{
    // 避免在异常高波动率时交易
    $(5m).ATR(14).value > @CURRENT_PRICE * 0.03;
    
    // 避免在价格跳空时交易
    ABS(KLINE(5m).open(0) - KLINE(5m).close(-1)) > $(5m).ATR(14).value * 1.5;
    
    // 避免在成交量异常时交易
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 3.0;
} = BUY;

NONE{
    // 避免在异常高波动率时交易
    $(5m).ATR(14).value > @CURRENT_PRICE * 0.03;
    
    // 避免在价格跳空时交易
    ABS(KLINE(5m).open(0) - KLINE(5m).close(-1)) > $(5m).ATR(14).value * 1.5;
    
    // 避免在成交量异常时交易
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 3.0;
} = SELL;