# Prophet 情报模块 PRD v1.0

**文档版本**：1.0  
**创建日期**：2025-12-31  
**方法论来源**：比特币橙子 Vibe Coding 实战案例  
**预计开发周期**：4-8周（Phase 0-2）

---

## 1. 项目愿景

### 核心目标
在Prophet量化平台中集成"情报驱动"决策能力，让用户的策略不仅依赖技术指标，还能感知市场情绪、异常事件、宏观信息等"软信息"，提升极端行情下的反应速度和胜率。

### 核心价值主张
- **对DSL友好**：用户可在策略中直接调用 `GetIntelligence(category, symbol)`
- **可回测验证**：所有信号都带时间戳，支持历史回测，避免"未来函数"
- **可解释性优先**：每条信号必须附带LLM推理过程、置信度、证据链
- **闭环学习**：基于交易结果反馈，逐步提升信号质量
- **成本可控**：用户使用自己的LLM API key，自主控制成本

### 非目标（V1.0不做）
- ❌ 云端集中式服务（无部署预算）
- ❌ 全自动下单执行（仅提供信号，决策权在用户）
- ❌ 覆盖小币种（初期聚焦BTC/ETH/SOL等主流币）
- ❌ 多语言情绪细粒度分析（英文为主，中文辅助）

---

## 2. 系统架构

### 2.1 总体架构图

```
┌─────────────────────────────────────────────────────────────┐
│                    Prophet.Client (主程序)                    │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │ StrategyView │    │ BacktestView │    │Intelligence  │  │
│  │   (策略)     │◄───┤   (回测)     │◄───┤    View      │  │
│  │              │    │              │    │  (情报中心)  │  │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘  │
│         │                   │                    │          │
│         └───────────────────┴────────────────────┘          │
│                             │                                │
│                    ┌────────▼────────┐                       │
│                    │ Intelligence    │                       │
│                    │   Service       │                       │
│                    │  (情报服务核心) │                       │
│                    └────────┬────────┘                       │
│                             │                                │
│         ┌───────────────────┼───────────────────┐           │
│         │                   │                   │           │
│    ┌────▼────┐        ┌────▼────┐        ┌────▼────┐      │
│    │Data     │        │LLM      │        │Signal   │      │
│    │Collector│        │Analyzer │        │Scorer   │      │
│    │  层     │        │  层     │        │  层     │      │
│    └────┬────┘        └────┬────┘        └────┬────┘      │
│         │                   │                   │           │
│         └───────────────────┴───────────────────┘           │
│                             │                                │
│                    ┌────────▼────────┐                       │
│                    │   SQLite DB     │                       │
│                    │ ┌──────────────┐│                       │
│                    │ │raw_data      ││                       │
│                    │ │signals       ││                       │
│                    │ │outcomes      ││                       │
│                    │ └──────────────┘│                       │
│                    └─────────────────┘                       │
└─────────────────────────────────────────────────────────────┘
         │                   │                   │
         │                   │                   │
    ┌────▼────┐        ┌────▼────┐        ┌────▼────┐
    │Binance  │        │CoinGlass│        │RSS/News │
    │WebSocket│        │  API    │        │  Feeds  │
    └─────────┘        └─────────┘        └─────────┘
    
         │                                        │
         │                                        │
    ┌────▼────────────────────────────────────────▼────┐
    │    User's LLM API (Gemini/Claude/GPT)            │
    │    (用户配置自己的API Key，本地调用)              │
    └──────────────────────────────────────────────────┘
```

### 2.2 数据流设计（借鉴橙子的双池模型）

**两个处理管道：**

#### 🔴 实时信号池（Fast Lane）
```
触发条件: 极端异常事件
更新频率: WebSocket实时 + 5分钟批处理
数据源:   Binance资金费率、CoinGlass爆仓
处理逻辑: 规则触发 → 快速LLM验证(可选) → 生成信号
时效性:   < 5分钟延迟
用途:     DSL中实时查询、UI弹窗告警
```

#### 🟡 趋势信号池（Slow Lane）
```
触发条件: 深度内容分析
更新频率: 每30-60分钟
数据源:   RSS新闻、社交媒体、宏观数据
处理逻辑: 批量采集 → LLM深度分析 → 打分排序 → 生成信号
时效性:   < 1小时延迟
用途:     调整策略参数、中长期持仓决策
```

### 2.3 关键组件说明

| 组件 | 职责 | 技术选型 |
|------|------|---------|
| **DataCollector** | 采集原始数据 | Binance API, CoinGlass API, FeedParser |
| **LLMAnalyzer** | 调用LLM分析 | Gemini Flash (主), Claude Haiku (辅) |
| **SignalScorer** | 信号打分排序 | 本地算法 + LLM打分 |
| **IntelligenceService** | 统一对外接口 | C# Service |
| **IntelligenceRepository** | 数据库访问 | SQLite + DBHelper |
| **DSL Functions** | Prophet.Core扩展 | C++ (binding到Python) |

---

## 3. 数据库设计

### 3.1 表结构

#### `intelligence_raw_data` - 原始数据层

```sql
CREATE TABLE intelligence_raw_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source VARCHAR(50) NOT NULL,           -- 'binance', 'coinglass', 'rss_coindesk'
    category VARCHAR(50) NOT NULL,         -- 'funding_rate', 'liquidation', 'news'
    symbol VARCHAR(20),                    -- 'BTCUSDT', 'ETHUSDT', NULL(全市场)
    content TEXT NOT NULL,                 -- 原始内容JSON
    metadata TEXT,                         -- 额外信息JSON
    collected_at DATETIME NOT NULL,        -- 采集时间
    processed BOOLEAN DEFAULT 0,           -- 是否已处理
    process_batch_id INTEGER,              -- 处理批次ID
    
    INDEX idx_source_category (source, category),
    INDEX idx_collected_time (collected_at),
    INDEX idx_processed (processed)
);
```

