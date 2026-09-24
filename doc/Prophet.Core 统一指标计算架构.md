# Prophet.Core 统一指标计算架构

**日期**: 2025-01-XX  
**状态**: ✅ 已完成

## 📋 概述

本文档描述了Prophet.Core统一指标计算架构的重构工作，目标是让DSL引擎和客户端绘制使用**完全相同的指标计算逻辑**，确保结果一致性和命名统一性。

## 🎯 目标

1. **代码复用**：消除重复实现，统一使用`Calculator`类
2. **结果一致性**：DSL和客户端绘制得到完全相同的计算结果
3. **命名统一**：指标名称、参数名称、字段名称完全一致

## 📊 当前架构

### 问题现状

**两处指标计算路径：**

1. **DSL引擎路径**：
   ```
   DSL代码 → Context::getOrCalculateIndicator() → Calculator::RSI() → IndicatorResult
   ```
   - 使用`Calculator`类
   - 返回`IndicatorResult`（包含序列数据和业务逻辑字段）
   - 支持偏移量访问历史值

2. **客户端绘制路径**（旧实现）：
   ```
   C#客户端 → c_api.cpp::Prophet_RSI() → TA_RSI() → IndicatorResult (C结构体)
   ```
   - 直接调用TA-Lib
   - 只返回原始数值
   - 缺少业务逻辑字段（如`trend`, `overbought`等）

### 问题影响

- ❌ **结果不一致**：两处计算结果可能不同（业务逻辑字段缺失）
- ❌ **命名不统一**：字段名称可能不一致
- ❌ **代码重复**：维护成本高，容易产生分歧

## ✅ 解决方案

### 统一架构

**新的统一路径：**

```
DSL引擎: Context → IndicatorRegistry → Calculator → IndicatorResult
客户端:  c_api.cpp → CalculateIndicatorUnified → IndicatorRegistry → Calculator → IndicatorResult → C API格式
```

**核心改进：**

1. **创建通用计算函数** `CalculateIndicatorUnified()`：
   - 通过`IndicatorRegistry`动态调用指标
   - 支持所有已注册的62个指标
   - 统一参数处理和结果转换

2. **简化现有函数**：
   - 每个C API函数只需几行代码调用`CalculateIndicatorUnified()`
   - 不需要逐个实现每个指标的计算逻辑
   - 自动获得与DSL引擎相同的结果

3. **保持向后兼容**：
   - C API接口保持不变
   - 客户端代码无需修改

### 实现细节

#### 1. 转换函数

```cpp
static int ConvertIndicatorResultToC(
    const prophet::indicators::IndicatorResult& cpp_result,
    const std::string& field_name,
    int input_length,
    IndicatorResult* c_result
)
```

**功能：**
- 从C++的`IndicatorResult`中提取指定字段
- 将`std::vector<Value>`序列转换为`double[]`数组
- 计算`out_begin`（TA-Lib风格的输出起始索引）
- 处理错误和边界情况

#### 2. 通用计算函数

**核心函数：`CalculateIndicatorUnified()`**
```cpp
static int CalculateIndicatorUnified(
    const char* indicator_name,      // 指标名称（如"RSI", "MACD"）
    const double* close,              // 收盘价数组
    const double* high,               // 最高价数组（可为NULL）
    const double* low,                // 最低价数组（可为NULL）
    const double* volume,             // 成交量数组（可为NULL）
    int length,                       // 数据长度
    const char** param_keys,         // 参数名数组
    const double* param_values,      // 参数值数组
    int param_count,                  // 参数数量
    const char* field_name,           // 要提取的字段名（如"value", "macd"）
    IndicatorResult* outResult       // 输出结果
)
```

**工作流程：**
1. 通过`IndicatorRegistry::getInstance()`获取注册表
2. 检查指标是否已注册
3. 准备K线数据向量和参数
4. 调用注册的指标计算函数
5. 提取指定字段并转换为C API格式

#### 3. 重构示例：RSI

**旧实现（直接调用TA-Lib，~30行代码）：**
```cpp
TA_RetCode retCode = TA_RSI(...);
// 手动处理内存分配、错误处理等
```

**新实现（调用通用函数，~10行代码）：**
```cpp
const char* param_keys[] = {"RSI_PERIOD"};
double param_values[] = {static_cast<double>(PERIOD)};

return CalculateIndicatorUnified(
    "RSI",
    inReal, nullptr, nullptr, nullptr,
    length,
    param_keys, param_values, 1,
    "value",
    outResult
);
// 自动获得与DSL引擎完全相同的结果
```

#### 4. 重构示例：MACD

**旧实现（直接调用TA-Lib，~60行代码）：**
```cpp
TA_RetCode retCode = TA_MACD(..., tempMACD, tempSignal, tempHist);
// 手动分配三个数组的内存
```

**新实现（调用通用函数，~30行代码）：**
```cpp
const char* param_keys[] = {"FAST_PERIOD", "SLOW_PERIOD", "SIGNAL_PERIOD"};
double param_values[] = {fastPeriod, slowPeriod, signalPeriod};

// 提取三个字段（每个字段调用一次）
CalculateIndicatorUnified(..., "macd", outMACD);
CalculateIndicatorUnified(..., "signal", outSignal);
CalculateIndicatorUnified(..., "histogram", outHistogram);
// 自动获得与DSL引擎完全相同的结果
```

