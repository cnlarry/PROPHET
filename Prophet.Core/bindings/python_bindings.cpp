/*
 * ============================================================================
 * Prophet.Core - Python Bindings (Minimal Interface)
 * ============================================================================
 * 
 * 这个文件只暴露核心引擎接口给 Python 客户端：
 *   - Engine - 策略引擎（接收 DSL，返回信号）
 *   - Signal, Value, PatternInfo 等数据结构
 * 
 * v10.0: 移除 BatchEngine（客户端可以用多进程/多线程实现批量评估）
 * 
 * 指标计算和数据函数都在引擎内部完成，不暴露给客户端。
 * 
 * 版本: 4.0.0
 * ============================================================================
 */

#include <pybind11/pybind11.h>
#include <pybind11/stl.h>
#include <pybind11/functional.h>
#include <pybind11/numpy.h>

#include "prophet/common/signal.hpp"
#include "prophet/common/types.hpp"
#include "prophet/core/engine.hpp"
// v10.0: 移除 #include "prophet/core/batch_engine.hpp"
#include "prophet/functions/Pattern.hpp"

namespace py = pybind11;

using namespace prophet;

// ============================================================================
// Prophet.Core 主模块 - 最小化接口
// ============================================================================
PYBIND11_MODULE(Prophet, m) {
    m.doc() = R"pbdoc(
        Prophet.Core - High-performance Quantitative Trading Strategy Engine
        
        这是一个基于 DSL 的量化交易策略引擎。
        客户端只需提供 DSL 代码和 K线数据，引擎会在内部完成所有计算并返回交易信号。
        
        主要接口:
            - Engine: 策略引擎（单策略评估）
            - Signal: 交易信号数据结构
            
        v10.0 变更：移除 BatchEngine（客户端可以用多进程实现）
            
        使用示例:
            import Prophet
            
            # 创建引擎
            engine = Prophet.Engine("ALL{$(5m).RSI(14).value < 30} = BUY;")
            
            # 设置 K线数据
            engine.set_klines(open, high, low, close, volume, open_time, close_time)
            
            # 获取交易信号
            signal = engine.get_signal(current_price, current_time)
            print(signal.action, signal.confidence)
    )pbdoc";

    // ========================================================================
    // SignalAction 枚举
    // ========================================================================
    py::enum_<SignalAction>(m, "SignalAction")
        .value("BUY", SignalAction::BUY)
        .value("SELL", SignalAction::SELL)
        .value("HOLD", SignalAction::HOLD)
        .export_values();

    // ========================================================================
    // Value 类型
    // ========================================================================
    py::class_<Value>(m, "Value")
        .def(py::init<>())
        .def_static("from_number", &Value::fromNumber,
                   "创建数值类型的 Value")
        .def_static("from_string", &Value::fromString,
                   "创建字符串类型的 Value")
        .def_static("from_boolean", &Value::fromBoolean,
                   "创建布尔类型的 Value")
        .def("to_number", &Value::toNumber,
             "转换为数值")
        .def("to_string", &Value::toString,
             "转换为字符串")
        .def("to_bool", &Value::toBool,
             "转换为布尔值")
        .def_readwrite("type", &Value::type)
        .def_readwrite("number_value", &Value::number_value)
        .def_readwrite("string_value", &Value::string_value)
        .def_readwrite("boolean_value", &Value::boolean_value);

    // ========================================================================
    // PatternInfo 结构
    // ========================================================================
    py::class_<PatternInfo>(m, "PatternInfo",
        "形态信息（用于形态识别功能）")
        .def(py::init<>())
        .def_readwrite("name", &PatternInfo::name, "形态名称")
        .def_readwrite("side", &PatternInfo::side, "方向（BUY/SELL）")
        .def_readwrite("raw", &PatternInfo::raw, "原始值")
        .def_readwrite("score", &PatternInfo::score, "评分")
        .def_readwrite("rank", &PatternInfo::rank, "排名")
        .def_readwrite("kline_open_time", &PatternInfo::kline_open_time, "K线开盘时间")
        .def_readwrite("kline_close_time", &PatternInfo::kline_close_time, "K线收盘时间")
        .def_readwrite("description", &PatternInfo::description, "描述");
        
    // ========================================================================
    // IndicatorSnapshot 结构（指标快照）
    // ========================================================================
    py::class_<IndicatorSnapshot>(m, "IndicatorSnapshot",
        "指标快照信息（用于详细分析条件触发）")
        .def(py::init<>())
        .def_readwrite("condition_id", &IndicatorSnapshot::condition_id, "条件ID")
        .def_readwrite("label", &IndicatorSnapshot::label, "可读标签")
        .def_readwrite("timeframe", &IndicatorSnapshot::timeframe, "时间框架")
        .def_readwrite("indicator", &IndicatorSnapshot::indicator, "指标名称")
        .def_readwrite("offset", &IndicatorSnapshot::offset, "偏移量")
        .def_readwrite("comparison", &IndicatorSnapshot::comparison, "比较表达式")
        .def_readwrite("status", &IndicatorSnapshot::status, "状态(hit/miss)")
        .def_property("parameters",
            [](const IndicatorSnapshot& snap) -> py::dict {
                py::dict result;
                for (const auto& [key, value] : snap.parameters) {
                    if (value.type == ValueType::NUMBER) {
                        result[py::cast(key)] = value.toNumber();
                    } else if (value.type == ValueType::STRING) {
                        result[py::cast(key)] = value.toString();
                    } else if (value.type == ValueType::BOOLEAN) {
                        result[py::cast(key)] = value.toBool();
                    }
                }
                return result;
            },
            [](IndicatorSnapshot& snap, py::dict dict) {
                snap.parameters.clear();
                for (auto item : dict) {
                    std::string key = py::cast<std::string>(item.first);
                    py::object value = py::cast<py::object>(item.second);
                    
                    if (py::isinstance<py::float_>(value) || py::isinstance<py::int_>(value)) {
                        snap.parameters[key] = Value::fromNumber(py::cast<double>(value));
                    } else if (py::isinstance<py::str>(value)) {
                        snap.parameters[key] = Value::fromString(py::cast<std::string>(value));
                    } else if (py::isinstance<py::bool_>(value)) {
                        snap.parameters[key] = Value::fromBoolean(py::cast<bool>(value));
                    }
                }
            },
            "参数快照")
        .def_property("ohlcv",
            [](const IndicatorSnapshot& snap) -> py::dict {
                py::dict result;
                for (const auto& [key, value] : snap.ohlcv) {
                    if (value.type == ValueType::NUMBER) {
                        result[py::cast(key)] = value.toNumber();
                    }
                }
                return result;
            },
            [](IndicatorSnapshot& snap, py::dict dict) {
                snap.ohlcv.clear();
                for (auto item : dict) {
                    std::string key = py::cast<std::string>(item.first);
                    py::object value = py::cast<py::object>(item.second);
                    
                    if (py::isinstance<py::float_>(value) || py::isinstance<py::int_>(value)) {
                        snap.ohlcv[key] = Value::fromNumber(py::cast<double>(value));
                    }
                }
            },
            "OHLCV快照")
        .def_property("results",
            [](const IndicatorSnapshot& snap) -> py::dict {
                py::dict result;
                for (const auto& [key, value] : snap.results) {
                    if (value.type == ValueType::NUMBER) {
                        result[py::cast(key)] = value.toNumber();
                    } else if (value.type == ValueType::STRING) {
                        result[py::cast(key)] = value.toString();
                    } else if (value.type == ValueType::BOOLEAN) {
                        result[py::cast(key)] = value.toBool();
                    }
                }
                return result;
            },
            [](IndicatorSnapshot& snap, py::dict dict) {
                snap.results.clear();
                for (auto item : dict) {
                    std::string key = py::cast<std::string>(item.first);
                    py::object value = py::cast<py::object>(item.second);
                    
                    if (py::isinstance<py::float_>(value) || py::isinstance<py::int_>(value)) {
                        snap.results[key] = Value::fromNumber(py::cast<double>(value));
                    } else if (py::isinstance<py::str>(value)) {
                        snap.results[key] = Value::fromString(py::cast<std::string>(value));
                    } else if (py::isinstance<py::bool_>(value)) {
                        snap.results[key] = Value::fromBoolean(py::cast<bool>(value));
                    }
                }
            },
            "指标计算结果");
        
    // ========================================================================
    // Signal 结构（交易信号）
    // ========================================================================
    py::class_<Signal, std::shared_ptr<Signal>>(m, "Signal",
        R"pbdoc(
            交易信号
            
            引擎评估策略后返回的信号，包含交易动作、置信度、止损止盈等信息。
        )pbdoc")
        .def(py::init<>())
        .def(py::init<const std::string&, double, const std::string&>(),
             py::arg("action"),
             py::arg("confidence"),
             py::arg("reason") = "")
        .def_readwrite("id", &Signal::id, "信号 ID")
        .def_readwrite("strategy", &Signal::strategy, "策略名称")
        .def_readwrite("action", &Signal::action, "交易动作（BUY/SELL/HOLD）")
        .def_readwrite("confidence", &Signal::confidence, "置信度（0-100）")
        .def_readwrite("reason", &Signal::reason, "信号原因")
        .def_readwrite("trend", &Signal::trend, "趋势")
        .def_readwrite("sl", &Signal::sl, "止损价")
        .def_readwrite("tp", &Signal::tp, "止盈价")
        .def_readwrite("create_at", &Signal::create_at, "创建时间")
        .def_readwrite("stop_loss", &Signal::stop_loss, "止损价（新）")
        .def_readwrite("take_profit", &Signal::take_profit, "止盈价（新）")
        .def_readwrite("timestamp", &Signal::timestamp, "时间戳")
        .def_property("configs",
            [](const Signal& sig) -> py::dict {
                py::dict result;
                for (const auto& [key, value] : sig.configs) {
                    if (value.type == ValueType::NUMBER) {
                        result[py::cast(key)] = value.toNumber();
                    } else if (value.type == ValueType::STRING) {
                        result[py::cast(key)] = value.toString();
                    } else if (value.type == ValueType::BOOLEAN) {
                        result[py::cast(key)] = value.toBool();
                    } else {
                        result[py::cast(key)] = py::none();
                    }
                }
                return result;
            },
            [](Signal& sig, py::dict dict) {
                sig.configs.clear();
                for (auto item : dict) {
                    std::string key = py::cast<std::string>(item.first);
                    py::object value = py::cast<py::object>(item.second);
                    
                    if (py::isinstance<py::float_>(value) || py::isinstance<py::int_>(value)) {
                        sig.configs[key] = Value::fromNumber(py::cast<double>(value));
                    } else if (py::isinstance<py::str>(value)) {
                        sig.configs[key] = Value::fromString(py::cast<std::string>(value));
                    } else if (py::isinstance<py::bool_>(value)) {
                        sig.configs[key] = Value::fromBoolean(py::cast<bool>(value));
                    }
                }
            },
            "配置参数字典")
        .def_property("indicators",
            [](const Signal& sig) -> py::dict {
                py::dict result;
                for (const auto& [key, value] : sig.indicators) {
                    if (value.type == ValueType::NUMBER) {
                        result[py::cast(key)] = value.toNumber();
                    } else if (value.type == ValueType::STRING) {
                        result[py::cast(key)] = value.toString();
                    } else if (value.type == ValueType::BOOLEAN) {
                        result[py::cast(key)] = value.toBool();
                    } else {
                        result[py::cast(key)] = py::none();
                    }
                }
                return result;
            },
            [](Signal& sig, py::dict dict) {
                sig.indicators.clear();
                for (auto item : dict) {
                    std::string key = py::cast<std::string>(item.first);
                    py::object value = py::cast<py::object>(item.second);
                    
                    if (py::isinstance<py::float_>(value) || py::isinstance<py::int_>(value)) {
                        sig.indicators[key] = Value::fromNumber(py::cast<double>(value));
                    } else if (py::isinstance<py::str>(value)) {
                        sig.indicators[key] = Value::fromString(py::cast<std::string>(value));
                    } else if (py::isinstance<py::bool_>(value)) {
                        sig.indicators[key] = Value::fromBoolean(py::cast<bool>(value));
                    }
                }
            },
            "指标值字典（调试用）")
        .def_readwrite("debug", &Signal::debug, "调试信息")
        .def_readwrite("indicator_snapshots", &Signal::indicator_snapshots, "指标快照列表（新增）")
        .def("is_valid", &Signal::isValid,
             "检查信号是否有效")
        .def("to_string", &Signal::toString,
             "转换为字符串")
        .def("__repr__", [](const Signal& sig) {
            return "<Signal action=" + sig.action +
                   " confidence=" + std::to_string(sig.confidence) + ">";
        });

    // ========================================================================
    // Kline 结构（仅用于类型定义，不用于直接操作）
    // ========================================================================
    py::class_<prophet::Kline>(m, "Kline",
        "K线数据结构（供参考，通常使用 NumPy 数组传递数据）")
        .def(py::init<>())
        .def(py::init<double, double, double, double, double, int64_t, int64_t>(),
             py::arg("open"),
             py::arg("high"),
             py::arg("low"),
             py::arg("close"),
             py::arg("volume"),
             py::arg("open_time"),
             py::arg("close_time"))
        .def_readwrite("open", &prophet::Kline::open)
        .def_readwrite("high", &prophet::Kline::high)
        .def_readwrite("low", &prophet::Kline::low)
        .def_readwrite("close", &prophet::Kline::close)
        .def_readwrite("volume", &prophet::Kline::volume)
        .def_readwrite("open_time", &prophet::Kline::open_time)
        .def_readwrite("close_time", &prophet::Kline::close_time)
        .def("__repr__", [](const prophet::Kline& k) {
            return "<Kline O=" + std::to_string(k.open) +
                   " H=" + std::to_string(k.high) +
                   " L=" + std::to_string(k.low) +
                   " C=" + std::to_string(k.close) + ">";
        });

    // ========================================================================
    // Engine - 策略引擎（核心接口）
    // ========================================================================
    py::class_<core::Engine>(m, "Engine",
        R"pbdoc(
            策略引擎
            
            核心功能：
            1. 解析 DSL 策略代码
            2. 接收 K线数据（自动合成所有时间框架）
            3. 评估策略并返回交易信号
            
            工作流程：
                engine = Prophet.Engine(dsl_code)
                engine.set_klines(...)  # 只需提供基础时间框架数据
                signal = engine.get_signal(price, time)
            
            引擎会在内部：
                - 解析 DSL 代码
                - 合成所需的时间框架
                - 计算所有指标
                - 执行策略逻辑
                - 返回交易信号
        )pbdoc")
        .def(py::init<const std::string&, const std::unordered_map<std::string, 
                std::unordered_map<std::string, std::unordered_map<std::string, double>>>&>(),
             py::arg("dsl_code"),
             py::arg("params") = std::unordered_map<std::string, 
                std::unordered_map<std::string, std::unordered_map<std::string, double>>>(),
             R"pbdoc(
                构造策略引擎
                
                参数:
                    dsl_code (str): DSL 策略代码
                    params (dict, optional): 指标参数配置
                        格式: {'指标名': {'时间框架': {'参数名': 参数值}}}
                        示例: {'MACD': {'5m': {'FAST_PERIOD': 12.0}}}
                
                示例:
                    engine = Prophet.Engine(
                        "ALL{$(5m).RSI(14).value < 30} = BUY;"
                    )
             )pbdoc")

        .def("set_klines",
             [](core::Engine& self,
                const std::string& timeframe,  // v10.0: 新增时间框架参数
                py::array_t<double> open,
                py::array_t<double> high,
                py::array_t<double> low,
                py::array_t<double> close,
                py::array_t<double> volume,
                py::array_t<int64_t> open_time,
                py::array_t<int64_t> close_time) {
                 
                 auto count = open.size();
                 if (high.size() != count || low.size() != count || 
                     close.size() != count || volume.size() != count || 
                     open_time.size() != count || close_time.size() != count) {
                     throw std::invalid_argument("All arrays must have the same length");
                 }
                 
                 self.set_klines(
                     timeframe,  // v10.0: 传递时间框架
                     open.data(),
                     high.data(),
                     low.data(),
                     close.data(),
                     volume.data(),
                     open_time.data(),
                     close_time.data(),
                     count
                 );
             },
             py::arg("timeframe"),  // v10.0: 新增参数
             py::arg("open"),
             py::arg("high"),
             py::arg("low"),
             py::arg("close"),
             py::arg("volume"),
             py::arg("open_time"),
             py::arg("close_time"),
             R"pbdoc(
                v10.0 设置 K线数据（必须指定时间框架）
                
                用于初始化时设置完整的300根K线窗口。
                注意：如果传入超过300根，只保留最后300根。
                
                参数:
                    timeframe (str): 时间框架（如"5m", "1h", "1d"）
                    open, high, low, close, volume: OHLCV 数据（NumPy 数组）
                    open_time, close_time: 时间戳数组（UTC 毫秒）
                
                示例:
                    import pandas as pd
                    import numpy as np
                    
                    # 加载 5m 数据
                    df_5m = load_klines('BTCUSDT', '5m', start, end)
                    engine.set_klines(
                        "5m",  # 明确指定时间框架
                        df_5m['open'].values,
                        df_5m['high'].values,
                        df_5m['low'].values,
                        df_5m['close'].values,
                        df_5m['volume'].values,
                        df_5m['open_time'].values,
                        df_5m['close_time'].values
                    )
                    
                    # 加载 1h 数据
                    df_1h = load_klines('BTCUSDT', '1h', start, end)
                    engine.set_klines("1h", df_1h['open'].values, ...)
             )pbdoc")
        
        .def("append_kline",
             [](core::Engine& self,
                const std::string& timeframe,
                double open, double high, double low, double close, double volume,
                int64_t open_time, int64_t close_time) {
                 
                 self.append_kline(timeframe, open, high, low, close, volume, 
                                  open_time, close_time);
             },
             py::arg("timeframe"),
             py::arg("open"), py::arg("high"), py::arg("low"), 
             py::arg("close"), py::arg("volume"),
             py::arg("open_time"), py::arg("close_time"),
             R"pbdoc(
                v10.0 增量追加单根K线（高性能，回测循环使用）
                
                自动维护300根窗口：追加新K线，丢弃最旧的。
                注意：必须先调用 set_klines() 初始化窗口。
                
                性能优势：相比全量设置快300倍，内存分配减少99.7%
                
                示例（回测循环）:
                    # 初始化：设置初始300根
                    engine.set_klines("5m", initial_300_candles...)
                    
                    # 回测循环：增量追加
                    for i in range(300, len(candles)):
                        engine.append_kline(
                            "5m",
                            candles[i]['open'],
                            candles[i]['high'],
                            candles[i]['low'],
                            candles[i]['close'],
                            candles[i]['volume'],
                            candles[i]['open_time'],
                            candles[i]['close_time']
                        )
                        signal = engine.get_signal(candles[i]['close'], candles[i]['time'])
             )pbdoc")
        
        .def("append_klines",
             [](core::Engine& self,
                const std::string& timeframe,
                py::array_t<double> open,
                py::array_t<double> high,
                py::array_t<double> low,
                py::array_t<double> close,
                py::array_t<double> volume,
                py::array_t<int64_t> open_time,
                py::array_t<int64_t> close_time) {
                 
                 auto count = open.size();
                 if (high.size() != count || low.size() != count || 
                     close.size() != count || volume.size() != count || 
                     open_time.size() != count || close_time.size() != count) {
                     throw std::invalid_argument("All arrays must have the same length");
                 }
                 
                 self.append_klines(
                     timeframe,
                     open.data(),
                     high.data(),
                     low.data(),
                     close.data(),
                     volume.data(),
                     open_time.data(),
                     close_time.data(),
                     count
                 );
             },
             py::arg("timeframe"),
             py::arg("open"),
             py::arg("high"),
             py::arg("low"),
             py::arg("close"),
             py::arg("volume"),
             py::arg("open_time"),
             py::arg("close_time"),
             R"pbdoc(
                v10.0 增量追加多根K线（批量追加，处理跳跃场景）
                
                参数:
                    timeframe (str): 时间框架
                    open, high, low, close, volume: OHLCV 数据（NumPy 数组）
                    open_time, close_time: 时间戳数组（UTC 毫秒）
             )pbdoc")

        .def("set_fear_greed_series",
             [](core::Engine& self,
                py::array_t<int64_t> date_timestamps,
                py::array_t<int> values,
                py::list classifications) {
                 
                size_t count = static_cast<size_t>(date_timestamps.size());
                if (static_cast<size_t>(values.size()) != count || 
                    static_cast<size_t>(py::len(classifications)) != count) {
                    throw std::invalid_argument("All arrays must have the same length");
                }
                
                std::vector<FearGreedData> series;
                series.reserve(count);
                
                auto ts_ptr = date_timestamps.data();
                auto val_ptr = values.data();
                
                for (size_t i = 0; i < count; ++i) {
                     std::string classification = py::cast<std::string>(classifications[i]);
                     series.emplace_back(ts_ptr[i], val_ptr[i], classification);
                 }
                 
                 self.set_fear_greed_series(series);
             },
             py::arg("date_timestamps"),
             py::arg("values"),
             py::arg("classifications"),
             "设置恐惧与贪婪指数序列数据（用于 FEARGREED 环境变量）")

        .def("set_funding_rate_series",
             [](core::Engine& self, py::array_t<int64_t> timestamps, py::array_t<double> values) {
                 if (timestamps.size() != values.size()) {
                     throw std::runtime_error("Timestamps and values arrays must have the same size");
                 }
                 
                 size_t count = timestamps.size();
                 if (count == 0) {
                     return;
                 }
                 
                 std::vector<FundingRateData> series;
                 series.reserve(count);
                 
                 auto ts_ptr = timestamps.data();
                 auto val_ptr = values.data();
                 
                 for (size_t i = 0; i < count; ++i) {
                     series.emplace_back(ts_ptr[i], val_ptr[i]);
                 }
                 
                 self.set_funding_rate_series(series);
             },
             py::arg("timestamps"),
             py::arg("values"),
             "设置资金费率序列数据（用于 CURRENT().fundingrate 访问）")

        .def("get_signal",
             &core::Engine::get_signal,
             py::arg("current_price"),
             py::arg("current_time"),
             R"pbdoc(
                生成交易信号
                
                引擎会在内部完成所有计算并返回信号。
                
                参数:
                    current_price (float): 当前价格
                    current_time (int): 当前时间戳（UTC 毫秒）
                
                返回:
                    Signal: 交易信号对象
                
                示例:
                    signal = engine.get_signal(
                        current_price=50000.0,
                        current_time=1234567890000
                    )
                    
                    if signal.action == "BUY":
                        print(f"买入信号，置信度: {signal.confidence}%")
             )pbdoc")

        .def("get_rule_count",
             &core::Engine::get_rule_count,
             "获取策略规则数量")

        .def("get_rules_string",
             &core::Engine::get_rules_string,
             "获取 DSL 规则字符串（调试用）")

        .def("__repr__", [](const core::Engine& engine) {
            return "<Engine rules=" + std::to_string(engine.get_rule_count()) + ">";
        });

    // ========================================================================
    // v10.0: BatchEngine 已移除
    // 如果需要批量评估，请在客户端使用多进程/多线程：
    //   from multiprocessing import Pool
    //   with Pool(processes=4) as pool:
    //       results = pool.map(evaluate_strategy, engines)
    // ========================================================================

    // ========================================================================
    // 异常类型
    // ========================================================================
    py::register_exception<DSLException>(m, "DSLException");
    py::register_exception<LexerException>(m, "LexerException");
    py::register_exception<ParserException>(m, "ParserException");
    py::register_exception<EvaluatorException>(m, "EvaluatorException");

    // ========================================================================
    // 辅助函数
    // ========================================================================
    m.def("signal_action_to_string", &signalActionToString,
          py::arg("action"),
          "将 SignalAction 枚举转换为字符串");

    m.def("string_to_signal_action", &stringToSignalAction,
          py::arg("action_str"),
          "将字符串转换为 SignalAction 枚举");

    // ========================================================================
    // 模块元数据
    // ========================================================================
    m.attr("__version__") = "4.0.0";
    m.attr("__doc__") = R"pbdoc(
        Prophet.Core - 量化交易策略引擎
        
        这是一个最小化接口设计，遵循单一职责原则：
        - 客户端：提供 DSL 和数据
        - 引擎：内部完成所有计算
        - 结果：返回交易信号
        
        不暴露：
        - 指标计算接口（内部使用）
        - 数据函数接口（内部使用）
        - K线转换接口（C# 客户端专用，在 DLL 中暴露）
    )pbdoc";
}