**字段说明：**
- `content`: JSON格式存储，例如：
  ```json
  {
    "funding_rate": 0.0015,
    "timestamp": "2025-12-31T10:00:00Z",
    "predicted_rate": 0.0014
  }
  ```
- `metadata`: 证据链、API响应等

#### `intelligence_signals` - 情报信号层（核心表）

```sql
CREATE TABLE intelligence_signals (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- 基本信息
    timestamp DATETIME NOT NULL,           -- 信号生成时间
    symbol VARCHAR(20) NOT NULL,           -- 'BTCUSDT', 'ETHUSDT', 'BTC'
    category VARCHAR(50) NOT NULL,         -- 见下方分类
    pool VARCHAR(20) NOT NULL,             -- 'realtime', 'trend'
    
    -- 方向与强度
    direction VARCHAR(10) NOT NULL,        -- 'long', 'short', 'neutral'
    strength DECIMAL(6,2) NOT NULL,        -- -100 to +100
    confidence DECIMAL(3,2) NOT NULL,      -- 0.00 to 1.00
    urgency VARCHAR(10) NOT NULL,          -- 'critical', 'high', 'medium', 'low'
    
    -- 描述信息
    title VARCHAR(200) NOT NULL,           -- 一句话总结
    reason TEXT NOT NULL,                  -- LLM生成的分析逻辑
    impact_assessment TEXT,                -- 预期影响
    
    -- 可追溯性
    evidence_links TEXT,                   -- JSON数组: ["url1", "url2"]
    raw_data_ids TEXT,                     -- JSON数组: [123, 456]
    
    -- 量化字段（供DSL使用）
    time_sensitivity INTEGER NOT NULL,     -- 时效性（秒），如14400=4小时
    price_target DECIMAL(10,2),            -- 目标价位（可选）
    stop_loss DECIMAL(10,2),               -- 止损建议（可选）
    
    -- 质量控制
    llm_model VARCHAR(50),                 -- 'gemini-flash-1.5', 'claude-haiku'
    generation_method VARCHAR(50),         -- 'rule_based', 'llm_analyzed', 'hybrid'
    process_batch_id INTEGER,
    
    -- 生命周期
    created_at DATETIME NOT NULL,
    expires_at DATETIME,                   -- 信号失效时间
    is_active BOOLEAN DEFAULT 1,
    
    -- 用户反馈
    user_feedback VARCHAR(20),             -- 'useful', 'noise', 'misleading', NULL
    user_feedback_note TEXT,
    feedback_time DATETIME,
    
    INDEX idx_symbol_time (symbol, timestamp),
    INDEX idx_category_pool (category, pool),
    INDEX idx_active (is_active, timestamp),
    INDEX idx_urgency (urgency, timestamp)
);
```

**信号分类 (category)：**
- `funding_extreme` - 极端资金费率
- `liquidation_cascade` - 爆仓潮
- `whale_movement` - 巨鲸异动
- `exchange_flow` - 交易所资金流
- `macro_event` - 宏观事件（美联储、监管）
- `sentiment_shift` - 情绪转向
- `technical_divergence` - 技术与情报背离
- `news_catalyst` - 新闻催化剂

#### `intelligence_processing_batches` - 处理批次记录

```sql
CREATE TABLE intelligence_processing_batches (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    pool VARCHAR(20) NOT NULL,             -- 'realtime', 'trend'
    batch_start_time DATETIME NOT NULL,
    batch_end_time DATETIME,
    
    -- 统计信息
    raw_data_count INTEGER,
    signals_generated INTEGER,
    signals_discarded INTEGER,
    
    -- 成本追踪
    llm_model VARCHAR(50),
    llm_tokens_used INTEGER,
    llm_cost_usd DECIMAL(10,4),
    
    -- 性能指标
    processing_duration_ms INTEGER,
    average_latency_ms INTEGER,
    
    -- 状态
    status VARCHAR(20),                    -- 'processing', 'completed', 'failed'
    error_message TEXT,
    
    INDEX idx_time (batch_start_time)
);
```

#### `intelligence_signal_outcomes` - 信号效果跟踪

```sql
CREATE TABLE intelligence_signal_outcomes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    signal_id INTEGER NOT NULL,
    
    -- 评估时间点
    evaluation_time DATETIME NOT NULL,     -- 信号发出后N小时
    hours_after_signal INTEGER NOT NULL,   -- 4, 12, 24, 48
    
    -- 价格变化
    price_at_signal DECIMAL(10,2),
    price_at_evaluation DECIMAL(10,2),
    price_change_pct DECIMAL(8,4),
    
    -- 方向正确性
    direction_correct BOOLEAN,
    magnitude_score DECIMAL(3,2),          -- 0-1, 涨跌幅度评分
    
    -- 交易记录
    user_traded BOOLEAN DEFAULT 0,
    trade_result VARCHAR(20),              -- 'profit', 'loss', 'breakeven'
    trade_pnl_pct DECIMAL(8,4),
    
    -- 备注
    notes TEXT,
    created_at DATETIME NOT NULL,
    
    FOREIGN KEY (signal_id) REFERENCES intelligence_signals(id),
    INDEX idx_signal (signal_id),
    INDEX idx_eval_time (evaluation_time)
);
```

#### `intelligence_config` - 配置表

```sql
CREATE TABLE intelligence_config (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    config_key VARCHAR(100) UNIQUE NOT NULL,
    config_value TEXT NOT NULL,            -- JSON格式
    description TEXT,
    updated_at DATETIME NOT NULL,
    
    INDEX idx_key (config_key)
);

-- 预设配置项
INSERT INTO intelligence_config (config_key, config_value, description) VALUES
('llm_api_keys', '{"gemini": "", "claude": "", "openai": ""}', 'LLM API密钥'),
('data_sources', '{"binance": true, "coinglass": true, "rss": false}', '启用的数据源'),
('signal_filters', '{"min_confidence": 0.6, "min_strength": 30}', '信号过滤阈值'),
('processing_schedule', '{"realtime_interval": 300, "trend_interval": 3600}', '处理频率（秒）'),
('notification_settings', '{"critical_only": true, "telegram_bot_token": ""}', '通知设置');
```

