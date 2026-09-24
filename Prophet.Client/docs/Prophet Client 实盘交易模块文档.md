# Prophet Client 实盘交易模块文档

## 📋 目录

- [概述](#概述)
- [架构设计](#架构设计)
- [核心组件](#核心组件)
- [多实盘架构](#多实盘架构)
- [使用指南](#使用指南)
- [API参考](#api参考)
- [数据库设计](#数据库设计)
- [配置说明](#配置说明)
- [最佳实践](#最佳实践)
- [故障排查](#故障排查)

---

## 概述

Prophet实盘交易模块是一个完整的量化交易执行系统，支持多交易所、多币种、多策略同时运行。该模块基于Prophet DSL策略引擎，提供了从策略编写、回测验证到实盘执行的完整解决方案。

### 主要特性

- ✅ **多交易所支持** - 币安（Binance）U本位合约，未来支持OKX、Bybit等
- ✅ **多实盘管理** - 支持同时运行多个独立的实盘实例
- ✅ **完整风险控制** - 8项风险检查、紧急停止、实时监控
- ✅ **策略复用** - 回测验证的策略可直接用于实盘
- ✅ **数据持久化** - SQLite存储配置和历史数据
- ✅ **实时监控** - WebSocket实时数据流、账户和仓位监控
- ✅ **跨实例统计** - 全局视图、交易所汇总、策略排行

### 系统要求

- .NET 8.0 或更高版本
- Windows 10/11 或 Linux
- 至少 2GB 可用内存
- 稳定的网络连接

---

## 架构设计

### 整体架构图

```
┌─────────────────────────────────────────────────────────────┐
│                   Prophet Client (Avalonia UI)               │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │          LiveTradingView (UI层)                        │ │
│  │  - 多标签页界面                                         │ │
│  │  - 实时监控                                             │ │
│  │  - 批量控制                                             │ │
│  └──────────────────────┬─────────────────────────────────┘ │
│                         │                                    │
│  ┌──────────────────────▼─────────────────────────────────┐ │
│  │      LiveTradingViewModel (ViewModel层)                │ │
│  │  - 数据绑定                                             │ │
│  │  - 命令处理                                             │ │
│  │  - 事件订阅                                             │ │
│  └──────────────────────┬─────────────────────────────────┘ │
└─────────────────────────┼───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│              TradingInstanceManager (核心层)                 │
│  - 实例生命周期管理                                          │
│  - 批量操作                                                  │
│  - 事件协调                                                  │
└──┬──────────────────┬──────────────────┬───────────────────┘
   │                  │                  │
┌──▼──────┐  ┌────────▼────────┐  ┌─────▼──────────────────┐
│Instance │  │Instance         │  │CrossInstance           │
│Storage  │  │Snapshot         │  │Statistics              │
└─────────┘  └─────────────────┘  └────────────────────────┘
   │
┌──▼────────────────────────────────────────────────────────┐
│     ConcurrentDictionary<Guid, LiveTradingEngine>         │
│              (运行中的交易引擎实例)                         │
└──┬────────────────────────────────────────────────────────┘
   │
┌──▼────────────────────────────────────────────────────────┐
│  LiveTradingEngine (交易引擎)                              │
│  ├─ WebSocket数据流                                        │
│  ├─ 订单管理 (LiveOrderManager)                            │
│  ├─ 风险控制 (RiskController)                              │
│  ├─ 策略生成器 (IStrategySignalGenerator)                  │
│  └─ 数据存储 (LiveOrderStorage)                            │
└──┬────────────────────────────────────────────────────────┘
   │
┌──▼────────────────────────────────────────────────────────┐
│            IExchange (交易所抽象层)                         │
│  ├─ BinanceExchange                                        │
│  │   ├─ BinanceRestClient (REST API)                      │
│  │   ├─ BinanceWebSocketClient (WebSocket)                │
│  │   └─ BinanceAuthHelper (签名认证)                       │
│  ├─ OKXExchange (未来)                                     │
│  └─ BybitExchange (未来)                                   │
└────────────────────────────────────────────────────────────┘
```

### 核心设计理念

1. **多实例隔离** - 每个实盘实例完全独立，互不影响
2. **风险优先** - 每个实例独立的风险控制，全局风险聚合
3. **策略复用** - 回测策略直接用于实盘，无需修改
4. **数据驱动** - 所有决策基于数据和指标，不依赖主观判断
5. **事件驱动** - 异步事件模型，高效响应市场变化

---

## 核心组件

### 1. TradingInstanceManager

**职责**: 管理所有实盘实例的生命周期

**文件**: `Prophet.Client/Trading/Engine/TradingInstanceManager.cs`

**主要方法**:

```csharp
// 实例管理
Task<Guid> CreateInstanceAsync(TradingInstanceConfig config)
Task<bool> StartInstanceAsync(Guid instanceId)
Task<bool> StopInstanceAsync(Guid instanceId)
Task<bool> PauseInstanceAsync(Guid instanceId)
Task<bool> ResumeInstanceAsync(Guid instanceId)
Task<bool> DeleteInstanceAsync(Guid instanceId)

// 批量操作
Task<int> StartAllAsync()
Task<int> StopAllAsync()
Task<int> StartByExchangeAsync(string exchange)
Task<int> StopByExchangeAsync(string exchange)

// 查询
List<TradingInstanceConfig> GetAllInstances()
TradingInstanceStatus GetInstanceStatus(Guid instanceId)
Task<TradingInstanceSnapshot?> GetInstanceSnapshotAsync(Guid instanceId)
```

**事件**:
- `InstanceCreated` - 实例创建
- `InstanceDeleted` - 实例删除
- `InstanceStatusChanged` - 状态变化
- `InstanceSnapshotUpdated` - 快照更新
- `InstanceError` - 错误发生

### 2. LiveTradingEngine

**职责**: 单个实盘实例的交易引擎

**文件**: `Prophet.Client/Trading/Engine/LiveTradingEngine.cs`

**核心功能**:
- WebSocket实时数据订阅
- 策略信号生成
- 风险检查
- 订单执行
- 状态同步

**属性**:
```csharp
public Guid InstanceId { get; set; }           // 实例ID
public string InstanceName { get; set; }       // 实例名称
public TradingEngineStatus Status { get; }     // 引擎状态
```

**方法**:
```csharp
Task StartAsync()                              // 启动引擎
Task StopAsync()                               // 停止引擎
Task PauseAsync()                              // 暂停交易
Task ResumeAsync()                             // 恢复交易
Task<RiskReport?> GetRiskReportAsync()         // 获取风险报告
```

### 3. RiskController

**职责**: 风险控制和监控

**文件**: `Prophet.Client/Trading/RiskControl/RiskController.cs`

**8项风险检查**:
1. 紧急停止检查
2. 最大持仓数量检查
3. 最大回撤检查
4. 单笔交易风险检查
5. 每日交易次数限制
6. 最小余额检查
7. 连续亏损检查
8. 仓位风险度检查

**主要方法**:
```csharp
Task<RiskCheckResult> CheckSignalAsync(Signal signal, decimal currentEquity)
Task TriggerEmergencyStopAsync(string reason)
void ResetEmergencyStop()
Task<RiskReport> GetRiskReportAsync()
void RecordTrade(Order order)
void UpdateEquity(decimal equity)
```

### 4. TradingInstanceStorage

**职责**: 数据持久化

**文件**: `Prophet.Client/Trading/Storage/TradingInstanceStorage.cs`

**数据库表**:
- `trading_instances` - 实例配置
- `instance_snapshots` - 状态快照历史

**主要方法**:
```csharp
Task SaveInstanceAsync(TradingInstanceConfig config)
Task<TradingInstanceConfig?> LoadInstanceAsync(Guid id)
Task<List<TradingInstanceConfig>> LoadAllInstancesAsync()
Task DeleteInstanceAsync(Guid id)
Task SaveSnapshotAsync(TradingInstanceSnapshot snapshot)
Task<List<TradingInstanceSnapshot>> GetSnapshotsAsync(Guid instanceId, DateTime from, DateTime to)
Task<int> CleanupOldSnapshotsAsync(int keepDays = 30)
```

### 5. CrossInstanceStatistics

**职责**: 跨实例统计聚合

**文件**: `Prophet.Client/Trading/Statistics/CrossInstanceStatistics.cs`

**主要方法**:
```csharp
Task<GlobalStatisticsSummary> GetGlobalSummaryAsync()
Task<List<ExchangeSummary>> GetExchangeSummariesAsync()
Task<List<StrategySummary>> GetStrategySummariesAsync()
Task<List<InstanceRanking>> GetInstanceRankingsAsync(RankingType rankingType)
```

---

## 多实盘架构

### 实例模型

Prophet支持细粒度的实例模型：**每个【交易所+交易对+策略】= 一个独立实例**

#### 示例场景

```
实例1: 币安-BTCUSDT-MACD策略
实例2: 币安-ETHUSDT-MACD策略
实例3: 币安-BTCUSDT-网格策略
实例4: OKX-BTCUSDT-MACD策略
```

以上4个实例可以同时运行，互不干扰。

### 实例配置

```csharp
public class TradingInstanceConfig
{
    public Guid Id { get; set; }                    // 唯一标识
    public string Name { get; set; }                // 名称
    public string Exchange { get; set; }            // 交易所
    public string Symbol { get; set; }              // 交易对
    public string Timeframe { get; set; }           // 周期
    public string StrategyName { get; set; }        // 策略名称
    public string StrategyCode { get; set; }        // DSL代码
    public RiskConfig RiskConfig { get; set; }      // 风险配置
    public CapitalConfig CapitalConfig { get; set; } // 资金配置
    public ExchangeConfig ExchangeConfig { get; set; } // 交易所配置
    public bool IsEnabled { get; set; }             // 是否启用
}
```

### 资金分配

每个实例可以独立配置资金：

```csharp
public class CapitalConfig
{
    public decimal InitialCapital { get; set; }     // 初始资金
    public decimal MaxCapital { get; set; }         // 最大资金限制
    public decimal ReservedCapital { get; set; }    // 保留资金
}
```

### 风险隔离

每个实例有独立的风险控制：

- 独立的`RiskController`
- 独立的风险参数
- 独立的紧急停止
- 全局风险聚合（可选）

---

## 使用指南

### 快速开始

#### 1. 程序化创建实例

```csharp
using Prophet.Client.Trading.Engine;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.RiskControl;
using Prophet.Client.Trading.Storage;

// 1. 初始化管理器
var storage = new TradingInstanceStorage();
var manager = new TradingInstanceManager(storage);
await manager.InitializeAsync();

// 2. 创建实例配置
var config = new TradingInstanceConfig
{
    Name = "币安-BTC-MACD策略",
    Exchange = "Binance",
    Symbol = "BTCUSDT",
    Timeframe = "5m",
    StrategyName = "MACD_RSI",
    StrategyCode = @"
        ALL {
            $(5m).MACD().macd > $(5m).MACD().signal,
            $(5m).RSI().value > 50,
            $(5m).RSI().value < 70
        } = BUY
        
        ALL {
            $(5m).MACD().macd < $(5m).MACD().signal,
            $(5m).RSI().value < 50
        } = SELL
    ",
    RiskConfig = new RiskConfig
    {
        InitialCapital = 1000m,
        MaxOpenPositions = 3,
        MaxDrawdown = 0.15m,
        MaxRiskPerTrade = 0.02m,
        MaxDailyTrades = 10,
        MinBalance = 100m,
        MaxConsecutiveLosses = 5
    },
    CapitalConfig = new CapitalConfig
    {
        InitialCapital = 1000m,
        MaxCapital = 5000m,
        ReservedCapital = 100m
    },
    ExchangeConfig = new ExchangeConfig
    {
        ExchangeName = "Binance",
        UseTestnet = true,
        ApiKey = "your_api_key",
        ApiSecret = "your_api_secret"
    }
};

// 3. 创建实例
var instanceId = await manager.CreateInstanceAsync(config);
Console.WriteLine($"实例已创建: {instanceId}");

// 4. 启动实例
var success = await manager.StartInstanceAsync(instanceId);
if (success)
{
    Console.WriteLine("实例启动成功");
}

// 5. 监控实例
while (true)
{
    await Task.Delay(5000);
    
    var snapshot = await manager.GetInstanceSnapshotAsync(instanceId);
    if (snapshot != null)
    {
        Console.WriteLine($"权益: {snapshot.TotalEquity:F2} USDT");
        Console.WriteLine($"今日盈亏: {snapshot.TodayPnL:F2} USDT");
        Console.WriteLine($"持仓: {snapshot.PositionCount} 个");
        Console.WriteLine($"风险等级: {snapshot.RiskLevel}");
    }
}
```

#### 2. UI操作（推荐）

1. 打开Prophet Client
2. 进入【实盘交易】页面
3. 点击【+ 新建实盘】
4. 按向导配置实例
5. 点击【▶ 启动】按钮

### 批量管理

```csharp
// 启动所有实例
var count = await manager.StartAllAsync();
Console.WriteLine($"已启动 {count} 个实例");

// 停止所有实例
count = await manager.StopAllAsync();
Console.WriteLine($"已停止 {count} 个实例");

// 按交易所批量启动
count = await manager.StartByExchangeAsync("Binance");
Console.WriteLine($"已启动币安的 {count} 个实例");

// 按交易所批量停止
count = await manager.StopByExchangeAsync("Binance");
Console.WriteLine($"已停止币安的 {count} 个实例");
```

### 全局统计

```csharp
using Prophet.Client.Trading.Statistics;

var statistics = new CrossInstanceStatistics(manager);

// 获取全局摘要
var summary = await statistics.GetGlobalSummaryAsync();
Console.WriteLine($"总权益: {summary.TotalEquity:F2} USDT");
Console.WriteLine($"运行中: {summary.RunningInstances}/{summary.TotalInstances}");
Console.WriteLine($"今日盈亏: {summary.TotalTodayPnL:F2} USDT");
Console.WriteLine($"全局回撤: {summary.GlobalDrawdown:P2}");

// 按交易所汇总
var exchanges = await statistics.GetExchangeSummariesAsync();
foreach (var ex in exchanges)
{
    Console.WriteLine($"{ex.ExchangeName}:");
    Console.WriteLine($"  实例数: {ex.InstanceCount}");
    Console.WriteLine($"  总权益: {ex.TotalEquity:F2}");
    Console.WriteLine($"  总盈亏: {ex.TotalPnL:F2}");
}

// 按策略汇总
var strategies = await statistics.GetStrategySummariesAsync();
foreach (var strat in strategies)
{
    Console.WriteLine($"{strat.StrategyName}:");
    Console.WriteLine($"  总盈亏: {strat.TotalPnL:F2}");
    Console.WriteLine($"  胜率: {strat.AverageWinRate:P2}");
}

// 实例排行榜
var rankings = await statistics.GetInstanceRankingsAsync(RankingType.ByProfit);
Console.WriteLine("盈利排行榜:");
for (int i = 0; i < Math.Min(5, rankings.Count); i++)
{
    var rank = rankings[i];
    Console.WriteLine($"{i+1}. {rank.InstanceName}: {rank.TotalPnL:F2} USDT");
}
```

---

## API参考

### TradingInstanceStatus (枚举)

```csharp
public enum TradingInstanceStatus
{
    Stopped = 0,      // 已停止
    Starting = 1,     // 启动中
    Running = 2,      // 运行中
    Paused = 3,       // 已暂停
    Stopping = 4,     // 停止中
    Error = 5         // 错误
}
```

### RiskLevel (枚举)

```csharp
public enum RiskLevel
{
    Low = 0,          // 低风险
    Medium = 1,       // 中等风险
    High = 2,         // 高风险
    Critical = 3      // 严重风险
}
```

### TradingInstanceSnapshot (类)

实例状态快照，包含完整的实时数据：

```csharp
public class TradingInstanceSnapshot
{
    // 账户信息
    public decimal TotalEquity { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal UsedMargin { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal TodayPnL { get; set; }
    public decimal TodayPnLPercent { get; set; }
    
    // 持仓信息
    public int PositionCount { get; set; }
    public int OpenOrderCount { get; set; }
    
    // 风险指标
    public decimal CurrentDrawdown { get; set; }
    public decimal MaxDrawdown { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public bool EmergencyStopActivated { get; set; }
    
    // 交易统计
    public int TotalTrades { get; set; }
    public int TodayTrades { get; set; }
    public decimal WinRate { get; set; }
    public int ConsecutiveLosses { get; set; }
    public int ConsecutiveWins { get; set; }
}
```

### 事件参数

```csharp
// 实例状态变化事件
public class InstanceStatusChangedEventArgs : EventArgs
{
    public Guid InstanceId { get; set; }
    public TradingInstanceStatus NewStatus { get; set; }
    public DateTime Timestamp { get; set; }
}

// 实例错误事件
public class InstanceErrorEventArgs : EventArgs
{
    public Guid InstanceId { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}
```

---

## 数据库设计

### trading_instances 表

存储实例配置信息

```sql
CREATE TABLE trading_instances (
    id TEXT PRIMARY KEY,                 -- 实例ID (GUID)
    name TEXT NOT NULL,                  -- 实例名称
    exchange TEXT NOT NULL,              -- 交易所
    symbol TEXT NOT NULL,                -- 交易对
    timeframe TEXT NOT NULL,             -- 周期
    strategy_name TEXT NOT NULL,         -- 策略名称
    strategy_code TEXT,                  -- DSL代码
    risk_config TEXT,                    -- 风险配置 (JSON)
    capital_config TEXT,                 -- 资金配置 (JSON)
    exchange_config TEXT,                -- 交易所配置 (JSON)
    is_enabled INTEGER DEFAULT 1,        -- 是否启用
    created_at TEXT NOT NULL,            -- 创建时间
    last_run_at TEXT,                    -- 最后运行时间
    UNIQUE(exchange, symbol, strategy_name)
);
```

### instance_snapshots 表

存储实例历史快照

```sql
CREATE TABLE instance_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    instance_id TEXT NOT NULL,           -- 实例ID
    timestamp TEXT NOT NULL,             -- 快照时间
    status INTEGER NOT NULL,             -- 状态
    total_equity REAL NOT NULL,          -- 总权益
    available_balance REAL NOT NULL,     -- 可用余额
    used_margin REAL NOT NULL,           -- 占用保证金
    unrealized_pnl REAL NOT NULL,        -- 未实现盈亏
    realized_pnl REAL NOT NULL,          -- 已实现盈亏
    today_pnl REAL NOT NULL,             -- 今日盈亏
    today_pnl_percent REAL NOT NULL,     -- 今日盈亏百分比
    position_count INTEGER NOT NULL,     -- 持仓数量
    open_order_count INTEGER NOT NULL,   -- 挂单数量
    current_drawdown REAL NOT NULL,      -- 当前回撤
    max_drawdown REAL NOT NULL,          -- 最大回撤
    risk_level INTEGER NOT NULL,         -- 风险等级
    emergency_stop_activated INTEGER,    -- 紧急停止状态
    total_trades INTEGER NOT NULL,       -- 总交易次数
    today_trades INTEGER NOT NULL,       -- 今日交易次数
    win_rate REAL NOT NULL,              -- 胜率
    consecutive_losses INTEGER NOT NULL, -- 连续亏损
    consecutive_wins INTEGER NOT NULL,   -- 连续盈利
    error_message TEXT,                  -- 错误消息
    running_duration_seconds INTEGER,    -- 运行时长
    FOREIGN KEY (instance_id) REFERENCES trading_instances(id)
);

CREATE INDEX idx_snapshots_instance ON instance_snapshots(instance_id);
CREATE INDEX idx_snapshots_time ON instance_snapshots(timestamp);
```

### 数据保留策略

- 实例配置：永久保留
- 快照数据：默认保留30天（可配置）
- 自动清理：调用`CleanupOldSnapshotsAsync(keepDays)`

---

## 配置说明

### 风险配置参数

```csharp
public class RiskConfig
{
    // 资金相关
    public decimal InitialCapital { get; set; } = 1000m;        // 初始资金
    
    // 持仓限制
    public int MaxOpenPositions { get; set; } = 3;              // 最大持仓数
    public decimal MaxPositionPerSymbol { get; set; } = 0.30m;  // 单币种最大仓位(30%)
    
    // 风险控制
    public decimal MaxDrawdown { get; set; } = 0.15m;           // 最大回撤(15%)
    public decimal MaxRiskPerTrade { get; set; } = 0.02m;       // 单笔最大风险(2%)
    public decimal PositionSizePercent { get; set; } = 0.95m;   // 仓位大小(95%)
    
    // 交易限制
    public int MaxDailyTrades { get; set; } = 10;               // 每日最大交易次数
    public decimal MinBalance { get; set; } = 100m;             // 最小余额(USDT)
    public int MaxConsecutiveLosses { get; set; } = 5;          // 最大连续亏损
    
    // 紧急停止
    public bool EnableEmergencyStopOnMaxDrawdown { get; set; } = true;
    public bool EnableEmergencyStopOnConsecutiveLosses { get; set; } = true;
    public bool CloseAllPositionsOnEmergencyStop { get; set; } = true;
    
    // 监控配置
    public bool EnableRealtimeMonitoring { get; set; } = true;
    public int MonitoringIntervalSeconds { get; set; } = 60;
}
```

### 推荐配置

**保守型**:
```csharp
new RiskConfig
{
    MaxDrawdown = 0.10m,        // 10%
    MaxRiskPerTrade = 0.01m,    // 1%
    MaxOpenPositions = 1,
    MaxDailyTrades = 5
}
```

**中等型**:
```csharp
new RiskConfig
{
    MaxDrawdown = 0.15m,        // 15%
    MaxRiskPerTrade = 0.02m,    // 2%
    MaxOpenPositions = 3,
    MaxDailyTrades = 10
}
```

**激进型**:
```csharp
new RiskConfig
{
    MaxDrawdown = 0.20m,        // 20%
    MaxRiskPerTrade = 0.03m,    // 3%
    MaxOpenPositions = 5,
    MaxDailyTrades = 20
}
```

---

## 最佳实践

### 1. 测试流程

**第1周：测试网验证**
- 使用测试网API
- 验证策略逻辑
- 测试风险控制
- 检查订单执行

**第2-3周：小额实盘**
- 100-200 USDT
- 低杠杆（1-5x）
- 严格止损
- 密切监控

**第4周+：逐步扩大**
- 根据表现调整
- 优化参数
- 增加资金

### 2. 风险管理

**必须遵守**:
- ✅ 设置合理的止损
- ✅ 控制单笔风险≤2%
- ✅ 启用最大回撤保护
- ✅ 启用紧急停止
- ✅ 分散交易对

**禁止操作**:
- ❌ 不设止损
- ❌ 重仓单一币种
- ❌ 频繁修改策略
- ❌ 情绪化操作
- ❌ 加大杠杆追损

### 3. 策略选择

**适合实盘的策略**:
- 回测胜率 > 55%
- 盈利因子 > 1.5
- 最大回撤 < 20%
- 交易次数适中（不过度交易）
- 样本数量充足（>100笔）

**不适合实盘的策略**:
- 回测数据不足
- 过拟合（曲线完美但逻辑牵强）
- 过度交易（每天>50笔）
- 高频策略（Prophet不适合）

### 4. 监控要点

**每日必查**:
- ✅ 账户权益变化
- ✅ 当前回撤
- ✅ 持仓情况
- ✅ 风险等级
- ✅ 交易日志

**每周必做**:
- ✅ 查看风险报告
- ✅ 分析交易统计
- ✅ 检查策略表现
- ✅ 对比回测结果
- ✅ 调整风险参数（如需）

### 5. 异常处理

**如果触发紧急停止**:
1. 停止所有交易
2. 查看触发原因
3. 检查账户状态
4. 分析问题根源
5. 调整后再启动

**如果策略表现异常**:
1. 暂停实盘
2. 对比回测数据
3. 检查市场环境
4. 重新验证策略
5. 考虑调整或更换

---

## 故障排查

### 常见问题

#### 1. 实例无法启动

**现象**: 启动失败，状态变为Error

**可能原因**:
- API密钥错误
- 网络连接问题
- 策略代码错误
- 交易所限制

**解决方法**:
```csharp
// 检查API连接
var exchange = new BinanceExchange();
var result = await exchange.InitializeAsync(config.ExchangeConfig);
if (!result)
{
    Console.WriteLine("API连接失败，请检查密钥和网络");
}

// 检查策略代码
var validator = new DSLValidator();
var validationResult = validator.Validate(config.StrategyCode);
if (!validationResult.IsValid)
{
    Console.WriteLine($"策略验证失败: {validationResult.Errors}");
}
```

#### 2. WebSocket断线

**现象**: 实时数据停止更新

**解决方法**:
- WebSocket客户端有自动重连机制
- 等待自动重连（指数退避）
- 如果长时间未恢复，重启实例

#### 3. 订单未成交

**现象**: 下单后订单一直挂着

**可能原因**:
- 限价单价格不合理
- 市场流动性不足
- 交易所维护

**解决方法**:
- 检查订单簿深度
- 使用市价单
- 调整限价单价格
- 手动撤单

#### 4. 数据库锁定

**现象**: 保存数据时报错

**解决方法**:
```csharp
// TradingInstanceStorage使用异步操作，避免锁定
// 如果仍然出现问题，检查是否有其他程序访问数据库
```

#### 5. 内存占用过高

**现象**: 运行时间长后内存持续增长

**解决方法**:
- 限制日志数量（默认1000条）
- 定期清理旧快照
- 重启长时间运行的实例

```csharp
// 清理30天前的快照
await storage.CleanupOldSnapshotsAsync(keepDays: 30);
```

### 日志分析

查看实例日志：

```csharp
var vm = instanceViewModels.FirstOrDefault(i => i.Config.Id == instanceId);
if (vm != null)
{
    foreach (var log in vm.Logs.Take(100))
    {
        Console.WriteLine(log);
    }
}
```

### 性能监控

```csharp
// 检查运行中实例数量
var runningCount = manager.GetRunningCount();
Console.WriteLine($"运行中实例: {runningCount}");

// 建议：不超过10个同时运行的实例
if (runningCount > 10)
{
    Console.WriteLine("⚠️ 警告：实例数量过多，可能影响性能");
}
```

---

## 附录

### A. 支持的交易所

| 交易所 | 状态 | 支持功能 |
|--------|------|----------|
| Binance (币安) | ✅ 已支持 | U本位合约 REST+WebSocket |
| OKX | 🚧 开发中 | 计划支持 |
| Bybit | 📋 计划中 | 计划支持 |

### B. 支持的订单类型

- ✅ 市价单 (Market Order)
- ✅ 限价单 (Limit Order)
- ✅ 止盈 (Take Profit)
- ✅ 止损 (Stop Loss)

### C. 系统限制

- 最大同时运行实例数：建议≤10个
- 单个实例最大持仓数：由RiskConfig.MaxOpenPositions控制
- 快照保存频率：可配置，默认1秒
- 日志保留数量：每个实例1000条

### D. 版本历史

**v3.0.0** (2025-12-05)
- ✅ 多实盘架构支持
- ✅ 完整风险控制系统
- ✅ 跨实例统计

**v2.0.0** (2025-12-05)
- ✅ 订单管理系统
- ✅ 仓位跟踪
- ✅ 数据持久化

**v1.0.0** (2025-12-05)
- ✅ 币安交易所接入
- ✅ REST API + WebSocket
- ✅ 基础交易引擎

### E. 相关文档

- [Prophet DSL 规范](../doc/Prophet%20DSL%20规范/Prophet%20DSL%20函数.md)
- [Prophet Client AI 集成文档](./Prophet%20Client%20AI%20集成文档.md)
- [实盘交易模块开发完成总览](../../实盘交易模块-开发完成总览.md)

---

## 联系支持

如有问题或建议，请通过以下方式联系：

- 📧 邮件: support@prophet-trading.com
- 💬 社区: github.com/prophet-trading/issues
- 📖 文档: docs.prophet-trading.com

---

**文档版本**: 1.0.0  
**最后更新**: 2025-12-05  
**适用版本**: Prophet Client v3.0.0+

