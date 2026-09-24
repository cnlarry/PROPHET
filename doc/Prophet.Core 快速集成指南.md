# Prophet.Core 快速集成指南

## 1. 快速开始

### 1.1 环境要求

| 组件 | 版本要求 | 说明 |
|------|----------|------|
| Visual Studio | 2022/2026 | C++编译环境 |
| CMake | 3.15+ | 构建系统 |
| Python | 3.10+ | Python API支持 |
| TA-Lib | 0.4.0+ | 技术指标计算 |
| Git | 最新版 | 版本控制 |

### 1.2 编译与安装

#### 1.2.1 编译Prophet.Core

```powershell
cd Prophet.Core
.uild.bat
```

**构建脚本会自动完成：**
- 配置CMake
- 编译C++代码
- 生成Python绑定和C API
- 验证编译结果

#### 1.2.2 编译选项

| 选项 | 说明 |
|------|------|
| `--debug` | 仅编译Debug版本 |
| `--release` | 仅编译Release版本 |
| `--both` | 编译Debug和Release版本（默认） |
| `--help` | 显示帮助信息 |

### 1.3 简单示例

#### 1.3.1 Python示例

```python
import sys
import os

# 将Prophet.Core添加到Python路径
sys.path.insert(0, os.path.abspath('Prophet.Core'))

# 导入Prophet.Core
import Core

# 创建引擎
dsl_code = "ALL{1=1}=HOLD;"
engine = Core.Engine(dsl_code)

# 设置K线数据（示例数据）
open_5m = [100.0, 101.0, 102.0] * 100
high_5m = [101.0, 102.0, 103.0] * 100
low_5m = [99.0, 100.0, 101.0] * 100
close_5m = [101.0, 102.0, 103.0] * 100
volume_5m = [1000.0, 2000.0, 3000.0] * 100
open_time_5m = [1620000000000 + i * 300000 for i in range(300)]
close_time_5m = [1620000300000 + i * 300000 for i in range(300)]

# 设置初始300根K线
engine.set_klines("5m", open_5m, high_5m, low_5m, close_5m, volume_5m, open_time_5m, close_time_5m)

# 增量追加新K线
new_open = 104.0
new_high = 105.0
new_low = 103.0
new_close = 104.0
new_volume = 4000.0
new_open_time = 1620000300000 + 300000
new_close_time = 1620000600000 + 300000

engine.append_kline("5m", new_open, new_high, new_low, new_close, new_volume, new_open_time, new_close_time)

# 获取信号
current_price = new_close
current_time = new_close_time
signal = engine.get_signal(current_price, current_time)

# 输出结果
print(f"信号: {signal.action}")
print(f"置信度: {signal.confidence:.2f}")
print(f"原因: {signal.reason}")
```

#### 1.3.2 C#示例

```csharp
using System;
using System.Runtime.InteropServices;

namespace ProphetClient
{
    class Program
    {
        // 导入C API函数
        [DllImport("prophet_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Prophet_CreateEngine(string dsl_code, string params_json);

        [DllImport("prophet_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Prophet_SetKlines(IntPtr engine, string timeframe, 
            double[] opens, double[] highs, double[] lows, double[] closes, 
            double[] volumes, long[] open_times, long[] close_times, int count);

        [DllImport("prophet_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Prophet_AppendKline(IntPtr engine, string timeframe, 
            double open, double high, double low, double close, double volume, 
            long open_time, long close_time);

        [DllImport("prophet_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Prophet_GetSignal(IntPtr engine, double current_price, 
            long current_time, ref Signal signal);

        [DllImport("prophet_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Prophet_DestroyEngine(IntPtr engine);

        // 信号结构体
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct Signal
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
            public string Action;
            public double Confidence;
            public double TakeProfit;
            public double StopLoss;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string Reason;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
            public string ConfigsJson;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4096)]
            public string IndicatorsJson;
        }

        static void Main(string[] args)
        {
            // 创建引擎
            string dslCode = "ALL{1=1}=HOLD;";
            IntPtr engine = Prophet_CreateEngine(dslCode, "{}");
            if (engine == IntPtr.Zero)
            {
                Console.WriteLine("创建引擎失败");
                return;
            }

            try
            {
                // 设置初始300根K线（示例数据）
                double[] opens = new double[300];
                double[] highs = new double[300];
                double[] lows = new double[300];
                double[] closes = new double[300];
                double[] volumes = new double[300];
                long[] openTimes = new long[300];
                long[] closeTimes = new long[300];

                for (int i = 0; i < 300; i++)
                {
                    opens[i] = 100.0 + i * 0.1;
                    highs[i] = opens[i] + 0.5;
                    lows[i] = opens[i] - 0.5;
                    closes[i] = opens[i] + 0.2;
                    volumes[i] = 1000 + i * 10;
                    openTimes[i] = 1620000000000 + i * 300000;
                    closeTimes[i] = 1620000300000 + i * 300000;
                }

                Prophet_SetKlines(engine, "5m", opens, highs, lows, closes, volumes, openTimes, closeTimes, 300);

                // 增量追加新K线
                double newOpen = 130.0;
                double newHigh = 130.5;
                double newLow = 129.5;
                double newClose = 130.2;
                double newVolume = 4000.0;
                long newOpenTime = 1620000300000 + 300000;
                long newCloseTime = 1620000600000 + 300000;

                Prophet_AppendKline(engine, "5m", newOpen, newHigh, newLow, newClose, newVolume, newOpenTime, newCloseTime);

                // 获取信号
                Signal signal = new Signal();
                int result = Prophet_GetSignal(engine, newClose, newCloseTime, ref signal);

                if (result == 0)
                {
                    Console.WriteLine($"信号: {signal.Action}");
                    Console.WriteLine($"置信度: {signal.Confidence:F2}");
                    Console.WriteLine($"原因: {signal.Reason}");
                }
                else
                {
                    Console.WriteLine($"获取信号失败，错误码: {result}");
                }
            }
            finally
            {
                // 释放引擎
                Prophet_DestroyEngine(engine);
            }
        }
    }
}
```