---

## 4. DSL函数设计

### 4.1 Prophet.Core 新增函数

#### 基础查询函数

```cpp
// 获取信号强度（最常用）
double GetIntelligenceStrength(string category, string symbol, int lookback_seconds = 3600);
// 返回: -100 to +100, 0表示无信号
// 示例: GetIntelligenceStrength("funding_extreme", "BTCUSDT", 1800)

// 获取信号置信度
double GetIntelligenceConfidence(string category, string symbol, int lookback_seconds = 3600);
// 返回: 0.0 to 1.0

// 获取信号方向
int GetIntelligenceDirection(string category, string symbol, int lookback_seconds = 3600);
// 返回: 1=long, -1=short, 0=neutral/none

// 检查是否有紧急信号
bool HasCriticalSignal(string symbol, int lookback_seconds = 1800);
// 返回: true表示有urgency=critical的信号
```

#### 高级查询函数

```cpp
// 获取最新信号详情（返回JSON字符串）
string GetLatestIntelligence(string symbol, string category = "", string pool = "");
// 返回JSON示例:
// {
//   "timestamp": "2025-12-31T10:30:00Z",
//   "category": "funding_extreme",
//   "direction": "short",
//   "strength": -68,
//   "confidence": 0.78,
//   "title": "BTC资金费率持续极端正值",
//   "reason": "连续3小时维持0.12%以上..."
// }

// 获取指定时间范围内的信号数量
int CountIntelligenceSignals(string symbol, string category, int lookback_seconds, double min_confidence = 0.6);

// 获取信号平均强度（用于趋势判断）
double GetAverageIntelligenceStrength(string symbol, string category, int lookback_seconds);

// 检查信号一致性（多个category是否同向）
bool IsIntelligenceAligned(string symbol, vector<string> categories, int lookback_seconds);
// 示例: IsIntelligenceAligned("BTCUSDT", ["funding_extreme", "whale_movement"], 3600)
```

#### 组合信号函数

```cpp
// 计算综合情报评分
double GetCompositeIntelligenceScore(string symbol, map<string, double> weights, int lookback_seconds);
// 示例:
// GetCompositeIntelligenceScore("BTCUSDT", 
//   {"funding_extreme": 0.4, "whale_movement": 0.3, "sentiment_shift": 0.3}, 
//   3600)
```

### 4.2 DSL使用示例

#### 示例1: 基础应用

```python
def MyStrategy():
    # 获取情报信号
    funding_strength = GetIntelligenceStrength("funding_extreme", "BTCUSDT", 1800)
    funding_confidence = GetIntelligenceConfidence("funding_extreme", "BTCUSDT", 1800)
    
    # 获取技术指标
    rsi = RSI(14)
    
    # 综合判断
    if funding_strength < -50 and funding_confidence > 0.7 and rsi < 35:
        Buy()
        SetStopLoss(0.03)  # 3%止损
    
    # 紧急信号立即平仓
    if HasCriticalSignal("BTCUSDT", 900):  # 15分钟内
        CloseAll()
        Log("检测到紧急信号，已平仓")
```

#### 示例2: 多维度情报融合

```python
def AdvancedStrategy():
    symbol = "BTCUSDT"
    
    # 定义各类情报权重
    weights = {
        "funding_extreme": 0.35,
        "liquidation_cascade": 0.25,
        "whale_movement": 0.20,
        "sentiment_shift": 0.20
    }
    
    # 计算综合评分
    intel_score = GetCompositeIntelligenceScore(symbol, weights, 3600)
    
    # 检查信号一致性
    is_aligned = IsIntelligenceAligned(symbol, 
        ["funding_extreme", "whale_movement"], 
        3600)
    
    # 技术指标
    macd_hist = MACD_Hist()
    
    # 只在情报与技术同向且一致性高时交易
    if intel_score > 40 and is_aligned and macd_hist > 0:
        size = intel_score / 100 * 0.5  # 根据信号强度调整仓位
        Buy(size=size)
    elif intel_score < -40 and is_aligned and macd_hist < 0:
        size = abs(intel_score) / 100 * 0.5
        Sell(size=size)
```

#### 示例3: 信号详情查询

```python
def IntelligenceAwareStrategy():
    symbol = "BTCUSDT"
    
    # 获取最新情报详情
    intel_json = GetLatestIntelligence(symbol, "funding_extreme", "realtime")
    intel = ParseJSON(intel_json)  # 假设有JSON解析函数
    
    if intel["confidence"] > 0.8:
        Log(f"高置信度信号: {intel['title']}")
        Log(f"分析: {intel['reason']}")
        
        if intel["direction"] == "short":
            Sell()
    
    # 统计信号数量（用于过滤噪音）
    signal_count = CountIntelligenceSignals(symbol, "whale_movement", 3600, 0.7)
    if signal_count >= 3:  # 1小时内至少3个高置信度信号
        Log("检测到持续巨鲸活动")
        # 采取相应策略...
```

### 4.3 回测兼容性

**关键原则：避免"未来函数"**

```cpp
// 在回测模式下，必须使用历史时间
double GetIntelligenceStrength(
    string category, 
    string symbol, 
    int lookback_seconds,
    DateTime? backtest_time = null  // 回测时传入当前bar时间
) {
    DateTime query_time = backtest_time ?? DateTime.Now;
    
    // 只查询 query_time 之前的信号
    // WHERE timestamp <= query_time AND timestamp >= query_time - lookback_seconds
    
    return CalculateStrength(...);
}
```

---

## 5. 核心服务设计

### 5.1 IntelligenceService (总调度器)

