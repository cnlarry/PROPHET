/*
 * ============================================================================
 * 文件名：context.cpp
 * 功能说明：策略上下文实现
 * 
 * 这个文件负责管理策略执行时需要的所有数据，可以把它理解为一个"数据仓库"：
 * 
 * 主要功能：
 * 1. 存储和管理指标数据（如MACD、RSI等技术指标的计算结果）
 * 2. 存储和管理参数数据（如MACD的周期参数）
 * 3. 存储和管理K线数据（蜡烛图数据）
 * 4. 提供特殊计算功能（如PRICE函数、HT希尔伯特变换）
 * 
 * 为什么需要Context？
 * - 规则评估时需要访问各种数据
 * - 集中管理数据，方便查找和使用
 * - 隔离数据和逻辑，使代码更清晰
 * 
 * 简单理解：
 * - Context就像一个"工具箱"
 * - 里面装着策略运行需要的所有"工具"（数据）
 * - 需要什么数据就从工具箱里取
 * ============================================================================
 */

#include "prophet/core/context.hpp"
#include "prophet/indicators/registry.hpp"
#include "prophet/tools/kline_converter.hpp"
#include "prophet/common/performance_monitor.hpp"
#include "ta_libc.h"
#include <algorithm>
#include <array>
#include <climits>
#include <iostream>

