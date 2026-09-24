// 成交量突破策略 - 使用 Prophet DSL
// 基于异常成交量的突破策略
// 使用OBV、成交量分析

// ========== 放量向上突破 ==========
ALL {
    // 价格突破关键阻力位
    KLINE(5m).close(0) > HIGHEST(5m).high(20);
    
    // 成交量异常放大（至少2倍均量）
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 2.0;
    KLINE(5m).volume(0) > KLINE(5m).volume(-1) * 1.5;
    
    // OBV上升确认买盘增加
    $(5m).OBV().trend == BULLISH;
    $(5m).OBV().value > $(5m).OBV().value(-1);
    
    // MFI显示资金流入
    $(5m).MFI().value > 50;
    $(5m).MFI().value < 80;
    $(5m).MFI().trend == BULLISH;
    
    // 价格涨幅确认
    CHANGE(5m).close(1).pct > 0.008;  // 单根K线涨幅>0.8%
    
    // RSI配合
    $(5m).RSI().value > 55;
    $(5m).RSI().value < 75;
    
    // VWAP之上
    KLINE(5m).close(0) > VWAP(5m).value;
    
    // 连续上涨
    CONSECUTIVE(5m).close(3).rising() >= 2;
} = BUY;

// ========== 成交量萎缩或背离 ==========
ANY {
    // 成交量萎缩（低于平均）
    KLINE(5m).volume(0) < AVERAGE(5m).volume(10) * 0.8;
    
    // OBV背离（价格新高但OBV未新高）
    $(5m).OBV().divergence == BEARISH;
    
    // MFI超买或资金流出
    $(5m).MFI().value > 80;
    $(5m).MFI().trend == BEARISH;
    
    // 跌破VWAP
    KLINE(5m).close(0) < VWAP(5m).value;
    
    // 价格回落到突破点下方
    KLINE(5m).close(0) < HIGHEST(5m).high(20) * 0.995;
} = SELL;

// ========== 放量向下突破 ==========
ALL {
    // 价格跌破关键支撑位
    KLINE(5m).close(0) < LOWEST(5m).low(20);
    
    // 成交量异常放大
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 2.0;
    KLINE(5m).volume(0) > KLINE(5m).volume(-1) * 1.5;
    
    // OBV下降确认卖盘增加
    $(5m).OBV().trend == BEARISH;
    $(5m).OBV().value < $(5m).OBV().value(-1);
    
    // MFI显示资金流出
    $(5m).MFI().value < 50;
    $(5m).MFI().value > 20;
    $(5m).MFI().trend == BEARISH;
    
    // 价格跌幅确认
    CHANGE(5m).close(1).pct < -0.008;  // 单根K线跌幅>0.8%
    
    // RSI配合
    $(5m).RSI().value < 45;
    $(5m).RSI().value > 25;
    
    // VWAP之下
    KLINE(5m).close(0) < VWAP(5m).value;
    
    // 连续下跌
    CONSECUTIVE(5m).close(3).falling() >= 2;
} = SELL;

// ========== 成交量萎缩或背离 ==========
ANY {
    // 成交量萎缩
    KLINE(5m).volume(0) < AVERAGE(5m).volume(10) * 0.8;
    
    // OBV背离（价格新低但OBV未新低）
    $(5m).OBV().divergence == BULLISH;
    
    // MFI超卖或资金流入
    $(5m).MFI().value < 20;
    $(5m).MFI().trend == BULLISH;
    
    // 突破VWAP
    KLINE(5m).close(0) > VWAP(5m).value;
    
    // 价格反弹到突破点上方
    KLINE(5m).close(0) > LOWEST(5m).low(20) * 1.005;
} = BUY;