## 2. 核心概念

### 2.1 模块化架构

Prophet.Core采用7个独立DLL模块设计，实现高内聚低耦合：

| 模块 | 功能 |
|------|------|
| prophet_core_common.dll | 公共类型和工具 |
| prophet_core_logging.dll | 日志系统 |
| prophet_core_cache.dll | 缓存管理 |
| prophet_core_indicators.dll | 指标计算 |
| prophet_core_functions.dll | 函数库 |
| prophet_core_dsl.dll | DSL解析和执行 |
| prophet_core_engine.dll | 核心引擎 |

### 2.2 工作原理

1. **初始化阶段**：创建引擎实例，解析DSL策略
2. **数据准备阶段**：为每个时间框架设置初始300根K线
3. **回测/实时阶段**：
   - 增量追加新K线
   - 自动更新缓存
   - 生成交易信号
4. **清理阶段**：释放资源

### 2.3 关键术语

| 术语 | 说明 |
|------|------|
| DSL | 领域特定语言，用于编写交易策略 |
| 时间框架 | K线周期，如5m、15m、1h |
| 增量更新 | 只追加新K线，不重新加载全部数据 |
| 滑动窗口 | 固定300根K线，自动移除最旧数据 |
| 环境变量 | 策略执行时的外部参数，如恐慌指数 |

## 3. API参考

### 3.1 Python API

#### 3.1.1 核心类

| 类名 | 说明 |
|------|------|
| `Core.Engine` | 策略引擎类，用于生成交易信号 |
| `Core.Signal` | 信号结果类，包含信号类型、置信度等 |
| `Core.Value` | 通用值类型，用于DSL表达式求值 |

#### 3.1.2 主要方法

| 方法 | 说明 | 参数 | 返回值 |
|------|------|------|--------|
| `Engine(dsl_code, params)` | 构造函数 | `dsl_code`: 策略代码<br>`params`: 初始参数 | 引擎实例 |
| `set_klines(timeframe, opens, highs, lows, closes, volumes, open_times, close_times)` | 设置初始K线 | `timeframe`: 时间框架<br>`opens`: 开盘价数组<br>`highs`: 最高价数组<br>`lows`: 最低价数组<br>`closes`: 收盘价数组<br>`volumes`: 成交量数组<br>`open_times`: 开盘时间数组<br>`close_times`: 收盘时间数组 | 无 |
| `append_kline(timeframe, open, high, low, close, volume, open_time, close_time)` | 追加单根K线 | `timeframe`: 时间框架<br>`open`: 开盘价<br>`high`: 最高价<br>`low`: 最低价<br>`close`: 收盘价<br>`volume`: 成交量<br>`open_time`: 开盘时间<br>`close_time`: 收盘时间 | 无 |
| `get_signal(current_price, current_time, env_data=None)` | 获取信号 | `current_price`: 当前价格<br>`current_time`: 当前时间<br>`env_data`: 环境变量（可选） | `Signal` 对象 |
| `set_fear_greed_series(series)` | 设置恐惧与贪婪指数序列 | `series`: FearGreedData列表 | 无 |
| `set_funding_rate_series(series)` | 设置资金费率序列 | `series`: FundingRateData列表 | 无 |
| `set_long_short_ratio_series(series)` | 设置多空比序列 | `series`: LongShortRatioData列表 | 无 |

### 3.2 C# API

#### 3.2.1 核心函数

