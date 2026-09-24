# Prophet 情报模块 PRD v1.1

**文档版本**：1.1  
**创建日期**：2025-12-31  
**修订日期**：2026-01-01  
**方法论来源**：比特币橙子 Vibe Coding 实战案例  
**预计开发周期**：4-8周（Phase 0-2）

**📋 版本变更说明**：
- 🔴 **致命缺陷修复**：明确时间语义、数据库范式修正、成本熔断、LLM校验
- 🟡 **架构简化**：Phase 0纯规则引擎、DSL函数从10+个简化为3个
- 🟢 **用户体验**：提供默认使用路径、策略模板、7天价值验证

---

## 1. 项目愿景

### 核心目标
在Prophet量化平台中集成"情报驱动"决策能力，让用户的策略不仅依赖技术指标，还能感知市场情绪、异常事件、宏观信息等"软信息"，提升极端行情下的反应速度和胜率。

### 核心价值主张
- **对DSL友好**：用户可在策略中直接调用 3个核心函数
- **可回测验证**：严格的时间语义，避免未来函数
- **可解释性优先**：每条信号附带推理过程、置信度、证据链
- **成本可控**：用户自付API key，熔断器防止爆费
- **渐进式价值**：Phase 0纯规则即可用，Phase 1 LLM增强

### 非目标（V1.1不做）
- ❌ 云端集中式服务（无部署预算）
- ❌ 全自动下单执行（仅提供信号，决策权在用户）
- ❌ 覆盖小币种（初期聚焦BTC/ETH/SOL等主流币）
- ❌ 多语言情绪细粒度分析（英文为主，中文辅助）
- ❌ Phase 0不接入LLM（先验证数据流和回测正确性）

---

## 2. 系统架构

### 2.1 总体架构图（修正后）

```
┌─────────────────────────────────────────────────────────────┐
│                    Prophet.Client (主程序)                    │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │   IntelligenceService (核心服务)                     │  │
│  ├──────────────────────────────────────────────────────┤  │
│  │                                                        │  │
│  │  【Phase 0: 规则引擎】                                │  │
│  │  RuleBasedSignalGenerator                            │  │
│  │    ├─ FundingRateAnalyzer      (资金费率分析)       │  │
│  │    ├─ LiquidationAnalyzer      (清算数据分析)       │  │
│  │    └─ LongShortRatioAnalyzer   (多空比分析)         │  │
│  │                                                        │  │
│  │  【Phase 1: LLM增强】                                 │  │
│  │  LLMSignalGenerator (Phase 1)                        │  │
│  │    ├─ LLMOutputValidator       (输出校验器)         │  │
│  │    ├─ EvidenceChainVerifier    (证据链验证)         │  │
│  │    └─ RetryPolicy              (重试策略)           │  │
│  │                                                        │  │
│  │  【风控与缓存】                                       │  │
│  │  ├─ CostCircuitBreaker         (成本熔断器)         │  │
│  │  ├─ SignalCache                (L1内存缓存)         │  │
│  │  └─ HealthChecker              (数据源健康检查)     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │   DSL函数 (Prophet.Core注册) - 简化版               │  │
│  ├──────────────────────────────────────────────────────┤  │
│  │  INTELLIGENCE(cat, sym).strength([lookback_min])    │  │
│  │  INTELLIGENCE(sym).critical([lookback_min])         │  │
│  │  INTELLIGENCE(sym).score([lookback_min])            │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │   SQLite Database (WAL模式 - 解决并发)              │  │
│  ├──────────────────────────────────────────────────────┤  │
│  │  intelligence_signals (信号表 - 时间语义明确)       │  │
│  │  intelligence_signal_sources (关联表 - 替代JSON)    │  │
│  │  intelligence_raw_data (原始数据 - 30天清理)        │  │
│  │  intelligence_cost_tracking (成本追踪)              │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 时间语义模型（🔴 Claude致命缺陷修复）

```
事件时间轴:
─────────────────────────────────────────────────────►
     t1              t2              t3         t4
     │               │               │          │
  事件发生      数据采集到      信号生成    写入数据库
EventOccurredAt  CollectedAt   GeneratedAt  FirstQueryableAt
     │               │               │          │
     │               │               │          │
     └───────────────┴───────────────┴──────────┘
                                                  │
                                         回测查询用这个!
                                         (避免未来函数)