```csharp
// Prophet.Client/Services/Intelligence/IntelligenceService.cs
public class IntelligenceService
{
    private readonly IntelligenceRepository _repository;
    private readonly LLMAnalyzer _llmAnalyzer;
    private readonly SignalScorer _signalScorer;
    private readonly List<IDataCollector> _collectors;
    private Timer _realtimeTimer;
    private Timer _trendTimer;
    
    public IntelligenceService()
    {
        // 初始化数据采集器（按Phase顺序）
        _collectors = new List<IDataCollector>
        {
            new FundingRateCollector(),      // Phase 0
            new LiquidationCollector(),      // Phase 1
            new NewsRSSCollector()           // Phase 2
        };
    }
    
    // 启动服务
    public void Start()
    {
        // 实时池：每5分钟
        _realtimeTimer = new Timer(ProcessRealtimePool, null, 0, 300000);
        
        // 趋势池：每30分钟
        _trendTimer = new Timer(ProcessTrendPool, null, 0, 1800000);
    }
    
    // 实时池处理
    private async void ProcessRealtimePool(object state)
    {
        var batch = await CreateProcessingBatch("realtime");
        
        try
        {
            // 1. 采集数据
            var rawData = await CollectRealtimeData();
            
            // 2. 规则过滤（快速通道）
            var candidates = FilterByRules(rawData);
            
            // 3. LLM增强（可选，仅高优先级）
            var signals = await GenerateSignals(candidates, batch.Id, useRules: true);
            
            // 4. 存储
            await _repository.SaveSignals(signals);
            
            // 5. 推送通知
            NotifyCriticalSignals(signals);
            
            batch.Complete(signals.Count);
        }
        catch (Exception ex)
        {
            batch.Fail(ex.Message);
        }
    }
    
    // 趋势池处理（深度LLM分析）
    private async void ProcessTrendPool(object state)
    {
        var batch = await CreateProcessingBatch("trend");
        
        try
        {
            // 1. 采集批量数据
            var rawData = await CollectTrendData();
            
            // 2. 去重、聚合
            var deduplicated = DeduplicateData(rawData);
            
            // 3. LLM批量分析
            var signals = await _llmAnalyzer.AnalyzeBatch(deduplicated, batch.Id);
            
            // 4. 打分排序
            signals = _signalScorer.ScoreAndRank(signals);
            
            // 5. 存储
            await _repository.SaveSignals(signals);
            
            batch.Complete(signals.Count);
        }
        catch (Exception ex)
        {
            batch.Fail(ex.Message);
        }
    }
    
    // DSL查询接口
    public double GetIntelligenceStrength(string category, string symbol, int lookbackSeconds, DateTime? backtestTime = null)
    {
        return _repository.GetLatestSignalStrength(category, symbol, lookbackSeconds, backtestTime);
    }
}
```

### 5.2 数据采集器架构

```csharp
// Prophet.Client/Services/Intelligence/Collectors/IDataCollector.cs
public interface IDataCollector
{
    string Source { get; }
    string[] SupportedCategories { get; }
    string Pool { get; }  // "realtime" or "trend"
    
    Task<List<RawData>> CollectAsync();
    bool IsEnabled();
}

// Phase 0: 资金费率采集器
public class FundingRateCollector : IDataCollector
{
    public string Source => "binance";
    public string[] SupportedCategories => new[] { "funding_rate" };
    public string Pool => "realtime";
    
    public async Task<List<RawData>> CollectAsync()
    {
        // 调用Binance API
        var response = await BinanceAPI.GetFundingRate("BTCUSDT");
        
        return new List<RawData>
        {
            new RawData
            {
                Source = Source,
                Category = "funding_rate",
                Symbol = "BTCUSDT",
                Content = JsonSerializer.Serialize(response),
                CollectedAt = DateTime.UtcNow
            }
        };
    }
}
```

### 5.3 LLM分析器

```csharp
// Prophet.Client/Services/Intelligence/Processing/LLMAnalyzer.cs
public class LLMAnalyzer
{
    private readonly IConfiguration _config;
    private string _geminiApiKey;
    private string _claudeApiKey;
    
    public async Task<List<IntelligenceSignal>> AnalyzeBatch(List<RawData> batch, int batchId)
    {
        // 构建prompt
        var prompt = PromptTemplates.GetSignalAnalysisPrompt(batch);
        
        // 调用LLM（优先Gemini Flash，便宜）
        string response;
        try
        {
            response = await CallGeminiFlash(prompt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Gemini调用失败，切换到Claude: {ex.Message}");
            response = await CallClaudeHaiku(prompt);
        }
        
        // 解析JSON响应
        var result = JsonSerializer.Deserialize<LLMAnalysisResult>(response);
        
        // 转换为IntelligenceSignal对象
        var signals = new List<IntelligenceSignal>();
        foreach (var s in result.Signals)
        {
            signals.Add(new IntelligenceSignal
            {
                Timestamp = DateTime.UtcNow,
                Symbol = s.Symbol,
                Category = s.Category,
                Direction = s.Direction,
                Strength = s.Strength,
                Confidence = s.Confidence,
                Urgency = s.Urgency,
                Title = s.Title,
                Reason = s.Reason,
                ImpactAssessment = s.ImpactAssessment,
                LlmModel = "gemini-flash-1.5",
                GenerationMethod = "llm_analyzed",
                ProcessBatchId = batchId,
                CreatedAt = DateTime.UtcNow
            });
        }
        
        return signals;
    }
    
    private async Task<string> CallGeminiFlash(string prompt)
    {
        // 实现Gemini API调用
        // 使用用户配置的API Key
        var client = new HttpClient();
        // ... API调用逻辑
        return await response;
    }
}
```

### 5.4 信号打分器（借鉴橙子的打分逻辑）