| 函数名 | 说明 |
|--------|------|
| `Prophet_CreateEngine` | 创建引擎实例 |
| `Prophet_SetKlines` | 设置初始K线 |
| `Prophet_AppendKline` | 追加单根K线 |
| `Prophet_GetSignal` | 获取交易信号 |
| `Prophet_GetSignalWithEnv` | 获取交易信号（带环境变量） |
| `Prophet_SetFearGreedSeries` | 设置恐惧与贪婪指数序列数据 |
| `Prophet_SetFundingRateSeries` | 设置资金费率序列数据 |
| `Prophet_SetLongShortRatioSeries` | 设置多空比序列数据 |
| `Prophet_DestroyEngine` | 释放引擎资源 |

#### 3.2.2 信号结构体

```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Signal
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
    public string Action;         // 信号类型: BUY/SELL/HOLD
    public double Confidence;     // 置信度: 0.0-1.0
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
    public string Trend;          // 趋势: BULLISH/BEARISH/NEUTRAL
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
    public string Reason;         // 信号生成原因
    public double TakeProfit;     // 止盈价格
    public double StopLoss;       // 止损价格
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
    public string ConfigsJson;    // 配置信息JSON
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4096)]
    public string IndicatorsJson; // 指标快照JSON
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8192)]
    public string DebugJson;      // 调试信息JSON
}
```

## 4. 最佳实践

### 4.1 性能优化

1. **重用引擎实例**：避免频繁创建和销毁引擎
2. **使用NumPy数组**：Python中使用NumPy数组传递数据，实现零拷贝
3. **批量操作**：尽量使用批量方法，减少API调用次数
4. **合理设置步长**：回测时根据需要设置合适的评估步长
5. **关闭不必要的日志**：Release模式下关闭详细日志

### 4.2 错误处理

1. **Python错误处理**：
   ```python
try:
    engine = Prophet.Engine(dsl_code, params)
except Exception as e:
    print(f"创建引擎失败: {e}")
    ```

2. **C#错误处理**：
   ```csharp
int result = Prophet_SetKlines(engine, ...);
if (result != 0)
{
    Console.WriteLine($"设置K线失败，错误码: {result}");
}
```

### 4.3 调试技巧

1. **启用详细日志**：
   - Python: 设置环境变量 `PROPHET_API_VERBOSE_LOG=1`
   - C++: 在DEBUG模式下编译

2. **检查DLL版本**：
   ```csharp
var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prophet_core.dll");
var fileInfo = new FileInfo(dllPath);
Console.WriteLine($"DLL大小: {fileInfo.Length / 1024.0:F2} KB");
```

### 4.4 C#对接关键陷阱与解决方案

在C#与C++核心引擎对接过程中，我们遇到了多个严重问题，耗费大量时间排查。以下是关键陷阱及解决方案：

#### ❌ 陷阱1: C#/C++ 结构体字段不匹配导致崩溃

**问题描述**：
- C# `Signal` 结构体与 C++ `NativeSignal` 结构体字段大小、顺序不一致
- 导致内存对齐错误，程序在调用 `Prophet_GetSignal` 时崩溃
- **崩溃时机**：无任何错误信息，程序直接闪退

**解决方案**：
确保C#结构体与C++结构体完全匹配：
- 字段顺序完全一致
- 每个字段大小完全匹配
- 添加 `CharSet = CharSet.Ansi` 到 `StructLayout`
- 包含所有必需字段

#### ❌ 陷阱2: DLL版本不匹配

**问题描述**：
- 客户端加载的是旧版本 `prophet_core.dll`（1.3MB）
- 实际编译的是新版本（5.1MB）
- 导致函数签名不匹配或缺少字段

**解决方案**：
在 `.csproj` 文件中添加自动复制逻辑：
```xml
<!-- Prophet.Client.csproj -->
<ItemGroup>
  <!-- 根据配置自动复制正确版本的DLL -->
  <None Include="..\Prophet.Core\build\bin\Debug\prophet_core.dll" 
        Condition="'$(Configuration)' == 'Debug'">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>prophet_core.dll</Link>
  </None>
  <None Include="..\Prophet.Core\build\bin\Release\prophet_core.dll" 
        Condition="'$(Configuration)' == 'Release'">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>prophet_core.dll</Link>
  </None>
</ItemGroup>
```

#### ❌ 陷阱3: 环境变量未正确传递

**问题描述**：
- DSL中使用了 `FEARGREED()`、`FUNDINGRATE()`、`CURRENT().longshort()` 等函数
- 但C#端未传递环境变量或时间序列数据给核心引擎
- 导致函数返回默认值或出错

**解决方案**：
1. **环境变量方式**（适用于简单数值，如FEARGREED）：
   总是使用 `Prophet_GetSignalWithEnv` 并自动注入环境变量：