```

**关键原则**：
1. **回测查询条件**：`first_queryable_at <= currentBarTime`
2. **实盘查询条件**：`first_queryable_at <= DateTime.UtcNow`
3. **回测引擎自动注入时间**：DSL函数不需要（也不能）手动传入时间
4. **信号失效检查**：`expires_at IS NULL OR expires_at > queryTime`

---

## 3. 数据库设计（修正后）

### 3.1 intelligence_signals（核心信号表）

**🔴 重大修正**：
- ✅ 4个明确时间字段（Claude建议）
- ✅ strength/confidence改为INTEGER（Gemini建议）
- ✅ 删除JSON数组字段（Gemini建议）

```sql
CREATE TABLE intelligence_signals (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- ========================================
    -- 时间语义（🔴 Claude致命缺陷修复）
    -- ========================================
    event_occurred_at DATETIME,           -- 事件实际发生时间（可选）
    signal_generated_at DATETIME NOT NULL,-- 信号生成时间（规则计算或LLM返回）
    first_queryable_at DATETIME NOT NULL, -- 首次可查询时间（写入DB后）⚠️ 回测用这个
    expires_at DATETIME,                  -- 信号失效时间（可选，默认永不过期）
    
    -- ========================================
    -- 核心字段
    -- ========================================
    symbol VARCHAR(20) NOT NULL,          -- 交易对
    category VARCHAR(50) NOT NULL,        -- 大类：funding_extreme, liquidation_cascade等
    subcategory VARCHAR(50),              -- 小类（可选）
    
    -- ========================================
    -- 信号方向与强度（🟡 Gemini建议改为INTEGER）
    -- ========================================
    direction VARCHAR(10) NOT NULL CHECK(direction IN ('BULLISH', 'BEARISH', 'NEUTRAL')),
    strength INTEGER NOT NULL CHECK(strength BETWEEN -100 AND 100),
    confidence INTEGER NOT NULL CHECK(confidence BETWEEN 0 AND 100),  -- 0-100而非0-1
    
    -- ========================================
    -- 分析结果（简化）
    -- ========================================
    summary TEXT NOT NULL,                -- 简短摘要（50-100字）
    reasoning TEXT,                       -- 推理过程（JSON格式，可选）
    
    -- ========================================
    -- 元数据
    -- ========================================
    source VARCHAR(100),                  -- 数据来源
    priority INTEGER DEFAULT 0 CHECK(priority BETWEEN 0 AND 10),  -- 0=正常, 8+=紧急
    
    -- ========================================
    -- 索引（🟡 Gemini性能优化）
    -- ========================================
    INDEX idx_queryable_lookup (symbol, category, first_queryable_at DESC),
    INDEX idx_category_time (category, first_queryable_at),
    INDEX idx_priority (priority, first_queryable_at)
);
```

### 3.2 intelligence_signal_sources（关联表）

**🔴 Gemini强烈建议**：替代JSON数组存储，支持关联查询和外键约束

```sql
CREATE TABLE intelligence_signal_sources (
    signal_id INTEGER NOT NULL,
    raw_data_id INTEGER NOT NULL,
    PRIMARY KEY (signal_id, raw_data_id),
    FOREIGN KEY (signal_id) REFERENCES intelligence_signals(id) ON DELETE CASCADE,
    FOREIGN KEY (raw_data_id) REFERENCES intelligence_raw_data(id) ON DELETE RESTRICT
);

CREATE INDEX idx_sources_signal ON intelligence_signal_sources(signal_id);
CREATE INDEX idx_sources_raw ON intelligence_signal_sources(raw_data_id);
```

### 3.3 intelligence_raw_data（原始数据表）

```sql
CREATE TABLE intelligence_raw_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source VARCHAR(50) NOT NULL,          -- binance_funding, coinglass_liquidation等
    category VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,                -- JSON格式
    collected_at DATETIME NOT NULL,       -- 采集时间
    processed BOOLEAN DEFAULT 0,          -- 是否已处理
    
    INDEX idx_raw_source_collected ON intelligence_raw_data(source, collected_at),
    INDEX idx_raw_processed ON intelligence_raw_data(processed, collected_at)
);
```

### 3.4 intelligence_cost_tracking（成本追踪表）

**🔴 新增**：成本熔断需要持久化记录

```sql
CREATE TABLE intelligence_cost_tracking (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date DATE NOT NULL,
    llm_provider VARCHAR(50),             -- gemini, claude等
    tokens_used INTEGER NOT NULL,
    cost_usd DECIMAL(10, 4) NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    
    INDEX idx_cost_date (date)
);
```

### 3.5 SQLite配置（🔴 Gemini并发修复）

```sql
-- 必须在程序启动时执行
PRAGMA journal_mode = WAL;           -- Write-Ahead Logging模式
PRAGMA synchronous = NORMAL;         -- 性能与安全的平衡
PRAGMA cache_size = -64000;          -- 64MB缓存
PRAGMA temp_store = MEMORY;          -- 临时表存内存
```

---

## 4. DSL函数设计（大幅简化）

### 4.1 设计原则（🟡 GPT产品建议）

**v1.0问题**：10+个函数，用户认知负担重，学习曲线陡峭

**v1.1改进**：
- ✅ Phase 0只公开**3个核心函数**
- ✅ 提供**默认使用路径**（策略模板）
- ✅ 参数简化（lookback用分钟而非秒）
- ✅ 返回值语义明确（0表示无信号，而非null）

### 4.2 核心函数（Phase 0）

#### 函数1: `INTELLIGENCE(category, symbol).strength([lookback_minutes])`

**用途**：获取指定类别的信号强度（最常用）

**参数**：
- `category`: 信号类别（字符串）
  - Phase 0支持：`"funding_extreme"`, `"liquidation_cascade"`, `"longshort_extreme"`
  - Phase 1扩展：`"news_event"`, `"whale_movement"`等
- `symbol`: 交易对（字符串），如 `"BTCUSDT"`
- `lookback_minutes`: 回溯分钟数（整数，默认60）

**返回值**：
- `-100` ~ `+100`：信号强度（负数=看跌，正数=看涨）
- `0`：该时间范围内无信号或信号已过期

**回测保证**：自动使用 `first_queryable_at <= currentBarTime` 查询

**示例**：

```javascript
// 获取最近1小时的资金费率极端信号强度
strength = INTELLIGENCE("funding_extreme", "BTCUSDT").strength(60)

// 如果强度>50，认为极端看涨
ALL {
    INTELLIGENCE("funding_extreme", "BTCUSDT").strength(60) > 50,
    $(5m).MACD().trend = BULLISH
} = BUY;

