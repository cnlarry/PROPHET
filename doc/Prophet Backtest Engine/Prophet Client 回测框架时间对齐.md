# Prophet Client 回测框架时间对齐

## 概述

本文档记录了 Prophet Client 回测框架中时间对齐问题的完整解决方案。时间对齐是多时间框架回测的核心问题，涉及K线数据的开盘时间、收盘时间、窗口对齐、信号生成时间等多个方面。

## 问题背景

在多时间框架回测中，需要确保：
1. **回测区间正确**：用户选择的日期范围应该正确映射到实际的信号生成时间
2. **窗口对齐准确**：不同时间框架的滑动窗口应该对齐到同一时间点
3. **收盘时间精确**：K线的收盘时间应该使用数据库中的真实值，秒部分必须是59
4. **信号时间正确**：第一个和最后一个信号的生成时间应该基于K线的收盘时间

## 核心概念

### 1. K线时间

#### 开盘时间（OpenTime）
- K线的开始时间，例如：`2025-11-01 00:00:00 UTC`
- 存储在数据库的 `open_time` 字段

#### 收盘时间（CloseTime）
- K线的结束时间，例如：`2025-11-01 00:04:59 UTC`（5m K线）
- 存储在数据库的 `close_time` 字段
- **关键规则**：收盘时间的秒部分必须是 `59`（例如：`00:04:59`，而不是 `00:05:00`）
- 计算公式：`收盘时间 = 开盘时间 + 时间框架 - 1秒`

#### 时间框架与收盘时间对应关系

| 时间框架 | 开盘时间示例 | 收盘时间示例 |
|---------|------------|------------|
| 1m | 00:00:00 | 00:00:59 |
| 5m | 00:00:00 | 00:04:59 |
| 15m | 00:00:00 | 00:14:59 |
| 30m | 00:00:00 | 00:29:59 |
| 1h | 00:00:00 | 00:59:59 |
| 4h | 00:00:00 | 03:59:59 |
| 1d | 00:00:00 | 23:59:59 |

### 2. 回测区间

#### 用户选择 vs 实际回测区间

**用户选择**：`2025-11-01` 至 `2025-11-30`

**实际回测区间**：
- **开始时间**：第一根K线的收盘时间
  - 示例：`2025-11-01 00:04:59 UTC`（5m采样频率）
- **结束时间**：最后一根K线的收盘时间
  - 示例：`2025-11-30 23:59:59 UTC`（5m采样频率）

**关键点**：
- 回测区间应该显示**信号生成时间**（K线收盘时间），而不是用户选择的日期
- 第一个信号生成于第一根K线的收盘时间
- 最后一个信号生成于最后一根K线的收盘时间

### 3. 滑动窗口对齐

#### 窗口初始化

**对齐原则**：
- 所有时间框架的窗口应该对齐到**同一时间点**
- 对齐时间点是**回测开始前最后一根K线的收盘时间**

**示例**（回测开始时间：`2025-11-01 00:00:00 UTC`，采样频率：5m）：

```
对齐时间：2025-11-01 00:04:59 UTC（回测开始前最后一根5m K线的收盘时间）

5m窗口：
  - 第一根：2025-10-30 23:00:00 UTC（开盘），2025-10-30 23:04:59 UTC（收盘）
  - 最后一根：2025-11-01 00:00:00 UTC（开盘），2025-11-01 00:04:59 UTC（收盘）

1h窗口：
  - 第一根：2025-10-19 12:00:00 UTC（开盘），2025-10-19 12:59:59 UTC（收盘）
  - 最后一根：2025-10-31 23:00:00 UTC（开盘），2025-10-31 23:59:59 UTC（收盘）
```

**关键点**：
- 窗口的最后一根K线是 `backtestStartIndex - 1`（回测开始前的最后一根）
- 窗口的第一根K线是 `windowEndIndex - windowSize + 1`（向前取300根）
- 所有时间框架的窗口最后一根K线的**收盘时间**应该对齐到同一时间点

#### 窗口滑动

**滑动规则**：
- 每次回测迭代，追加新的K线到窗口
- 只追加时间戳**大于**窗口最后一根K线的K线
- 避免重复和倒序

**关键代码**：
```csharp
// 只追加时间戳大于窗口最后一根K线且 <= currentTime 的K线
if (lastWindowKlineTime.HasValue && candidateKline.Time <= lastWindowKlineTime.Value)
{
    continue;  // 跳过已经在窗口中的K线
}
```

### 4. 收盘时间计算

#### 优先使用数据库真实值

**原则**：优先使用数据库中的 `close_time` 字段，而不是计算值

