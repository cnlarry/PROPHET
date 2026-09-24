# Prophet DSL 常见错误避免指南

## 必读！避免生成错误代码

### 错误1：使用不存在的信号类型

- ❌ 错误：`ENTRY_LONG`, `EXIT_SHORT`, `LONG`, `SHORT`
- ✅ 正确：只有 `BUY`（做多）、`SELL`（做空）、`HOLD`（等待）三种

### 错误2：使用逗号分隔条件

- ❌ 错误：`ALL{ condition1, condition2 } = BUY;`
- ✅ 正确：`ALL{ condition1; condition2; } = BUY;` - 必须使用分号

### 错误3：使用不存在的枚举值

- ❌ 错误：`$(5m).OBV().trend == RISING` - OBV的trend字段没有RISING值
- ✅ 正确：`$(5m).OBV().trend == BULLISH` - 只有BULLISH、BEARISH、NEUTRAL
- **重要**：大部分指标的trend字段值为 BULLISH/BEARISH/NEUTRAL，不是RISING/FALLING
- **例外**：只有TSI指标的trend字段值为STRONG_BULLISH/BULLISH/BEARISH/STRONG_BEARISH（没有NEUTRAL）；ElderRay指标的trend字段值为STRONG_BULLISH/BULLISH/NEUTRAL/BEARISH/STRONG_BEARISH；还有FEARGREED()和FUNDINGRATE()的trend()方法返回RISING/FALLING/STABLE

### 错误4：枚举值使用双引号

- ❌ 错误：`$(5m).ATR(14).volatility == "HIGH"` - 枚举值不是字符串，不要加引号
- ✅ 正确：`$(5m).ATR(14).volatility == HIGH` - 直接使用枚举常量
- **原因**：volatility、trend、crossover_type等字段都是枚举类型，不是String类型

### 错误5：混淆指标和数据函数的访问语法

- ❌ 错误：`$(5m).KLINE().volume` - KLINE是数据函数，不是指标
- ✅ 正确：`KLINE(5m).volume` - 数据函数直接使用函数名，不需要$前缀
- **规则**：
  - **指标访问**需要 `$(tf)` 前缀：`$(5m).RSI(14).value`、`$(5m).MACD().trend`
  - **数据函数访问**直接使用函数名：`KLINE(5m).close`、`HIGHEST(5m).high(20)`

### 错误6：在信号函数内定义变量

- ❌ 错误：
  ```dsl
  ALL{
    @current_price: Double = CURRENT_PRICE;  // ❌ 不能在信号函数内定义变量
    @current_price > $(5m).EMA().value;
  } = BUY;
  ```
- ✅ 正确：
  ```dsl
  ALL{
    CURRENT_PRICE > $(5m).EMA().value;  // ✅ 直接使用环境变量或表达式
  } = BUY;
  ```
- **规则**：变量只能在自定义函数内部或策略顶层定义，信号函数内只能是条件表达式

### 错误7：自定义函数参数使用枚举类型名称

- ❌ 错误：`calcScore(trend: TrendType): Double { ... }` - 参数不能用TrendType
- ✅ 正确：`calcScore(trend: String): Double { ... }` - 使用String类型接收枚举值
- **原因**：自定义函数参数类型只能是基础类型（Integer、Double、Boolean、String）

### 错误8：自定义函数中使用 var、let 关键字

- ❌ 错误：`var score = 0.0;` 或 `let score = 0.0;`
- ✅ 正确：`@score: Double = 0.0;` 或 `@score = 0.0;`
- **原因**：Prophet DSL不是JavaScript，所有用户变量必须使用 @ 前缀

### 错误9：使用不存在的函数

- ❌ 错误：`CROSSOVER(line1, line2)`, `CROSSUNDER(line1, line2)` - 这些函数不存在
- ✅ 正确：使用指标的 `crossover_type` 字段：`$(5m).MACD().crossover_type == GOLDEN_CROSS`

### 错误10：使用不支持的逻辑运算符符号

- ❌ 错误：`if (volatility == "HIGH" || volatility == "VERY_HIGH")` - 不支持 || 符号
- ✅ 正确：`if (volatility == HIGH OR volatility == VERY_HIGH)` - 使用 OR 关键字
- ❌ 错误：`condition1 && condition2` - 不支持 && 符号
- ✅ 正确：`condition1 AND condition2` - 使用 AND 关键字
- **重要**：Prophet DSL 只支持关键字形式的逻辑运算符（OR/AND/NOT），不支持符号形式（||/&&/!）

### 错误11：在条件块内进行参数赋值

- ❌ 错误：在ALL/ANY等信号函数内进行参数赋值
- ✅ 正确：参数赋值必须在策略顶层（规则外部）

## 正确示例汇总

```dsl
// ✅ 自定义函数参数类型使用基础类型
calcTrendScore(ema_slope: Double, adx_value: Double, macd_trend: String): Double {
  @score: Double = 0.0;  // ✅ 变量使用@前缀
  
  if (ema_slope > 0.05) {
    @score += 30.0;
  }
  
  // ✅ 使用 OR 关键字，不是 || 符号
  if (macd_trend == "BULLISH" OR macd_trend == "NEUTRAL") {
    @score += 25.0;
  }
  
  return @score;
}

// ✅ 信号函数：只有条件表达式，不定义变量
ALL{
  calcTrendScore($(5m).EMA(20).slope, $(5m).ADX(14).value, "BULLISH") > 50.0;
  $(5m).OBV().trend == BULLISH;  // ✅ BULLISH而不是RISING
  CURRENT_PRICE > $(5m).EMA(20).value;  // ✅ 直接使用环境变量
} = BUY;

// ✅ 使用逻辑运算符的正确方式
ALL{
  $(5m).RSI(14).value < 30 OR $(5m).RSI(14).value > 70;  // ✅ 使用 OR
  $(5m).MACD().trend == BULLISH AND $(1h).MACD().trend == BULLISH;  // ✅ 使用 AND
} = BUY;
```