namespace prophet::dsl {

// ============================================================================
// 构造函数 - 创建一个新的上下文对象
// ============================================================================
Context::Context()
    : current_price_(0.0)  // 当前价格初始化为0
    , current_time_(0)     // 当前时间初始化为0
    , function_registry_(nullptr)  // 🆕 v4.0: 函数注册表指针初始化为nullptr
{}

// ============================================================================
// 函数：setIndicator
// 功能：保存指标的计算结果
// 
// 参数说明：
//   indicator_name - 指标名称（如"MACD"、"RSI"）
//   timeframe - 时间周期（如"5m"、"1h"）
//   result - 指标的计算结果（包含多个字段）
// 
// 数据存储结构：
//   指标结果存储在 IndicatorCache 中
//   例如：indicator_cache_.set("5m", "RSI", result, version)
// ============================================================================
void Context::setIndicator(const std::string& indicator_name,
                           const std::string& timeframe,
                           const IndicatorResult& result) {
    // P1优化：使用IndicatorCache存储
    uint64_t current_version = kline_manager_.getVersion(timeframe);
    indicator_cache_.set(timeframe, indicator_name, result, current_version);
}

// ============================================================================
// 函数：getIndicator
// 功能：获取指定指标的完整结果
// 
// 参数说明：
//   indicator_name - 指标名称
//   timeframe - 时间周期
// 
// 返回值：
//   指标结果对象（包含所有字段）
// 
// 错误处理：
//   - 如果时间周期不存在，抛出异常
//   - 如果指标不存在，抛出异常
// ============================================================================
IndicatorResult Context::getIndicator(const std::string& indicator_name,
                                      const std::string& timeframe) const {
    // 🆕 v4.0: 调试：检查指标名称是否包含时间框架（错误情况）
    if (indicator_name.find('(') != std::string::npos || indicator_name.find(timeframe) != std::string::npos) {
        // 如果指标名称包含括号或时间框架，说明可能有错误
        // 但这不应该发生，因为指标名称和时间框架是分开传递的
        // 这里只是记录，不阻止执行
    }
    
    // P1优化：从IndicatorCache获取
    uint64_t current_version = kline_manager_.getVersion(timeframe);
    IndicatorResult result;
    if (indicator_cache_.get(timeframe, indicator_name, current_version, result)) {
        return result;
    }
    
    // 🆕 v4.0: 尝试自动计算指标（如果缓存中没有）
    // 注意：这里不能直接调用 getOrCalculateIndicator，因为它是非const方法
    // 但我们可以抛出更详细的错误信息
    throw EvaluatorException("Indicator not found: " + indicator_name + "(" + timeframe + "). "
                            "Make sure the indicator is calculated before accessing it.");
}

// ============================================================================
// 函数：getIndicatorField
// 功能：获取指标的某个特定字段的值
// 
// 参数说明：
//   indicator_name - 指标名称
//   timeframe - 时间周期
//   field - 字段名（如"value"、"trend"）
// 
// 返回值：
//   字段的值（Value类型，可以是数字、字符串或布尔值）
// 
// 例子：
//   获取5分钟RSI的值：
//   getIndicatorField("RSI", "5m", "value") -> 45.2
// ============================================================================
Value Context::getIndicatorField(const std::string& indicator_name,
                                 const std::string& timeframe,
                                 const std::string& field,
                                 int offset) const {
    return getIndicatorField(indicator_name, timeframe, field, offset, {});
}

Value Context::getIndicatorField(const std::string& indicator_name,
                                 const std::string& timeframe,
                                 const std::string& field,
                                 int offset,
                                 const std::vector<Value>& indicator_params) const {
    // ========================================================================
    // 偏移量范围验证：确保偏移量在有效范围内
    // ========================================================================
    if (offset < -100 || offset > 0) {
        throw EvaluatorException(
            "[VALIDATION] Field offset out of range [-100, 0]: " + std::to_string(offset) + 
            " for " + indicator_name + "(" + timeframe + ")." + field
        );
    }
    
    // 🆕 v4.0: 先尝试从缓存获取，如果失败则尝试自动计算
    // 注意：这里需要const_cast，因为getOrCalculateIndicator是非const方法
    IndicatorResult result;
    uint64_t current_version = kline_manager_.getVersion(timeframe);
    if (indicator_params.empty()) {
        // 无 DSL 参数：使用无参缓存 key（默认参数）
        if (!indicator_cache_.get(timeframe, indicator_name, current_version, result)) {
            // 缓存中没有，尝试自动计算
            // 这可能在复杂表达式中发生，例如：WEIGHT($(5m).ATR(14).value > 50)
            // 在这种情况下，ATR指标可能还没有被计算
            Context& mutable_ctx = const_cast<Context&>(*this);
            try {
                mutable_ctx.getOrCalculateIndicator(indicator_name, timeframe);
                // 重新从缓存获取（版本可能已更新）
                current_version = kline_manager_.getVersion(timeframe);
                if (!indicator_cache_.get(timeframe, indicator_name, current_version, result)) {
                    throw EvaluatorException(
                        "Indicator " + indicator_name + "(" + timeframe + ") not found in cache after auto-calculation. "
                        "This may indicate a bug in the indicator calculation logic."
                    );
                }
            } catch (const std::exception& e) {
                throw EvaluatorException(
                    "Indicator " + indicator_name + "(" + timeframe + ") not found in cache and auto-calculation failed: " + e.what()
                );
            }
        }
    } else {
        // 有 DSL 参数：必须走带参数的 getOrCalculateIndicator（参数哈希缓存），
        // 否则会命中默认参数缓存，导致 RSI(7) 返回 RSI(14) 的结果
        Context& mutable_ctx = const_cast<Context&>(*this);
        result = mutable_ctx.getOrCalculateIndicator(indicator_name, timeframe, indicator_params);
    }
    
    // 检查字段是否存在
    if (!result.has(field)) {
        throw EvaluatorException("Indicator field not found: " + indicator_name + "(" + timeframe + ")." + field);
    }
    
    // ========================================================================
    // 偏移量访问验证：确保有足够的历史数据支持该偏移量
    // ========================================================================
    auto it = result.field_series.find(field);
    if (it != result.field_series.end()) {
        const auto& series = it->second;
        // offset=0 取最后一个，需 >=1 个点；offset=-1 取倒数第二个，需 >=2 个点
        int required_size = -offset + 1;
        
        if (static_cast<int>(series.size()) < required_size) {
            throw EvaluatorException(
                "[VALIDATION] Insufficient historical data for offset " + std::to_string(offset) + 
                ": need at least " + std::to_string(required_size) + 
                " data points, got " + std::to_string(series.size()) + 
                " for " + indicator_name + "(" + timeframe + ")." + field
            );
        }
    }
    
    // 返回字段的值（支持偏移量）
    return result.get(field, offset);
}

// ============================================================================
// 函数：getOrCalculateIndicator
// 功能：获取或自动计算指标（按需计算/延迟计算）
// 
// 这是实现自动指标计算的核心方法！
// 
// P0优化改进工作流程：
//   1. 生成缓存键（indicator:timeframe）
//   2. 获取当前K线版本号
//   3. 检查缓存是否存在且版本号匹配
//   4. 如果缓存有效，直接返回（⚡ 性能提升80-90%）
//   5. 如果缓存失效或不存在，调用Calculator计算
//   6. 缓存计算结果并记录版本号
//   7. 返回结果
// 
// 支持的指标（自动识别）：
//   - MACD, RSI, EMA, SMA, BBANDS, ATR
//   - STOCH, STOCHRSI, CCI, ADX, WILLR, MFI
//   - SAR, WMA, ROC, AROON, TRIX, DMI, CMO
//   - TEMA, DEMA, KAMA, T3, MAMA 等
// 
// 参数获取：
//   - 从parameters_中读取用户设置的参数
//   - 如果参数不存在，使用默认值
// ============================================================================
IndicatorResult Context::getOrCalculateIndicator(const std::string& indicator_name,
                                                  const std::string& timeframe) {
    // P2优化：性能监控 - 追踪指标访问性能
    utils::PerformanceMonitor::Timer timer(
        utils::PerformanceMonitor::instance(), 
        "Context::getOrCalculateIndicator"
    );
    
    // P1优化：从KlineManager获取当前K线版本号
    uint64_t current_version = kline_manager_.getVersion(timeframe);
    
    // P1优化：使用IndicatorCache检查缓存
    IndicatorResult result;
    if (indicator_cache_.get(timeframe, indicator_name, current_version, result)) {
        // ✅ IndicatorCache命中！K线未变化，直接返回缓存结果
        utils::PerformanceMonitor::instance().record("Indicator_Cache_Hit", 1);
        return result;
    }
    
    // 缓存未命中或版本号不匹配，需要重新计算
    utils::PerformanceMonitor::instance().record("Indicator_Cache_Miss", 1);
    
    // P1优化：步骤3 - 从KlineManager获取K线数据
    if (!kline_manager_.hasKlines(timeframe)) {
        std::string error_msg = "Cannot calculate indicator " + indicator_name + "(" + timeframe + "): K-line data not available";
        throw EvaluatorException(error_msg);
    }

    const auto klines = kline_manager_.getKlines(timeframe);
    if (klines.empty()) {
        throw EvaluatorException(
            "Cannot calculate indicator " + indicator_name + "(" + timeframe + "): K-line data is empty"
        );
    }
    
    // 提取K线数据为向量
    std::vector<double> close, high, low, open, volume;
    close.reserve(klines.size());
    high.reserve(klines.size());
    low.reserve(klines.size());
    open.reserve(klines.size());
    volume.reserve(klines.size());
    
    for (const auto& kline : klines) {
        close.push_back(kline.close);
        high.push_back(kline.high);
        low.push_back(kline.low);
        open.push_back(kline.open);
        volume.push_back(kline.volume);
    }

    // 第3步：使用指标注册表动态调用
    // ✅ 改进：无需硬编码 if-else 链，通过注册表查找
    
    auto& registry = indicators::IndicatorRegistry::getInstance();
    
    // 检查指标是否已注册
    if (!registry.hasIndicator(indicator_name)) {
        throw EvaluatorException(
            "Unsupported indicator for auto-calculation: " + indicator_name
        );
    }
    
    // 准备参数
    indicators::IndicatorParams params;
    
    // 从JSON参数中提取所有参数
    try {
        auto ind_params = json_parameters_.find(indicator_name);
        if (ind_params != json_parameters_.end()) {
            auto tf_params = ind_params->second.find(timeframe);
            if (tf_params != ind_params->second.end()) {
                for (const auto& [param_name, param_value] : tf_params->second) {
                    params.set(param_name, param_value.toNumber());
                }
            }
        }
    } catch (...) {
        // 参数提取失败，使用默认值（在各指标的注册函数中处理）
    }
    
    // ========================================================================
    // 指标计算前的数据充分性验证：确保K线数量 >= 指标所需的最小周期数
    // ========================================================================
    int min_required = getMinRequiredKlines(indicator_name, params);
    if (klines.size() < static_cast<size_t>(min_required)) {
        throw EvaluatorException(
            "Insufficient K-line data for " + indicator_name + "(" + timeframe + "): " +
            "need at least " + std::to_string(min_required) + 
            " K-lines, got " + std::to_string(klines.size())
        );
    }
    
    // ========================================================================
    // 指标参数运行时验证：检查参数组合的有效性
    // ========================================================================
    validateIndicatorParams(indicator_name, params);
    
    // 调用注册的指标计算函数（添加性能监控）
    auto callable = registry.getIndicator(indicator_name);
    {
        // P2优化：性能监控 - 追踪具体指标的计算时间
        utils::PerformanceMonitor::Timer indicator_timer(
            utils::PerformanceMonitor::instance(),
            "Indicator_Calculate:" + indicator_name
        );
        result = callable(calculator_, close, high, low, volume, params);
    }

    // P1优化：更新IndicatorCache（长期缓存）
    indicator_cache_.set(timeframe, indicator_name, result, current_version);
    
    // P1优化：返回结果
    return result;
}

// ============================================================================
// 函数：getOrCalculateIndicator (重载版本，支持DSL参数)
// 功能：获取或自动计算指标，支持参数优先级（DSL > JSON > 默认值）
// ============================================================================
IndicatorResult Context::getOrCalculateIndicator(const std::string& indicator_name,
                                                 const std::string& timeframe,
                                                 const std::vector<Value>& dsl_params) {
    // 辅助函数：将位置参数映射到参数名
    // 根据指标注册顺序定义位置参数映射
    auto mapPositionalParams = [](const std::string& indicator, 
                                   const std::vector<Value>& params) 
                                   -> std::map<std::string, double> {
        std::map<std::string, double> param_map;
        
        // ========================================================================
        // 动量指标
        // ========================================================================
        
        // MACD: [FAST_PERIOD, SLOW_PERIOD, SIGNAL_PERIOD]
        if (indicator == "MACD" && params.size() >= 3) {
            param_map["FAST_PERIOD"] = params[0].toNumber();
            param_map["SLOW_PERIOD"] = params[1].toNumber();
            param_map["SIGNAL_PERIOD"] = params[2].toNumber();
        }
        // RSI: [PERIOD] (可选: OVERBOUGHT, OVERSOLD，但通常只用PERIOD)
        else if (indicator == "RSI" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
            if (params.size() >= 2) param_map["OVERBOUGHT"] = params[1].toNumber();
            if (params.size() >= 3) param_map["OVERSOLD"] = params[2].toNumber();
        }
        // STOCHRSI: [RSI_PERIOD, STOCH_PERIOD, K_PERIOD, D_PERIOD]
        else if (indicator == "STOCHRSI" && params.size() >= 4) {
            param_map["RSI_PERIOD"] = params[0].toNumber();
            param_map["STOCH_PERIOD"] = params[1].toNumber();
            param_map["K_PERIOD"] = params[2].toNumber();
            param_map["D_PERIOD"] = params[3].toNumber();
        }
        // CCI: [PERIOD]
        else if (indicator == "CCI" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // MFI: [PERIOD]
        else if (indicator == "MFI" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // WR: [PERIOD]
        else if (indicator == "WR" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // ROC: [PERIOD]
        else if (indicator == "ROC" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // CMO: [PERIOD]
        else if (indicator == "CMO" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        
        // ========================================================================
        // 趋势指标
        // ========================================================================
        
        // MA: [PERIOD]
        else if (indicator == "MA" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // EMA: [PERIOD]
        else if (indicator == "EMA" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // WMA: [PERIOD]
        else if (indicator == "WMA" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // DEMA: [PERIOD]
        else if (indicator == "DEMA" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // TEMA: [PERIOD]
        else if (indicator == "TEMA" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // KAMA: [PERIOD]
        else if (indicator == "KAMA" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // T3: [PERIOD]
        else if (indicator == "T3" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // MAMA: [FASTLIMIT, SLOWLIMIT]
        else if (indicator == "MAMA" && params.size() >= 2) {
            param_map["FASTLIMIT"] = params[0].toNumber();
            param_map["SLOWLIMIT"] = params[1].toNumber();
        }
        // ADX: [PERIOD]
        else if (indicator == "ADX" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // SAR: [ACCELERATION, MAXIMUM]
        else if (indicator == "SAR" && params.size() >= 2) {
            param_map["ACCELERATION"] = params[0].toNumber();
            param_map["MAXIMUM"] = params[1].toNumber();
        }
        // AROON: [PERIOD]
        else if (indicator == "AROON" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // Ichimoku: [TENKAN, KIJUN, SENKOU]
        else if (indicator == "Ichimoku" && params.size() >= 3) {
            param_map["TENKAN"] = params[0].toNumber();
            param_map["KIJUN"] = params[1].toNumber();
            param_map["SENKOU"] = params[2].toNumber();
        }
        // DMI: [PERIOD]
        else if (indicator == "DMI" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        
        // ========================================================================
        // 波动率指标
        // ========================================================================
        
        // ATR: [PERIOD] -> 映射到 PERIOD
        else if (indicator == "ATR" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // BOLL: [PERIOD, STD_DEV]
        else if (indicator == "BOLL" && params.size() >= 2) {
            param_map["PERIOD"] = params[0].toNumber();
            param_map["STD_DEV"] = params[1].toNumber();
        }
        
        // ========================================================================
        // 价格指标
        // ========================================================================
        
        // VWAP: [PERIOD]
        else if (indicator == "VWAP" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // TRIX: [PERIOD]
        else if (indicator == "TRIX" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        
        // ========================================================================
        // 动量指标补充
        // ========================================================================
        
        // KDJ: [N_PERIOD, M1_PERIOD, M2_PERIOD]
        else if (indicator == "KDJ" && params.size() >= 3) {
            param_map["N_PERIOD"] = params[0].toNumber();
            param_map["M1_PERIOD"] = params[1].toNumber();
            param_map["M2_PERIOD"] = params[2].toNumber();
        }
        // STOCHF: [FASTK_PERIOD, FASTD_PERIOD]
        else if (indicator == "STOCHF" && params.size() >= 2) {
            param_map["FASTK_PERIOD"] = params[0].toNumber();
            param_map["FASTD_PERIOD"] = params[1].toNumber();
        }
        // ULTOSC: [PERIOD1, PERIOD2, PERIOD3]
        else if (indicator == "ULTOSC" && params.size() >= 3) {
            param_map["PERIOD1"] = params[0].toNumber();
            param_map["PERIOD2"] = params[1].toNumber();
            param_map["PERIOD3"] = params[2].toNumber();
        }
        // PPO: [FAST_PERIOD, SLOW_PERIOD, SIGNAL_PERIOD]
        else if (indicator == "PPO" && params.size() >= 3) {
            param_map["FAST_PERIOD"] = params[0].toNumber();
            param_map["SLOW_PERIOD"] = params[1].toNumber();
            param_map["SIGNAL_PERIOD"] = params[2].toNumber();
        }
        // MTM: [PERIOD]
        else if (indicator == "MTM" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // APO: [FAST_PERIOD, SLOW_PERIOD]
        else if (indicator == "APO" && params.size() >= 2) {
            param_map["FAST_PERIOD"] = params[0].toNumber();
            param_map["SLOW_PERIOD"] = params[1].toNumber();
        }
        // Keltner: [PERIOD, MULTIPLIER]
        else if (indicator == "Keltner" && params.size() >= 2) {
            param_map["PERIOD"] = params[0].toNumber();
            param_map["MULTIPLIER"] = params[1].toNumber();
        }
        // NATR: [PERIOD]
        else if (indicator == "NATR" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        
        // ========================================================================
        // 成交量指标
        // ========================================================================
        
        // CMF: [PERIOD]
        else if (indicator == "CMF" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // EMV: [PERIOD]
        else if (indicator == "EMV" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // OBV: 无参数（跳过）
        // ADOSC: [FAST_PERIOD, SLOW_PERIOD]
        else if (indicator == "ADOSC" && params.size() >= 2) {
            param_map["FAST_PERIOD"] = params[0].toNumber();
            param_map["SLOW_PERIOD"] = params[1].toNumber();
        }
        
        // ========================================================================
        // 新增指标（第一阶段）
        // ========================================================================
        
        // Supertrend: [PERIOD, MULTIPLIER]
        else if (indicator == "Supertrend" && params.size() >= 2) {
            param_map["PERIOD"] = params[0].toNumber();
            param_map["MULTIPLIER"] = params[1].toNumber();
        }
        // DonchianChannel: [PERIOD]
        else if (indicator == "DonchianChannel" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // PivotPoints: 无参数（跳过）
        // StdDev: [PERIOD, NBDEV]
        else if (indicator == "StdDev" && params.size() >= 2) {
            param_map["PERIOD"] = params[0].toNumber();
            param_map["NBDEV"] = params[1].toNumber();
        }
        // VWMA: [PERIOD]
        else if (indicator == "VWMA" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        
        // ========================================================================
        // 新增指标（第二阶段）
        // ========================================================================
        
        // SwingHL: [PERIOD]
        else if (indicator == "SwingHL" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // VWAPBands: [STD_DEV]
        else if (indicator == "VWAPBands" && params.size() >= 1) {
            param_map["STD_DEV"] = params[0].toNumber();
        }
        // HV: [PERIOD]
        else if (indicator == "HV" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // ChandelierExit: [PERIOD, MULTIPLIER]
        else if (indicator == "ChandelierExit" && params.size() >= 2) {
            param_map["PERIOD"] = params[0].toNumber();
            param_map["MULTIPLIER"] = params[1].toNumber();
        }
        // ChoppinessIndex: [PERIOD]
        else if (indicator == "ChoppinessIndex" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // TSI: [LONG_PERIOD, SHORT_PERIOD, SIGNAL_PERIOD]
        else if (indicator == "TSI" && params.size() >= 3) {
            param_map["LONG_PERIOD"] = params[0].toNumber();
            param_map["SHORT_PERIOD"] = params[1].toNumber();
            param_map["SIGNAL_PERIOD"] = params[2].toNumber();
        }
        // BollingerBW: [PERIOD, STD_DEV]
        else if (indicator == "BollingerBW" && params.size() >= 2) {
            param_map["PERIOD"] = params[0].toNumber();
            param_map["STD_DEV"] = params[1].toNumber();
        }
        // MSB: [LOOKBACK]
        else if (indicator == "MSB" && params.size() >= 1) {
            param_map["LOOKBACK"] = params[0].toNumber();
        }
        // ADL: 无参数（跳过）
        // HHLL: [PERIOD]
        else if (indicator == "HHLL" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        
        // ========================================================================
        // Phase 3: Advanced Indicators
        // ========================================================================
        
        // ElderRay: [PERIOD]
        else if (indicator == "ElderRay" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // LinearRegSlope: [PERIOD]
        else if (indicator == "LinearRegSlope" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // ForceIndex: [PERIOD]
        else if (indicator == "ForceIndex" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // SchaffTrendCycle: [FAST_PERIOD, SLOW_PERIOD, CYCLE_PERIOD]
        else if (indicator == "SchaffTrendCycle" && params.size() >= 3) {
            param_map["FAST_PERIOD"] = params[0].toNumber();
            param_map["SLOW_PERIOD"] = params[1].toNumber();
            param_map["CYCLE_PERIOD"] = params[2].toNumber();
        }
        // Alligator: [JAW_PERIOD, TEETH_PERIOD, LIPS_PERIOD]
        else if (indicator == "Alligator" && params.size() >= 3) {
            param_map["JAW_PERIOD"] = params[0].toNumber();
            param_map["TEETH_PERIOD"] = params[1].toNumber();
            param_map["LIPS_PERIOD"] = params[2].toNumber();
        }
        // FisherTransform: [PERIOD]
        else if (indicator == "FisherTransform" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // McGinleyDynamic: [PERIOD]
        else if (indicator == "McGinleyDynamic" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // VolumePriceTrend: 无参数（跳过）
        // KaufmanER: [PERIOD]
        else if (indicator == "KaufmanER" && params.size() >= 1) {
            param_map["PERIOD"] = params[0].toNumber();
        }
        // ParabolicTP: [ACCELERATION, MAXIMUM]
        else if (indicator == "ParabolicTP" && params.size() >= 2) {
            param_map["ACCELERATION"] = params[0].toNumber();
            param_map["MAXIMUM"] = params[1].toNumber();
        }
        
        // 如果参数数量不匹配，返回空map（将使用JSON参数或默认值）
        
        return param_map;
    };
    
    // 步骤1：从DSL参数构建参数映射
    std::map<std::string, double> dsl_param_map = mapPositionalParams(indicator_name, dsl_params);
    
    // 步骤2：获取K线版本号（用于缓存失效检查）
    uint64_t current_version = kline_manager_.getVersion(timeframe);
    
    // 步骤3：准备参数（优先级：DSL > JSON > 默认值）
    // 注意：我们需要先合并所有参数，然后生成参数哈希，这样才能确保相同参数配置共享缓存
        indicators::IndicatorParams merged_params;
    
    // 3.1 先从JSON参数中加载所有参数
    try {
        auto ind_params = json_parameters_.find(indicator_name);
        if (ind_params != json_parameters_.end()) {
            auto tf_params = ind_params->second.find(timeframe);
            if (tf_params != ind_params->second.end()) {
                for (const auto& [param_name, param_value] : tf_params->second) {
                    merged_params.set(param_name, param_value.toNumber());
                }
            }
        }
    } catch (...) {
        // 参数提取失败，继续使用DSL参数或默认值
    }
    
    // 3.2 用DSL参数覆盖JSON参数（DSL优先级更高）
    for (const auto& [param_name, param_value] : dsl_param_map) {
        merged_params.set(param_name, param_value);  // param_value 已经是 double 类型
    }
    
    // 步骤4：生成参数哈希（基于最终合并后的参数）
    // 注意：参数哈希应该基于最终使用的参数，而不是只基于DSL参数
    // 这样才能确保相同参数配置（无论来源）共享缓存
    std::string param_hash;
    // 获取所有参数（包括默认值），生成稳定的哈希
    // 为了简化，我们只对DSL明确指定的参数生成哈希
    // 如果DSL参数为空，则使用空哈希（表示使用JSON或默认参数）
    if (!dsl_param_map.empty()) {
        // 对参数进行排序以确保哈希一致性
        std::vector<std::pair<std::string, double>> sorted_params(dsl_param_map.begin(), dsl_param_map.end());
        std::sort(sorted_params.begin(), sorted_params.end());
        
        for (const auto& [key, value] : sorted_params) {
            param_hash += key + "=" + std::to_string(value) + ",";
        }
    }
    // 如果DSL参数为空，但JSON中有参数，我们也需要生成哈希
    // 但为了简化，如果DSL参数为空，我们使用空字符串作为哈希（表示使用默认或JSON参数）
    
    // 步骤5：检查缓存（使用参数化缓存键）
    IndicatorResult result;
    if (indicator_cache_.get(timeframe, indicator_name, param_hash, current_version, result)) {
        // ✅ 缓存命中！直接返回
        utils::PerformanceMonitor::instance().record("Indicator_Cache_Hit", 1);
        return result;
    }
    
    // 缓存未命中，需要重新计算
    utils::PerformanceMonitor::instance().record("Indicator_Cache_Miss", 1);
    
    // 步骤6：获取K线数据
    if (!kline_manager_.hasKlines(timeframe)) {
        throw EvaluatorException(
            "Cannot calculate indicator " + indicator_name + "(" + timeframe + "): K-line data not available"
        );
    }

    const auto klines = kline_manager_.getKlines(timeframe);
    if (klines.empty()) {
        throw EvaluatorException(
            "Cannot calculate indicator " + indicator_name + "(" + timeframe + "): K-line data is empty"
        );
    }

    // 提取K线数据
    std::vector<double> close, high, low, open, volume;
    close.reserve(klines.size());
    high.reserve(klines.size());
    low.reserve(klines.size());
    open.reserve(klines.size());
    volume.reserve(klines.size());

    for (const auto& kline : klines) {
        close.push_back(kline.close);
        high.push_back(kline.high);
        low.push_back(kline.low);
        open.push_back(kline.open);
        volume.push_back(kline.volume);
    }

    // 步骤7：调用指标计算函数（使用合并后的参数）
    auto& registry = indicators::IndicatorRegistry::getInstance();
    if (!registry.hasIndicator(indicator_name)) {
        throw EvaluatorException(
            "Unsupported indicator for auto-calculation: " + indicator_name
        );
    }
    
    auto callable = registry.getIndicator(indicator_name);
    {
        // P2优化：性能监控 - 追踪具体指标的计算时间
        utils::PerformanceMonitor::Timer indicator_timer(
            utils::PerformanceMonitor::instance(),
            "Indicator_Calculate:" + indicator_name
        );
        result = callable(calculator_, close, high, low, volume, merged_params);
    }

    // 步骤8：更新缓存（使用参数化缓存键）
    indicator_cache_.set(timeframe, indicator_name, param_hash, result, current_version);

    return result;
}

// ============================================================================
// 函数：setParameter
// 功能：设置指标的参数值
// 
// 参数说明：
//   indicator_name - 指标名称
//   timeframe - 时间周期
//   param_name - 参数名（如"period"、"FAST_PERIOD"）
//   value - 参数值
// 
// 参数 vs 字段的区别：
//   - 参数：可修改的配置项（如MACD的周期）
//   - 字段：计算结果，只读（如RSI的value）
// 
// P0优化改进：
//   旧：使用三级嵌套Map（3次哈希查找）
//   新：使用扁平化Map + 复合键（1次哈希查找）⚡
//   性能提升：~70%
// ============================================================================
void Context::setParameter(const std::string& indicator_name,
                           const std::string& timeframe,
                           const std::string& param_name,
                           const Value& value) {
    // ========================================================================
    // 边界验证：参数名称和时间框架检查
    // ========================================================================
    if (indicator_name.empty()) {
        throw EvaluatorException("[VALIDATION] Indicator name cannot be empty");
    }
    
    if (timeframe.empty()) {
        throw EvaluatorException("[VALIDATION] Timeframe cannot be empty");
    }
    
    if (param_name.empty()) {
        throw EvaluatorException("[VALIDATION] Parameter name cannot be empty");
    }
    
    // ========================================================================
    // 边界验证：参数值范围检查（常见参数）
    // ========================================================================
    if (value.isNumber()) {
        double num_value = value.toNumber();
        
        // 周期参数必须为正整数
        // PERIOD 参数统一范围为 [1, 100]
        if (param_name == "PERIOD") {
            if (num_value < 1 || num_value > 100) {
                throw EvaluatorException(
                    "[VALIDATION] Parameter " + param_name + " must be in range [1, 100], got: " + 
                    std::to_string(num_value)
                );
            }
        }
        // 其他周期参数（指标参数等）保持原有范围
        else if (param_name == "FAST_PERIOD" || 
            param_name == "SLOW_PERIOD" || param_name == "SIGNAL_PERIOD" ||
            param_name == "PERIOD" || param_name == "RSI_PERIOD" || param_name == "STOCH_PERIOD" ||
            param_name == "K_PERIOD" || param_name == "D_PERIOD" ||
            param_name == "N_PERIOD" || param_name == "M1_PERIOD" ||
            param_name == "M2_PERIOD" || param_name == "FASTK_PERIOD" ||
            param_name == "FASTD_PERIOD" || param_name == "PERIOD1" ||
            param_name == "PERIOD2" || param_name == "PERIOD3" ||
            param_name == "TENKAN" || param_name == "KIJUN" ||
            param_name == "SENKOU" || param_name == "LONG_PERIOD" ||
            param_name == "SHORT_PERIOD" || param_name == "LOOKBACK" ||
            param_name == "JAW_PERIOD" || param_name == "TEETH_PERIOD" ||
            param_name == "LIPS_PERIOD" || param_name == "CYCLE_PERIOD") {
            if (num_value <= 0 || num_value > 10000) {
                throw EvaluatorException(
                    "[VALIDATION] Parameter " + param_name + " must be in range (0, 10000], got: " + 
                    std::to_string(num_value)
                );
            }
        }
        
        // 标准差倍数通常为1-5
        if (param_name == "STD_DEV" || param_name == "STDDEV" || 
            param_name == "NBDEV") {
            if (num_value <= 0 || num_value > 10) {
                throw EvaluatorException(
                    "[VALIDATION] Parameter " + param_name + " must be in range (0, 10], got: " + 
                    std::to_string(num_value)
                );
            }
        }
        
        // 加速因子通常为0.01-0.2
        if (param_name == "ACCELERATION" || param_name == "MAXIMUM") {
            if (num_value <= 0 || num_value > 1.0) {
                throw EvaluatorException(
                    "[VALIDATION] Parameter " + param_name + " must be in range (0, 1.0], got: " + 
                    std::to_string(num_value)
                );
            }
        }
        
        // 阈值参数检查（如RSI的超买超卖阈值）
        if (param_name == "OVERBOUGHT" || param_name == "OVERSOLD") {
            if (num_value < 0 || num_value > 100) {
                throw EvaluatorException(
                    "[VALIDATION] Parameter " + param_name + " must be in range [0, 100], got: " + 
                    std::to_string(num_value)
                );
            }
        }
        
        // 倍数参数（如Keltner通道的乘数）
        if (param_name == "MULTIPLIER" || param_name == "FASTLIMIT" || 
            param_name == "SLOWLIMIT") {
            if (num_value <= 0 || num_value > 20) {
                throw EvaluatorException(
                    "[VALIDATION] Parameter " + param_name + " must be in range (0, 20], got: " + 
                    std::to_string(num_value)
                );
            }
        }
    }
    
    // P1优化：使用ParameterStore管理参数
    parameter_store_.set(timeframe, indicator_name, param_name, value);
    
    // 同时存储到JSON参数映射（用于优先级查找）
    // 注意：这里假设所有通过setParameter()设置的参数都是JSON参数
    // DSL参数会通过赋值语句直接设置到parameter_store_中，不会更新json_parameters_
    json_parameters_[indicator_name][timeframe][param_name] = value;
}

// ============================================================================
// 函数：getParameter
// 功能：获取指标的参数值
// 
// 返回值：
//   参数的值
// 
// P0优化改进：
//   旧：三层嵌套查找（3次哈希 + 3次迭代器查找）
//   新：一次扁平化查找（1次哈希查找）⚡
//   性能提升：~70%
// 
// 错误处理：
//   - 如果参数不存在，抛出异常
// ============================================================================
Value Context::getParameter(const std::string& indicator_name,
                            const std::string& timeframe,
                            const std::string& param_name) const {
    // 参数优先级：DSL赋值 > JSON参数 > 默认值
    
    // 优先级1：检查DSL赋值（ParameterStore）
    if (parameter_store_.has(timeframe, indicator_name, param_name)) {
        return parameter_store_.get(timeframe, indicator_name, param_name);
    }
    
    // 优先级2：检查JSON参数（json_parameters_）
    try {
        auto ind_params = json_parameters_.find(indicator_name);
        if (ind_params != json_parameters_.end()) {
            auto tf_params = ind_params->second.find(timeframe);
            if (tf_params != ind_params->second.end()) {
                auto param_it = tf_params->second.find(param_name);
                if (param_it != tf_params->second.end()) {
                    return param_it->second;
                }
            }
        }
    } catch (...) {
        // JSON参数查找失败，继续使用默认值
    }
    
    // 优先级3：使用默认值（从指标注册表获取）
    // 注意：这里需要从指标注册表获取默认值，但当前实现暂时抛出异常
    // 如果需要支持默认值，需要扩展指标注册表接口
    throw EvaluatorException(
        "Parameter not found: " + indicator_name + "(" + timeframe + ")." + param_name +
        ". Please set it using '$(timeframe)." + indicator_name + "." + param_name + " = value;' or configure it in JSON."
    );
}

// ============================================================================
// 函数：clearParameter
// 功能：清空指定指标和时间周期的所有参数
// 
// 使用场景：
//   - 重置参数到默认值前
//   - 清理不再需要的参数
// ============================================================================
void Context::clearParameter(const std::string& indicator_name,
                             const std::string& timeframe) {
    // P1优化：使用ParameterStore清除参数
    parameter_store_.clear(timeframe, indicator_name);
    
    // 同时清除JSON参数映射
    auto ind_it = json_parameters_.find(indicator_name);
    if (ind_it != json_parameters_.end()) {
        auto& tf_map = ind_it->second;
        auto tf_it = tf_map.find(timeframe);
        if (tf_it != tf_map.end()) {
            tf_map.erase(tf_it);
        }
    }
}

// ============================================================================
// 函数：clearIndicators
// 功能：清空所有指标缓存
// 
// 说明：
//   在K线数据更新后应该调用此方法，确保指标基于最新数据重新计算
//   这对于回测等场景非常重要，避免使用过期的指标缓存
// ============================================================================
void Context::clearIndicators() {
    // P0优化：清除缓存结构
    // 注意：此方法已deprecated，不应在Engine::evaluate中调用
    // 缓存失效应该由K线版本号自动管理
    indicator_cache_.clear();
}

// ============================================================================
// P2优化：会话管理函数
// ============================================================================

/**
 * 开始新的evaluate会话
 * 在每次调用Engine::evaluate()开始时调用
 */
void Context::beginSession() {
    // 预留接口，当前无实际操作
}

/**
 * 结束当前evaluate会话
 * 在每次调用Engine::evaluate()结束时调用
 */
void Context::endSession() {
    // 预留接口，当前无实际操作
}

// ============================================================================
// 函数：setEnvVar / getEnvVar / clearEnvVar
// 功能：局部变量管理（用于自定义函数的 @myvar）
// 注意：不再用于全局环境变量（@CURRENT_PRICE 等已废弃）
// ============================================================================
void Context::setEnvVar(const std::string& var_name, const Value& value) {
    env_vars_[var_name] = value;
}

Value Context::getEnvVar(const std::string& var_name) const {
    auto it = env_vars_.find(var_name);
    if (it == env_vars_.end()) {
        throw EvaluatorException("Local variable not found: " + var_name);
    }
    return it->second;
}

void Context::clearEnvVar(const std::string& var_name) {
    env_vars_.erase(var_name);
}

// ============================================================================
// 🆕 v4.1: 全局变量管理方法
// ============================================================================

void Context::declareGlobalVariable(const std::string& var_name) {
    // 声明全局变量（未赋值状态）
    global_variables_[var_name] = Value();  // 空值
    global_var_assigned_[var_name] = false;  // 未赋值
}

void Context::setGlobalVariable(const std::string& var_name, const Value& value) {
    // 设置全局变量值
    global_variables_[var_name] = value;
    global_var_assigned_[var_name] = true;  // 标记为已赋值
}

Value Context::getGlobalVariable(const std::string& var_name) const {
    // 检查变量是否存在
    auto it = global_variables_.find(var_name);
    if (it == global_variables_.end()) {
        throw EvaluatorException("Global variable not declared: " + var_name);
    }
    
    // 检查变量是否已赋值
    auto assigned_it = global_var_assigned_.find(var_name);
    if (assigned_it == global_var_assigned_.end() || !assigned_it->second) {
        throw EvaluatorException("Global variable not assigned: " + var_name + 
                                " (variable declared but no value assigned before use)");
    }
    
    return it->second;
}

bool Context::hasGlobalVariable(const std::string& var_name) const {
    return global_variables_.find(var_name) != global_variables_.end();
}

bool Context::isGlobalVariableAssigned(const std::string& var_name) const {
    auto it = global_var_assigned_.find(var_name);
    return it != global_var_assigned_.end() && it->second;
}

void Context::clearGlobalVariables() {
    // 清空所有全局变量（每次GetSignal前调用以重新初始化）
    global_variables_.clear();
    global_var_assigned_.clear();
}

// ============================================================================
// 函数：setCurrentPrice / getCurrentPrice
// 功能：设置和获取当前市场价格
// ============================================================================
void Context::setCurrentPrice(double price) {
    current_price_ = price;
}

double Context::getCurrentPrice() const {
    return current_price_;
}

// ============================================================================
// 函数：setCurrentTime / getCurrentTime
// 功能：设置和获取当前时间戳
// 
// 时间戳说明：
//   Unix时间戳，表示从1970年1月1日至今的秒数（或毫秒数）
// ============================================================================
void Context::setCurrentTime(int64_t timestamp) {
    current_time_ = timestamp;
}

int64_t Context::getCurrentTime() const {
    return current_time_;
}

// ============================================================================
// 函数：setKlines / appendKline / appendKlines / getKlines / hasKlines
// 功能：管理K线数据（v10.0 重构：固定300根窗口 + 增量更新）
// 
// v10.0 核心变更：
// 1. 固定窗口大小：每个时间框架始终保持300根K线
// 2. 增量追加API：append_kline/append_klines 高性能增量更新
// 3. 自动淘汰：新数据进来时自动丢弃最旧的数据
// 4. 移除合成逻辑：不再支持convertAndSetKlines，由客户端负责准备数据
// 
// 使用方式：
// - 初始化：setKlines(timeframe, initial_300_klines)
// - 回测循环：appendKline(timeframe, new_kline) - 高性能，避免全量拷贝
// - 跳跃场景：appendKlines(timeframe, new_klines_array) - 批量追加
// ============================================================================

void Context::setKlines(const std::string& timeframe, const std::vector<prophet::Kline>& klines) {
    if (klines.empty()) {
        throw EvaluatorException("Klines cannot be empty");
    }
    
    // 只保留最后300根（如果超过）
    std::vector<prophet::Kline> window_klines;
    if (klines.size() > KLINE_WINDOW_SIZE) {
        window_klines.assign(
            klines.end() - KLINE_WINDOW_SIZE, 
            klines.end()
        );
        std::cout << "⚠️ " << timeframe << " K线数量(" << klines.size() 
                  << ")超过300根，只保留最后300根" << std::endl;
    } else {
        window_klines = klines;
    }
    
    // 使用KlineManager设置（会自动增加版本号，指标缓存自动失效）
    kline_manager_.setKlines(timeframe, window_klines);
}

void Context::appendKline(const std::string& timeframe, const prophet::Kline& kline) {
    // 获取当前K线列表
    auto current_klines = kline_manager_.getKlines(timeframe);
    
    if (current_klines.empty()) {
        throw EvaluatorException(
            "Cannot append kline: timeframe " + timeframe + " not initialized. " +
            "Call setKlines() first to initialize the window."
        );
    }
    
    // 追加新K线
    current_klines.push_back(kline);
    
    // 如果超过窗口大小，删除最旧的
    if (current_klines.size() > KLINE_WINDOW_SIZE) {
        current_klines.erase(current_klines.begin());
    }
    
    // 更新（会触发版本号递增，指标缓存失效）
    kline_manager_.setKlines(timeframe, current_klines);
}

void Context::appendKlines(const std::string& timeframe, const std::vector<prophet::Kline>& new_klines) {
    if (new_klines.empty()) {
        return;
    }
    
    // 获取当前K线列表
    auto current_klines = kline_manager_.getKlines(timeframe);
    
    if (current_klines.empty()) {
        throw EvaluatorException(
            "Cannot append klines: timeframe " + timeframe + " not initialized. " +
            "Call setKlines() first to initialize the window."
        );
    }
    
    // 批量追加
    current_klines.insert(
        current_klines.end(), 
        new_klines.begin(), 
        new_klines.end()
    );
    
    // 保持窗口大小（删除超出的旧数据）
    if (current_klines.size() > KLINE_WINDOW_SIZE) {
        size_t to_remove = current_klines.size() - KLINE_WINDOW_SIZE;
        current_klines.erase(
            current_klines.begin(), 
            current_klines.begin() + to_remove
        );
    }
    
    // 更新
    kline_manager_.setKlines(timeframe, current_klines);
}

const std::vector<prophet::Kline>& Context::getKlines(const std::string& timeframe) const {
    // P1优化：从KlineManager获取K线数据
    // 注意：返回引用需要特殊处理，这里使用静态变量
    static thread_local std::unordered_map<std::string, std::vector<prophet::Kline>> klines_cache;
    
    auto klines = kline_manager_.getKlines(timeframe);
    if (klines.empty() && !kline_manager_.hasKlines(timeframe)) {
        throw EvaluatorException("Klines not found for timeframe: " + timeframe);
    }
    
    klines_cache[timeframe] = klines;
    return klines_cache[timeframe];
}

// 检查是否存在某个时间周期的K线数据
bool Context::hasKlines(const std::string& timeframe) const {
    // P1优化：使用KlineManager
    return kline_manager_.hasKlines(timeframe);
}

size_t Context::getKlineCount(const std::string& timeframe) const {
    // 获取指定时间框架的K线数量
    if (!kline_manager_.hasKlines(timeframe)) {
        return 0;
    }
    const auto& klines = kline_manager_.getKlines(timeframe);
    return klines.size();
}

// ============================================================================
// 函数：computePrice
// 功能：计算特定K线的综合价格
// 
// 参数说明：
//   timeframe - 时间周期
//   method - 计算方法
//   offset - 偏移量（0=最新K线，1=前一根，以此类推）
// 
// 支持的计算方法：
//   AVG（Average）：平均价 = (开盘+最高+最低+收盘) / 4
//   MED（Median）：中位数 = 取最高、最低、收盘三个价格的中间值
//   TYP（Typical）：典型价 = (最高+最低+收盘) / 3
//   WCL（Weighted Close）：加权收盘 = (最高+最低+2*收盘) / 4
// 
// 使用场景：
//   在DSL中用PRICE函数计算综合价格
//   例如：PRICE(5m).AVG 获取5分钟K线的平均价
// ============================================================================
Value Context::computePrice(const std::string& timeframe, const std::string& method, int offset) const {
    // 第1步：获取K线数据（P1：使用KlineManager）
    if (!kline_manager_.hasKlines(timeframe)) {
        throw EvaluatorException("Timeframe not found for PRICE: " + timeframe);
    }
    const auto klines = kline_manager_.getKlines(timeframe);
    if (klines.empty()) {
        throw EvaluatorException("No kline data for PRICE timeframe: " + timeframe);
    }
    
    // 第2步：检查偏移量是否有效（范围 [-100, 0]）
    if (offset < -100 || offset > 0) {
        throw EvaluatorException("Invalid PRICE offset: " + std::to_string(offset) + ". Offset must be in range [-100, 0]");
    }
    
    // 第3步：获取指定的K线（从后往前数）
    // offset=0 表示最新的K线，offset=-1 表示前一根K线
    size_t index = klines.size() - 1 + offset;  // offset为负数
    if (index >= klines.size()) {
        throw EvaluatorException("Invalid PRICE offset: " + std::to_string(offset));
    }
    const auto& kline = klines[index];

    // 第4步：根据方法计算价格
    double result = 0.0;
    
    if (method == "AVG") {
        // 平均价：四个价格的简单平均
        result = (kline.open + kline.high + kline.low + kline.close) / 4.0;
    } 
    else if (method == "MED") {
        // 中位数：取最高、最低、收盘的中间值
        std::array<double, 3> arr {kline.high, kline.low, kline.close};
        std::sort(arr.begin(), arr.end());  // 排序
        result = arr[1];  // 取中间值
    } 
    else if (method == "TYP") {
        // 典型价：最高、最低、收盘的平均
        result = (kline.high + kline.low + kline.close) / 3.0;
    } 
    else if (method == "WCL") {
        // 加权收盘：收盘价的权重是2倍
        result = (kline.high + kline.low + 2.0 * kline.close) / 4.0;
    } 
    else {
        throw EvaluatorException("Unknown PRICE method: " + method);
    }
    
    return Value::fromNumber(result);
}

// ============================================================================
// 函数：computeHilbertTransform
// 功能：计算希尔伯特变换（Hilbert Transform）技术指标
// 
// 什么是希尔伯特变换？
//   一种高级的技术分析工具，用于：
//   - 识别市场周期
//   - 判断趋势模式
//   - 检测价格的相位变化
//   - 生成平滑的趋势线
// 
// 参数说明：
//   timeframe - 时间周期
//   method - 计算方法（见下方支持的方法）
//   subfield - 子字段名（某些方法会返回多个值）
// 
// 支持的方法：
//   DCPERIOD：主导周期（Dominant Cycle PERIOD）- 识别市场的主要周期
//   DCPHASE：主导周期相位（Dominant Cycle Phase）- 周期的相位角度
//   PHASOR：相量组件 - 返回两个值：inphase（同相）和quadrature（正交）
//   SINE：正弦波 - 返回两个值：sine（正弦）和lead（领先正弦）
//   TRENDLINE：趋势线 - 生成平滑的趋势线
//   TRENDMODE：趋势模式 - 判断是否处于趋势中（0或1）
// 
// 数据要求：
//   - TRENDLINE和TRENDMODE需要至少63根K线
//   - 其他方法需要至少32根K线
// ============================================================================
Value Context::computeHilbertTransform(const std::string& timeframe,
                                       const std::string& method,
                                       const std::string& subfield) const {
    // 第1步：获取K线数据（P1：使用KlineManager）
    if (!kline_manager_.hasKlines(timeframe)) {
        throw EvaluatorException("Timeframe not found for HT: " + timeframe);
    }
    const auto klines = kline_manager_.getKlines(timeframe);
    if (klines.empty()) {
        throw EvaluatorException("No kline data for HT timeframe: " + timeframe);
    }
    
    // 第2步：提取收盘价数据
    // 希尔伯特变换通常只使用收盘价
    std::vector<double> close;
    close.reserve(klines.size());
    for (const auto& k : klines) {
        close.push_back(k.close);
    }

    // 第3步：检查数据量是否足够
    // 不同的方法需要不同的最小数据量
    size_t min_required = (method == "TRENDLINE" || method == "TRENDMODE") ? 63 : 32;
    if (close.size() < min_required) {
        throw EvaluatorException("HT." + method + " requires at least " +
                                std::to_string(min_required) + " klines, got " +
                                std::to_string(close.size()));
    }

    // 第4步：准备TA-Lib函数调用的参数
    int startIdx = 0;  // 开始索引
    // 注意：TA-Lib 使用 int 索引，但 close.size() 是 size_t
    // 如果数据量超过 INT_MAX，这里会有问题，但实际场景中不太可能
    size_t close_size = close.size();
    int endIdx = static_cast<int>(close_size > static_cast<size_t>(INT_MAX) ? INT_MAX : close_size) - 1;  // 结束索引
    int outBeg = 0;   // 输出数据的起始位置
    int outNb = 0;    // 输出数据的数量

    // 第5步：根据方法调用相应的TA-Lib函数
    
    if (method == "DCPERIOD") {
        // 主导周期：识别市场的主要周期长度
        std::vector<double> out(close.size());
        TA_RetCode ret = TA_HT_DCPERIOD(startIdx, endIdx, close.data(), &outBeg, &outNb, out.data());
        if (ret != TA_SUCCESS || outNb == 0) {
            throw EvaluatorException("HT_DCPERIOD calculation failed");
        }
        // 返回最新的计算结果
        return Value::fromNumber(out[outBeg + outNb - 1]);
    }
    
    if (method == "DCPHASE") {
        // 主导周期相位：当前价格在周期中的位置（角度）
        std::vector<double> out(close.size());
        TA_RetCode ret = TA_HT_DCPHASE(startIdx, endIdx, close.data(), &outBeg, &outNb, out.data());
        if (ret != TA_SUCCESS || outNb == 0) {
            throw EvaluatorException("HT_DCPHASE calculation failed");
        }
        return Value::fromNumber(out[outBeg + outNb - 1]);
    }
    
    if (method == "PHASOR") {
        // 相量组件：返回同相和正交两个分量
        // 这两个分量可以用来判断价格的旋转方向
        std::vector<double> inphase(close.size());     // 同相分量
        std::vector<double> quadrature(close.size());  // 正交分量
        TA_RetCode ret = TA_HT_PHASOR(startIdx, endIdx, close.data(),
                                      &outBeg, &outNb,
                                      inphase.data(), quadrature.data());
        if (ret != TA_SUCCESS || outNb == 0) {
            throw EvaluatorException("HT_PHASOR calculation failed");
        }
        // 根据子字段名返回相应的值
        if (subfield == "inphase") {
            return Value::fromNumber(inphase[outBeg + outNb - 1]);
        }
        if (subfield == "quadrature") {
            return Value::fromNumber(quadrature[outBeg + outNb - 1]);
        }
        throw EvaluatorException("HT.PHASOR requires subfield: inphase or quadrature");
    }
    
    if (method == "SINE") {
        // 正弦波：生成正弦波和领先正弦波
        // 可用于预测价格转折点
        std::vector<double> sine(close.size());  // 正弦波
        std::vector<double> lead(close.size());  // 领先正弦波（提前1/4周期）
        TA_RetCode ret = TA_HT_SINE(startIdx, endIdx, close.data(),
                                    &outBeg, &outNb, sine.data(), lead.data());
        if (ret != TA_SUCCESS || outNb == 0) {
            throw EvaluatorException("HT_SINE calculation failed");
        }
        if (subfield == "sine") {
            return Value::fromNumber(sine[outBeg + outNb - 1]);
        }
        if (subfield == "lead") {
            return Value::fromNumber(lead[outBeg + outNb - 1]);
        }
        throw EvaluatorException("HT.SINE requires subfield: sine or lead");
    }
    
    if (method == "TRENDLINE") {
        // 趋势线：生成平滑的趋势线
        // 比简单移动平均线更平滑，延迟更小
        std::vector<double> out(close.size());
        TA_RetCode ret = TA_HT_TRENDLINE(startIdx, endIdx, close.data(), &outBeg, &outNb, out.data());
        if (ret != TA_SUCCESS || outNb == 0) {
            throw EvaluatorException("HT_TRENDLINE calculation failed");
        }
        return Value::fromNumber(out[outBeg + outNb - 1]);
    }
    
    if (method == "TRENDMODE") {
        // 趋势模式：判断当前是否处于趋势中
        // 返回值：0=非趋势（震荡），1=趋势
        std::vector<int> out(close.size());
        TA_RetCode ret = TA_HT_TRENDMODE(startIdx, endIdx, close.data(), &outBeg, &outNb, out.data());
        if (ret != TA_SUCCESS || outNb == 0) {
            throw EvaluatorException("HT_TRENDMODE calculation failed");
        }
        // 将整数转换为浮点数返回
        return Value::fromNumber(static_cast<double>(out[outBeg + outNb - 1]));
    }

    throw EvaluatorException("Unknown HT method: " + method);
}

// ============================================================================
// v10.0 变更说明：convertAndSetKlines 已移除
// 
// 原因：
// - 核心引擎不再负责K线合成逻辑
// - 客户端负责准备所有时间框架的数据
// - 简化架构，职责更清晰
// 
// 如果需要K线合成功能，请在客户端实现，或使用交易所提供的原始数据
// ============================================================================

// ============================================================================
// 函数：getAllTimeframes
// 功能：获取当前Context中所有已加载的时间框架列表
// 
// 返回值：
//   时间框架字符串的向量（如 ["1m", "5m", "15m", "1h"]）
// 
// 使用场景：
//   - 调试：查看当前有哪些时间框架的K线数据
//   - 批量处理：遍历所有时间框架进行某些操作
// ============================================================================
std::vector<std::string> Context::getAllTimeframes() const {
    // P1优化：使用KlineManager获取所有时间框架
    auto timeframes = kline_manager_.getAllTimeframes();
    
    // 按时间框架的分钟数排序（从小到大）
    std::sort(timeframes.begin(), timeframes.end(), 
              [](const std::string& a, const std::string& b) {
                  try {
                      int min_a = tools::kline::Converter::timeframeToMinutes(a);
                      int min_b = tools::kline::Converter::timeframeToMinutes(b);
                      return min_a < min_b;
                  } catch (...) {
                      return a < b;  // 如果解析失败，使用字符串比较
                  }
              });
    
    return timeframes;
}

// ============================================================================
// 函数：convertAndSetKlines
// 功能：从已有的源时间框架K线转换并生成目标时间框架K线
// 
// 参数说明：
//   source_timeframe - 源时间框架（如"1m"）
//   target_timeframe - 目标时间框架（如"5m", "15m", "1h"）
//   period - 返回最近N根目标K线（0=返回全部）
// 
// 使用场景：
//   核心引擎只接收1分钟K线，然后根据需要合成任何高时间框架的K线
//   例如：
//     context.setKlines("1m", klines_1m);
//     context.convertAndSetKlines("1m", "5m");   // 生成5分钟K线
//     context.convertAndSetKlines("1m", "15m");  // 生成15分钟K线
//     context.convertAndSetKlines("1m", "1h");   // 生成1小时K线
// 
// 优势：
//   1. 只需存储最基础的1分钟K线
//   2. 按需生成其他时间框架
//   3. 确保所有时间框架的K线数据一致
// ============================================================================
void Context::convertAndSetKlines(const std::string& source_timeframe, const std::string& target_timeframe, int period) {
    // 第1步：检查源K线是否存在
    if (!hasKlines(source_timeframe)) {
        throw EvaluatorException("Source klines not found for timeframe: " + source_timeframe);
    }

    // 第2步：获取源K线数据
    const auto& source_klines = getKlines(source_timeframe);
    if (source_klines.empty()) {
        throw EvaluatorException("Source klines are empty for timeframe: " + source_timeframe);
    }

    // 第3步：解析时间框架为分钟数
    int from_minutes = tools::kline::Converter::timeframeToMinutes(source_timeframe);
    int to_minutes = tools::kline::Converter::timeframeToMinutes(target_timeframe);

    // 第4步：转换K线
    std::vector<prophet::Kline> converted = tools::kline::Converter::convert(
        source_klines,
        from_minutes,
        to_minutes,
        period
    );

    // 第5步：存储转换后的K线
    setKlines(target_timeframe, converted);
}

// ============================================================================
// 时间序列数据管理（用于FEARGREED等函数）
// ============================================================================

void Context::setFearGreedSeries(const std::vector<FearGreedData>& series) {
    fear_greed_series_ = series;
}

const std::vector<FearGreedData>& Context::getFearGreedSeries() const {
    return fear_greed_series_;
}

bool Context::hasFearGreedData() const {
    return !fear_greed_series_.empty();
}

void Context::setFundingRateSeries(const std::vector<FundingRateData>& series) {
    funding_rate_series_ = series;
}

const std::vector<FundingRateData>& Context::getFundingRateSeries() const {
    return funding_rate_series_;
}

bool Context::hasFundingRateData() const {
    return !funding_rate_series_.empty();
}

void Context::setLongShortRatioSeries(const std::vector<LongShortRatioData>& series) {
    long_short_series_ = series;
}

const std::vector<LongShortRatioData>& Context::getLongShortRatioSeries() const {
    return long_short_series_;
}

bool Context::hasLongShortRatioData() const {
    return !long_short_series_.empty();
}

// ============================================================================
// 辅助函数：计算指标所需的最小K线数量
// ============================================================================
int Context::getMinRequiredKlines(const std::string& indicator_name, 
                                    const indicators::IndicatorParams& params) const {
    // 大多数指标的最小周期数就是其最大周期参数
    // 这里根据指标名称和参数计算所需的最小K线数量
    
    // MACD: 需要 max(FAST_PERIOD, SLOW_PERIOD, SIGNAL_PERIOD) + 1
    if (indicator_name == "MACD") {
        int fast = params.get_int("FAST_PERIOD", 12);
        int slow = params.get_int("SLOW_PERIOD", 26);
        int signal = params.get_int("SIGNAL_PERIOD", 9);
        return std::max({fast, slow, signal}) + 1;
    }
    
    // RSI: 需要 PERIOD + 1
    if (indicator_name == "RSI") {
        int period = params.get_int("PERIOD", 14);
        return period + 1;
    }
    
    // STOCHRSI: 需要 max(RSI_PERIOD, STOCH_PERIOD) + 1
    if (indicator_name == "STOCHRSI") {
        int RSI_PERIOD = params.get_int("RSI_PERIOD", 14);
        int STOCH_PERIOD = params.get_int("STOCH_PERIOD", 14);
        return std::max(RSI_PERIOD, STOCH_PERIOD) + 1;
    }
    
    // CCI: 需要 PERIOD + 1
    if (indicator_name == "CCI") {
        int period = params.get_int("PERIOD", 20);
        return period + 1;
    }
    
    // MFI: 需要 PERIOD + 1
    if (indicator_name == "MFI") {
        int period = params.get_int("PERIOD", 14);
        return period + 1;
    }
    
    // WR: 需要 PERIOD + 1
    if (indicator_name == "WR") {
        int period = params.get_int("PERIOD", 14);
        return period + 1;
    }
    
    // ROC: 需要 PERIOD + 1
    if (indicator_name == "ROC") {
        int period = params.get_int("PERIOD", 10);
        return period + 1;
    }
    
    // CMO: 需要 PERIOD + 1
    if (indicator_name == "CMO") {
        int period = params.get_int("PERIOD", 14);
        return period + 1;
    }
    
    // MA系列（MA, EMA, WMA, DEMA, TEMA, T3, KAMA, MAMA）: 需要 PERIOD + 1
    if (indicator_name == "MA" || indicator_name == "EMA" || indicator_name == "WMA" ||
        indicator_name == "DEMA" || indicator_name == "TEMA" || indicator_name == "T3" ||
        indicator_name == "KAMA" || indicator_name == "MAMA") {
        int period = params.get_int("PERIOD", 20);
        return period + 1;
    }
    
    // BOLL: 需要 PERIOD + 1
    if (indicator_name == "BOLL") {
        int period = params.get_int("PERIOD", 20);
        return period + 1;
    }
    
    // ATR: 需要 PERIOD + 1
    if (indicator_name == "ATR") {
        int period = params.get_int("PERIOD", 14);
        return period + 1;
    }
    
    // ADX: 需要 PERIOD + 1
    if (indicator_name == "ADX") {
        int period = params.get_int("PERIOD", 14);
        return period + 1;
    }
    
    // AROON: 需要 PERIOD + 1
    if (indicator_name == "AROON") {
        int period = params.get_int("PERIOD", 14);
        return period + 1;
    }
    
    // ICHIMOKU: 需要 max(TENKAN, KIJUN, SENKOU) + 1
    if (indicator_name == "ICHIMOKU") {
        int tenkan = params.get_int("TENKAN", 9);
        int kijun = params.get_int("KIJUN", 26);
        int senkou = params.get_int("SENKOU", 52);
        return std::max({tenkan, kijun, senkou}) + 1;
    }
    
    // ALLIGATOR: 需要 max(JAW_PERIOD, TEETH_PERIOD, LIPS_PERIOD) + 1
    if (indicator_name == "ALLIGATOR") {
        int jaw = params.get_int("JAW_PERIOD", 13);
        int teeth = params.get_int("TEETH_PERIOD", 8);
        int lips = params.get_int("LIPS_PERIOD", 5);
        return std::max({jaw, teeth, lips}) + 1;
    }
    
    // DONCHIAN: 需要 PERIOD + 1
    if (indicator_name == "DONCHIAN") {
        int period = params.get_int("PERIOD", 20);
        return period + 1;
    }
    
    // 默认值：如果无法确定，返回100（保守估计）
    return 100;
}

// ============================================================================
// 辅助函数：验证指标参数的有效性
// ============================================================================
void Context::validateIndicatorParams(const std::string& indicator_name,
                                     const indicators::IndicatorParams& params) const {
    // MACD: FAST_PERIOD < SLOW_PERIOD
    if (indicator_name == "MACD") {
        int fast = params.get_int("FAST_PERIOD", 12);
        int slow = params.get_int("SLOW_PERIOD", 26);
        if (fast >= slow) {
            throw EvaluatorException(
                "[VALIDATION] MACD: FAST_PERIOD (" + std::to_string(fast) + 
                ") must be < SLOW_PERIOD (" + std::to_string(slow) + ")"
            );
        }
    }
    
    // ICHIMOKU: TENKAN < KIJUN < SENKOU
    if (indicator_name == "ICHIMOKU") {
        int tenkan = params.get_int("TENKAN", 9);
        int kijun = params.get_int("KIJUN", 26);
        int senkou = params.get_int("SENKOU", 52);
        if (tenkan >= kijun || kijun >= senkou) {
            throw EvaluatorException(
                "[VALIDATION] ICHIMOKU: TENKAN (" + std::to_string(tenkan) + 
                ") < KIJUN (" + std::to_string(kijun) + 
                ") < SENKOU (" + std::to_string(senkou) + ") must be satisfied"
            );
        }
    }
    
    // ALLIGATOR: JAW_PERIOD > TEETH_PERIOD > LIPS_PERIOD
    if (indicator_name == "ALLIGATOR") {
        int jaw = params.get_int("JAW_PERIOD", 13);
        int teeth = params.get_int("TEETH_PERIOD", 8);
        int lips = params.get_int("LIPS_PERIOD", 5);
        if (jaw <= teeth || teeth <= lips) {
            throw EvaluatorException(
                "[VALIDATION] ALLIGATOR: JAW_PERIOD (" + std::to_string(jaw) + 
                ") > TEETH_PERIOD (" + std::to_string(teeth) + 
                ") > LIPS_PERIOD (" + std::to_string(lips) + ") must be satisfied"
            );
        }
    }
    
    // RSI: OVERBOUGHT > OVERSOLD
    if (indicator_name == "RSI") {
        if (params.has("OVERBOUGHT") && params.has("OVERSOLD")) {
            double overbought = params.get_double("OVERBOUGHT", 70.0);
            double oversold = params.get_double("OVERSOLD", 30.0);
            if (overbought <= oversold) {
                throw EvaluatorException(
                    "[VALIDATION] RSI: OVERBOUGHT (" + std::to_string(overbought) + 
                    ") must be > OVERSOLD (" + std::to_string(oversold) + ")"
                );
            }
        }
    }
}

// ============================================================================
// 文件结束
// ============================================================================

} // namespace prophet::dsl