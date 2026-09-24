# M4实现说明 - Prophet.Core集成方案

## 📌 问题与解决方案

### 问题
在M4实现初期，我错误地理解了Prophet.Core的工作机制，试图在C#端自己计算指标并生成信号，这与Prophet.Core的设计理念完全相反。

### 正确的理解（基于 `Prophet.Core 工作机制.md`）

**Prophet.Core的职责（C++端）：**
- ✅ DSL代码解析
- ✅ 指标计算（SMA, EMA, RSI, MACD等）
- ✅ 规则评估
- ✅ 信号生成（包括止盈止损）

**C#端的职责：**
- ✅ 准备数据（OHLCV K线数据）
- ✅ 准备环境变量（FUNDING_RATE, VOLATILITY等）
- ✅ 调用引擎获取信号
- ✅ 根据信号执行交易逻辑

---

## 🔧 实现方案

### 方案1：完整的C API绑定（理想方案）

创建完整的C API用于策略引擎，包括：

```cpp
// Prophet.Core/src/c_api/strategy_api.cpp

extern "C" {
    // 引擎生命周期
    PROPHET_API void* Prophet_CreateEngine(const char* dsl_code, const char* params_json);
    PROPHET_API void Prophet_DestroyEngine(void* engine);
    
    // K线数据设置
    PROPHET_API int Prophet_SetKlines(
        void* engine,
        const double* open, const double* high, const double* low, 
        const double* close, const double* volume,
        const int64_t* open_time, const int64_t* close_time,
        int count
    );
    
    // 信号获取
    PROPHET_API int Prophet_GetSignal(
        void* engine,
        double current_price,
        int64_t current_time,
        Signal* out_signal
    );
    
    PROPHET_API int Prophet_GetSignalWithEnv(
        void* engine,
        double current_price,
        int64_t current_time,
        const char** env_keys,
        const EnvValue* env_values,
        int env_count,
        Signal* out_signal
    );
}
```

**优点：**
- ✅ 完全使用Prophet.Core的能力
- ✅ 与Python端一致
- ✅ 支持完整的DSL功能

**缺点：**
- ❌ 需要C++端添加新的C API导出
- ❌ 需要重新编译Prophet.Core

**状态：**
- ✅ C# P/Invoke绑定已完成 (`ProphetCoreEngine.cs`)
- ⏳ C++ C API导出待实现
- ✅ DslStrategySignalGenerator已实现（等待C API）

---

### 方案2：使用现有指标API（当前临时方案）

直接使用现有的 `ProphetCoreNative` 指标计算API，在C#端实现简单的策略逻辑。

**优点：**
- ✅ 无需修改C++代码
- ✅ 立即可用
- ✅ 可用于测试回测框架

**缺点：**
- ❌ 无法使用真实的DSL代码
- ❌ 策略逻辑硬编码在C#中
- ❌ 无法享受Prophet.Core的优化（JIT、缓存等）

**状态：**
- ✅ 已实现 `SimpleMAStrategy`（临时方案）
- ✅ 可用于测试回测引擎

---

## 📦 当前代码结构

```
Prophet.Client/Backtest/Strategy/
├── IStrategySignalGenerator.cs          # 接口（已更新）
├── ProphetCoreEngine.cs                 # C API P/Invoke绑定（已完成）
├── DslStrategySignalGenerator.cs        # 基于Prophet.Core的实现（等待C API）
└── SimpleMAStrategy.cs                  # 临时简单策略（当前可用）
```

---

## 🚀 下一步建议

### 短期（立即可用）

1. ✅ 使用 `SimpleMAStrategy` 完成 M5（UI集成）
2. ✅ 验证整个回测系统的流程
3. ✅ 收集用户反馈

### 中期（最佳体验）

1. ⏳ 在Prophet.Core中添加C API导出
   - 参考Python绑定的实现
   - 导出 `Engine` 类的核心方法
   - 编译 `prophet_core.dll`

2. ✅ 启用 `DslStrategySignalGenerator`
   - 无需修改C#代码
   - 只需重新编译Prophet.Core

3. ✅ 用户可以使用真实的DSL代码

---

## 💡 使用示例

### 当前可用（方案2）

```csharp
// 使用简单MA策略
var config = new BacktestConfig
{
    Symbol = "BTCUSDT",
    Interval = "1m",
    StartDate = DateTime.Parse("2024-01-01"),
    EndDate = DateTime.Parse("2024-12-31"),
    InitialCapital = 10000,
    Parameters = new Dictionary<string, object>
    {
        ["FAST_PERIOD"] = 10,
        ["SLOW_PERIOD"] = 30
    }
};

var strategy = new SimpleMAStrategy();
strategy.Initialize("", config); // DSL代码为空

var engine = new BacktestEngine(dataFeed, orderManager, performanceAnalyzer, strategy);
var result = await engine.RunAsync("", config);
```

