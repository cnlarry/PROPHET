# Prophet.API 时间序列数据接口实现指南

## 概述

Prophet回测模块需要加载FEARGREED（恐惧与贪婪指数）和FUNDINGRATE（资金费率）数据，以支持DSL策略中的时间序列函数。

## 需要实现的接口

### 1. 恐惧与贪婪指数接口

**接口路径**: `GET /api/timeseries/feargreed`

**查询参数**:
- `days` (int, 可选): 加载天数，默认365天

**返回格式**:
```json
[
  {
    "date": "2025-11-20T00:00:00Z",
    "value": 45,
    "classification": "Fear"
  },
  {
    "date": "2025-11-19T00:00:00Z",
    "value": 42,
    "classification": "Fear"
  }
]
```

**SQL查询参考**:
```sql
SELECT `date`, `value`, `classification`
FROM `feargreed`
ORDER BY `date` DESC
LIMIT ?days
```

**数据库表结构** (参考 `SQL Scripts/feargreed.sql`):
```sql
CREATE TABLE `feargreed` (
  `date` date NOT NULL,
  `value` int NOT NULL,
  `classification` varchar(20) NOT NULL,
  PRIMARY KEY (`date` DESC),
  INDEX `idx_date`(`date` ASC),
  CONSTRAINT `feargreed_chk_1` CHECK ((`value` >= 0) and (`value` <= 100))
) ENGINE = InnoDB;
```

---

### 2. 资金费率接口

**接口路径**: `GET /api/timeseries/fundingrate`

**查询参数**:
- `symbol` (string, 必填): 交易对，如 "ETHUSDT"
- `count` (int, 可选): 加载条数，默认720条（约30天）

**返回格式**:
```json
[
  {
    "symbol": "ETHUSDT",
    "calc_time": 1732121943000,
    "calc_time_str": "2025-11-20T14:32:23",
    "funding_interval_hours": 8,
    "last_funding_rate": 0.0001234567
  },
  {
    "symbol": "ETHUSDT",
    "calc_time": 1732089943000,
    "calc_time_str": "2025-11-20T05:32:23",
    "funding_interval_hours": 8,
    "last_funding_rate": 0.0001123456
  }
]
```

**SQL查询参考**:
```sql
SELECT `symbol`, `calc_time`, `calc_time_str`, `funding_interval_hours`, `last_funding_rate`
FROM `fundingrate`
WHERE `symbol` = ?symbol
ORDER BY `calc_time` DESC
LIMIT ?count
```

**数据库表结构** (参考 `SQL Scripts/fundingrate.sql`):
```sql
CREATE TABLE `fundingrate` (
  `symbol` varchar(50) NOT NULL COMMENT '标的符号',
  `calc_time` bigint NOT NULL COMMENT '计算时间戳',
  `calc_time_str` datetime NULL DEFAULT NULL COMMENT '计算时间(格式化)',
  `funding_interval_hours` int NOT NULL COMMENT '资金费间隔(小时)',
  `last_funding_rate` decimal(20, 10) NOT NULL COMMENT '最新资金费率',
  PRIMARY KEY (`symbol`, `calc_time`)
) ENGINE = InnoDB;
```

---

## C# 客户端使用方式

客户端通过 `TimeSeriesApiClient` 调用这些接口：

```csharp
var client = new TimeSeriesApiClient(new HttpClient(), "http://localhost:5299");

// 加载恐惧与贪婪指数（365天）
var fearGreedData = await client.GetFearGreedDataAsync(365);

// 加载资金费率（720条）
var fundingRateData = await client.GetFundingRateDataAsync("ETHUSDT", 720);
```

---

## 实现注意事项

### 1. 排序顺序
- 数据应该按 **时间降序** 返回（最新的在前）
- 这样C#端接收到的数据顺序与数据库存储顺序一致

### 2. 时间戳格式
- **恐惧与贪婪指数**: `date` 字段是 `date` 类型，表示某一天（00:00:00）
- **资金费率**: `calc_time` 字段是 `bigint` 类型，存储的是毫秒级时间戳

### 3. 性能优化
- 建议在数据库表上添加索引:
  - `feargreed`: 已有 `idx_date` 索引
  - `fundingrate`: 建议添加 `INDEX idx_symbol_calc_time (symbol, calc_time DESC)`

### 4. 错误处理
- 如果表不存在或数据为空，返回空数组 `[]`
- 如果参数无效，返回 HTTP 400 Bad Request

---

## 数据注入流程

1. 客户端在初始化策略引擎时，通过 `TimeSeriesApiClient` 从 Prophet.API 加载数据
2. 数据加载完成后，调用 `Prophet.Core` 的 C API 注入数据:
   - `Prophet_SetFearGreedSeries()`
   - `Prophet_SetFundingRateSeries()`
3. 引擎内部保存这些数据，供 DSL 策略中的 `FEARGREED()` 和 `FUNDINGRATE()` 函数使用

---

## DSL 策略示例

```dsl
# 使用恐惧与贪婪指数
fear_greed = FEARGREED().value(0)

# 使用资金费率
funding_rate = FUNDINGRATE().value(0)
funding_avg = FUNDINGRATE().avg(24)

# 组合策略
ALL {
    fear_greed < 25,               # 极度恐慌
    funding_rate < 0,              # 资金费率为负
    $(5m).RSI().value < 30         # RSI超卖
} = BUY;
```

---

## 参考文档

- `doc/Prophet.Core 工作机制.md` - Prophet.Core 引擎工作流程
- `doc/Prophet DSL 规范/Prophet DSL 时间序列函数.md` - DSL 时间序列函数详细说明
- `SQL Scripts/feargreed.sql` - 恐惧与贪婪指数表结构
- `SQL Scripts/fundingrate.sql` - 资金费率表结构

---

**创建时间**: 2025-11-20  
**版本**: 1.0