// 如果强度<-50，认为极端看跌
ALL {
    INTELLIGENCE("funding_extreme", "BTCUSDT").strength(60) < -50,
    $(5m).RSI().value > 70
} = SELL;
```

---

#### 函数2: `INTELLIGENCE(symbol).critical([lookback_minutes])`

**用途**：检查是否有紧急信号（风控开关）

**参数**：
- `symbol`: 交易对
- `lookback_minutes`: 回溯分钟数（默认15）

**返回值**：
- `true`: 存在priority>=8的紧急信号
- `false`: 无紧急信号

**典型用法**：作为策略的风控开关

**示例**：

```javascript
// 风控开关：没有紧急看跌信号才允许入场
ALL {
    // 技术面入场条件
    $(5m).MACD().crossover_type = GOLDEN_CROSS,
    $(5m).RSI().value > 50,
    
    // 🎯 情报风控：没有紧急看跌信号
    NOT(INTELLIGENCE("BTCUSDT").critical(30))
} = BUY;

// 紧急出场：出现紧急信号立即平仓
ALL {
    INTELLIGENCE("BTCUSDT").critical(15) = true
} = SELL;
```

---

#### 函数3: `INTELLIGENCE(symbol).score([lookback_minutes])`

**用途**：综合评分（加权所有类别）

**参数**：
- `symbol`: 交易对
- `lookback_minutes`: 回溯分钟数（默认60）

**返回值**：
- `-100` ~ `+100`：综合评分
- 计算方式：所有类别的strength加权平均

**权重规则**：
```
funding_extreme:      30%
liquidation_cascade:  25%
longshort_extreme:    20%
whale_movement:       15%  (Phase 1)
news_event:           10%  (Phase 1)
```

**示例**：

```javascript
// 综合评分高度看涨
ALL {
    INTELLIGENCE("BTCUSDT").score(60) > 50,
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value < 70
} = BUY;

// 综合评分与技术面冲突时，优先听情报
ALL {
    INTELLIGENCE("BTCUSDT").score(30) < -70,  // 极度看跌
    $(5m).MACD().trend = BULLISH              // 但技术面看涨
} = HOLD;  // 暂停交易
```

---

### 4.3 历史数据空洞处理（🔴 Claude致命缺陷修复）

**v1.0问题**：回测2024年策略时，情报数据只有2025年的，返回值不明确

**v1.1解决方案**：

```cpp
// DSL函数内部实现
double GetIntelligenceStrength(string category, string symbol, int lookbackMinutes)
{
    DateTime queryTime = GetCurrentBarTime();  // 回测引擎自动注入
    DateTime lookbackStart = queryTime.AddMinutes(-lookbackMinutes);
    
    var signals = _repository.GetSignals(
        symbol, category, lookbackStart, queryTime
    );
    
    // ✅ 明确的返回值语义
    if (signals.Count == 0)
    {
        return 0.0;  // 无信号，返回0（而非null或抛异常）
    }
    
    // 返回最新信号的强度
    return signals.OrderByDescending(s => s.FirstQueryableAt).First().Strength;
}
```

**用户文档中明确说明**：
- `0` 表示"该时间范围内无信号"或"信号已过期"
- 用户可以通过条件判断：`if (strength != 0) { ... }`
- 回测早期数据空洞会自然地返回0，不影响策略运行

---

### 4.4 Phase 1扩展函数（用户反馈后再加）

```javascript
// 扩展函数1: 获取置信度
INTELLIGENCE(category, symbol).confidence([lookback_minutes])
// 返回: 0-100

// 扩展函数2: 获取方向
INTELLIGENCE(category, symbol).direction([lookback_minutes])
// 返回: "BULLISH" / "BEARISH" / "NEUTRAL"

// 扩展函数3: 多类别一致性
INTELLIGENCE(symbol).consistency([lookback_minutes])
// 返回: 0.0-1.0 (0=完全矛盾, 1=完全一致)