```csharp
// Prophet.Client/Services/Intelligence/Processing/SignalScorer.cs
public class SignalScorer
{
    public List<IntelligenceSignal> ScoreAndRank(List<IntelligenceSignal> signals)
    {
        foreach (var signal in signals)
        {
            // 1. 数据源可信度 (0-1)
            double sourceWeight = GetSourceCredibility(signal.Source);
            
            // 2. 时效性衰减 (0-1)
            double ageMinutes = (DateTime.UtcNow - signal.Timestamp).TotalMinutes;
            double timeFactor = Math.Exp(-ageMinutes / 60.0);  // 1小时半衰期
            
            // 3. 异常强度 (0-1)
            double anomalyScore = Math.Abs(signal.Strength) / 100.0;
            
            // 4. 历史准确率 (0-1)
            double historicalAccuracy = GetCategoryHistoricalAccuracy(signal.Category, signal.Symbol);
            
            // 5. 市场相关性 (0-1)
            double marketRelevance = IsMarketHours() ? 1.0 : 0.6;
            
            // 综合评分
            signal.CompositeScore = 
                sourceWeight * 0.25 +
                timeFactor * 0.30 +
                anomalyScore * 0.25 +
                historicalAccuracy * 0.15 +
                marketRelevance * 0.05;
        }
        
        return signals.OrderByDescending(s => s.CompositeScore).ToList();
    }
    
    private double GetSourceCredibility(string source)
    {
        return source switch
        {
            "binance" => 1.0,
            "coinglass" => 0.9,
            "coindesk" => 0.85,
            "twitter" => 0.6,
            _ => 0.5
        };
    }
    
    private double GetCategoryHistoricalAccuracy(string category, string symbol)
    {
        // 从intelligence_signal_outcomes表查询历史准确率
        var outcomes = _repository.GetOutcomesByCategory(category, symbol, days: 30);
        if (outcomes.Count == 0) return 0.5;  // 默认50%
        
        var correctCount = outcomes.Count(o => o.DirectionCorrect);
        return (double)correctCount / outcomes.Count;
    }
}
```

---

## 6. Prompt模板设计

### 6.1 批量信号分析Prompt

```csharp
// Prophet.Client/Services/Intelligence/Processing/PromptTemplates.cs
public static class PromptTemplates
{
    public static string GetSignalAnalysisPrompt(List<RawData> batch)
    {
        var dataSection = FormatRawDataForPrompt(batch);
        
        return $@"
你是一个专业的加密货币交易情报分析师。请分析以下市场数据，生成结构化的交易信号。

## 分析数据
{dataSection}

## 输出要求
以JSON格式输出，结构如下：
```json
{{
  ""signals"": [
    {{
      ""symbol"": ""BTCUSDT"",
      ""category"": ""funding_extreme"",
      ""direction"": ""short"",
      ""strength"": -68,
      ""confidence"": 0.78,
      ""urgency"": ""high"",
      ""title"": ""BTC资金费率持续极端正值，多头过热"",
      ""reason"": ""过去3小时资金费率维持在0.12%以上，远超历史均值（0.01%），通常预示着多头过度拥挤，短期内可能出现多头挤兑和价格回调"",
      ""impact_assessment"": ""短期（4-12小时内）可能出现3-5%的价格回调，建议减仓或做空"",
      ""time_sensitivity"": 14400,
      ""raw_data_ids"": [1, 2, 3]
    }}
  ],
  ""analysis_summary"": ""市场整体呈现多头过热迹象...""
}}
```

## 分析标准
1. **置信度阈值**：只生成confidence ≥ 0.6的信号（低置信度的直接丢弃）
2. **逻辑推理**：reason字段必须包含清晰的因果逻辑，避免模糊表述
3. **方向明确**：direction必须是long/short/neutral之一，strength范围-100到+100
4. **时效性标注**：
   - critical: 事件影响<1小时
   - high: 1-6小时
   - medium: 6-24小时
   - low: >24小时
5. **信号合并**：相同方向、相同类别的信号可以合并为一条
6. **可追溯性**：raw_data_ids必须对应输入数据的ID
7. **避免幻觉**：不要编造数据中不存在的信息

## 类别定义
- funding_extreme: 资金费率异常（>0.1%或<-0.05%）
- liquidation_cascade: 大规模爆仓（1h内>$100M）
- whale_movement: 巨鲸转账（单笔>500 BTC或等值）
- sentiment_shift: 市场情绪突变
- macro_event: 宏观事件（美联储、监管、ETF）
- news_catalyst: 重大新闻催化
";
    }
    
    private static string FormatRawDataForPrompt(List<RawData> batch)
    {
        var sb = new StringBuilder();
        foreach (var data in batch)
        {
            sb.AppendLine($"### 数据{data.Id} - {data.Source}/{data.Category}");
            sb.AppendLine($"时间: {data.CollectedAt:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine($"币种: {data.Symbol}");
            sb.AppendLine($"内容: {data.Content}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
```

### 6.2 单信号验证Prompt（快速通道）

```csharp
public static string GetQuickValidationPrompt(RawData data)
{
    return $@"
请快速判断以下市场事件是否值得生成交易信号。

## 数据
类别: {data.Category}
币种: {data.Symbol}
内容: {data.Content}

## 判断标准
- 是否属于异常事件？（偏离常态）
- 是否有交易价值？（可操作性）
- 方向是否明确？（多/空/中性）

## 输出格式（JSON）
```json
{{
  ""is_valid"": true,
  ""direction"": ""short"",
  ""confidence"": 0.75,
  ""one_sentence_reason"": ""资金费率达到极端水平，历史上通常会回调""
}}
```

只需输出JSON，不要额外解释。
";
}
```

---

## 7. UI设计

