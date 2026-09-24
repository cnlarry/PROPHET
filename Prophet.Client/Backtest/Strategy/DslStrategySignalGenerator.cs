using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;
using Prophet.Client.Services.Data;

namespace Prophet.Client.Backtest.Strategy;

/// <summary>
/// DSL策略信号生成器
/// 基于Prophet.Core引擎
/// 
/// 工作流程（参考 Prophet.Core 工作机制.md）：
/// 1. 构造阶段：创建引擎，传入DSL代码和参数
/// 2. 注入时间序列数据：FEARGREED和FUNDINGRATE
/// 3. 运行阶段：循环调用 set_klines + get_signal
/// </summary>
public class DslStrategySignalGenerator : IStrategySignalGenerator, IDisposable
{
    private IntPtr _engineHandle = IntPtr.Zero;
    private string _dslCode = string.Empty;
    private BacktestConfig _config = null!;
    private bool _isInitialized = false;
    private bool _firstHoldSignal = true; // 用于跟踪第一个HOLD信号
    
    // 时间序列数据（由BacktestEngine在数据准备阶段设置）
    private List<FearGreedData>? _fearGreedData;
    private List<FundingRateData>? _fundingRateData;
    private List<LongShortRatioData>? _longShortRatioData;
    
    /// <summary>
    /// 设置时间序列数据（在Initialize之前调用）
    /// </summary>
    public void SetTimeSeriesData(List<FearGreedData> fearGreedData, List<FundingRateData> fundingRateData, List<LongShortRatioData>? longShortRatioData = null)
    {
        _fearGreedData = fearGreedData;
        _fundingRateData = fundingRateData;
        _longShortRatioData = longShortRatioData ?? new List<LongShortRatioData>();
        Console.WriteLine($"📊 时间序列数据已设置: FEARGREED={fearGreedData.Count}, FUNDINGRATE={fundingRateData.Count}, LONGSHORT={_longShortRatioData.Count}");
    }
    