```csharp
// ✅ 正确做法：自动注入所有环境变量
var envKeys = new List<string>();
var envValues = new List<ProphetCoreEngine.EnvValue>();

// 自动注入恐慌指数
if (_fearGreedData != null && _fearGreedData.Count > 0)
{
    var closestFearGreed = _fearGreedData
        .Where(fg => fg.Date <= currentTime)
        .OrderByDescending(fg => fg.Date)
        .FirstOrDefault();
    
    if (closestFearGreed != null)
    {
        envKeys.Add("FEARGREED");
        envValues.Add(new ProphetCoreEngine.EnvValue
        {
            NumberValue = closestFearGreed.Value,
            IsNumber = 1
        });
    }
}

// 总是使用带环境变量的API（即使环境变量为空）
int result = ProphetCoreEngine.Prophet_GetSignalWithEnv(
    _engineHandle,
    currentCandle.Close,
    currentTimeMs,
    envKeys.ToArray(),
    envValues.ToArray(),
    envKeys.Count,
    ref coreSignal
);
```

2. **时间序列数据方式**（适用于历史数据，如FUNDINGRATE、LONGSHORT）：
   使用专门的API设置时间序列数据，支持历史偏移量访问：
```csharp
// ✅ 正确做法：在引擎初始化后设置时间序列数据
// 1. 设置资金费率序列数据
if (_fundingRateData != null && _fundingRateData.Count > 0)
{
    var timestamps = _fundingRateData.Select(d => d.CalcTime).ToArray();
    var values = _fundingRateData.Select(d => (double)d.LastFundingRate).ToArray();
    
    int result = ProphetCoreEngine.Prophet_SetFundingRateSeries(
        _engineHandle,
        timestamps,
        values,
        _fundingRateData.Count
    );
    
    if (result != 0)
    {
        Console.WriteLine($"⚠️ 设置资金费率数据失败");
    }
}

// 2. 设置多空比序列数据
if (_longShortRatioData != null && _longShortRatioData.Count > 0)
{
    var timestamps = _longShortRatioData
        .Select(d => new DateTimeOffset(d.UpdateTime).ToUnixTimeSeconds())
        .ToArray();
    
    // 计算多头和空头比例
    var longRatios = _longShortRatioData.Select(d => 
    {
        if (d.LongPositionRatio > 0 && d.LongPositionRatio <= 1.0m)
            return (double)d.LongPositionRatio;
        else if (d.LongShortRatio > 0)
            return (double)(d.LongShortRatio / (d.LongShortRatio + 1.0m));
        return 0.5;
    }).ToArray();
    
    var shortRatios = longRatios.Select(lr => 1.0 - lr).ToArray();
    var ratios = _longShortRatioData.Select(d => (double)d.LongShortRatio).ToArray();
    
    int result = ProphetCoreEngine.Prophet_SetLongShortRatioSeries(
        _engineHandle,
        timestamps,
        longRatios,
        shortRatios,
        ratios,
        _longShortRatioData.Count
    );
    
    if (result != 0)
    {
        Console.WriteLine($"⚠️ 设置多空比数据失败");
    }
}
```

**重要提示**：
- 时间序列数据必须在调用 `Prophet_GetSignal` 之前设置
- 数据应按时间戳降序排列（最新的在前）
- 支持使用偏移量访问历史数据，如 `CURRENT().longshort(-1).ratio` 访问前一个周期的数据

#### ❌ 陷阱4: C++ 日志输出被优化掉

**问题描述**：
- Release模式下，`std::cerr` 输出被编译器优化
- 导致无法看到C++核心引擎的调试信息
- 核心引擎变成"黑盒"，难以调试

**解决方案**：
使用条件编译控制日志输出：
```cpp
// strategy_api.cpp
// C++ API 详细日志开关（仅在DEBUG模式或设置环境变量时启用）
#if defined(_DEBUG) || defined(PROPHET_API_VERBOSE_LOG)
    #define API_LOG_ENABLED 1
#else
    #define API_LOG_ENABLED 0
#endif

#if API_LOG_ENABLED
    #define API_LOG(msg) std::cerr << msg << std::endl; std::cerr.flush()
#else
    #define API_LOG(msg) ((void)0)
#endif
```

**启用详细日志**：
- Debug模式：自动启用
- Release模式：设置环境变量 `PROPHET_API_VERBOSE_LOG=1` 然后重新编译

#### ❌ 陷阱5: 回测循环阻塞UI线程

**问题描述**：
- 回测是一个大循环（8000+次迭代）
- 所有代码运行在UI线程
- 导致界面无响应，进度条不更新

**解决方案**：
使用 `async/await` 并定期让出控制权：
```csharp
public async Task<BacktestResult> RunAsync(/*...*/)
{
    int progressReportInterval = Math.Max(totalSteps / 100, 10);
    
    for (int i = startStep * stepSize; i < totalCandles; i += stepSize)
    {
        cancellationToken.ThrowIfCancellationRequested();
        currentStep++;
        
        // 进度报告（减少输出频率）
        if (currentStep % progressReportInterval == 0)
        {
            double progress = (double)currentStep / (totalSteps - startStep) * 100;
            Console.WriteLine($"⏳ 回测进度: {progress:F1}% ({currentStep}/{totalSteps})");
            
            // ✅ 关键：让出控制权给UI线程
            await Task.Yield();
        }
        
        // ... 信号生成和订单处理 ...
    }
}
```

