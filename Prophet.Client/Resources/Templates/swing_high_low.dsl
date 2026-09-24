// 摆动高低点突破策略 - 使用 Prophet DSL
// 基于市场结构的突破策略
// 关注Swing High/Low和BOS

// ========== 看涨结构突破 ==========
ALL {
    // Swing趋势向上
    SWING(15m).trend == BULLISH;
    
    // 突破前期摆动高点
    SWING(15m).high().count >= 2;
    KLINE(5m).close(0) > SWING(15m).high().value(1);
    
    // BOS确认（突破市场结构）
    BOS(5m).bullish(5, 5) == true;
    
    // 价格创新高
    KLINE(5m).close(0) > HIGHEST(5m).high(20);
    
    // 高低点逐步抬高
    SWING(5m).low().value(0) > SWING(5m).low().value(1);
    
    // RSI确认强势
    $(5m).RSI().value > 50;
    $(5m).RSI().trend == BULLISH;
    
    // 成交量放大确认
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.5;
    
    // 连续上涨
    CONSECUTIVE(5m).close(3).rising() >= 2;
} = BUY;

// ========== 结构破坏出场 ==========
ANY {
    // CHOCH出现（结构改变）
    CHOCH(5m).bearish(5, 5) == true;
    
    // 跌破前期摆动低点
    KLINE(5m).close(0) < SWING(15m).low().value(0);
    
    // Swing趋势转弱
    SWING(15m).trend == BEARISH;
    
    // BOS反向信号
    BOS(5m).bearish(5, 5) == true;
    
    // 跌破关键EMA
    KLINE(5m).close(0) < $(5m).EMA().value;
} = SELL;

// ========== 看跌结构突破 ==========
ALL {
    // Swing趋势向下
    SWING(15m).trend == BEARISH;
    
    // 跌破前期摆动低点
    SWING(15m).low().count >= 2;
    KLINE(5m).close(0) < SWING(15m).low().value(1);
    
    // BOS确认
    BOS(5m).bearish(5, 5) == true;
    
    // 价格创新低
    KLINE(5m).close(0) < LOWEST(5m).low(20);
    
    // 高低点逐步降低
    SWING(5m).high().value(0) < SWING(5m).high().value(1);
    
    // RSI确认弱势
    $(5m).RSI().value < 50;
    $(5m).RSI().trend == BEARISH;
    
    // 成交量放大确认
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.5;
    
    // 连续下跌
    CONSECUTIVE(5m).close(3).falling() >= 2;
} = SELL;

// ========== 结构破坏出场 ==========
ANY {
    // CHOCH出现
    CHOCH(5m).bullish(5, 5) == true;
    
    // 突破前期摆动高点
    KLINE(5m).close(0) > SWING(15m).high().value(0);
    
    // Swing趋势转强
    SWING(15m).trend == BULLISH;
    
    // BOS反向信号
    BOS(5m).bullish(5, 5) == true;
    
    // 突破关键EMA
    KLINE(5m).close(0) > $(5m).EMA().value;
} = BUY;