### 7.1 IntelligenceView.axaml 布局

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Prophet.Client.Views.IntelligenceView">
    
    <Grid RowDefinitions="Auto,*,Auto">
        
        <!-- 顶部工具栏 -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="10">
            <TextBlock Text="情报中心" FontSize="20" FontWeight="Bold"/>
            <Button Content="⚙️ 配置" Click="OnConfigClick" Margin="20,0,0,0"/>
            <Button Content="🔄 刷新" Click="OnRefreshClick" Margin="10,0,0,0"/>
            <ComboBox x:Name="SymbolFilter" Width="120" Margin="10,0,0,0">
                <ComboBoxItem Content="全部币种"/>
                <ComboBoxItem Content="BTCUSDT"/>
                <ComboBoxItem Content="ETHUSDT"/>
                <ComboBoxItem Content="SOLUSDT"/>
            </ComboBox>
            <ComboBox x:Name="PoolFilter" Width="100" Margin="10,0,0,0">
                <ComboBoxItem Content="全部"/>
                <ComboBoxItem Content="实时池"/>
                <ComboBoxItem Content="趋势池"/>
            </ComboBox>
        </StackPanel>
        
        <!-- 主内容区：三栏布局 -->
        <Grid Grid.Row="1" ColumnDefinitions="1*,1.5*,1*">
            
            <!-- 左栏：原始数据源 -->
            <Border Grid.Column="0" BorderBrush="Gray" BorderThickness="1" Margin="5">
                <StackPanel>
                    <TextBlock Text="📰 原始数据源" FontWeight="Bold" Margin="10,10,10,5"/>
                    <ListBox x:Name="RawDataList" Height="600">
                        <!-- 数据绑定到RawData列表 -->
                    </ListBox>
                </StackPanel>
            </Border>
            
            <!-- 中栏：LLM分析与推理 -->
            <Border Grid.Column="1" BorderBrush="Gray" BorderThickness="1" Margin="5">
                <StackPanel>
                    <TextBlock Text="🤖 LLM分析推理" FontWeight="Bold" Margin="10,10,10,5"/>
                    <ScrollViewer Height="600">
                        <StackPanel x:Name="AnalysisPanel">
                            <!-- 动态显示选中信号的分析过程 -->
                        </StackPanel>
                    </ScrollViewer>
                </StackPanel>
            </Border>
            
            <!-- 右栏：生成的量化信号 -->
            <Border Grid.Column="2" BorderBrush="Gray" BorderThickness="1" Margin="5">
                <StackPanel>
                    <TextBlock Text="📊 量化信号" FontWeight="Bold" Margin="10,10,10,5"/>
                    <ListBox x:Name="SignalList" Height="600" SelectionChanged="OnSignalSelected">
                        <!-- 数据绑定到IntelligenceSignal列表 -->
                    </ListBox>
                </StackPanel>
            </Border>
            
        </Grid>
        
        <!-- 底部状态栏 -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" Background="#F0F0F0" Padding="10">
            <TextBlock Text="数据源状态: "/>
            <TextBlock x:Name="StatusBinance" Text="🟢 Binance" Margin="5,0"/>
            <TextBlock x:Name="StatusCoinGlass" Text="🟢 CoinGlass" Margin="5,0"/>
            <TextBlock x:Name="StatusRSS" Text="🔴 RSS (未启用)" Margin="5,0"/>
            <TextBlock Text=" | " Margin="10,0"/>
            <TextBlock x:Name="LastUpdateTime" Text="最后更新: 2分钟前"/>
            <TextBlock Text=" | " Margin="10,0"/>
            <TextBlock x:Name="TodayCost" Text="今日API成本: $0.23"/>
        </StackPanel>
        
    </Grid>
    