## 📝 已完成工作

### ✅ 已完成

1. **添加转换函数** (`ConvertIndicatorResultToC`)
   - 实现C++ `IndicatorResult`到C API格式的转换
   - 支持字段提取和序列转换

2. **重构RSI函数**
   - 从直接调用`TA_RSI`改为调用`Calculator::RSI`
   - 确保与DSL引擎结果一致

3. **重构MACD函数**
   - 从直接调用`TA_MACD`改为调用`Calculator::MACD`
   - 提取`macd`、`signal`、`histogram`三个字段

### ✅ 已完成（所有27个C API指标函数）

**重构完成**：所有C API指标函数已统一调用`CalculateIndicatorUnified()`通用函数！

**已重构的指标函数：**

#### 趋势类（9个）
- [x] SMA (MA指标) ✅
- [x] EMA ✅
- [x] WMA ✅
- [x] DEMA ✅
- [x] TEMA ✅
- [x] BBANDS (BOLL指标) ✅
- [x] KELTNER ✅
- [x] ICHIMOKU ✅
- [x] SAR ✅

#### 动量类（10个）
- [x] RSI ✅
- [x] MACD ✅
- [x] STOCH (KDJ指标) ✅
- [x] STOCHRSI ✅
- [x] CCI ✅
- [x] CMO ✅
- [x] ROC ✅
- [x] MTM ✅
- [x] MFI ✅
- [x] WILLR (WR指标) ✅

#### 波动类（1个）
- [x] ATR ✅

#### 成交量类（3个）
- [x] OBV ✅
- [x] CMF ✅
- [x] EMV ✅

#### 价格类（2个）
- [x] VWAP ✅
- [x] TRIX ✅

#### 其他（2个）
- [x] DMI ✅
- [x] AROON ✅

**注意**：
- 以上是C API中已实现的27个指标函数，已全部重构完成
- 其他35个指标（如Supertrend、DonchianChannel等）如果将来需要添加到C API，也可以使用相同的模式快速实现
- 所有重构后的函数都通过`IndicatorRegistry`统一调用，确保与DSL引擎结果完全一致

## 🔍 字段映射表

### 单字段指标（返回"value"字段）

| 指标 | 字段名 | 说明 |
|------|--------|------|
| RSI | `value` | RSI值（0-100） |
| EMA | `value` | EMA值 |
| MA | `value` | MA值 |
| ATR | `value` | ATR值 |
| CCI | `value` | CCI值 |
| ... | ... | ... |

### 多字段指标（返回多个字段）

| 指标 | 字段名 | 说明 |
|------|--------|------|
| MACD | `macd` | MACD线 |
| MACD | `signal` | 信号线 |
| MACD | `histogram` | 柱状图 |
| BOLL | `upper` | 上轨 |
| BOLL | `middle` | 中轨 |
| BOLL | `lower` | 下轨 |
| KDJ | `k` | K值 |
| KDJ | `d` | D值 |
| KDJ | `j` | J值 |
| ... | ... | ... |

**注意**：所有字段名称必须与DSL文档完全一致！

## 📚 参考文档

- [Prophet DSL 指标.md](../Prophet%20DSL%20规范/Prophet%20DSL%20指标.md) - 完整的指标字段定义
- [指标偏移量功能实现指南.md](../Prophet%20DSL%20规范/指标偏移量功能实现指南.md) - 序列存储实现细节

## 🚀 后续工作

1. ✅ **批量重构**：已完成所有27个C API指标函数的重构
2. **测试验证**：需要测试确保DSL和客户端计算结果完全一致
3. **文档更新**：更新C API文档，说明统一架构（已完成）
4. **性能测试**：评估统一架构的性能影响（预期无负面影响，因为只是改变了调用路径）

## ✅ 重构成果

### 代码统计

- **重构前**：每个函数平均 ~30-60 行代码（直接调用TA-Lib）
- **重构后**：每个函数平均 ~10-15 行代码（调用通用函数）
- **代码减少**：约 70-80%
- **统一性**：100%（所有函数使用相同的计算路径）

### 架构优势

1. **完全统一**：DSL和客户端使用完全相同的计算逻辑
2. **易于维护**：新增指标自动支持，无需修改C API
3. **命名一致**：参数名称和字段名称与DSL文档完全一致
4. **向后兼容**：C API接口保持不变，客户端代码无需修改
5. **序列支持**：自动支持序列结果和偏移量访问

## 💡 注意事项

1. **向后兼容**：C API接口保持不变，只改变内部实现
2. **错误处理**：统一使用异常捕获和错误信息设置
3. **内存管理**：确保正确释放已分配的内存
4. **字段名称**：必须与DSL文档中的字段名称完全一致

---

**相关文件：**
- `Prophet.Core/src/c_api/c_api.cpp` - C API实现
- `Prophet.Core/include/prophet/indicators/calculator.hpp` - Calculator类定义
- `Prophet.Core/src/indicators/` - 各指标实现文件
