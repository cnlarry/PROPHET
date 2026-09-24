// 多周期趋势共振策略 - 使用 Prophet DSL
// 使用5分钟、15分钟、1小时三个周期确认
// 多周期共振提高成功率

// ========== 三周期共振看涨 ==========
ALL {
    // 1小时级别：大周期趋势向上
    $(1h).EMA().trend == BULLISH;
    KLINE(1h).close(0) > $(1h).EMA().value;
    $(1h).MACD().trend == BULLISH;
    
    // 15分钟级别：中周期确认
    $(15m).EMA().trend == BULLISH;
    KLINE(15m).close(0) > $(15m).EMA().value;
    $(15m).MACD().histogram > 0;
    
    // 5分钟级别：入场时机
    $(5m).MACD().trend == BULLISH;
    $(5m).MACD().crossover_type == GOLDEN_CROSS;
    $(5m).RSI().value > 50;
    $(5m).RSI().value < 70;
    
    // 三个周期的EMA对齐（多头排列）
    $(5m).EMA().value > $(15m).EMA().value;
    $(15m).EMA().value > $(1h).EMA().value;
    
    // 5分钟ADX确认趋势
    $(5m).ADX().value > 20;
    
    // 成交量配合
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.3;
} = BUY;

// ========== 任一周期趋势破坏 ==========
ANY {
    // 1小时级别趋势反转
    $(1h).MACD().trend == BEARISH;
    KLINE(1h).close(0) < $(1h).EMA().value;
    
    // 15分钟级别趋势反转
    $(15m).MACD().histogram < 0;
    KLINE(15m).close(0) < $(15m).EMA().value;
    
    // 5分钟级别MACD死叉
    $(5m).MACD().crossover_type == DEATH_CROSS;
    
    // 5分钟RSI超买
    $(5m).RSI().value > 75;
} = SELL;

// ========== 三周期共振看跌 ==========
ALL {
    // 1小时级别：大周期趋势向下
    $(1h).EMA().trend == BEARISH;
    KLINE(1h).close(0) < $(1h).EMA().value;
    $(1h).MACD().trend == BEARISH;
    
    // 15分钟级别：中周期确认
    $(15m).EMA().trend == BEARISH;
    KLINE(15m).close(0) < $(15m).EMA().value;
    $(15m).MACD().histogram < 0;
    
    // 5分钟级别：入场时机
    $(5m).MACD().trend == BEARISH;
    $(5m).MACD().crossover_type == DEATH_CROSS;
    $(5m).RSI().value < 50;
    $(5m).RSI().value > 30;
    
    // 三个周期的EMA对齐（空头排列）
    $(5m).EMA().value < $(15m).EMA().value;
    $(15m).EMA().value < $(1h).EMA().value;
    
    // 5分钟ADX确认趋势
    $(5m).ADX().value > 20;
    
    // 成交量配合
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.3;
} = SELL;

// ========== 任一周期趋势破坏 ==========
ANY {
    // 1小时级别趋势反转
    $(1h).MACD().trend == BULLISH;
    KLINE(1h).close(0) > $(1h).EMA().value;
    
    // 15分钟级别趋势反转
    $(15m).MACD().histogram > 0;
    KLINE(15m).close(0) > $(15m).EMA().value;
    
    // 5分钟级别MACD金叉
    $(5m).MACD().crossover_type == GOLDEN_CROSS;
    
    // 5分钟RSI超卖
    $(5m).RSI().value < 25;
} = BUY;