// 扩展函数4: 最强信号类别
INTELLIGENCE(symbol).strongest([lookback_minutes])
// 返回: category字符串
```

---

## 5. Phase 0 实施计划（修正后）

### 5.1 MVP范围调整（🟡 GPT产品建议）

**v1.0问题**：
- 只做"资金费率"不足以验证"情报驱动决策"的核心价值
- 用户会觉得"这不就是又一个指标吗？"

**v1.1改进**：Phase 0增加两个数据源，形成"风险告警"价值

| 数据源 | 类型 | 是否用LLM | 优先级 | 理由 |
|--------|------|----------|--------|------|
| **资金费率异常** | 结构化 | ❌ 纯规则 | P0 | 已有数据，简单可靠 |
| **清算/爆仓数据** | 结构化 | ❌ 纯规则 | P0 | 与资金费率互补，形成"级联风险"识别 |
| **多空比极端** | 结构化 | ❌ 纯规则 | P0 | 已有数据，情绪指标 |
| ~~新闻/Twitter~~ | 非结构化 | ✅ LLM | ❌ P2 | 噪音大、幻觉风险高、推后 |
| ~~宏观日历~~ | 半结构化 | ❌ 规则 | ⚠️ P1可选 | 可做但非必须 |

### 5.2 Phase 0 技术范围

**时间**：Week 1-2（10个工作日）

**必须完成**：
1. ✅ 数据库Schema实现（4个时间字段、关联表、WAL模式）
2. ✅ 规则引擎实现
   - `FundingRateAnalyzer`
   - `LiquidationAnalyzer`
   - `LongShortRatioAnalyzer`
3. ✅ 3个核心DSL函数（Prophet.Core注册）
4. ✅ 成本熔断器（架构预留，Phase 0暂不启用）
5. ✅ 回测时间注入验证
6. ✅ SignalCache（L1内存缓存）
7. ✅ 2个策略模板
8. ✅ UI基础展示

**暂不做**：
- ❌ LLM集成（Phase 1）
- ❌ outcomes效果跟踪（Phase 2）
- ❌ 复杂的SignalScorer
- ❌ 10+个DSL函数

---

### 5.3 规则引擎详细设计（Phase 0核心）

#### 5.3.1 FundingRateAnalyzer（资金费率分析）

```csharp
public class FundingRateAnalyzer
{
    public IntelligenceSignal? Analyze(List<FundingRateData> recentRates)
    {
        if (recentRates.Count < 3) return null;
        
        var latest = recentRates[0];
        var avg = recentRates.Average(r => r.FundingRate);
        var std = CalculateStdDev(recentRates.Select(r => r.FundingRate));
        
        // 规则1: 极端高资金费率（多头疯狂）
        if (latest.FundingRate > avg + 2 * std && latest.FundingRate > 0.001)
        {
            return new IntelligenceSignal
            {
                EventOccurredAt = latest.Timestamp,
                SignalGeneratedAt = DateTime.UtcNow,
                FirstQueryableAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(8),  // 8小时后过期
                Symbol = latest.Symbol,
                Category = "funding_extreme",
                Direction = "BEARISH",  // 反向信号：资金费率过高，可能反转
                Strength = -CalculateStrength(latest.FundingRate, avg, std),
                Confidence = 75,
                Summary = $"资金费率{latest.FundingRate:P4}，远高于均值{avg:P4}，多头过热",
                Priority = latest.FundingRate > 0.002 ? 8 : 5
            };
        }
        
        // 规则2: 极端低资金费率（空头疯狂）
        if (latest.FundingRate < avg - 2 * std && latest.FundingRate < -0.001)
        {
            return new IntelligenceSignal
            {
                // ... 类似逻辑，Direction = "BULLISH"
            };
        }
        
        return null;
    }
}
```

#### 5.3.2 LiquidationAnalyzer（清算数据分析）

```csharp
public class LiquidationAnalyzer
{
    public IntelligenceSignal? Analyze(List<LiquidationData> recentLiqs, int windowMinutes = 30)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var recentData = recentLiqs.Where(l => l.Timestamp >= cutoffTime).ToList();
        
        if (recentData.Count < 5) return null;
        
        var longLiqValue = recentData.Where(l => l.Side == "long").Sum(l => l.Value);
        var shortLiqValue = recentData.Where(l => l.Side == "short").Sum(l => l.Value);
        var totalValue = longLiqValue + shortLiqValue;
        
        // 规则1: 多头大规模清算（级联风险）
        if (longLiqValue > totalValue * 0.7 && totalValue > 10_000_000)  // 7:3比例且金额超1000万
        {
            return new IntelligenceSignal
            {
                EventOccurredAt = recentData.Max(l => l.Timestamp),
                SignalGeneratedAt = DateTime.UtcNow,
                FirstQueryableAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(2),  // 2小时后过期
                Symbol = recentData[0].Symbol,
                Category = "liquidation_cascade",
                Direction = "BEARISH",
                Strength = -CalculateLiquidationStrength(longLiqValue, totalValue),
                Confidence = 80,
                Summary = $"最近{windowMinutes}分钟多头清算${longLiqValue/1_000_000:F1}M，可能继续下跌",
                Priority = totalValue > 50_000_000 ? 9 : 6
            };
        }
        
        // 规则2: 空头大规模清算
        // ...类似逻辑
        
        return null;
    }
}
```

#### 5.3.3 LongShortRatioAnalyzer（多空比分析）

```csharp
public class LongShortRatioAnalyzer
{
    public IntelligenceSignal? Analyze(List<LongShortRatioData> recentRatios)
    {
        if (recentRatios.Count < 10) return null;
        
        var latest = recentRatios[0];
        var avg = recentRatios.Average(r => r.LongRatio);
        
        // 规则1: 多头比例极端高（70%+）
        if (latest.LongRatio > 0.7 && latest.LongRatio > avg + 0.1)
        {
            return new IntelligenceSignal
            {
                EventOccurredAt = latest.UpdateTime,
                SignalGeneratedAt = DateTime.UtcNow,
                FirstQueryableAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(6),
                Symbol = latest.Symbol,
                Category = "longshort_extreme",
                Direction = "BEARISH",  // 反向信号
                Strength = -CalculateRatioStrength(latest.LongRatio, avg),
                Confidence = 70,
                Summary = $"多头占比{latest.LongRatio:P0}，远超均值{avg:P0}，警惕反转",
                Priority = latest.LongRatio > 0.75 ? 7 : 5
            };
        }
        
        // 规则2: 空头比例极端高
        // ...类似逻辑
        
        return null;
    }
}
```

---

### 5.4 成本熔断器（🔴 Claude高风险修复）

```csharp
public class CostCircuitBreaker
{
    private readonly IIntelligenceRepository _repository;
    private decimal _dailyBudget;
    private decimal _todaySpent;
    private DateTime _lastResetDate;
    
    public CostCircuitBreaker(decimal dailyBudget = 5.0m)
    {
        _dailyBudget = dailyBudget;
        LoadTodaySpent();
    }
    
    private void LoadTodaySpent()
    {
        var today = DateTime.UtcNow.Date;
        _todaySpent = _repository.GetDailyCost(today);
        _lastResetDate = today;
    }
    
    private void ResetIfNewDay()
    {
        if (DateTime.UtcNow.Date > _lastResetDate)
        {
            _lastResetDate = DateTime.UtcNow.Date;
            _todaySpent = 0;
        }
    }
    