### 4.5 DSLAnalyzer使用指南

DSLAnalyzer是一个强大的工具，用于分析DSL策略并提取关键信息。以下是核心功能：

#### 核心函数

1. **ExtractTimeframes(dslCode)**
   - 提取策略使用的时间框架
   - 返回示例：`["5m", "15m", "1h"]`

2. **ExtractIndicatorPeriods(dslCode)**
   - 提取指标周期
   - 返回示例：`{ "RSI": [14], "EMA": [20, 50], "MACD": [12, 26, 9] }`

3. **CalculateDateRange(dslCode, timeframe, start, end)**
   - 计算含预热期的数据范围
   - 预热期 = 最大指标周期 * 3

4. **CalculateRequiredKlines(dslCode, timeframe, start, end)**
   - 计算所需K线数量

#### 使用示例

```csharp
// 分析DSL，提取时间框架
var analyzer = new DSLAnalyzer();
var timeframes = analyzer.ExtractTimeframes(dsl_code); // ["5m", "15m", "1h"]

// 为每个时间框架准备数据
foreach (var tf in timeframes)
{
    var (actualStart, actualEnd) = analyzer.CalculateDateRange(
        dsl_code, tf, backtestStart, backtestEnd
    );
    
    var candles = await dataFeed.LoadCandlesAsync(symbol, tf, actualStart, actualEnd);
    
    // 设置初始300根K线
    ProphetCoreEngine.SetKlines(engine, tf, 
        candles.Take(300).Select(c => c.Open).ToArray(),
        candles.Take(300).Select(c => c.High).ToArray(),
        candles.Take(300).Select(c => c.Low).ToArray(),
        candles.Take(300).Select(c => c.Close).ToArray(),
        candles.Take(300).Select(c => c.Volume).ToArray(),
        candles.Take(300).Select(c => c.OpenTime).ToArray(),
        candles.Take(300).Select(c => c.CloseTime).ToArray(),
        300
    );
}
```

## 5. 数据准备流程

### 5.1 智能数据准备系统

v10.0引入了完整的客户端数据准备系统，确保回测前所有数据准备就绪：

#### 核心组件

1. **DSLAnalyzer** - DSL分析器
   - 提取策略使用的时间框架和指标周期
   - 计算含预热期的数据范围

2. **DataIntegrityChecker** - 数据完整性检查器
   - 检查数据库中指定时间范围的K线数据完整性
   - 识别数据缺口（缺失的K线）
   - 计算数据完整度百分比

3. **BinanceHistoricalDataDownloader** - 币安历史数据下载器
   - 从 `data.binance.vision` 下载Futures历史数据
   - 智能下载策略：月度 → 日度 → API补齐
   - 自动处理当前月份和今天的数据

4. **BinanceGapFiller** - 币安API数据补齐器
   - 使用币安公开API补齐小数据缺口
   - 速率限制和重试机制

5. **TimeSeriesDataIntegrityChecker** - 时间序列数据完整性检查器
   - 检查恐惧与贪婪指数、资金费率、多空比数据的完整性
   - 自动识别数据缺口
   - 从API补齐缺失数据
   - 支持多周期数据（如多空比的5m、15m、1h等）

6. **BacktestDataPreparationService** - 回测数据准备服务
   - 协调上述所有组件
   - 完整的数据准备流程（K线 + 时间序列数据）
   - 进度报告和取消支持

#### 智能下载策略

**K线数据下载流程：**
```
数据缺口: 2025-10-24 ~ 2025-11-24 (32天)
    ↓
尝试月度下载
    ├─ 2025-10.zip → ✅ 成功（10月已结束）
    └─ 2025-11.zip → ❌ 404（当前月份）
        ↓ 自动降级
        ├─ 2025-11-01.zip → ✅
        ├─ 2025-11-02.zip → ✅
        ├─ ...
        ├─ 2025-11-23.zip → ✅（昨天）
        └─ 2025-11-24.zip → ⏭️ 跳过（今天，未生成）
    ↓
重新检查缺口
    ↓
API补齐剩余小缺口
    └─ 2025-11-24 00:00 ~ 15:55 → 币安API
```

**时间序列数据准备流程：**
```
回测开始前
    ↓
1. 检查恐惧与贪婪指数数据完整性
   ├─ 计算需要的时间范围（回测期 + 预热期）
   ├─ 从数据库查询现有数据
   ├─ 识别数据缺口
   └─ 从API补齐缺失数据
    ↓
2. 检查资金费率数据完整性
   ├─ 计算需要的时间范围（考虑结算周期）
   ├─ 从数据库查询现有数据
   ├─ 识别数据缺口
   └─ 从API批量补齐缺失数据
    ↓
3. 检查多空比数据完整性
   ├─ 确定周期（默认5m，与K线周期一致）
   ├─ 计算需要的时间范围（回测期 + 100个周期预热）
   ├─ 从数据库查询现有数据
   ├─ 识别数据缺口
   └─ 从API批量补齐缺失数据
    ↓
4. 加载所有时间序列数据到内存
   └─ 传递给核心引擎
```

