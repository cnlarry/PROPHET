// SMC综合策略 - 使用 Prophet DSL
// 完整的Smart Money概念策略
// 包含FVG、OrderBlock、BOS、Liquidity等

// ========== 看涨SMC入场：FVG + OrderBlock + BOS ==========
ALL {
    // 看涨FVG形成
    FVG(5m).bullish == true;
    FVG(5m).isfilled == false;
    FVG(5m).filled_pct < 30;
    
    // FVG大小足够（至少ATR的1.5倍）
    FVG(5m).size > $(5m).ATR().value * 1.5;
    
    // 价格回踩到FVG区域
    KLINE(5m).close(0) > FVG(5m).bottom;
    KLINE(5m).low(0) <= FVG(5m).mid;
    
    // OrderBlock确认（看涨订单块）
    ORDERBLOCK(5m).bullish == true;
    ORDERBLOCK(5m).strength > 0.6;
    
    // 市场结构突破（BOS）
    BOS(5m).bullish(5, 5) == true;
    
    // Swing结构支持
    SWING(5m).trend == BULLISH;
    SWING(5m).high().count >= 2;
    
    // 流动性扫荡
    LIQUIDITY(5m).buyside(0.5) == true;
    
    // RSI确认动量
    $(5m).RSI().value > 45;
    $(5m).RSI().value < 70;
    
    // 成交量配合
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.3;
} = BUY;

// ========== 看涨出场：FVG填补或结构破坏 ==========
ANY {
    // FVG完全填补
    FVG(5m).isfilled == true;
    
    // 市场结构反转（CHOCH）
    CHOCH(5m).bearish(5, 5) == true;
    
    // Breaker Block出现（支撑转阻力）
    BREAKER(5m).bearish() == true;
    
    // Premium区域过度延伸（价格在高位）
    PREMIUM(5m).premium(100) == true;
    
    // RSI超买
    $(5m).RSI().value > 75;
} = SELL;

// ========== 看跌SMC入场：FVG + OrderBlock + BOS ==========
ALL {
    // 看跌FVG形成
    FVG(5m).bearish == true;
    FVG(5m).isfilled == false;
    FVG(5m).filled_pct < 30;
    
    // FVG大小足够
    FVG(5m).size > $(5m).ATR().value * 1.5;
    
    // 价格反弹到FVG区域
    KLINE(5m).close(0) < FVG(5m).top;
    KLINE(5m).high(0) >= FVG(5m).mid;
    
    // OrderBlock确认（看跌订单块）
    ORDERBLOCK(5m).bearish == true;
    ORDERBLOCK(5m).strength > 0.6;
    
    // 市场结构突破（BOS）
    BOS(5m).bearish(5, 5) == true;
    
    // Swing结构支持
    SWING(5m).trend == BEARISH;
    SWING(5m).low().count >= 2;
    
    // 流动性扫荡
    LIQUIDITY(5m).sellside(0.5) == true;
    
    // RSI确认动量
    $(5m).RSI().value < 55;
    $(5m).RSI().value > 30;
    
    // 成交量配合
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.3;
} = SELL;

// ========== 看跌出场：FVG填补或结构破坏 ==========
ANY {
    // FVG完全填补
    FVG(5m).isfilled == true;
    
    // 市场结构反转（CHOCH）
    CHOCH(5m).bullish(5, 5) == true;
    
    // Breaker Block出现（阻力转支撑）
    BREAKER(5m).bullish() == true;
    
    // Discount区域过度延伸（价格在低位）
    PREMIUM(5m).discount(100) == true;
    
    // RSI超卖
    $(5m).RSI().value < 25;
} = BUY;