    public bool CanMakeLLMCall(int estimatedTokens, out string reason)
    {
        ResetIfNewDay();
        
        var estimatedCost = CalculateCost(estimatedTokens);
        
        if (_todaySpent + estimatedCost > _dailyBudget)
        {
            reason = $"⚠️ 成本熔断: 今日已花费${_todaySpent:F2}/${_dailyBudget:F2}";
            Console.WriteLine(reason);
            Console.WriteLine("→ 已降级为纯规则引擎模式");
            return false;
        }
        
        reason = null;
        return true;
    }
    
    public void RecordCost(string provider, int tokensUsed, decimal cost)
    {
        _todaySpent += cost;
        
        // 持久化到数据库
        _repository.InsertCostRecord(new CostRecord
        {
            Date = DateTime.UtcNow.Date,
            LlmProvider = provider,
            TokensUsed = tokensUsed,
            CostUsd = cost
        });
        
        // 实时警告
        if (_todaySpent > _dailyBudget * 0.8m)
        {
            Console.WriteLine($"⚠️ 今日成本已达预算的{_todaySpent/_dailyBudget:P0}");
        }
    }
    
    private decimal CalculateCost(int tokens)
    {
        // Gemini 1.5 Flash定价（2025年）
        return tokens * 0.00000015m;  // $0.15 / 1M tokens
    }
}
```

---

### 5.5 策略模板（🟡 GPT用户体验改进）

**v1.0问题**：用户不知道如何使用情报函数

**v1.1解决方案**：提供2个开箱即用的策略模板

#### 模板1: 情报风控开关

```javascript
/*
 * 策略名称: MACD趋势策略 + 情报风控
 * 适用场景: 趋势跟随，使用情报作为风控开关
 * 风险等级: 中
 */

// ========================================
// 入场条件
// ========================================
ALL {
    // 技术面：MACD金叉
    $(5m).MACD().crossover_type = GOLDEN_CROSS,
    $(5m).RSI().value > 50,
    $(5m).RSI().value < 70,
    
    // 🎯 情报风控：没有紧急看跌信号
    NOT(INTELLIGENCE("BTCUSDT").critical(30))
} = BUY;

// ========================================
// 出场条件
// ========================================
ANY {
    // 技术面：MACD死叉
    $(5m).MACD().crossover_type = DEATH_CROSS,
    
    // 🎯 紧急情报：立即平仓
    INTELLIGENCE("BTCUSDT").critical(15) = true
} = SELL;
```

#### 模板2: 情报确认策略

```javascript
/*
 * 策略名称: 多维度确认策略
 * 适用场景: 情报与技术面双重确认
 * 风险等级: 低（确认严格）
 */

// ========================================
// 入场条件（双重确认）
// ========================================
ALL {
    // 🎯 情报层面：综合看涨
    INTELLIGENCE("BTCUSDT").score(60) > 30,
    
    // 资金费率不极端（避免过热）
    INTELLIGENCE("funding_extreme", "BTCUSDT").strength(60) < 60,
    
    // 技术面：趋势向上
    $(5m).MACD().trend = BULLISH,
    $(5m).RSI().value > 50,
    $(5m).ADX().value > 25
} = BUY;

// ========================================
// 出场条件
// ========================================
ANY {
    // 🎯 情报反转：综合评分转为看跌
    INTELLIGENCE("BTCUSDT").score(30) < -50,
    
    // 技术面：RSI超买
    $(5m).RSI().value > 80
} = SELL;
```

---

### 5.6 UI设计（简化版）

```
┌─────────────────────────────────────────────────────────┐
│  Prophet - 情报模块                                      │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  📊 今日成本: $0.00 / $5.00   [熔断器状态: ✅ 正常]      │
│                                                           │
│  ┌───────────────────────────────────────────────────┐  │
│  │  最新情报 (最近1小时)                              │  │
│  ├───────────────────────────────────────────────────┤  │
│  │  🔴 [紧急] 多头清算级联                            │  │
│  │      BTCUSDT · 5分钟前                             │  │
│  │      最近30分钟多头清算$45M，可能继续下跌          │  │
│  │      强度: -85  置信: 80%  过期: 1小时55分         │  │
│  │      [应用到策略] [忽略] [详情]                    │  │
│  ├───────────────────────────────────────────────────┤  │
│  │  🟡 [中等] 资金费率极端                            │  │
│  │      BTCUSDT · 12分钟前                            │  │
│  │      资金费率0.15%，多头过热                       │  │
│  │      强度: -70  置信: 75%  过期: 7小时48分         │  │
│  │      [应用到策略] [标记噪音] [详情]                │  │
│  └───────────────────────────────────────────────────┘  │
│                                                           │
│  ┌───────────────────────────────────────────────────┐  │
│  │  快速入门                                          │  │
│  ├───────────────────────────────────────────────────┤  │
│  │  1. [查看策略模板] (2个开箱即用)                  │  │
│  │  2. [7分钟上手教程] (视频+文字)                   │  │
│  │  3. [DSL函数参考] (3个核心函数)                   │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## 6. Phase 1 实施计划（LLM增强）

### 6.1 Phase 1 范围

**时间**：Week 3-5（3周）

**核心目标**：LLM作为"增强"而非"核心"

**新增功能**：
1. ✅ LLMSignalGenerator
2. ✅ LLMOutputValidator（🔴 Claude致命缺陷修复）
3. ✅ EvidenceChainVerifier（防幻觉）
4. ✅ 新闻RSS数据源（结构化数据）
5. ✅ 扩展DSL函数（从3个→6个）

**数据源扩展**：
- 新闻RSS（CoinDesk, CoinTelegraph等）
- 交易所公告（Binance, Coinbase等）