</UserControl>
```

### 7.2 信号详情弹窗

当用户点击信号时，弹出详细信息窗口：

```
┌──────────────────────────────────────────┐
│  信号详情 - BTC资金费率极端                │
├──────────────────────────────────────────┤
│                                          │
│  🔴 实时池 | 紧急程度: High               │
│  生成时间: 2025-12-31 10:30:22           │
│  失效时间: 2025-12-31 14:30:22 (4小时)   │
│                                          │
│  方向: SHORT                             │
│  强度: -68 / 100                         │
│  置信度: 78%                             │
│                                          │
│  ───────────────────────────────        │
│  💡 分析逻辑                             │
│  ───────────────────────────────        │
│  过去3小时BTC资金费率维持在0.12%以上，    │
│  远超历史均值（0.01%）。这种极端正值      │
│  通常表示多头过度拥挤，历史数据显示，     │
│  82%的情况下会在8小时内出现3-5%的回调。   │
│                                          │
│  ───────────────────────────────        │
│  📈 预期影响                             │
│  ───────────────────────────────        │
│  短期（4-12小时）可能出现3-5%价格回调，   │
│  建议减仓或考虑做空机会。                │
│                                          │
│  ───────────────────────────────        │
│  🔗 证据链接                             │
│  ───────────────────────────────        │
│  • Binance资金费率API [查看]            │
│  • 历史数据对比 [查看]                   │
│                                          │
│  ───────────────────────────────        │
│  📊 历史表现                             │
│  ───────────────────────────────        │
│  该类信号过去30天触发45次                │
│  方向正确: 37次 (82%)                    │
│  平均涨跌幅: -4.2%                       │
│                                          │
│  ───────────────────────────────        │
│  💬 您的反馈                             │
│  ───────────────────────────────        │
│  [👍 有用] [👎 噪音] [⚠️ 误导]           │
│  备注: [文本框]                          │
│                                          │
│  [应用到当前策略] [关闭]                  │
└──────────────────────────────────────────┘
```

### 7.3 配置界面

```
┌──────────────────────────────────────────┐
│  情报模块配置                             │
├──────────────────────────────────────────┤
│                                          │
│  🔑 LLM API密钥                          │
│  ─────────────────────────────          │
│  Gemini Flash: [****************] ✅     │
│  Claude Haiku: [****************] ✅     │
│  OpenAI GPT-4o mini: [未配置]     ⚠️     │
│                                          │
│  📊 数据源启用                            │
│  ─────────────────────────────          │
│  ☑ Binance (资金费率、Open Interest)     │
│  ☑ CoinGlass (爆仓数据)                  │
│  ☐ RSS新闻 (需要LLM分析)                 │
│  ☐ Twitter/X (高级功能)                  │
│                                          │
│  ⚙️ 处理频率                             │
│  ─────────────────────────────          │
│  实时池: [5] 分钟                        │
│  趋势池: [30] 分钟                       │
│                                          │
│  🎚️ 信号过滤                             │
│  ─────────────────────────────          │
│  最低置信度: [0.6] (60%)                 │
│  最低强度: [30] / 100                    │
│                                          │
│  🔔 通知设置                             │
│  ─────────────────────────────          │
│  ☑ 仅推送Critical级别信号                │
│  ☐ 启用声音提醒                          │
│  Telegram Bot Token: [可选]             │
│                                          │
│  💰 成本控制                             │
│  ─────────────────────────────          │
│  每日最大API调用次数: [500]              │
│  每日预算上限: $[5.00]                   │
│  当前月度花费: $12.34 / $50.00           │
│                                          │
│  [保存配置] [重置为默认] [取消]           │
└──────────────────────────────────────────┘
```

---

## 8. 成本估算

### 8.1 LLM API成本（基于用户自付模式）

**Gemini Flash 1.5（推荐主力）**
- 输入: $0.075 / 1M tokens
- 输出: $0.30 / 1M tokens
- 免费额度: 1500次请求/天（足够初期使用）

**典型场景成本：**

| 场景 | 频率 | 单次tokens | 每日成本 |
|------|------|-----------|----------|
| 实时池（规则为主） | 5分钟 | 1000 in + 200 out | $0.015 |
| 趋势池（LLM分析） | 30分钟 | 5000 in + 1000 out | $0.096 |
| **每日总计** | - | - | **$0.111** |
| **每月总计** | - | - | **$3.33** |

**成本优化策略：**
1. 利用免费额度（Gemini每天1500次）
2. 实时池优先规则判断（90%场景不调LLM）
3. 批处理（20条数据一起分析，省tokens）
4. 缓存相似内容（避免重复分析）

### 8.2 数据源API成本

| 数据源 | 成本 | 备注 |
|--------|------|------|
| Binance API | 免费 | 有rate limit |
| CoinGlass API | 免费 (基础) | 高级功能付费 |
| RSS Feeds | 免费 | - |
| Whale Alert | 免费 (部分) | 完整数据付费 |

**总计：数据源成本≈$0/月（初期）**

### 8.3 存储成本

SQLite本地存储，几乎无成本。

**预估数据量：**
- 每天300条raw_data（约1MB）
- 每天50条signals（约200KB）
- 每月总计：30MB raw + 6MB signals ≈ **36MB/月**

**清理策略：**
- raw_data保留30天（自动删除）
- signals永久保留（用于学习）

---

## 9. 风险评估与应对

### 9.1 技术风险

| 风险 | 概率 | 影响 | 应对措施 |
|------|------|------|---------|
| **LLM幻觉/误判** | 中 | 高 | 1. 强制附证据链<br>2. 多模型交叉验证<br>3. 置信度阈值过滤(>0.6) |
| **API限额/超支** | 中 | 中 | 1. 优先免费额度<br>2. 用户自付模式<br>3. 降频+缓存 |
| **数据源不稳定** | 低 | 中 | 1. 多源冗余<br>2. 降级方案（规则为主） |
| **延迟过大** | 中 | 中 | 1. WebSocket实时数据<br>2. 规则快速通道<br>3. 异步处理 |
| **回测未来函数** | 低 | 高 | 1. 强制时间参数<br>2. 单元测试覆盖<br>3. 代码审查 |

### 9.2 业务风险

| 风险 | 概率 | 影响 | 应对措施 |
|------|------|------|---------|
| **垃圾信号淹没** | 高 | 高 | 1. 白名单机制<br>2. 三层过滤器<br>3. 用户反馈学习 |
| **信号价值低** | 中 | 高 | 1. 回测验证<br>2. 效果跟踪表<br>3. 持续优化 |
| **用户不会用** | 中 | 中 | 1. 提供DSL示例<br>2. 内置策略模板<br>3. 视频教程 |
| **数据隐私** | 低 | 中 | 1. 本地存储<br>2. API key加密<br>3. 明确隐私政策 |

### 9.3 应急预案

**场景1：LLM API完全不可用**
- 降级为纯规则模式
- 仍可生成基础信号（资金费率、爆仓）
- 功能降级但不影响核心可用性

**场景2：成本失控**
- 自动暂停LLM调用
- 弹窗提醒用户
- 切换到规则模式

**场景3：信号准确率低于50%**
- 自动标记为"实验性"
- 降低该类信号权重
- 通知用户暂时不建议使用

---

## 10. 开发路线图

### Phase 0: 技术验证（Week 1-2）

**目标：最小可行版本，验证架构可行性**

- [x] 任务1：数据库设计与Migration（2天）
  - 创建4张核心表
  - 编写初始化脚本
  - 单元测试

- [x] 任务2：FundingRateCollector实现（2天）
  - 调用Binance API
  - 存储到raw_data表
  - 定时采集测试

- [x] 任务3：规则生成信号（3天）
  - 简单阈值判断（不用LLM）
  - 生成signals表记录
  - DSL函数桩实现

- [x] 任务4：第一个DSL函数（2天）
  - Prophet.Core中实现`GetFundingRateSignal()`
  - 查询signals表
  - 回测兼容性测试

- [x] 任务5：UI基础框架（3天）
  - IntelligenceView.axaml创建
  - 简单列表展示
  - 配置界面

**验收标准：**
- ✅ 能自动采集Binance资金费率
- ✅ 触发极端值时自动生成信号
- ✅ DSL中可查询到信号强度
- ✅ UI能显示信号列表

---

### Phase 1: LLM集成（Week 3-5）

**目标：接入LLM，提升信号质量**

- [ ] 任务6：Gemini API集成（3天）
  - API客户端封装
  - Prompt模板
  - 错误处理与重试

- [ ] 任务7：批处理管道（3天）
  - 数据预处理（去重、聚合）
  - 批量调用LLM
  - JSON响应解析

- [ ] 任务8：SignalScorer实现（2天）
  - 多维度打分算法
  - 历史准确率统计
  - 排序与过滤

- [ ] 任务9：LiquidationCollector（3天）
  - CoinGlass API对接
  - 爆仓数据解析
  - 与资金费率联动分析

- [ ] 任务10：UI完善（3天）
  - 三栏布局实现
  - 信号详情弹窗
  - 实时更新

**验收标准：**
- ✅ LLM能分析资金费率+爆仓数据生成信号
- ✅ 信号包含清晰的reason和置信度
- ✅ UI能展示LLM推理过程
- ✅ 成本<$0.2/天

---

### Phase 2: 深度优化（Week 6-8）

**目标：闭环反馈、效果跟踪、多数据源**

- [ ] 任务11：NewsRSSCollector（4天）
  - RSS订阅源配置
  - 新闻内容提取
  - LLM情绪分析

- [ ] 任务12：信号效果跟踪（3天）
  - 自动记录outcomes
  - 准确率统计面板
  - 反馈学习机制

- [ ] 任务13：DSL函数扩展（3天）
  - 10+个查询函数
  - 组合信号支持
  - 完整文档

- [ ] 任务14：回测深度集成（3天）
  - 历史信号回溯
  - 效果对比报告
  - 参数优化建议

- [ ] 任务15：用户反馈循环（2天）
  - 信号标注功能
  - 偏好学习
  - 自动调整过滤器

**验收标准：**
- ✅ 支持3种数据源（资金费率、爆仓、新闻）
- ✅ 历史准确率可追溯
- ✅ 用户反馈能改善信号质量
- ✅ 回测中信号有明显增益

---

## 11. 成功指标（KPI）

### 11.1 技术指标

| 指标 | 目标值 | 测量方式 |
|------|--------|---------|
| 信号延迟 | <5分钟（实时池） | 事件发生到DSL可查询的时间差 |
| 信号生成率 | 每天10-30条 | signals表记录数 |
| LLM成本 | <$5/月/用户 | processing_batches表统计 |
| 信号准确率 | >65% | outcomes表direction_correct率 |
| 系统可用性 | >99% | 定时采集成功率 |

### 11.2 业务指标

| 指标 | 目标值 | 测量方式 |
|------|--------|---------|
| 用户采用率 | >30% | 使用情报DSL函数的策略占比 |
| 策略胜率提升 | +5% | 回测对比（加情报 vs 纯技术） |
| 夏普比率提升 | +0.2 | 回测对比 |
| 最大回撤降低 | -3% | 回测对比 |
| 用户反馈好评率 | >70% | "有用"标注占比 |

### 11.3 学习指标

| 指标 | 目标值 | 测量方式 |
|------|--------|---------|
| 噪音信号率 | <20% | "噪音"标注占比 |
| 信号去重率 | >90% | 相似信号自动合并率 |
| 历史准确率提升 | 每月+2% | outcomes表趋势分析 |

---

## 12. AI陪审团审查清单

### 12.1 需要Claude审查的问题

**请Claude扮演"资深量化架构师"，重点审查：**

1. **架构合理性**
   - 双池设计是否合理？
   - 数据流是否有瓶颈？
   - 服务解耦是否充分？

2. **回测风险**
   - 是否可能有未来函数？
   - 时间戳处理是否严谨？
   - 历史数据完整性如何保证？

3. **成本控制**
   - LLM成本估算是否准确？
   - 是否有失控风险？
   - 降级方案是否可行？

### 12.2 需要Gemini审查的问题

**请Gemini扮演"数据工程师"，重点审查：**

1. **数据库设计**
   - 表结构是否规范？
   - 索引是否合理？
   - 扩展性如何？

2. **数据质量**
   - 去重逻辑是否充分？
   - 脏数据如何处理？
   - 数据一致性如何保证？

3. **性能优化**
   - 查询是否会慢？
   - 是否需要缓存？
   - 存储增长如何控制？

### 12.3 需要GPT审查的问题

**请GPT扮演"产品经理"，重点审查：**

1. **用户体验**
   - DSL函数是否易用？
   - UI是否直观？
   - 新手能否快速上手？

2. **价值交付**
   - 核心价值是否清晰？
   - 功能优先级是否合理？
   - MVP是否足够小？

3. **风险应对**
   - 失败场景是否考虑全？
   - 应急预案是否可行？
   - 迭代策略是否灵活？

---

## 13. 下一步行动

### 立即行动（本周）

1. **AI陪审团互攻**（2天）
   - 将本PRD分别提问Claude/Gemini/GPT
   - 收集批评和建议
   - 修改PRD v1.1

2. **技术预研**（2天）
   - 注册Gemini API（免费）
   - 测试Binance WebSocket连接
   - 验证SQLite查询性能

3. **环境准备**（1天）
   - 创建`Prophet.Client/Services/Intelligence/`目录结构
   - 安装必要的NuGet包（HttpClient, System.Text.Json等）
   - 配置开发环境

### Phase 0启动检查清单

- [ ] PRD已通过AI陪审团审查
- [ ] 数据库Migration脚本已编写
- [ ] Gemini API测试成功
- [ ] Binance API测试成功
- [ ] 项目结构已创建
- [ ] 团队（或自己）已对齐目标

---

## 附录A：参考资料

1. **比特币橙子 Vibe Coding案例**
   - Twitter: @chengzi_95330
   - 核心方法论：需求>架构>编码（50%+25%+25%）
   - AI陪审团互攻验证

2. **Prophet现有架构**
   - `FundingFeeCalculator.cs`: 已有资金费率计算逻辑
   - `KlineCache.cs`: 可参考的缓存机制
   - `DataIntegrityChecker.cs`: 数据质量保障范例

3. **LLM API文档**
   - Gemini Flash: https://ai.google.dev/
   - Claude API: https://docs.anthropic.com/
   - OpenAI API: https://platform.openai.com/docs/

4. **数据源API**
   - Binance: https://binance-docs.github.io/apidocs/
   - CoinGlass: https://coinglass.com/
   - Whale Alert: https://whale-alert.io/

---

**PRD版本历史**
- v1.0 (2025-12-31): 初始版本，基于橙子方法论
- v1.1 (待定): AI陪审团审查后修订版

**文档维护者**：Prophet项目组  
**反馈渠道**：（待补充）