**代码实现**：
```csharp
// 优先使用K线的真实收盘时间
DateTime closeTime;
if (currentCandle.CloseTime.HasValue)
{
    closeTime = currentCandle.CloseTime.Value;
}
else
{
    // 备选方案：计算收盘时间（开盘时间 + 时间框架 - 1秒）
    var currentTimeframeMinutes = DSLAnalyzer.TimeframeToMinutes(minTimeframe);
    closeTime = currentTime.AddMinutes(currentTimeframeMinutes).AddSeconds(-1);
}
```

#### 数据库存储规则

**插入逻辑**：
```csharp
var closeTime = candle.CloseTime.HasValue
    ? new DateTimeOffset(candle.CloseTime.Value).ToUnixTimeMilliseconds()
    : openTime + GetIntervalMilliseconds(interval) - 1;  // 减1毫秒
```

**关键点**：
- 如果K线有 `CloseTime` 字段，直接使用
- 如果没有，计算为：`开盘时间 + 时间框架 - 1毫秒`
- 确保收盘时间的秒部分永远是 `59`

## 实现细节

### 1. 回测区间计算

**位置**：`BacktestEngine.cs` - `RunAsync` 方法

**实现**：
```csharp
// 计算实际回测区间（第一个信号和最后一个信号的收盘时间）
DateTime? firstSignalCloseTime = null;
DateTime? lastSignalCloseTime = null;

// 找到第一个 >= utcStartDate 的K线
for (int i = 0; i < minTimeframeData.Count; i++)
{
    if (minTimeframeData[i].Time >= utcStartDate)
    {
        var firstCandleCloseTime = minTimeframeData[i].CloseTime;
        if (firstCandleCloseTime.HasValue)
        {
            firstSignalCloseTime = firstCandleCloseTime.Value;
        }
        else
        {
            firstSignalCloseTime = minTimeframeData[i].Time.AddMinutes(timeframeMinutes).AddSeconds(-1);
        }
        break;
    }
}

// 找到最后一个 <= alignedEndDate 的K线
for (int i = minTimeframeData.Count - 1; i >= 0; i--)
{
    if (minTimeframeData[i].Time <= alignedEndDate)
    {
        var lastCandleCloseTime = minTimeframeData[i].CloseTime;
        if (lastCandleCloseTime.HasValue)
        {
            lastSignalCloseTime = lastCandleCloseTime.Value;
        }
        else
        {
            lastSignalCloseTime = minTimeframeData[i].Time.AddMinutes(timeframeMinutes).AddSeconds(-1);
        }
        break;
    }
}
```

### 2. 窗口初始化对齐

**位置**：`BacktestEngine.cs` - `InitializeKlineWindowsAsync` 方法

**关键步骤**：
1. 找到最小时间框架的窗口对齐点（回测开始前最后一根K线的收盘时间）
2. 对于每个时间框架，找到收盘时间最接近且 <= 对齐时间的K线
3. 从对齐点向前取300根K线作为初始窗口

**代码片段**：
```csharp
// 窗口的最后一根K线是 backtestStartIndex - 1（回测开始前的最后一根）
int windowEndIndex = backtestStartIndex - 1;

// 获取对齐时间（窗口最后一根K线的收盘时间）
var alignmentCandle = minTimeframeCandles[windowEndIndex];
DateTime alignmentCloseTime = alignmentCandle.CloseTime.Value;

// 对于每个时间框架，对齐到 alignmentCloseTime
foreach (var timeframe in timeframes)
{
    // 找到收盘时间最接近且 <= alignmentCloseTime 的K线
    int alignmentIndex = -1;
    for (int i = candles.Count - 1; i >= 0; i--)
    {
        var candleCloseTime = candles[i].CloseTime.Value;
        if (candleCloseTime <= alignmentCloseTime)
        {
            alignmentIndex = i;
            break;
        }
    }
    
    // 从对齐点向前取 windowSize 根K线
    int startIndex = alignmentIndex - windowSize + 1;
    int endIndex = alignmentIndex;
}
```

### 3. 收盘时间使用

**位置**：`BacktestEngine.cs` - 回测循环

**实现**：
```csharp
// 使用K线的真实收盘时间（秒部分应该是59）
DateTime closeTime;
if (currentCandle.CloseTime.HasValue)
{
    closeTime = currentCandle.CloseTime.Value;
}
else
{
    // 备选方案：计算收盘时间（开盘时间 + 时间框架 - 1秒）
    var currentTimeframeMinutes = DSLAnalyzer.TimeframeToMinutes(minTimeframe);
    closeTime = currentTime.AddMinutes(currentTimeframeMinutes).AddSeconds(-1);
}
```