    public void Initialize(string dslCode, BacktestConfig config)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException("策略生成器已经初始化");
        }
        
        _dslCode = dslCode;
        _config = config;
        
        try
        {
            // 0. 检查 Prophet.Core 绑定是否可用
            Console.WriteLine("🔍 检查 Prophet.Core 引擎绑定...");
            try
            {
                ProphetCoreEngine.EnsureInitialized();
                Console.WriteLine("✅ Prophet.Core 引擎绑定可用");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Prophet.Core 引擎绑定不可用: {ex.Message}");
                throw new Exception($"无法使用 Prophet.Core 引擎: {ex.Message}", ex);
            }
            
            // 1. 准备参数JSON
            // 格式: { "指标名": { "时间框架": { "参数名": 值 } } }
            string? paramsJson = null;
            if (config.Parameters.Count > 0)
            {
                // 简化处理：将用户参数转换为JSON
                // 实际使用时，需要根据DSL中的指标来组织参数
                var paramsDict = new Dictionary<string, object>();
                
                // 示例：如果用户提供了 FAST_PERIOD, SLOW_PERIOD, SIGNAL_PERIOD
                // 我们假设这是MACD的参数
                if (config.Parameters.ContainsKey("FAST_PERIOD") && 
                    config.Parameters.ContainsKey("SLOW_PERIOD"))
                {
                    paramsDict["MACD"] = new Dictionary<string, object>
                    {
                        ["5m"] = new Dictionary<string, object>
                        {
                            ["FAST_PERIOD"] = Convert.ToDouble(config.Parameters["FAST_PERIOD"]),
                            ["SLOW_PERIOD"] = Convert.ToDouble(config.Parameters["SLOW_PERIOD"]),
                            ["SIGNAL_PERIOD"] = config.Parameters.ContainsKey("SIGNAL_PERIOD") 
                                ? Convert.ToDouble(config.Parameters["SIGNAL_PERIOD"]) 
                                : 9.0
                        }
                    };
                }
                
                if (config.Parameters.ContainsKey("RSI_PERIOD"))
                {
                    paramsDict["RSI"] = new Dictionary<string, object>
                    {
                        ["5m"] = new Dictionary<string, object>
                        {
                            ["PERIOD"] = Convert.ToDouble(config.Parameters["RSI_PERIOD"])
                        }
                    };
                }
                
                if (paramsDict.Count > 0)
                {
                    paramsJson = JsonSerializer.Serialize(paramsDict);
                }
            }
            
            // 2. 创建引擎
            Console.WriteLine($"🔧 正在创建引擎...");
            Console.WriteLine($"   策略DSL长度: {_dslCode.Length} 字符");
            Console.WriteLine($"   参数JSON: {paramsJson ?? "null"}");
            
            // 确保paramsJson不为null，避免C++侧可能得空指针崩溃
            // 如果为null，传递空JSON对象 "{}"
            string safeParamsJson = paramsJson ?? "{}";
            
            Console.WriteLine($"   传递给C++的参数: {safeParamsJson}");
            Console.WriteLine($"   开始调用 Prophet_CreateEngine...");
            
            try
            {
                _engineHandle = ProphetCoreEngine.Prophet_CreateEngine(_dslCode, safeParamsJson);
                Console.WriteLine($"   Prophet_CreateEngine 返回，句柄: {_engineHandle}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 调用 Prophet_CreateEngine 异常: {ex.GetType().Name}");
                Console.WriteLine($"   错误信息: {ex.Message}");
                Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
                throw new Exception($"调用 Prophet.Core DLL 失败: {ex.Message}", ex);
            }
            
            if (_engineHandle == IntPtr.Zero)
            {
                Console.WriteLine($"⚠️ 引擎创建失败，正在获取错误信息...");
                
                string? error = null;
                try
                {
                    error = ProphetCoreEngine.GetLastErrorMessage();
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"✅ 成功获取C++错误信息:");
                        Console.WriteLine($"   {error}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ C++未返回错误信息");
                        error = "引擎创建返回NULL指针，但未提供具体错误信息";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 获取C++错误信息时发生异常:");
                    Console.WriteLine($"   类型: {ex.GetType().Name}");
                    Console.WriteLine($"   信息: {ex.Message}");
                    error = "无法获取C++错误信息（发生异常）";
                }
                
                Console.WriteLine($"");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"❌ Prophet.Core 引擎创建失败");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"");
                Console.WriteLine($"错误详情: {error}");
                Console.WriteLine($"");
                Console.WriteLine($"可能的原因：");
                Console.WriteLine($"  1. DSL 语法错误（最常见）");
                Console.WriteLine($"     - 拼写错误（如 MACdD 应为 MACD）");
                Console.WriteLine($"     - 缺少分号");
                Console.WriteLine($"     - 括号不匹配");
                Console.WriteLine($"     - 使用了不存在的函数或字段");
                Console.WriteLine($"");
                Console.WriteLine($"  2. 技术问题");
                Console.WriteLine($"     - Prophet.Core DLL 版本不兼容");
                Console.WriteLine($"     - 缺少 VC++ Redistributable 运行时");
                Console.WriteLine($"     - 内存不足");
                Console.WriteLine($"");
                
                // 只在没有获取到C++错误信息时才显示DSL代码
                if (string.IsNullOrEmpty(error) || error.Contains("NULL") || error.Contains("未提供"))
                {
                    try
                    {
                        Console.WriteLine($"DSL 代码内容（用于诊断）：");
                        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        Console.WriteLine(_dslCode);
                        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        Console.WriteLine($"");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 无法显示DSL代码: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"建议：");
                Console.WriteLine($"  1. 仔细检查DSL语法，特别注意拼写错误");
                Console.WriteLine($"  2. 使用策略编辑器的语法检查功能");
                Console.WriteLine($"  3. 参考 Prophet DSL 文档确认函数名称");
                Console.WriteLine($"  4. 如果问题持续，尝试使用 Python 回测: python backtest.py");
                Console.WriteLine($"");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"");
                
                throw new Exception($"创建Prophet.Core引擎失败: {error}");
            }
            
            Console.WriteLine($"✅ 引擎创建成功，句柄: {_engineHandle}");
            
            // 3. 验证引擎
            var ruleCount = ProphetCoreEngine.Prophet_GetRuleCount(_engineHandle);
            if (ruleCount <= 0)
            {
                throw new Exception("DSL代码没有有效的交易规则");
            }
            
            _isInitialized = true;
            
            Console.WriteLine($"✅ Prophet.Core引擎已初始化");
            Console.WriteLine($"   规则数量: {ruleCount}");
            Console.WriteLine($"   DSL代码长度: {dslCode.Length} 字符");
            if (!string.IsNullOrEmpty(paramsJson))
            {
                Console.WriteLine($"   参数配置: {paramsJson}");
            }
            
            // 3. 如果有时间序列数据，注入到引擎
            if (_fearGreedData != null && _fearGreedData.Count > 0)
            {
                try
                {
                    InjectFearGreedData(_fearGreedData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 注入恐慌指数数据失败: {ex.Message}");
                }
            }
            
            if (_fundingRateData != null && _fundingRateData.Count > 0)
            {
                try
                {
                    InjectFundingRateData(_fundingRateData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 注入资金费率数据失败: {ex.Message}");
                }
            }
            
            if (_longShortRatioData != null && _longShortRatioData.Count > 0)
            {
                try
                {
                    InjectLongShortRatioData(_longShortRatioData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 注入多空比数据失败: {ex.Message}");
                }
            }
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 初始化Prophet.Core引擎失败: {ex.Message}");
            
            // 清理资源
            if (_engineHandle != IntPtr.Zero)
            {
                ProphetCoreEngine.Prophet_DestroyEngine(_engineHandle);
                _engineHandle = IntPtr.Zero;
            }
            
            throw;
        }
    }
    
    public Signal? GenerateSignal(Candlestick currentCandle, List<Candlestick> historicalCandles)
    {
        // 🔧 v4.0: 优先使用K线的真实收盘时间，如果不存在则计算
        DateTime closeTime;
        if (currentCandle.CloseTime.HasValue)
        {
            closeTime = currentCandle.CloseTime.Value;
        }
        else
        {
            // 备选方案：计算收盘时间（开盘时间 + 时间框架 - 1秒）
            var timeframeMinutes = GetTimeframeMinutes(_config.SignalSamplingInterval ?? "5m");
            closeTime = currentCandle.Time.AddMinutes(timeframeMinutes).AddSeconds(-1);
        }
        
        return GenerateSignal(currentCandle, closeTime, historicalCandles);
    }
    
    /// <summary>
    /// 生成交易信号（使用指定的收盘时间进行时间对齐）
    /// </summary>
    /// <param name="currentCandle">当前K线</param>
    /// <param name="closeTime">K线收盘时间（UTC时间，用于时间对齐，传递给核心引擎）</param>
    /// <param name="historicalCandles">历史K线（已废弃，保留以兼容接口）</param>
    public Signal? GenerateSignal(Candlestick currentCandle, DateTime closeTime, List<Candlestick> historicalCandles)
    {
        if (!_isInitialized || _engineHandle == IntPtr.Zero)
        {
            Console.WriteLine("⚠️ 引擎未初始化");
            return null;
        }
        
        try
        {
            // 调用引擎获取信号
            var coreSignal = new ProphetCoreEngine.Signal();
            int getSignalResult;
            
            try
            {
                // 使用收盘时间作为CurrentTime传递给引擎
                var closeTimeMs = new DateTimeOffset(closeTime).ToUnixTimeMilliseconds();
                
                // 环境变量概念已废弃，直接调用 Prophet_GetSignal
                getSignalResult = ProphetCoreEngine.Prophet_GetSignal(
                    _engineHandle,
                    currentCandle.Close,
                    closeTimeMs,  // 使用收盘时间
                    ref coreSignal
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Prophet_GetSignal崩溃!");
                Console.WriteLine($"   异常类型: {ex.GetType().Name}");
                Console.WriteLine($"   异常消息: {ex.Message}");
                Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
                throw;
            }
            
            // 核心引擎设计：在任何情况下都会返回信号（包括HOLD信号）
            // 如果getSignalResult != 0，说明是C API层面的错误（如空指针），应该创建HOLD信号
            if (getSignalResult != 0)
            {
                var error = ProphetCoreEngine.GetLastErrorMessage();
                Console.WriteLine($"⚠️ [C API错误] 获取信号失败: {error}");
                // 创建错误HOLD信号并返回
                return new Signal
                {
                    Action = SignalAction.HOLD,
                    SignalPrice = (decimal)currentCandle.Close,
                    Time = closeTime,  // 🔧 v4.0: 使用收盘时间
                    Strength = 0,
                    Description = $"[C_API_ERROR] {error}",
                    Trend = "NEUTRAL"
                };
            }
            
            // 转换C++信号为C#信号
            // 核心引擎只返回 BUY、SELL、HOLD 三种信号，且总是返回信号
            SignalAction action = coreSignal.Action.ToUpper() switch
            {
                "BUY" => SignalAction.BUY,
                "SELL" => SignalAction.SELL,
                "HOLD" => SignalAction.HOLD,
                _ => throw new InvalidOperationException($"Unknown signal action from core engine: {coreSignal.Action}")
            };
            
            // 检查是否为包含错误信息的HOLD信号
            bool isErrorHold = action == SignalAction.HOLD && 
                              !string.IsNullOrEmpty(coreSignal.Reason) &&
                              (coreSignal.Reason.StartsWith("[VALIDATION]") ||
                               coreSignal.Reason.StartsWith("[EVALUATOR]") ||
                               coreSignal.Reason.StartsWith("[DSL]") ||
                               coreSignal.Reason.StartsWith("[RUNTIME]") ||
                               coreSignal.Reason.StartsWith("[UNKNOWN]"));
            
            // 输出HOLD信号的调试信息（只输出第一个HOLD信号）
            if (action == SignalAction.HOLD && _firstHoldSignal)
            {
                if (isErrorHold)
                {
                    Console.WriteLine($"⚠️ [HOLD信号-错误-首个] {currentCandle.Time:yyyy-MM-dd HH:mm:ss} | 价格: {currentCandle.Close:F2} | 原因: {coreSignal.Reason}");
                }
                _firstHoldSignal = false;
            }
            
            // 核心引擎总是返回信号，包括HOLD信号
            // 构造信号对象并返回（即使是HOLD信号也返回，让上层决定如何处理）
            return new Signal
            {
                Action = action,
                SignalPrice = (decimal)currentCandle.Close,
                Time = closeTime,  // 🔧 v4.0: 使用收盘时间（UTC时间，秒部分为59）
                Strength = coreSignal.Confidence,
                Description = coreSignal.Reason,
                TakeProfit = coreSignal.TakeProfit > 0 ? (decimal)coreSignal.TakeProfit : null,
                StopLoss = coreSignal.StopLoss > 0 ? (decimal)coreSignal.StopLoss : null,
                
                // v11.0: 调试信息
                Trend = coreSignal.Trend,
                DebugJson = coreSignal.DebugJson,
                ConfigsJson = coreSignal.ConfigsJson,
                IndicatorsJson = coreSignal.IndicatorsJson,
                IndicatorSnapshotsJson = coreSignal.IndicatorSnapshotsJson,
                
                // 🆕 v4.0: K线数据快照
                KlinesJson = coreSignal.KlinesJson
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 生成信号时出错: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// v10.0: 设置指定时间框架的K线数据（初始化300根）
    /// </summary>
    public void SetKlines(string timeframe, List<Candlestick> candles)
    {
        if (!_isInitialized || _engineHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("引擎未初始化");
        }
        
        if (candles == null || candles.Count == 0)
        {
            throw new ArgumentException("K线数据不能为空");
        }
        
        int count = candles.Count;
        var opens = new double[count];
        var highs = new double[count];
        var lows = new double[count];
        var closes = new double[count];
        var volumes = new double[count];
        var openTimes = new long[count];
        var closeTimes = new long[count];
        
        for (int i = 0; i < count; i++)
        {
            opens[i] = candles[i].Open;
            highs[i] = candles[i].High;
            lows[i] = candles[i].Low;
            closes[i] = candles[i].Close;
            volumes[i] = candles[i].Volume;
            openTimes[i] = new DateTimeOffset(candles[i].Time).ToUnixTimeMilliseconds();
            // 🔧 v4.0: 优先使用真实的收盘时间，如果不存在则计算
            var closeTime = candles[i].CloseTime;
            closeTimes[i] = closeTime.HasValue
                ? new DateTimeOffset(closeTime.Value).ToUnixTimeMilliseconds()
                : openTimes[i] + GetTimeframeMilliseconds(timeframe) - 1;
        }
        
        // 🔍 v4.0: 调试：检查是否有重复的 openTime
        var duplicates = openTimes.GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => new { Time = g.Key, Count = g.Count() })
            .ToList();
        
        if (duplicates.Any())
        {
            Console.WriteLine($"⚠️ 警告：[{timeframe}] 发现 {duplicates.Count} 个重复的开盘时间:");
            foreach (var dup in duplicates.Take(5))
            {
                var dupTime = DateTimeOffset.FromUnixTimeMilliseconds(dup.Time).UtcDateTime;
                Console.WriteLine($"   - {dupTime:yyyy-MM-dd HH:mm:ss} UTC 重复 {dup.Count} 次");
            }
        }
        
        // v10.0: 调用包装方法（会自动检查错误并抛出异常）
        try
        {
            ProphetCoreEngine.SetKlines(
                _engineHandle,
                timeframe,
                opens, highs, lows, closes, volumes,
                openTimes, closeTimes,
                count
            );
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ 注入 {timeframe} K线数据失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// v10.0: 追加单根K线到指定时间框架
    /// </summary>
    public void AppendKline(string timeframe, Candlestick candle)
    {
        if (!_isInitialized || _engineHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("引擎未初始化");
        }
        
        var openTimeMs = new DateTimeOffset(candle.Time).ToUnixTimeMilliseconds();
        // 🔧 v4.0: 优先使用真实的收盘时间，如果不存在则计算
        var closeTimeNullable = candle.CloseTime;
        var closeTimeMs = closeTimeNullable.HasValue
            ? new DateTimeOffset(closeTimeNullable.Value).ToUnixTimeMilliseconds()
            : openTimeMs + GetTimeframeMilliseconds(timeframe) - 1;

        // v10.0: 调用包装方法（会自动检查错误并抛出异常）
        ProphetCoreEngine.AppendKline(
            _engineHandle,
            timeframe,
            candle.Open,
            candle.High,
            candle.Low,
            candle.Close,
            candle.Volume,
            openTimeMs,
            closeTimeMs
        );
    }
    
    /// <summary>
    /// 获取时间框架的毫秒数
    /// </summary>
    private long GetTimeframeMilliseconds(string timeframe)
    {
        if (string.IsNullOrEmpty(timeframe))
            return 60000; // 默认1分钟
        
        var unit = timeframe[^1];
        var valueStr = timeframe[..^1];
        
        if (!int.TryParse(valueStr, out int value))
            return 60000;
        
        return unit switch
        {
            'm' => value * 60000L,
            'h' => value * 3600000L,
            'd' => value * 86400000L,
            'w' => value * 604800000L,
            _ => 60000L
        };
    }
    
    /// <summary>
    /// 获取时间框架的分钟数
    /// </summary>
    private int GetTimeframeMinutes(string timeframe)
    {
        if (string.IsNullOrEmpty(timeframe))
            return 1; // 默认1分钟
        
        var unit = timeframe[^1];
        var valueStr = timeframe[..^1];
        
        if (!int.TryParse(valueStr, out int value))
            return 1;
        
        return unit switch
        {
            'm' => value,
            'h' => value * 60,
            'd' => value * 1440,
            'w' => value * 10080,
            _ => 1
        };
    }
    
    /// <summary>
    /// 注入恐慌指数数据到引擎
    /// </summary>
    private void InjectFearGreedData(List<FearGreedData> data)
    {
        if (_engineHandle == IntPtr.Zero || data.Count == 0)
            return;
        
        var timestamps = data.Select(d => new DateTimeOffset(d.Date).ToUnixTimeSeconds()).ToArray();
        var values = data.Select(d => d.Value).ToArray();
        var classifications = data.Select(d => d.Classification).ToArray();
        
        int result = ProphetCoreEngine.Prophet_SetFearGreedSeries(
            _engineHandle, timestamps, values, classifications, data.Count);
        
        if (result != 0)
        {
            var error = ProphetCoreEngine.GetLastErrorMessage();
            throw new Exception($"注入恐慌指数数据失败: {error}");
        }
    }
    
    /// <summary>
    /// 注入资金费率数据到引擎
    /// </summary>
    private void InjectFundingRateData(List<FundingRateData> data)
    {
        if (_engineHandle == IntPtr.Zero || data.Count == 0)
            return;
        
        var timestamps = data.Select(d => d.CalcTime).ToArray();
        var values = data.Select(d => (double)d.LastFundingRate).ToArray();
        
        int result = ProphetCoreEngine.Prophet_SetFundingRateSeries(
            _engineHandle, timestamps, values, data.Count);
        
        if (result != 0)
        {
            var error = ProphetCoreEngine.GetLastErrorMessage();
            throw new Exception($"注入资金费率数据失败: {error}");
        }
    }
    
    /// <summary>
    /// 注入多空比数据到引擎
    /// </summary>
    private void InjectLongShortRatioData(List<LongShortRatioData> data)
    {
        if (_engineHandle == IntPtr.Zero || data.Count == 0)
            return;
        
        // 将C#的LongShortRatioData转换为C++的格式
        // C++需要: timestamp(秒), long_ratio, short_ratio, ratio
        var timestamps = data.Select(d => new DateTimeOffset(d.UpdateTime).ToUnixTimeSeconds()).ToArray();
        
        // 计算多头和空头比例
        // LongPositionRatio 是持仓量多空比，需要转换为比例
        var longRatios = data.Select(d => 
        {
            // 如果 LongPositionRatio 已经是比例（0~1），直接使用
            // 否则需要从 LongShortRatio 计算
            if (d.LongPositionRatio > 0 && d.LongPositionRatio <= 1.0m)
            {
                return (double)d.LongPositionRatio;
            }
            // 从 LongShortRatio 计算：long_ratio = ratio / (ratio + 1)
            else if (d.LongShortRatio > 0)
            {
                return (double)(d.LongShortRatio / (d.LongShortRatio + 1.0m));
            }
            return 0.5; // 默认值
        }).ToArray();
        
        var shortRatios = longRatios.Select(lr => 1.0 - lr).ToArray(); // 空头比例 = 1 - 多头比例
        var ratios = data.Select(d => (double)d.LongShortRatio).ToArray();
        
        int result = ProphetCoreEngine.Prophet_SetLongShortRatioSeries(
            _engineHandle, timestamps, longRatios, shortRatios, ratios, data.Count);
        
        if (result != 0)
        {
            var error = ProphetCoreEngine.GetLastErrorMessage();
            throw new Exception($"注入多空比数据失败: {error}");
        }
        
        Console.WriteLine($"✅ 已注入 {data.Count} 条多空比数据");
    }
    
    public void Dispose()
    {
        if (_engineHandle != IntPtr.Zero)
        {
            ProphetCoreEngine.Prophet_DestroyEngine(_engineHandle);
            _engineHandle = IntPtr.Zero;
            Console.WriteLine("✅ Prophet.Core引擎已释放");
        }
        
        _isInitialized = false;
    }
}