### 理想方案（方案1 - 需要C API）

```csharp
// 使用真实的DSL代码
var dslCode = @"
    PERIOD = 14
    OVERSOLD = 30
    OVERBOUGHT = 70
    
    rsi = $(1m).RSI(PERIOD).value
    
    ALL {
        rsi < OVERSOLD
    } = BUY;
    
    ALL {
        rsi > OVERBOUGHT
    } = SELL;
";

var config = new BacktestConfig
{
    Symbol = "BTCUSDT",
    Interval = "1m",
    StartDate = DateTime.Parse("2024-01-01"),
    EndDate = DateTime.Parse("2024-12-31"),
    InitialCapital = 10000,
    Parameters = new Dictionary<string, object>()
};

var strategy = new DslStrategySignalGenerator();
strategy.Initialize(dslCode, config); // 传入真实的DSL代码

var engine = new BacktestEngine(dataFeed, orderManager, performanceAnalyzer, strategy);
var result = await engine.RunAsync(dslCode, config);
```

---

## 📋 需要在Prophet.Core中添加的C API

```cpp
// Prophet.Core/src/c_api/strategy_api.cpp
#include "prophet/core/engine.hpp"

#define PROPHET_API __declspec(dllexport)

extern "C" {

// 创建引擎
PROPHET_API void* Prophet_CreateEngine(const char* dsl_code, const char* params_json) {
    try {
        // 解析params_json为C++ map
        auto params = parseParamsJson(params_json);
        
        // 创建引擎
        auto* engine = new prophet::core::Engine(dsl_code, params);
        return engine;
    } catch (const std::exception& e) {
        setLastError(e.what());
        return nullptr;
    }
}

// 销毁引擎
PROPHET_API void Prophet_DestroyEngine(void* engine_handle) {
    if (engine_handle) {
        delete static_cast<prophet::core::Engine*>(engine_handle);
    }
}

// 设置K线
PROPHET_API int Prophet_SetKlines(
    void* engine_handle,
    const double* open, const double* high, const double* low,
    const double* close, const double* volume,
    const int64_t* open_time, const int64_t* close_time,
    int count
) {
    try {
        auto* engine = static_cast<prophet::core::Engine*>(engine_handle);
        engine->set_klines(open, high, low, close, volume, open_time, close_time, count);
        return 0; // 成功
    } catch (const std::exception& e) {
        setLastError(e.what());
        return -1; // 失败
    }
}

// 获取信号（简单版本）
PROPHET_API int Prophet_GetSignal(
    void* engine_handle,
    double current_price,
    int64_t current_time,
    Signal* out_signal
) {
    try {
        auto* engine = static_cast<prophet::core::Engine*>(engine_handle);
        auto signal = engine->get_signal(current_price, current_time);
        
        // 转换信号到C结构
        copySignalToC(signal, out_signal);
        return 0;
    } catch (const std::exception& e) {
        setLastError(e.what());
        return -1;
    }
}

// 获取信号（带环境变量）
PROPHET_API int Prophet_GetSignalWithEnv(
    void* engine_handle,
    double current_price,
    int64_t current_time,
    const char** env_keys,
    const EnvValue* env_values,
    int env_count,
    Signal* out_signal
) {
    try {
        auto* engine = static_cast<prophet::core::Engine*>(engine_handle);
        
        // 构造env_data map
        std::unordered_map<std::string, prophet::Value> env_data;
        for (int i = 0; i < env_count; i++) {
            env_data[env_keys[i]] = prophet::Value::from_number(env_values[i].NumberValue);
        }
        
        auto signal = engine->get_signal(current_price, current_time, env_data);
        
        // 转换信号到C结构
        copySignalToC(signal, out_signal);
        return 0;
    } catch (const std::exception& e) {
        setLastError(e.what());
        return -1;
    }
}

} // extern "C"
```

---

## ✅ 总结

1. **当前状态**：M4已完成，使用临时的 `SimpleMAStrategy`
2. **编译状态**：✅ 成功（0警告/0错误）
3. **可用性**：✅ 可以立即用于M5（UI集成）和M6（参数优化）
4. **后续优化**：添加C API后切换到 `DslStrategySignalGenerator`

**关键认知：**
- Prophet.Core负责一切智能（DSL、指标、规则、信号）
- C#只负责数据传递和交易执行
- 这是Prophet系统设计的核心理念