---

### 6.2 LLM输出校验器（🔴 Claude高风险修复）

```csharp
public class LLMOutputValidator
{
    public ValidationResult Validate(string rawOutput, List<int> expectedRawDataIds)
    {
        // ========================================
        // 步骤1: 清理Markdown代码块
        // ========================================
        var cleaned = CleanMarkdownCodeBlocks(rawOutput);
        
        // ========================================
        // 步骤2: JSON格式校验
        // ========================================
        if (!TryParseJson(cleaned, out var result))
        {
            return ValidationResult.FormatError("Invalid JSON format");
        }
        
        if (!result.ContainsKey("signals") || !(result["signals"] is List<object> signals))
        {
            return ValidationResult.StructureError("Missing 'signals' array");
        }
        
        var validatedSignals = new List<LLMSignal>();
        
        // ========================================
        // 步骤3: 逐个信号校验
        // ========================================
        foreach (var signalObj in signals)
        {
            var signal = signalObj as Dictionary<string, object>;
            
            // 3.1 强度范围检查
            if (!signal.ContainsKey("strength") || 
                !IsIntInRange(signal["strength"], -100, 100))
            {
                return ValidationResult.RangeError($"Strength out of range: {signal["strength"]}");
            }
            
            // 3.2 置信度检查（LLM可能输出0-1，需转换为0-100）
            if (!signal.ContainsKey("confidence"))
            {
                return ValidationResult.MissingFieldError("confidence");
            }
            
            var confidence = Convert.ToDouble(signal["confidence"]);
            if (confidence > 1.0)  // 如果>1，认为是百分比
            {
                if (confidence < 0 || confidence > 100)
                    return ValidationResult.RangeError($"Confidence out of range: {confidence}");
            }
            else  // 如果<=1，认为是小数，转换为百分比
            {
                signal["confidence"] = (int)(confidence * 100);
            }
            
            // 3.3 枚举值检查
            var urgency = signal["urgency"]?.ToString()?.ToLower();
            if (!new[] { "high", "medium", "low" }.Contains(urgency))
            {
                return ValidationResult.EnumError($"Invalid urgency: {urgency}");
            }
            
            // 3.4 方向检查
            var direction = signal["direction"]?.ToString()?.ToUpper();
            if (!new[] { "BULLISH", "BEARISH", "NEUTRAL" }.Contains(direction))
            {
                return ValidationResult.EnumError($"Invalid direction: {direction}");
            }
            
            // ========================================
            // 🔴 步骤4: 证据链验证（防幻觉）
            // ========================================
            if (signal.ContainsKey("raw_data_ids") && signal["raw_data_ids"] is List<object> ids)
            {
                foreach (var id in ids)
                {
                    var rawDataId = Convert.ToInt32(id);
                    if (!expectedRawDataIds.Contains(rawDataId))
                    {
                        return ValidationResult.HallucinationError(
                            $"LLM referenced non-existent raw_data_id: {rawDataId}"
                        );
                    }
                }
            }
            
            validatedSignals.Add(ParseSignal(signal));
        }
        
        return ValidationResult.Success(validatedSignals);
    }
    
    private string CleanMarkdownCodeBlocks(string text)
    {
        // 移除 ```json ... ``` 或 ``` ... ```
        return Regex.Replace(text, @"```(json)?\s*|\s*```", "").Trim();
    }
}

// 使用示例
var validator = new LLMOutputValidator();
var result = validator.Validate(llmOutput, batch.RawDataIds);

if (!result.IsSuccess)
{
    _logger.LogError($"LLM validation failed: {result.ErrorType} - {result.ErrorMessage}");
    
    // 重试1次（可能是格式问题）
    if (retryCount < 1)
    {
        return await CallLLMWithRetry(batch, retryCount + 1);
    }
    
    // 记录失败，降级为规则引擎
    await _failedBatchRepository.InsertAsync(new FailedBatch
    {
        BatchId = batch.Id,
        ErrorType = result.ErrorType,
        ErrorMessage = result.ErrorMessage,
        RawOutput = llmOutput,
        Timestamp = DateTime.UtcNow
    });
    
    return null;
}

// 成功，记录成本
_costBreaker.RecordCost("gemini", tokensUsed, actualCost);
```

---

### 6.3 Prompt模板（简化版）

```
你是一个加密货币市场分析专家。请分析以下数据，生成结构化的交易情报信号。

【输入数据】
{raw_data_json}

【输出要求】
1. 严格返回JSON格式（不要用markdown代码块包裹）
2. strength必须是-100到+100的整数
3. confidence必须是0到1之间的小数（如0.75）
4. direction必须是BULLISH/BEARISH/NEUTRAL之一
5. urgency必须是high/medium/low之一
6. raw_data_ids必须是输入数据中实际存在的ID

【输出格式】
{
  "signals": [
    {
      "symbol": "BTCUSDT",
      "category": "news_event",
      "direction": "BULLISH",
      "strength": 65,
      "confidence": 0.8,
      "urgency": "high",
      "summary": "简短摘要（不超过100字）",
      "reasoning": "推理过程",
      "raw_data_ids": [123, 456],
      "expires_in_hours": 2
    }
  ]
}

【分析】
```

---

## 7. Phase 2 实施计划（闭环优化）

### 7.1 Phase 2 范围

**时间**：Week 6-8（3周）

**核心目标**：用户反馈闭环、性能优化

**新增功能**：
1. ✅ 用户反馈系统（有用/噪音/误导）
2. ✅ 历史效果追踪（outcomes表）
3. ✅ 信号质量报告
4. ✅ Twitter数据源（可选）
5. ✅ 归档策略（老数据归档）

