# Prophet 情报模块 - INTELLIGENCE 函数完整设计

**文档版本**：2.0  
**创建日期**：2026-01-01  
**最后更新**：2026-01-01  
**设计原则**：入门简单、进阶灵活、高级强大  
**参考规范**：Prophet DSL 规范 v2.0  
**重大更新**：v2.0 采用链式调用语法，删除symbol参数，简化category命名

---

## 📚 目录

1. [设计理念](#1-设计理念)
2. [函数层次体系](#2-函数层次体系)
3. [Level 1: 入门级函数](#3-level-1-入门级函数)
4. [Level 2: 进阶级函数](#4-level-2-进阶级函数)
5. [Level 3: 高级函数](#5-level-3-高级函数)
6. [完整示例](#6-完整示例)
7. [最佳实践](#7-最佳实践)
8. [与现有DSL的集成](#8-与现有dsl的集成)

---

## 1. 设计理念

### 1.1 参考现有DSL模式

通过学习 `CURRENT().longshort`、`$(5m).MA()`、`KLINE(5m).close()` 等函数的设计，我们总结出Prophet DSL的核心设计模式：

**模式1: 命名空间式访问**
```javascript
CURRENT().longshort.ratio()     // 时间序列数据
CURRENT().feargreed.value       // 外部数据源
CURRENT().fundingrate           // 资金费率
```

**模式2: 链式调用**
```javascript
$(5m).MA(20).value              // 指标 → 参数 → 字段
KLINE(5m).close(0)              // 数据源 → 字段 → 偏移
```

**模式3: 简化默认值**
```javascript
KLINE(5m).close()               // 省略offset，默认0
CURRENT().longshort.ratio()     // 省略offset，默认0
```

**模式4: 统计与分析方法**
```javascript
CURRENT().longshort.trend(7)    // 趋势分析
CURRENT().longshort.extreme(2.0) // 极端检测
```

---

### 1.2 INTELLIGENCE 函数设计原则

基于以上观察，我们设计 INTELLIGENCE 函数遵循以下原则：

✅ **渐进式学习曲线**
- Level 1：新手只需3个函数即可上手（`strength`, `critical`, `score`）
- Level 2：熟练后可使用筛选、统计、趋势分析
- Level 3：高手可使用组合评分、证据链、多维分析

✅ **一致性**
- 遵循 `CURRENT()` 命名空间模式
- 支持 `offset` 参数（可选，默认0）
- 支持 `lookback` 时间范围参数（可选，默认值）
- 返回值类型明确（数值、布尔、枚举）

✅ **灵活性**
- 支持按类别访问：`INTELLIGENCE().funding.strength()`
- 支持全局查询：`INTELLIGENCE().score()`
- 支持多类别分析：`INTELLIGENCE().consistency()`

✅ **性能优化**
- 内部缓存机制（类似现有DSL）
- 内存查询，无需访问数据库
- 避免重复计算

---

## 2. 函数层次体系

```
INTELLIGENCE 函数体系
│
├── Level 1: 入门级（3个核心函数，覆盖90%使用场景）
│   ├── {category}.strength()  - 获取指定类别的信号强度（最常用）
│   ├── critical()              - 检查紧急信号（风控开关）
│   └── score()                 - 综合评分（加权所有类别）
│
├── Level 2: 进阶级（筛选、统计、趋势，覆盖95%使用场景）
│   ├── {category}.direction()    - 信号方向
│   ├── {category}.confidence()   - 置信度
│   ├── {category}.count()        - 单类别信号数量
│   ├── {category}.avgstrength()  - 平均强度
│   ├── {category}.trend()        - 趋势判断
│   ├── {category}.exists()       - 存在性检查
│   └── count()                   - 所有类别信号总数
│
└── Level 3: 高级（组合、证据链、多维分析，专家使用）
    ├── consistency()           - 多类别一致性
    ├── conflicting()           - 冲突检测
    ├── strongest()             - 最强信号类别
    ├── {category}.evidence()   - 证据链查询
    ├── composite()             - 自定义加权评分
    └── matrix()                - 多维信号矩阵
```

**支持的category（Phase 0）**：
- `funding` - 资金费率情报（原 funding_extreme）
- `liquidation` - 清算情报（原 liquidation_cascade）
- `longshort` - 多空比情报（原 longshort_extreme）

**支持的category（Phase 1，未来扩展）**：
- `news` - 新闻情报（原 news_event）
- `whale` - 巨鲸情报（原 whale_movement）

---

## 3. Level 1: 入门级函数

### 3.1 设计目标

✅ **极简API**：3个函数解决90%的使用场景  
✅ **零学习成本**：函数名自解释，不看文档也能猜到用法  
✅ **开箱即用**：提供策略模板，复制粘贴即可运行

---

### 3.2 函数1: `strength()` - 信号强度

**用途**：获取指定类别的信号强度（最常用）

**语法**：
```javascript
// 形式1：指定类别（推荐）
INTELLIGENCE().{category}.strength([lookback_minutes])

// 示例
INTELLIGENCE().funding.strength(60)
INTELLIGENCE().liquidation.strength(60)
INTELLIGENCE().longshort.strength(60)
```

**参数**：
- `category`: 信号类别（通过属性访问）
  - Phase 0: `funding`, `liquidation`, `longshort`
  - Phase 1: `news`, `whale` 等
- `lookback_minutes`: 回溯分钟数（整数，可选，默认60）

**返回值**：
- `-100` ~ `+100`：信号强度（负数=看跌，正数=看涨）
- `0`：该时间范围内无信号或信号已过期

**回测保证**：自动使用 `first_queryable_at <= currentBarTime` 查询，避免未来函数

**示例**：

```javascript
// ========================================
// 示例1：最简单用法（使用默认值）
// ========================================
strength = INTELLIGENCE().funding.strength()

ALL {
    INTELLIGENCE().funding.strength() > 50,
    $(5m).MACD().trend = BULLISH
} = BUY;

// ========================================
// 示例2：自定义时间范围
// ========================================
ALL {
    // 最近30分钟的资金费率信号强度 > 70
    INTELLIGENCE().funding.strength(30) > 70,
    
    // 最近1小时的清算信号强度 < -60
    INTELLIGENCE().liquidation.strength(60) < -60,
    
    $(5m).RSI().value > 50
} = BUY;

// ========================================
// 示例3：保存到变量（避免重复计算）
// ========================================
@funding_str: Double = INTELLIGENCE().funding.strength(60);
@liq_str: Double = INTELLIGENCE().liquidation.strength(60);

ALL {
    @funding_str > 50,
    @liq_str < -30,
    $(5m).MACD().histogram > 0
} = BUY;
```

---

### 3.3 函数2: `critical()` - 紧急信号检查

**用途**：检查是否有紧急信号（风控开关）

**语法**：
```javascript
INTELLIGENCE().critical([lookback_minutes])
```

**参数**：
- `lookback_minutes`: 回溯分钟数（整数，可选，默认15）

**返回值**：
- `true`: 存在 priority>=8 的紧急信号
- `false`: 无紧急信号

**典型用法**：作为策略的风控开关

**示例**：

```javascript
// ========================================
// 示例1：风控开关（最常用）
// ========================================
ALL {
    // 技术面入场条件
    $(5m).MACD().crossover_type = GOLDEN_CROSS,
    $(5m).RSI().value > 50,
    
    // 🎯 情报风控：没有紧急看跌信号
    NOT(INTELLIGENCE().critical(30))
} = BUY;

// ========================================
// 示例2：紧急出场
// ========================================
ANY {
    // 技术面止损
    KLINE(5m).close(0) < @stop_loss,
    
    // 🎯 情报紧急出场：出现紧急信号立即平仓
    INTELLIGENCE().critical(15) = true
} = SELL;

// ========================================
// 示例3：分时段检查（短期vs长期）
// ========================================
@short_term_critical: Boolean = INTELLIGENCE().critical(15);
@long_term_critical: Boolean = INTELLIGENCE().critical(120);

// 短期紧急但长期还好 → 暂停交易
ALL {
    @short_term_critical = true,
    @long_term_critical = false
} = HOLD;

// 长期紧急 → 强制平仓
ALL {
    @long_term_critical = true
} = SELL;
```

---

### 3.4 函数3: `score()` - 综合评分

**用途**：综合评分（加权所有类别）

**语法**：
```javascript
INTELLIGENCE().score([lookback_minutes])
```

**参数**：
- `lookback_minutes`: 回溯分钟数（整数，可选，默认60）

**返回值**：
- `-100` ~ `+100`：综合评分
- 计算方式：所有类别的 strength 加权平均

**权重规则**（可配置）：
```
funding:      30%  (资金费率)
liquidation:  25%  (清算)
longshort:    20%  (多空比)
whale:        15%  (巨鲸, Phase 1)
news:         10%  (新闻, Phase 1)
```

**示例**：

```javascript
// ========================================
// 示例1：综合评分高度看涨
// ========================================
ALL {
    INTELLIGENCE().score(60) > 50,
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value < 70
} = BUY;

// ========================================
// 示例2：综合评分与技术面冲突时的处理
// ========================================
@intel_score: Double = INTELLIGENCE().score(30);

// 情报极度看跌 + 技术面看涨 → 暂停交易（情报优先）
ALL {
    @intel_score < -70,               // 极度看跌
    $(5m).MACD().trend = BULLISH      // 但技术面看涨
} = HOLD;  // 观望

// 情报和技术面一致 → 强力信号
ALL {
    @intel_score > 60,
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value > 50
} = BUY;

// ========================================
// 示例3：多时间范围综合评分
// ========================================
@score_short: Double = INTELLIGENCE().score(30);   // 短期
@score_long: Double = INTELLIGENCE().score(120);   // 长期

// 短期和长期都看涨 → 强烈买入
ALL {
    @score_short > 40,
    @score_long > 30,
    $(5m).MACD().histogram > 0
} = BUY;
```

---

### 3.5 入门级函数总结

✅ **3个函数，覆盖90%场景**：
1. `strength()` - 获取单个类别的信号强度（精确控制）
2. `critical()` - 检查紧急信号（风控开关）
3. `score()` - 综合评分（快速判断）

✅ **学习曲线平缓**：
- 新手：只用 `score()` 和 `critical()`（2个函数）
- 熟练：加上 `strength()` 进行精细控制（3个函数）
- 进阶：使用 Level 2 函数

✅ **策略模板**：
```javascript
// 模板1: 最简单策略（只用2个函数）
ALL {
    INTELLIGENCE().score(60) > 30,
    NOT(INTELLIGENCE().critical(15)),
    $(5m).MACD().trend = BULLISH
} = BUY;

// 模板2: 进阶策略（3个函数）
ALL {
    INTELLIGENCE().funding.strength(60) > 50,
    INTELLIGENCE().score(60) > 40,
    NOT(INTELLIGENCE().critical(30)),
    $(5m).RSI().value > 50
} = BUY;
```

---

## 4. Level 2: 进阶级函数

### 4.1 设计目标

✅ **更细粒度控制**：获取direction、confidence等详细信息  
✅ **统计分析**：count、avgstrength等统计函数  
✅ **趋势判断**：trend()、exists()等辅助判断

---

### 4.2 函数4: `direction()` - 信号方向

**用途**：获取信号方向

**语法**：
```javascript
INTELLIGENCE().{category}.direction([lookback_minutes])
```

**返回值**：
- `"BULLISH"`: 看涨
- `"BEARISH"`: 看跌
- `"NEUTRAL"`: 中性
- `null`: 无信号

**示例**：

```javascript
// 检查多个类别的方向一致性
ALL {
    INTELLIGENCE().funding.direction(60) = "BULLISH",
    INTELLIGENCE().liquidation.direction(60) = "BULLISH",
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 4.3 函数5: `confidence()` - 置信度

**用途**：获取信号置信度

**语法**：
```javascript
INTELLIGENCE().{category}.confidence([lookback_minutes])
```

**返回值**：
- `0` ~ `100`：置信度（百分比）
- `0`：无信号

**示例**：

```javascript
// 只在高置信度信号时交易
ALL {
    INTELLIGENCE().funding.strength(60) > 50,
    INTELLIGENCE().funding.confidence(60) >= 70,  // 置信度>=70%
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 4.4 函数6: `count()` - 信号数量

**用途**：统计指定时间范围内的信号数量

**语法**：
```javascript
// 形式1：单个类别
INTELLIGENCE().{category}.count([lookback_minutes])

// 形式2：所有类别
INTELLIGENCE().count([lookback_minutes])
```

**返回值**：
- `0` ~ `N`：信号数量

**示例**：

```javascript
// 信号密集度判断
ALL {
    // 最近1小时内有3个以上的资金费率信号
    INTELLIGENCE().funding.count(60) >= 3,
    
    // 综合评分也看涨
    INTELLIGENCE().score(60) > 40,
    
    $(5m).MACD().histogram > 0
} = BUY;  // 多次确认的信号更可靠
```

---

### 4.5 函数7: `avgstrength()` - 平均强度

**用途**：计算指定时间范围内所有信号的平均强度

**语法**：
```javascript
INTELLIGENCE().{category}.avgstrength([lookback_minutes])
```

**返回值**：
- `-100` ~ `+100`：平均强度
- `0`：无信号

**示例**：

```javascript
// 平均强度平滑波动
@avg_str: Double = INTELLIGENCE().funding.avgstrength(120);
@cur_str: Double = INTELLIGENCE().funding.strength(60);

// 当前强度显著高于平均水平
ALL {
    @cur_str > @avg_str + 20,
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 4.6 函数8: `trend()` - 趋势判断

**用途**：判断信号强度的趋势方向

**语法**：
```javascript
INTELLIGENCE().{category}.trend([lookback_minutes])
```

**返回值**：
- `"RISING"`: 信号强度上升（看涨情绪增强）
- `"FALLING"`: 信号强度下降（看跌情绪增强）
- `"STABLE"`: 稳定/横盘
- `null`: 数据不足

**判断逻辑**：对最近N分钟内的信号进行线性回归，分析斜率

**示例**：

```javascript
// 趋势与技术面共振
ALL {
    // 情报信号强度上升趋势
    INTELLIGENCE().funding.trend(60) = "RISING",
    
    // 当前强度已经较高
    INTELLIGENCE().funding.strength(60) > 50,
    
    // 技术面也看涨
    $(5m).MACD().trend = BULLISH
} = BUY;  // 趋势一致性强
```

---

### 4.7 函数9: `exists()` - 存在性检查

**用途**：检查指定条件的信号是否存在

**语法**：
```javascript
// 形式1：检查是否有任何信号
INTELLIGENCE().{category}.exists([lookback_minutes])

// 形式2：检查是否有特定方向的信号
INTELLIGENCE().{category}.exists([lookback_minutes], direction)
```

**参数**：
- `direction`: `"BULLISH"`, `"BEARISH"`, `"NEUTRAL"`（可选）

**返回值**：
- `true`: 存在
- `false`: 不存在

**示例**：

```javascript
// 确认信号存在
ALL {
    // 确认有看涨的资金费率信号
    INTELLIGENCE().funding.exists(60, "BULLISH") = true,
    
    // 强度也足够
    INTELLIGENCE().funding.strength(60) > 50,
    
    $(5m).MACD().trend = BULLISH
} = BUY;

// 避免在有冲突信号时交易
ALL {
    // 技术面看涨
    $(5m).MACD().crossover_type = GOLDEN_CROSS,
    
    // 但没有看跌的清算信号
    INTELLIGENCE().liquidation.exists(30, "BEARISH") = false
} = BUY;
```

---

### 4.8 进阶级函数总结

✅ **Level 2 新增6个函数**：
4. `direction()` - 信号方向（BULLISH/BEARISH/NEUTRAL）
5. `confidence()` - 置信度（0-100）
6. `count()` - 信号数量
7. `avgstrength()` - 平均强度
8. `trend()` - 趋势判断（RISING/FALLING/STABLE）
9. `exists()` - 存在性检查

✅ **适用场景**：
- 需要更细粒度控制时
- 需要统计分析时
- 需要趋势判断时
- 需要过滤特定信号时

✅ **进阶策略示例**：
```javascript
// 多维度确认策略
ALL {
    // Level 1: 基础评分
    INTELLIGENCE().score(60) > 40,
    
    // Level 2: 方向一致性
    INTELLIGENCE().funding.direction(60) = "BULLISH",
    INTELLIGENCE().liquidation.direction(60) = "BULLISH",
    
    // Level 2: 置信度过滤
    INTELLIGENCE().funding.confidence(60) >= 70,
    
    // Level 2: 趋势判断
    INTELLIGENCE().funding.trend(60) = "RISING",
    
    // 技术面
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

## 5. Level 3: 高级函数

### 5.1 设计目标

✅ **专家级功能**：多类别组合分析、证据链查询、自定义权重  
✅ **复杂场景**：处理冲突、一致性分析、多维矩阵  
✅ **灵活性**：支持自定义权重、自定义聚合规则

---

### 5.2 函数10: `consistency()` - 多类别一致性

**用途**：计算多个类别信号的一致性

**语法**：
```javascript
// 针对所有类别
INTELLIGENCE().consistency([lookback_minutes])
```

**返回值**：
- `0.0` ~ `1.0`：一致性评分
  - `1.0`：所有信号方向完全一致
  - `0.0`：信号方向完全矛盾
  - `0.5`：一半一致，一半矛盾

**计算方法**：
```
一致性 = (同向信号数 - 反向信号数) / 总信号数
```

**示例**：

```javascript
// 一致性过滤
ALL {
    // 多类别信号高度一致（>=80%）
    INTELLIGENCE().consistency(60) >= 0.8,
    
    // 综合评分也看涨
    INTELLIGENCE().score(60) > 50,
    
    $(5m).MACD().trend = BULLISH
} = BUY;  // 高一致性 = 高可信度
```

---

### 5.3 函数11: `conflicting()` - 冲突检测

**用途**：检测是否有冲突的信号

**语法**：
```javascript
INTELLIGENCE().conflicting([lookback_minutes], [threshold])
```

**参数**：
- `threshold`: 冲突阈值（可选，默认0.5）
  - 当一致性 < threshold 时，判定为冲突

**返回值**：
- `true`: 有冲突
- `false`: 无冲突

**示例**：

```javascript
// 避免在信号冲突时交易
ALL {
    // 技术面看涨
    $(5m).MACD().crossover_type = GOLDEN_CROSS,
    
    // 但情报信号不冲突
    INTELLIGENCE().conflicting(60, 0.5) = false,
    
    $(5m).RSI().value > 50
} = BUY;

// 冲突时暂停交易
ALL {
    INTELLIGENCE().conflicting(60, 0.5) = true
} = HOLD;  // 信号混乱，观望
```

---

### 5.4 函数12: `strongest()` - 最强信号类别

**用途**：返回强度绝对值最大的信号类别

**语法**：
```javascript
INTELLIGENCE().strongest([lookback_minutes])
```

**返回值**：
- 类别字符串（如 `"funding"`, `"liquidation"`, `"longshort"`）
- `null`: 无信号

**示例**：

```javascript
// 根据最强信号类别决策
@strongest_cat: String = INTELLIGENCE().strongest(60);

// 如果最强信号是资金费率，且强度>70
ALL {
    @strongest_cat = "funding",
    INTELLIGENCE().funding.strength(60) > 70,
    $(5m).MACD().trend = BULLISH
} = BUY;

// 如果最强信号是清算，策略不同
ALL {
    @strongest_cat = "liquidation",
    INTELLIGENCE().liquidation.strength(60) < -60,
    $(5m).RSI().value < 30
} = BUY;  // 清算反转策略
```

---

### 5.5 函数13: `evidence()` - 证据链查询

**用途**：获取信号的证据链详情（JSON格式）

**语法**：
```javascript
INTELLIGENCE().{category}.evidence([lookback_minutes])
```

**返回值**：
- JSON字符串（包含 `summary`、`reasoning`、`raw_data_ids` 等）
- `null`: 无信号

**示例**：

```javascript
// 获取证据链用于日志记录或分析
@evidence: String = INTELLIGENCE().funding.evidence(60);

// 在自定义函数中解析证据链
checkEvidence(evidence: String): Boolean {
    // 这里可以解析JSON，检查特定字段
    // 返回是否通过验证
    return true;
}

ALL {
    INTELLIGENCE().score(60) > 50,
    checkEvidence(@evidence) = true,
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 5.6 函数14: `composite()` - 自定义加权评分

**用途**：使用自定义权重计算综合评分

**语法**：
```javascript
INTELLIGENCE().composite(
    {
        "category1": weight1,
        "category2": weight2,
        ...
    },
    [lookback_minutes]
)
```

**参数**：
- 第一个参数：类别权重映射（JSON对象）
- `lookback_minutes`: 回溯分钟数（可选，默认60）

**返回值**：
- `-100` ~ `+100`：加权评分

**示例**：

```javascript
// 自定义权重（强调资金费率）
@custom_score: Double = INTELLIGENCE().composite(
    {
        "funding": 0.5,       // 50% 权重
        "liquidation": 0.3,   // 30% 权重
        "longshort": 0.2      // 20% 权重
    },
    60
);

ALL {
    @custom_score > 60,
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 5.7 函数15: `matrix()` - 多维信号矩阵

**用途**：获取多个类别 × 多个时间范围的信号矩阵

**语法**：
```javascript
INTELLIGENCE().matrix([time1, time2, ...])
```

**参数**：
- 时间范围列表（分钟）

**返回值**：
- 二维数组（JSON格式）：`[[strength11, strength12], [strength21, strength22], ...]`
- 行：类别（funding, liquidation, longshort）
- 列：时间范围

**示例**：

```javascript
// 获取信号矩阵
@matrix: String = INTELLIGENCE().matrix([30, 60, 120]);  // 30分钟、1小时、2小时

// 在自定义函数中分析矩阵
analyzeMatrix(matrix: String): Double {
    // 解析矩阵，计算自定义评分
    // 例如：短期强度 × 2 + 中期强度 × 1 + 长期强度 × 0.5
    return 75.0;  // 示例返回值
}

@matrix_score: Double = analyzeMatrix(@matrix);

ALL {
    @matrix_score > 60,
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 5.8 高级函数总结

✅ **Level 3 新增6个函数**：
10. `consistency()` - 多类别一致性（0-1）
11. `conflicting()` - 冲突检测（true/false）
12. `strongest()` - 最强信号类别（字符串）
13. `evidence()` - 证据链查询（JSON）
14. `composite()` - 自定义加权评分（-100~100）
15. `matrix()` - 多维信号矩阵（二维数组）

✅ **适用场景**：
- 复杂策略开发
- 多信号融合
- 自定义权重计算
- 证据链验证
- 多维分析

✅ **高级策略示例**：
```javascript
// 多维度融合策略
@consistency: Double = INTELLIGENCE("BTCUSDT").consistency(60);
@conflicting: Boolean = INTELLIGENCE("BTCUSDT").conflicting(60, 0.5);
@strongest: String = INTELLIGENCE("BTCUSDT").strongest(60);

ALL {
    // 一致性高
    @consistency >= 0.8,
    
    // 无冲突
    @conflicting = false,
    
    // 最强信号是资金费率
    @strongest = "funding_extreme",
    
    // 自定义权重评分
    INTELLIGENCE("BTCUSDT").composite({
        "funding_extreme": 0.5,
        "liquidation_cascade": 0.3,
        "longshort_extreme": 0.2
    }, 60) > 60,
    
    // 技术面
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

## 6. 完整示例

### 6.1 入门级策略（Level 1）

```javascript
// ============================================
// 策略1: 最简单的情报策略（2个函数）
// ============================================
ALL {
    // 综合评分看涨
    INTELLIGENCE().score(60) > 30,
    
    // 没有紧急风险信号
    NOT(INTELLIGENCE().critical(15)),
    
    // 技术面确认
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value > 50
} = BUY;

// 出场
ANY {
    INTELLIGENCE().score(30) < -50,
    INTELLIGENCE().critical(15) = true,
    $(5m).MACD().crossover_type = DEATH_CROSS
} = SELL;
```

---

### 6.2 进阶级策略（Level 1 + Level 2）

```javascript
// ============================================
// 策略2: 多维度确认策略
// ============================================
@funding_str: Double = INTELLIGENCE().funding.strength(60);
@liq_str: Double = INTELLIGENCE().liquidation.strength(60);
@score: Double = INTELLIGENCE().score(60);

ALL {
    // Level 1: 综合评分
    @score > 40,
    
    // Level 2: 单个类别方向一致
    INTELLIGENCE().funding.direction(60) = "BULLISH",
    INTELLIGENCE().liquidation.direction(60) = "BULLISH",
    
    // Level 2: 置信度过滤
    INTELLIGENCE().funding.confidence(60) >= 70,
    
    // Level 2: 趋势判断
    INTELLIGENCE().funding.trend(60) = "RISING",
    
    // Level 2: 信号数量确认
    INTELLIGENCE().count(60) >= 2,
    
    // Level 1: 风控
    NOT(INTELLIGENCE().critical(30)),
    
    // 技术面
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value BETWEEN(40, 70)
} = BUY;
```

---

### 6.3 高级策略（Level 1 + Level 2 + Level 3）

```javascript
// ============================================
// 策略3: 专家级多维融合策略
// ============================================

// Level 3: 一致性检查
@consistency: Double = INTELLIGENCE().consistency(60);

// Level 3: 冲突检测
@conflicting: Boolean = INTELLIGENCE().conflicting(60, 0.5);

// Level 3: 最强信号
@strongest: String = INTELLIGENCE().strongest(60);

// Level 3: 自定义权重评分
@custom_score: Double = INTELLIGENCE().composite({
    "funding": 0.4,
    "liquidation": 0.3,
    "longshort": 0.2,
    "whale": 0.1
}, 60);

// Level 2: 单类别详情
@funding_str: Double = INTELLIGENCE().funding.strength(60);
@funding_conf: Double = INTELLIGENCE().funding.confidence(60);
@funding_trend: String = INTELLIGENCE().funding.trend(60);

// 入场条件
ALL {
    // Level 3: 高一致性
    @consistency >= 0.8,
    
    // Level 3: 无冲突
    @conflicting = false,
    
    // Level 3: 自定义评分高
    @custom_score > 60,
    
    // Level 2: 最强信号的详细检查
    @strongest = "funding",
    @funding_str > 70,
    @funding_conf >= 75,
    @funding_trend = "RISING",
    
    // Level 1: 综合评分确认
    INTELLIGENCE().score(60) > 50,
    
    // Level 1: 风控
    NOT(INTELLIGENCE().critical(30)),
    
    // 技术面
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value BETWEEN(50, 70),
    $(5m).ADX().value > 25
} = BUY;

// 出场条件
ANY {
    // Level 3: 一致性崩溃
    @consistency < 0.3,
    
    // Level 3: 出现冲突
    @conflicting = true,
    
    // Level 1: 综合评分反转
    INTELLIGENCE().score(30) < -60,
    
    // Level 1: 紧急信号
    INTELLIGENCE().critical(15) = true,
    
    // 技术面止损
    $(5m).MACD().crossover_type = DEATH_CROSS
} = SELL;
```

---

### 6.4 分层策略（多时间框架）

```javascript
// ============================================
// 策略4: 多时间框架分层策略
// ============================================

// 短期（30分钟）
@score_short: Double = INTELLIGENCE().score(30);
@critical_short: Boolean = INTELLIGENCE().critical(15);

// 中期（1小时）
@score_mid: Double = INTELLIGENCE().score(60);
@consistency_mid: Double = INTELLIGENCE().consistency(60);

// 长期（2小时）
@score_long: Double = INTELLIGENCE().score(120);
@trend_long: String = INTELLIGENCE().funding.trend(120);

// 多层确认
ALL {
    // 短期：快速反应
    @score_short > 30,
    @critical_short = false,
    
    // 中期：稳定确认
    @score_mid > 40,
    @consistency_mid >= 0.7,
    
    // 长期：趋势一致
    @score_long > 20,
    @trend_long = "RISING",
    
    // 技术面（使用5分钟图，快速反应）
    $(5m).MACD().crossover_type = GOLDEN_CROSS,
    $(5m).RSI().value > 50
} = BUY;
```

---

## 7. 最佳实践

### 7.1 渐进式学习路径

✅ **第1周：入门级（Level 1）**
- 只使用3个函数：`strength()`, `critical()`, `score()`
- 先从简单策略开始，理解基本概念
- 建议使用策略模板

✅ **第2-3周：进阶级（Level 2）**
- 添加 `direction()`, `confidence()`, `count()`, `trend()`
- 学习统计分析和趋势判断
- 构建多维度确认策略

✅ **第4周+：高级（Level 3）**
- 使用 `consistency()`, `composite()`, `strongest()`
- 处理复杂场景和多信号融合
- 自定义权重和评分规则

---

### 7.2 性能优化建议

✅ **缓存变量，避免重复查询**：
```javascript
// ✅ 推荐：缓存到变量
@score: Double = INTELLIGENCE("BTCUSDT").score(60);
@critical: Boolean = INTELLIGENCE("BTCUSDT").critical(15);

ALL {
    @score > 40,
    @critical = false,
    $(5m).MACD().trend = BULLISH
} = BUY;

// ❌ 不推荐：重复查询
ALL {
    INTELLIGENCE("BTCUSDT").score(60) > 40,
    INTELLIGENCE("BTCUSDT").score(60) < 80,  // 重复查询
    NOT(INTELLIGENCE("BTCUSDT").critical(15)),
    $(5m).MACD().trend = BULLISH
} = BUY;
```

✅ **合理设置 lookback 参数**：
```javascript
// 短期信号（15-30分钟）
INTELLIGENCE("BTCUSDT").critical(15)

// 中期信号（30-60分钟）
INTELLIGENCE("BTCUSDT").score(60)

// 长期信号（1-2小时）
INTELLIGENCE("BTCUSDT").consistency(120)
```

---

### 7.3 异常处理

✅ **检查信号存在性**：
```javascript
// Level 2 函数提供存在性检查
ALL {
    // 确认有信号再使用
    INTELLIGENCE("funding_extreme", "BTCUSDT").exists(60) = true,
    
    INTELLIGENCE("funding_extreme", "BTCUSDT").strength(60) > 50,
    $(5m).MACD().trend = BULLISH
} = BUY;
```

✅ **默认值处理**：
- `strength()` 无信号时返回 `0`
- `score()` 无信号时返回 `0`
- `critical()` 无信号时返回 `false`
- 用户无需担心null/异常，可以直接使用

---

### 7.4 数据时间语义

✅ **回测正确性保证**：
- 所有 `INTELLIGENCE()` 函数自动使用 `first_queryable_at <= currentBarTime` 查询
- 用户无需担心未来函数问题
- `lookback_minutes` 是相对于当前Bar时间的回溯

✅ **实盘与回测一致**：
```javascript
// 这个策略在回测和实盘中行为完全一致
ALL {
    INTELLIGENCE("BTCUSDT").score(60) > 40,  // 查询最近60分钟的信号
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 7.5 调试技巧

✅ **打印中间变量（开发阶段）**：
```javascript
@score: Double = INTELLIGENCE("BTCUSDT").score(60);
@funding_str: Double = INTELLIGENCE("funding_extreme", "BTCUSDT").strength(60);
@critical: Boolean = INTELLIGENCE("BTCUSDT").critical(15);

// 在日志中可以看到这些变量的值
// Console.WriteLine() 会输出变量值

ALL {
    @score > 40,
    @critical = false,
    $(5m).MACD().trend = BULLISH
} = BUY;
```

✅ **分段测试**：
```javascript
// 先测试单个类别
ALL {
    INTELLIGENCE().funding.strength(60) > 50
} = BUY;

// 再测试组合
ALL {
    INTELLIGENCE().funding.strength(60) > 50,
    INTELLIGENCE().liquidation.strength(60) > 30
} = BUY;

// 最后加入技术面
ALL {
    INTELLIGENCE().score(60) > 40,
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

## 8. 与现有DSL的集成

### 8.1 与技术指标结合

```javascript
// 情报 + MACD
ALL {
    INTELLIGENCE().score(60) > 40,
    $(5m).MACD().trend = BULLISH,
    $(5m).MACD().histogram > 0
} = BUY;

// 情报 + RSI
ALL {
    INTELLIGENCE().score(60) > 30,
    $(5m).RSI().value BETWEEN(40, 70),
    $(5m).RSI().oversold = false
} = BUY;

// 情报 + 布林带
ALL {
    INTELLIGENCE().score(60) > 40,
    KLINE(5m).close(0) > $(5m).BOLL().lower,
    KLINE(5m).close(0) < $(5m).BOLL().upper
} = BUY;
```

---

### 8.2 与时间序列函数结合

```javascript
// 情报 + 多空比
ALL {
    INTELLIGENCE().score(60) > 40,
    CURRENT().longshort.ratio() > 1.2,
    CURRENT().longshort.trend(7) = RISING
} = BUY;

// 情报 + 恐惧指数
ALL {
    INTELLIGENCE().score(60) > 30,
    CURRENT().feargreed.value < 30,  // 恐慌
    CURRENT().feargreed.classification = "FEAR"
} = BUY;  // 逆向交易

// 情报 + 资金费率
ALL {
    INTELLIGENCE().score(60) > 40,
    CURRENT().fundingrate < 0,  // 空头支付多头
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 8.3 与统计函数结合

```javascript
// 情报 + ATR（动态止损）
@entry_price: Double = KLINE(5m).close();
@atr: Double = $(5m).ATR().value;
@intel_score: Double = INTELLIGENCE().score(60);

ALL {
    @intel_score > 40,
    $(5m).MACD().crossover_type = GOLDEN_CROSS
} = BUY(
    @entry_price + @atr * 2.0,  // 止盈
    @entry_price - @atr * 1.5   // 止损
);

// 情报 + ZSCORE
ALL {
    INTELLIGENCE("BTCUSDT").score(60) > 40,
    ZSCORE(5m).close(50) > 0,  // 价格在均值上方
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 8.4 与序列条件函数结合

```javascript
// 情报 + CONSECUTIVE
ALL {
    INTELLIGENCE("BTCUSDT").score(60) > 40,
    CONSECUTIVE(5m).close(3).rising(),  // 连续3根K线上涨
    $(5m).RSI().value > 50
} = BUY;

// 情报 + COUNT
@rising_count: Integer = COUNT(5m).close(20).rising();

ALL {
    INTELLIGENCE("BTCUSDT").score(60) > 40,
    @rising_count >= 15,  // 最近20根中有15根上涨
    $(5m).MACD().trend = BULLISH
} = BUY;
```

---

### 8.5 与信号函数结合

```javascript
// 情报 + VOTE
VOTE(0.7) {
    // 情报维度
    INTELLIGENCE("BTCUSDT").score(60) > 40,
    INTELLIGENCE("BTCUSDT").consistency(60) >= 0.7,
    NOT(INTELLIGENCE("BTCUSDT").critical(15)),
    
    // 技术维度
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value > 50,
    $(5m).ADX().value > 25,
    
    // 成交量维度
    KLINE(5m).volume() > AVERAGE(5m).volume(20)
} = BUY;

// 情报 + WEIGHTED
WEIGHTED(0.7) {
    // 情报权重 50%
    WEIGHT(INTELLIGENCE().score(60) > 40) = 0.3,
    WEIGHT(NOT(INTELLIGENCE().critical(15))) = 0.2,
    
    // 技术权重 30%
    WEIGHT($(5m).MACD().trend = BULLISH) = 0.2,
    WEIGHT($(5m).RSI().value > 50) = 0.1,
    
    // 成交量权重 20%
    WEIGHT(KLINE(5m).volume() > AVERAGE(5m).volume(20)) = 0.2
} = BUY;
```

---

## 9. 完整函数速查表

### Level 1: 入门级（3个核心函数）

| 函数 | 用途 | 返回值 | 默认参数 | 示例 |
|------|------|--------|----------|------|
| `{category}.strength(lookback)` | 单类别信号强度 | `-100~100` | lookback=60 | `INTELLIGENCE().funding.strength(60)` |
| `critical(lookback)` | 紧急信号检查 | `true/false` | lookback=15 | `INTELLIGENCE().critical(15)` |
| `score(lookback)` | 综合评分（全类别） | `-100~100` | lookback=60 | `INTELLIGENCE().score(60)` |

### Level 2: 进阶级（7个函数）

| 函数 | 用途 | 返回值 | 默认参数 | 示例 |
|------|------|--------|----------|------|
| `{category}.direction(lookback)` | 信号方向 | `BULLISH/BEARISH/NEUTRAL` | lookback=60 | `INTELLIGENCE().funding.direction(60)` |
| `{category}.confidence(lookback)` | 置信度 | `0~100` | lookback=60 | `INTELLIGENCE().funding.confidence(60)` |
| `{category}.count(lookback)` | 单类别信号数量 | `0~N` | lookback=60 | `INTELLIGENCE().funding.count(60)` |
| `count(lookback)` | 所有类别信号数量 | `0~N` | lookback=60 | `INTELLIGENCE().count(60)` |
| `{category}.avgstrength(lookback)` | 平均强度 | `-100~100` | lookback=120 | `INTELLIGENCE().funding.avgstrength(120)` |
| `{category}.trend(lookback)` | 趋势判断 | `RISING/FALLING/STABLE` | lookback=60 | `INTELLIGENCE().funding.trend(60)` |
| `{category}.exists(lookback, dir)` | 存在性检查 | `true/false` | lookback=60 | `INTELLIGENCE().funding.exists(60, "BULLISH")` |

### Level 3: 高级（5个函数）

| 函数 | 用途 | 返回值 | 默认参数 | 示例 |
|------|------|--------|----------|------|
| `consistency(lookback)` | 多类别一致性 | `0.0~1.0` | lookback=60 | `INTELLIGENCE().consistency(60)` |
| `conflicting(lookback, th)` | 冲突检测 | `true/false` | lookback=60, th=0.5 | `INTELLIGENCE().conflicting(60, 0.5)` |
| `strongest(lookback)` | 最强类别 | 字符串 | lookback=60 | `INTELLIGENCE().strongest(60)` |
| `{category}.evidence(lookback)` | 证据链 | JSON字符串 | lookback=60 | `INTELLIGENCE().funding.evidence(60)` |
| `composite(weights, lookback)` | 自定义权重 | `-100~100` | lookback=60 | `INTELLIGENCE().composite({...}, 60)` |

**说明**：
- `{category}` 表示链式调用的类别属性：`funding`, `liquidation`, `longshort`, `news`, `whale` 等
- Phase 0 支持 `funding`, `liquidation`, `longshort` 三个类别
- 所有函数均自动绑定当前策略运行的交易对（symbol），无需手动传入
- 移除了 `matrix()` 函数（过于复杂，使用频率低）

---

## 10. 总结

### 10.1 核心设计优势

✅ **渐进式学习**：
- Level 1: 3个核心函数，覆盖90%场景
- Level 2: +7个进阶函数，覆盖95%场景
- Level 3: +5个高级函数，覆盖100%场景

✅ **一致性**：
- ✅ 遵循现有 `CURRENT()` 命名空间模式
- ✅ 链式调用：`INTELLIGENCE().{category}.{function}()`
- ✅ 自动绑定symbol，无需手动传入
- ✅ 支持可选参数和默认值
- ✅ 返回值类型明确

✅ **灵活性**：
- ✅ 支持单类别查询：`INTELLIGENCE().funding.strength()`
- ✅ 支持全局查询：`INTELLIGENCE().score()`
- ✅ 支持自定义权重：`INTELLIGENCE().composite({...})`
- ✅ 支持多维分析：`consistency()`, `conflicting()`, `strongest()`
- ✅ 支持证据链：`{category}.evidence()`

✅ **性能优化**：
- ✅ 数据在C#层预加载到内存
- ✅ 核心引擎直接访问内存数据
- ✅ 内部缓存机制
- ✅ 避免重复计算

✅ **用户友好**：
- ✅ 入门简单：2-3个函数即可上手
- ✅ 代码提示友好：链式调用支持IDE自动补全
- ✅ 命名简洁：`funding`, `liquidation`, `longshort`
- ✅ 语义清晰：`strength`, `critical`, `score`

---

### 10.2 与现有DSL的完美融合

```javascript
// 现有 DSL 风格
CURRENT().longshort.ratio()    ✅
CURRENT().feargreed.value       ✅
CURRENT().fundingrate           ✅

// 新的 INTELLIGENCE 函数（完美匹配）
INTELLIGENCE().funding.strength(60)    ✅
INTELLIGENCE().liquidation.direction(60) ✅
INTELLIGENCE().score(60)                ✅
```

**一致性要点**：
1. 命名空间访问：`INTELLIGENCE()` 类似 `CURRENT()`
2. 链式调用：`{namespace}.{attribute}.{function}()`
3. 无需symbol参数：策略运行时自动绑定
4. 简洁的category命名：`funding`, `liquidation`, `longshort`

---

### 10.3 下一步工作

#### Phase 0 (MVP, 7天内完成)
1. ✅ 设计 INTELLIGENCE 函数体系（已完成）
2. ⏳ 更新 PRD v1.1，整合函数设计
3. ⏳ 数据库建表（`intelligence_signals` 等）
4. ⏳ 实现规则引擎（资金费率、清算、多空比）
5. ⏳ C# 实现 DSL 函数绑定
6. ⏳ C++ 核心引擎注入 intelligence 数据
7. ⏳ 单元测试 + 回测验证
8. ⏳ 策略模板（2个）

#### Phase 1 (LLM 集成)
- LLM 输出校验器
- 成本熔断器
- 证据链验证
- `evidence()` 函数实现

#### Phase 2 (高级功能)
- 新闻情报（`news` 类别）
- 巨鲸情报（`whale` 类别）
- 用户反馈系统
- 信号评分优化

---

### 10.4 关键决策记录

| 决策 | 原因 | 影响 |
|------|------|------|
| ❌ 删除 `symbol` 参数 | 策略运行时已绑定交易对，无需重复传入 | 简化函数调用，减少用户认知负担 |
| ✅ 链式调用 `{category}` | 支持IDE自动补全，代码更优雅 | 提升用户体验，减少拼写错误 |
| ✅ 简化category命名 | `funding` 比 `funding_extreme` 更简洁 | 降低输入成本，提高可读性 |
| ❌ 移除 `matrix()` 函数 | 使用频率低，复杂度高 | 聚焦核心功能，简化设计 |
| ✅ 15个函数（3+7+5） | 渐进式学习曲线，满足不同水平用户 | 入门简单，进阶灵活，高级强大 |

---

**文档状态**：✅ 设计完成（v2.0）  
**批准状态**：待批准  
**版本**：2.0（采用链式调用，删除symbol参数，简化category命名）  
**作者**：Prophet 开发团队

*本文档基于 Prophet DSL 规范 v2.0 设计，遵循现有DSL的设计模式和最佳实践。*