### 4. 数据库存储

**位置**：`MarketDataRepository.cs` - `BulkInsertKlinesAsync` 方法

**实现**：
```csharp
var closeTime = candle.CloseTime.HasValue
    ? new DateTimeOffset(candle.CloseTime.Value).ToUnixTimeMilliseconds()
    : openTime + GetIntervalMilliseconds(interval) - 1;  // 减1毫秒，确保秒部分是59
```

## 关键修复点

### 1. 收盘时间计算错误

**问题**：之前使用 `开盘时间 + 时间框架`，结果是下一根K线的开盘时间

**修复**：改为 `开盘时间 + 时间框架 - 1毫秒`，确保秒部分是59

### 2. 窗口对齐错误

**问题**：窗口包含回测开始的第一根K线，导致第一次迭代时重复追加

**修复**：窗口的最后一根K线改为 `backtestStartIndex - 1`（回测开始前的最后一根）

### 3. 回测区间显示错误

**问题**：显示用户选择的日期，而不是实际信号生成时间

**修复**：计算第一个和最后一个信号的实际收盘时间，显示真实的回测区间

### 4. K线倒序问题

**问题**：追加的K线时间戳早于窗口最后一根K线，导致倒序

**修复**：只追加时间戳严格大于窗口最后一根K线的K线

## 验证方法

### 1. 回测区间验证

**检查点**：
- 第一个信号时间应该是第一根K线的收盘时间（例如：`00:04:59`）
- 最后一个信号时间应该是最后一根K线的收盘时间（例如：`23:59:59`）
- 所有收盘时间的秒部分必须是 `59`

### 2. 窗口对齐验证

**检查点**：
- 所有时间框架的窗口最后一根K线的收盘时间应该对齐到同一时间点
- 窗口大小应该是300根K线
- 窗口K线应该按时间升序排列

### 3. 数据完整性验证

**检查点**：
- 数据库中所有K线的 `close_time` 字段应该正确
- 收盘时间的秒部分应该是 `59`
- 没有重复的K线数据

## 数据库修复

如果数据库中的 `close_time` 字段不正确，可以使用以下SQL脚本修复：

```sql
UPDATE klines 
SET close_time = open_time + (
    CASE interval 
        WHEN '1m' THEN 60000 
        WHEN '5m' THEN 300000 
        WHEN '15m' THEN 900000 
        WHEN '30m' THEN 1800000 
        WHEN '1h' THEN 3600000 
        WHEN '4h' THEN 14400000 
        WHEN '1d' THEN 86400000 
        WHEN '1w' THEN 604800000 
        ELSE 0 
    END
) - 1 
WHERE close_time != open_time + (
    CASE interval 
        WHEN '1m' THEN 60000 
        WHEN '5m' THEN 300000 
        WHEN '15m' THEN 900000 
        WHEN '30m' THEN 1800000 
        WHEN '1h' THEN 3600000 
        WHEN '4h' THEN 14400000 
        WHEN '1d' THEN 86400000 
        WHEN '1w' THEN 604800000 
        ELSE 0 
    END
) - 1;
```

## 经验总结

### 1. 时间对齐的重要性

时间对齐是多时间框架回测的核心问题，必须确保：
- 所有时间框架的窗口对齐到同一时间点
- 使用真实的收盘时间，而不是计算值
- 收盘时间的秒部分必须是 `59`

### 2. 数据完整性

- 优先使用数据库中的真实值，而不是计算值
- 确保数据库中的 `close_time` 字段正确
- 使用主键约束防止重复数据

### 3. 窗口管理

- 窗口应该只包含回测开始前的数据
- 追加新K线时，只追加时间戳严格大于窗口最后一根K线的K线
- 确保窗口K线按时间升序排列

### 4. 调试方法

- 添加详细的日志输出，验证窗口对齐和时间计算
- 检查第一个和最后一个信号的时间
- 验证所有收盘时间的秒部分

## 相关文件

- `Prophet.Client/Backtest/Engine/BacktestEngine.cs` - 回测引擎主逻辑
- `Prophet.Client/Database/Repositories/MarketDataRepository.cs` - K线数据存储
- `Prophet.Client/Models/Candlestick.cs` - K线数据模型
- `Prophet.Client/Database/fix_close_time.sql` - 数据库修复脚本

## 版本历史

- **2025-11-10**：初始版本，记录时间对齐问题的完整解决方案

---

**注意**：本文档记录了解决时间对齐问题的完整过程，包括问题分析、解决方案、实现细节和经验总结。在后续开发中，如果遇到类似的时间对齐问题，可以参考本文档。