**数据加载优先级：**
1. **优先从数据库加载**：性能好，支持历史偏移量访问
2. **缺失时从API补齐**：自动识别缺口并批量获取
3. **数据对齐验证**：确保时间戳与K线数据对齐

### 5.2 数据库优化建议

为提升数据完整性检查和回测数据加载性能，建议执行以下SQL脚本：

```sql
-- 核心索引1: 时间范围查询（最重要！）
CREATE INDEX IF NOT EXISTS idx_klines_symbol_interval_time_range
ON klines(symbol, interval, open_time);

-- 核心索引2: 覆盖索引（避免回表）
CREATE INDEX IF NOT EXISTS idx_klines_backtest_covering
ON klines(symbol, interval, open_time, open, high, low, close, volume);

-- 核心索引3: 最新数据查询
CREATE INDEX IF NOT EXISTS idx_klines_latest
ON klines(symbol, interval, open_time DESC);
```

**性能提升**：
- 数据完整性检查：10-100倍 ⚡
- 回测数据加载：2-5倍 ⚡
- 最新数据查询：极大提升 ⚡

## 6. C#对接完整检查清单

在C#项目中对接核心引擎时，请按此清单逐项检查：

**✅ 结构体定义**
- [ ] C# `Signal` 结构体与 C++ `NativeSignal` **字段顺序完全一致**
- [ ] 每个字段的**大小完全匹配**（如 `Action[20]` vs `SizeConst = 20`）
- [ ] 添加了 `CharSet = CharSet.Ansi` 到 `StructLayout`
- [ ] 包含所有必需字段（Action, Confidence, TakeProfit, StopLoss, Reason, ConfigsJson, IndicatorsJson）

**✅ DLL管理**
- [ ] `.csproj` 文件配置了自动复制 `prophet_core.dll`
- [ ] 根据 Debug/Release 配置复制正确版本
- [ ] 启动时验证DLL大小（应≥5MB）
- [ ] 验证DLL路径正确

**✅ 环境变量传递**
- [ ] 总是使用 `Prophet_GetSignalWithEnv`（不要用简化版）
- [ ] 自动注入 FEARGREED（环境变量方式）
- [ ] 从最接近当前时间的数据点查找值
- [ ] 包含用户自定义参数

**✅ 时间序列数据传递**
- [ ] 在引擎初始化后设置时间序列数据
- [ ] 使用 `Prophet_SetFundingRateSeries` 设置资金费率数据
- [ ] 使用 `Prophet_SetLongShortRatioSeries` 设置多空比数据
- [ ] 数据按时间戳降序排列（最新的在前）
- [ ] 在调用 `Prophet_GetSignal` 之前完成数据设置

**✅ 异常处理**
- [ ] 所有P/Invoke调用包裹在 `try-catch` 中
- [ ] 检查返回码（0=成功，非0=失败）
- [ ] 失败时调用 `Prophet_GetLastStrategyError()` 获取详细错误
- [ ] 显示友好的错误消息给用户

**✅ 性能优化**
- [ ] 回测方法声明为 `async Task`
- [ ] 循环中定期调用 `await Task.Yield()`
- [ ] 减少控制台输出频率（每1%或10步一次）
- [ ] 简化单次信号生成的日志

**✅ 调试支持**
- [ ] C++端添加了条件编译日志开关
- [ ] 保留关键错误信息的输出（不受开关控制）
- [ ] 添加了结构体布局诊断代码（调试时启用）
- [ ] 记录DLL版本和大小信息

---

## 7. 信号实体详解

### 7.1 Signal结构说明

`Signal` 是Prophet.Core引擎返回的核心数据结构，包含了策略评估后的完整交易决策信息。

#### 7.1.1 基础字段

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `id` | string | 信号唯一标识（自动生成）|
| `strategy` | string | 策略名称 |
| `action` | string | 交易动作：`BUY`、`SELL`、`HOLD` |
| `confidence` | double | 置信度（0.0-1.0） |
| `reason` | string | 触发原因（信号函数toString） |
| `trend` | string | 趋势判断：`BULLISH`、`BEARISH`、`NEUTRAL` |
| `sl` | double | 止损价格 |
| `tp` | double | 止盈价格 |
| `create_at` | int64 | 创建时间（Unix时间戳，秒） |

#### 7.1.2 K线形态列表

`patterns` 字段包含识别到的K线形态数组，每个形态包括：

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `name` | string | 形态名称（如HAMMER、DOJI） |
| `side` | string | 方向：BULLISH（看涨）/BEARISH（看跌）/NEUTRAL（中性） |
| `raw` | int | TA-Lib原始值（-100到100） |
| `score` | int | 自定义打分 |
| `rank` | int | 排名 |
| `kline_open_time` | int64 | K线开盘时间（毫秒时间戳） |
| `kline_close_time` | int64 | K线收盘时间（毫秒时间戳） |
| `description` | string | 形态说明 |