**暂不做**：
- ❌ 实时推送（Telegram/邮件）
- ❌ 移动端应用
- ❌ 多用户系统

---

## 8. 成本估算（修正后）

### 8.1 Phase 0成本

**Phase 0不使用LLM**：成本为 **$0**

### 8.2 Phase 1成本（LLM启用后）

#### 基础估算（正常行情）

| 项目 | 频率 | Tokens/次 | 成本/次 | 每日成本 |
|------|------|-----------|---------|----------|
| 趋势池批处理 | 48次/天 (30分钟) | 3000 | $0.00045 | $0.022 |
| 新闻RSS分析 | 20次/天 | 2000 | $0.00030 | $0.006 |
| **小计** | | | | **$0.028** |
| **每月** | | | | **$0.84** |

#### 极端行情估算（2024年8月5日类似）

| 项目 | 频率 | Tokens/次 | 成本/次 | 每日成本 |
|------|------|-----------|---------|----------|
| 趋势池（新闻爆炸） | 48次/天 | 15000 | $0.00225 | $0.108 |
| 新闻RSS（加倍） | 40次/天 | 2000 | $0.00030 | $0.012 |
| 重试成本（20%） | | | | $0.024 |
| **小计** | | | | **$0.144** |
| **每月** | | | | **$4.32** |

#### 成本控制措施

1. **熔断器**：每日预算$5，超出后降级为规则引擎
2. **用户可配置**：用户可设置自己的每日预算
3. **透明化**：UI实时显示今日消费和月度预估

---

## 9. 成功指标（修正后）

### 9.1 Phase 0指标（7天内可验证）

**🎯 北极星指标**：启用情报函数的活跃策略占比

| 指标 | 目标 | 验证方法 |
|------|------|----------|
| 启用率 | >30% | 至少30%的用户启用情报模块 |
| 触达率 | >50% | 至少50%的启用用户点击过信号 |
| 应用率 | >20% | 至少20%的用户将情报函数应用到策略 |
| 噪音率 | <30% | 被标记"噪音"的信号<30% |
| 次日留存 | >60% | 启用后次日仍使用 |

### 9.2 Phase 1指标（30天内可验证）

| 指标 | 目标 | 验证方法 |
|------|------|----------|
| 策略胜率提升 | +3%~5% | 对比启用前后的回测结果 |
| 极端行情预警 | >80% | 极端行情前15分钟内触发critical信号 |
| LLM解析成功率 | >90% | 成功解析并入库的比例 |
| 用户反馈率 | >15% | 用户主动标注"有用"或"噪音" |

### 9.3 Phase 2指标（长期）

| 指标 | 目标 | 验证方法 |
|------|------|----------|
| 信号准确率 | >70% | outcomes表中的实际效果统计 |
| 用户满意度 | >4.0/5.0 | 用户调研 |
| 成本可控性 | <$5/月 | 99%用户的月度成本 |

---

## 10. 风险与缓解

### 10.1 技术风险

| 风险 | 严重度 | 缓解措施 | 负责人 |
|------|--------|----------|--------|
| 🔴 回测时间戳错误 | 高 | 4个明确时间字段 + 自动化测试 | 核心引擎团队 |
| 🔴 SQLite并发锁 | 高 | 开启WAL模式 + 写入队列 | 数据库团队 |
| 🔴 LLM成本失控 | 高 | 熔断器 + 每日预算 | 服务层团队 |
| 🟡 LLM输出不稳定 | 中 | 校验器 + 重试逻辑 | LLM集成团队 |
| 🟡 数据源失效 | 中 | 健康检查 + 降级策略 | 数据采集团队 |

### 10.2 产品风险

| 风险 | 严重度 | 缓解措施 |
|------|--------|----------|
| 用户不理解如何使用 | 中 | 2个策略模板 + 7分钟教程 |
| 信号噪音过多 | 中 | 优先级过滤 + 用户反馈闭环 |
| 价值不明显 | 高 | Phase 0增加清算数据 + 7天快速验证 |

---

## 11. 附录

### 11.1 数据库完整Schema