#### 7.1.3 结构化指标快照（新增）

**`indicator_snapshots` 字段是信号分析的核心**，记录了每个触发条件的完整上下文，用于精准分析开仓原因。

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `condition_id` | string | 条件ID（由条件表达式生成） |
| `label` | string | 可读标签（基于条件表达式自动生成） |
| `timeframe` | string | 时间框架（如5m、15m、1h） |
| `indicator` | string | 指标名称（如MACD、RSI） |
| `offset` | int | 偏移量（0=当前K线，-1=前一根，-2=前两根） |
| `ohlcv` | object | K线快照（用于计算指标的数据） |
| `parameters` | object | 指标参数快照 |
| `results` | object | 指标计算结果 |
| `comparison` | string | 比较表达式（如"macd > signal"） |
| `status` | string | 状态：`hit`（命中）或`miss`（未命中） |

**OHLCV快照示例：**
```json
{
  "open": 57950.0,
  "high": 59880.0,
  "low": 56980.0,
  "close": 57990.0,
  "volume": 1234.0
}
```

**parameters快照示例：**
```json
{
  "fast": 12,
  "slow": 26,
  "signal": 9
}
```

**results快照示例：**
```json
{
  "macd": 1.23,
  "signal": 0.98,
  "hist": 0.25
}
```

#### 7.1.4 完整信号示例

```json
{
  "id": "abc123...",
  "strategy": "动量突破策略",
  "action": "BUY",
  "confidence": 0.85,
  "reason": "MIN(2){MACD(5m).macd > MACD(5m).signal, RSI(5m).value < 30}",
  "trend": "BULLISH",
  "sl": 57500.0,
  "tp": 59500.0,
  "patterns": [...],
  "indicator_snapshots": [
    {
      "condition_id": "cond_macd_cross",
      "label": "MACD(5m).macd > MACD(5m).signal",
      "timeframe": "5m",
      "indicator": "MACD",
      "offset": 0,
      "ohlcv": {"open": 57950.0, "high": 59880.0, "low": 56980.0, "close": 57990.0, "volume": 1234.0},
      "parameters": {"fast": 12, "slow": 26, "signal": 9},
      "results": {"macd": 1.23, "signal": 0.98, "hist": 0.25},
      "comparison": "macd > signal",
      "status": "hit"
    }
  ],
  "create_at": 1638360300
}
```

### 7.2 异常信号处理

#### 7.2.1 异常信号识别

核心引擎在遇到异常情况时，会返回 `action="HOLD"` 的信号，并在 `reason` 字段中包含错误信息。错误信息使用前缀标识错误类型：

| 前缀 | 说明 | 示例 |
|------|------|------|
| `[VALIDATION]` | 参数验证错误（缺失参数、超出范围等） | `[VALIDATION] Invalid current_price: -1.0 (must be > 0)` |
| `[EVALUATOR]` | DSL表达式求值错误 | `[EVALUATOR] Indicator not found: MACD(5m)` |
| `[DSL]` | DSL语法或语义错误 | `[DSL] Parser error: Unexpected token` |
| `[RUNTIME]` | 运行时异常 | `[RUNTIME] Division by zero` |
| `[UNKNOWN]` | 未知异常 | `[UNKNOWN] Unexpected exception occurred` |

#### 7.2.2 客户端处理异常信号

**Python客户端示例：**
```python
signal = engine.get_signal(current_price, current_time)

# 检查是否为异常信号
if signal.action == "HOLD" and signal.reason.startswith("["):
    error_type = signal.reason.split("]")[0].replace("[", "")
    print(f"⚠️ 核心引擎异常: {error_type}")
    print(f"   错误详情: {signal.reason}")
    # 不执行交易，但记录错误信息用于分析
    return None
```

**C#客户端示例：**
```csharp
var signal = generator.GenerateSignal(currentCandle, historicalCandles);

// 检查是否为异常信号
if (signal != null && signal.Action == SignalAction.HOLD && 
    !string.IsNullOrEmpty(signal.Description) &&
    signal.Description.StartsWith("["))
{
    Console.WriteLine($"⚠️ [核心引擎验证错误] {signal.Description}");
    // 不返回信号，避免触发交易
    return null;
}
```

#### 7.2.3 常见异常情况

**1. 参数缺失**
```
[VALIDATION] Parameter not found: MACD(5m).FAST_PERIOD. 
Please set it using '$(5m).MACD.FAST_PERIOD = value;' or configure it in JSON.
```

**2. 参数超出范围**
```
[VALIDATION] Parameter PERIOD must be in range (0, 10000], got: -5
```

**3. 指标不存在**
```
[EVALUATOR] Indicator not found: MACD(5m)
```

**4. K线数据不足**
```
[EVALUATOR] Cannot calculate indicator MACD(5m): K-line data is empty
```

**5. 无效的输入参数**
```
[VALIDATION] Invalid current_price: -1.0 (must be > 0)
```

#### 7.2.4 异常信号统计

建议在回测或实盘中统计异常信号，用于分析策略稳定性：

```python
error_signals = []
for signal in all_signals:
    if signal.action == "HOLD" and signal.reason.startswith("["):
        error_type = signal.reason.split("]")[0].replace("[", "")
        error_signals.append({
            "time": signal.create_at,
            "type": error_type,
            "reason": signal.reason
        })

# 统计错误类型
from collections import Counter
error_types = Counter([e["type"] for e in error_signals])
print("异常信号统计:", error_types)
```

### 7.3 信号分析最佳实践

#### 7.3.1 使用indicator_snapshots进行详细分析

结构化指标快照帮助您精准定位信号触发原因：

1. **查看命中条件**
   ```python
   hit_conditions = [s for s in signal.indicator_snapshots if s['status'] == 'hit']
   for cond in hit_conditions:
       print(f"命中: {cond['label']}")
       print(f"  指标: {cond['indicator']}({cond['timeframe']})")
       print(f"  结果: {cond['results']}")
   ```

2. **回溯计算过程**
   ```python
   for snap in signal.indicator_snapshots:
       print(f"{snap['indicator']}({snap['timeframe']})")
       print(f"  参数: {snap['parameters']}")
       print(f"  结果: {snap['results']}")
       print(f"  K线: close={snap['ohlcv']['close']}")
       print(f"  状态: {snap['status']}")
   ```

3. **区分正常HOLD和异常HOLD**
   ```python
   if signal.action == "HOLD":
       if signal.reason.startswith("["):
           # 异常HOLD：包含错误信息
           print(f"⚠️ 异常: {signal.reason}")
       else:
           # 正常HOLD：策略逻辑未触发
           print(f"ℹ️ 正常HOLD: {signal.reason}")
   ```

3. **数据库查询分析**
   ```sql
   -- SQLite: 查询MACD金叉信号
   SELECT * FROM signals 
   WHERE JSON_EXTRACT(indicator_snapshots, '$[0].indicator') = 'MACD'
   AND JSON_EXTRACT(indicator_snapshots, '$[0].status') = 'hit';
   
   -- MySQL: 同样的查询
   SELECT * FROM signals 
   WHERE JSON_EXTRACT(indicator_snapshots, '$[0].indicator') = 'MACD'
   AND JSON_EXTRACT(indicator_snapshots, '$[0].status') = 'hit';
   ```

#### 7.2.2 为什么需要indicator_snapshots？

传统的 `reason` 字段只能提供简单的文本描述（如"ALL{条件1, 条件2} = BUY"），无法回答：
- 哪些条件命中了？哪些没命中？
- 命中时指标的具体值是多少？
- 使用了什么参数？
- 当时的K线数据是什么？

**indicator_snapshots** 完美解决了这些问题，提供了：
- ✅ 结构化的条件列表（命中/未命中）
- ✅ 完整的指标计算结果
- ✅ 参数快照（便于复现）
- ✅ K线数据快照（完整上下文）
- ✅ 可查询的JSON格式（支持数据库分析）

### 7.3 Python访问示例

```python
import Prophet

# 创建引擎并获取信号
engine = Prophet.Engine(dsl_code)
signal = engine.get_signal(price, time)

# 访问基础信息
print(f"动作: {signal.action}")
print(f"置信度: {signal.confidence}")
print(f"原因: {signal.reason}")

# 访问指标快照（新增）
for snapshot in signal.indicator_snapshots:
    print(f"\n条件: {snapshot.label}")
    print(f"  状态: {snapshot.status}")
    print(f"  指标: {snapshot.indicator}")
    print(f"  时间框架: {snapshot.timeframe}")
    print(f"  参数: {snapshot.parameters}")
    print(f"  结果: {snapshot.results}")
    print(f"  OHLCV: {snapshot.ohlcv}")
```

### 7.4 C#访问示例

```csharp
// 假设已通过Prophet.Client回测模块获取信号
var signal = backtestEngine.GetSignal(...);

// 访问基础信息
Console.WriteLine($"动作: {signal.Action}");
Console.WriteLine($"置信度: {signal.Confidence}");

// 访问结构化指标快照
if (!string.IsNullOrEmpty(signal.IndicatorSnapshotsJson))
{
    var snapshots = JsonSerializer.Deserialize<List<IndicatorSnapshot>>(
        signal.IndicatorSnapshotsJson
    );
    
    foreach (var snap in snapshots)
    {
        Console.WriteLine($"条件: {snap.Label}");
        Console.WriteLine($"  状态: {snap.Status}");
        Console.WriteLine($"  指标: {snap.Indicator}({snap.Timeframe})");
    }
}
```