```sql
-- ========================================
-- 核心信号表
-- ========================================
CREATE TABLE intelligence_signals (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- 时间语义
    event_occurred_at DATETIME,
    signal_generated_at DATETIME NOT NULL,
    first_queryable_at DATETIME NOT NULL,
    expires_at DATETIME,
    
    -- 核心字段
    symbol VARCHAR(20) NOT NULL,
    category VARCHAR(50) NOT NULL,
    subcategory VARCHAR(50),
    
    -- 信号属性
    direction VARCHAR(10) NOT NULL CHECK(direction IN ('BULLISH', 'BEARISH', 'NEUTRAL')),
    strength INTEGER NOT NULL CHECK(strength BETWEEN -100 AND 100),
    confidence INTEGER NOT NULL CHECK(confidence BETWEEN 0 AND 100),
    
    -- 内容
    summary TEXT NOT NULL,
    reasoning TEXT,
    
    -- 元数据
    source VARCHAR(100),
    priority INTEGER DEFAULT 0 CHECK(priority BETWEEN 0 AND 10),
    
    -- 索引
    INDEX idx_queryable_lookup (symbol, category, first_queryable_at DESC),
    INDEX idx_category_time (category, first_queryable_at),
    INDEX idx_priority (priority, first_queryable_at)
);

-- ========================================
-- 关联表（替代JSON数组）
-- ========================================
CREATE TABLE intelligence_signal_sources (
    signal_id INTEGER NOT NULL,
    raw_data_id INTEGER NOT NULL,
    PRIMARY KEY (signal_id, raw_data_id),
    FOREIGN KEY (signal_id) REFERENCES intelligence_signals(id) ON DELETE CASCADE,
    FOREIGN KEY (raw_data_id) REFERENCES intelligence_raw_data(id) ON DELETE RESTRICT
);

CREATE INDEX idx_sources_signal ON intelligence_signal_sources(signal_id);
CREATE INDEX idx_sources_raw ON intelligence_signal_sources(raw_data_id);

-- ========================================
-- 原始数据表
-- ========================================
CREATE TABLE intelligence_raw_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source VARCHAR(50) NOT NULL,
    category VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    collected_at DATETIME NOT NULL,
    processed BOOLEAN DEFAULT 0,
    
    INDEX idx_raw_source_collected (source, collected_at),
    INDEX idx_raw_processed (processed, collected_at)
);

-- ========================================
-- 成本追踪表
-- ========================================
CREATE TABLE intelligence_cost_tracking (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date DATE NOT NULL,
    llm_provider VARCHAR(50),
    tokens_used INTEGER NOT NULL,
    cost_usd DECIMAL(10, 4) NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    
    INDEX idx_cost_date (date)
);

-- ========================================
-- 用户反馈表（Phase 2）
-- ========================================
CREATE TABLE intelligence_user_feedback (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    signal_id INTEGER NOT NULL,
    feedback_type VARCHAR(20) CHECK(feedback_type IN ('useful', 'noise', 'misleading')),
    comment TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (signal_id) REFERENCES intelligence_signals(id),
    INDEX idx_feedback_signal (signal_id)
);

-- ========================================
-- SQLite配置（启动时执行）
-- ========================================
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = -64000;
PRAGMA temp_store = MEMORY;
```

### 11.2 DSL函数注册（Prophet.Core）

```cpp
// Prophet.Core/src/dsl/intelligence_functions.cpp

void RegisterIntelligenceFunctions(FunctionRegistry& registry)
{
    // ========================================
    // Phase 0: 核心函数
    // ========================================
    
    // 函数1: 获取信号强度
    registry.RegisterFunction("INTELLIGENCE", [](const FunctionCall& call) {
        // INTELLIGENCE(category, symbol).strength([lookback_minutes])
        auto category = call.GetStringArg(0);
        auto symbol = call.GetStringArg(1);
        auto lookbackMinutes = call.GetIntArg(2, 60);  // 默认60分钟
        
        return CreateIntelligenceObject(category, symbol, lookbackMinutes);
    });
    
    // 子方法: .strength()
    registry.RegisterMethod("IntelligenceObject", "strength", [](const Object& obj) {
        return GetIntelligenceStrength(obj.category, obj.symbol, obj.lookback);
    });
    
    // 子方法: .critical() - 检查紧急信号
    registry.RegisterMethod("IntelligenceObject", "critical", [](const Object& obj) {
        return HasCriticalSignal(obj.symbol, obj.lookback);
    });
    
    // 子方法: .score() - 综合评分
    registry.RegisterMethod("IntelligenceObject", "score", [](const Object& obj) {
        return GetCompositeScore(obj.symbol, obj.lookback);
    });
    
    // ========================================
    // Phase 1: 扩展函数（预留）
    // ========================================
    
    // .confidence() - 置信度
    // .direction() - 方向
    // .consistency() - 一致性
}
```

### 11.3 AI审查结果摘要

**🔴 致命缺陷（已修复）**：
1. ✅ 时间语义模糊 → 4个明确时间字段
2. ✅ 数据库范式问题 → 关联表 + INTEGER类型 + WAL模式
3. ✅ LLM输出不稳定 → 校验器 + 重试逻辑
4. ✅ 成本失控风险 → 熔断器 + 每日预算

**🟡 架构改进（已采纳）**：
1. ✅ Phase 0纯规则引擎（不用LLM）
2. ✅ DSL函数简化（10+→3个）
3. ✅ MVP范围调整（资金费率+清算+多空比）
4. ✅ 策略模板（2个开箱即用）
5. ✅ 应用层缓存（L1内存缓存）

**🟢 用户体验（已采纳）**：
1. ✅ 默认使用路径（风控开关模式）
2. ✅ 7天价值验证
3. ✅ 成本透明化
4. ✅ 信号优先级过滤

---

## 12. 总结

### v1.1核心改进

1. **🔴 致命缺陷修复**：时间语义、数据库范式、LLM校验、成本熔断
2. **🟡 架构简化**：Phase 0纯规则、DSL函数3个、MVP聚焦
3. **🟢 用户体验**：策略模板、快速上手、价值可验证

### 下一步行动

- [ ] **Week 1-2**：实施Phase 0（数据库+规则引擎+DSL）
- [ ] **Week 3-5**：实施Phase 1（LLM增强）
- [ ] **Week 6-8**：实施Phase 2（闭环优化）

### 关键成功因素

1. ✅ **回测正确性**：严格的时间语义
2. ✅ **成本可控**：熔断器+降级策略
3. ✅ **用户价值**：7天内可感知改进
4. ✅ **技术稳健**：规则引擎作为基础，LLM作为增强

---

**文档状态：** ✅ 已完成 AI陪审团审查并修正  
**批准状态：** 待批准  
**预计开始日期：** 2026-01-06

**贡献者**：
- Claude（量化架构师审查）
- Gemini（数据工程师审查）
- GPT（产品经理审查）
- 项目团队

---

*本PRD基于比特币橙子的Vibe Coding方法论编写，经过3轮AI陪审团严格审查，修正了所有致命缺陷和高风险问题。*

