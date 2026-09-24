/*
 * ============================================================================
 * 文件名：Data.cpp
 * 功能说明：数据访问函数实现
 * 
 * 这个文件实现了从K线数据中获取和计算各种信息的函数
 * 
 * 支持的数据函数：
 * - KLINE：获取K线的各个字段（open、high、low、close、volume）
 * - PRICE：计算综合价格（AVG平均价、MED中位数、TYP典型价、WCL加权收盘价、MID中点价）
 * - HIGHEST：获取一段时间内的最高值
 * - LOWEST：获取一段时间内的最低值
 * - AVERAGE：计算一段时间内的平均值
 * - STD：计算一段时间内的标准差
 * - CHANGE：计算变化量（.value=绝对值，.pct=百分比）
 * - SUM：计算累加和
 * - SLOPE：计算线性回归斜率
 * - RANK：计算排名（.value=绝对排名，.pct=百分位）
 * - MEDIAN：计算中位数
 * - VARIANCE：计算方差
 * - CROSS：交叉检测（.above/.below/.any）
 * - ZSCORE：标准化得分
 * - PERCENTILE：百分位数
 * - VOLA：波动率分析（.value/.pct）
 * - ATR：平均真实波幅（.value/.pct）
 * - HT：希尔伯特变换相关指标（周期、相位、趋势等）
 * - PATTERN：K线形态识别（吞没、锤子线、十字星等）
 * - POWER：力量平衡指标（BOP）
 * - FVG：Fair Value Gap检测和分析（bullish/bearish/top/bottom/mid/filled等）
 * - PREMIUM：溢价/折价区分析（zone/pct/level/strength等）
 * - ORDERBLOCK：订单块检测和分析（bullish/bearish/top/bottom/strength等）
 * - SWING：摆动高低点检测（high/low/price/strength等）
 * - BOS：结构突破检测（bullish/bearish/price/swing等）
 * - CHOCH：特征改变检测（bullish/bearish/price/swing等）
 * - LIQUIDITY：流动性区域检测（buyside/sellside/price/strength等）
 * - BREAKER：突破块检测（bullish/bearish/top/bottom等）
 * 
 * 使用示例：
 *   KLINE(5m).close(0)  // 获取5分钟K线的最新收盘价
 *   PRICE(5m).AVG  // 获取5分钟K线的平均价
 *   HIGHEST(5m).high(20)  // 获取过去20根5分钟K线的最高价
 *   AVERAGE(5m).close(20)  // 获取过去20根K线收盘价的平均值
 * ============================================================================
 */

#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/core/context.hpp"
#include "prophet/functions/Base.hpp"
#include "prophet/functions/Pattern.hpp"
#include "prophet/functions/Sequence.hpp"
#include "prophet/functions/FVG.hpp"
#include "prophet/functions/PremiumDiscount.hpp"
#include "prophet/functions/OrderBlock.hpp"
#include "prophet/functions/SwingPoint.hpp"
#include "prophet/functions/MarketStructure.hpp"
#include "prophet/functions/Liquidity.hpp"
#include "prophet/functions/BreakerBlock.hpp"
#include "ta_libc.h"

using prophet::functions::Base;

namespace prophet::functions {

namespace {

Base& get_base() {
    static Base instance;
    return instance;
}

struct SeriesData {
    std::vector<double> open;
    std::vector<double> high;
    std::vector<double> low;
    std::vector<double> close;
    std::vector<double> volume;
};

SeriesData extract_series(const std::vector<Kline>& klines) {
    SeriesData data;
    data.open.reserve(klines.size());
    data.high.reserve(klines.size());
    data.low.reserve(klines.size());
    data.close.reserve(klines.size());
    data.volume.reserve(klines.size());
    for (const auto& k : klines) {
        data.open.push_back(k.open);
        data.high.push_back(k.high);
        data.low.push_back(k.low);
        data.close.push_back(k.close);
        data.volume.push_back(k.volume);
    }
    return data;
}

const std::string& require_field(const FunctionCall& call) {
    if (call.field.empty()) {
        throw EvaluatorException(call.name + " requires field");
    }
    return call.field;
}

const std::string& require_timeframe(const FunctionCall& call) {
    if (call.timeframe.empty()) {
        throw EvaluatorException(call.name + " requires timeframe");
    }
    return call.timeframe;
}

SeriesData get_series(const FunctionCall& call, prophet::dsl::Context& ctx) {
    require_timeframe(call);
    if (!ctx.hasKlines(call.timeframe)) {
        throw EvaluatorException("Kline data unavailable for timeframe: " + call.timeframe);
    }
    return extract_series(ctx.getKlines(call.timeframe));
}

int require_period(const FunctionCall& call, prophet::dsl::Context& ctx) {
    if (call.arguments.empty()) {
        throw EvaluatorException(call.name + " requires period argument");
    }
    return static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
}

// ============================================================================
// 缓存机制辅助函数
// ============================================================================

/**
 * 通用缓存调用函数
 * 
 * 用于避免重复计算多返回值函数或计算密集型函数
 * 
 * @tparam Func 计算函数类型（通常是lambda）
 * @param call 函数调用信息
 * @param ctx 上下文（包含缓存）
 * @param cache_prefix 缓存键前缀（如"BB"、"RSI"）
 * @param params 参数列表（用于构建完整缓存键）
 * @param compute_func 计算函数（当缓存未命中时调用）
 * @return FunctionResult 函数结果
 * 
 * 使用示例：
 *   return cached_call(call, ctx, "BB", {std::to_string(period)}, [&]() {
 *       SeriesData data = get_series(call, ctx);
 *       return get_base().BB(data.close, period, 2.0);
 *   });
 */
template<typename Func>
FunctionResult cached_call(
    const FunctionCall& call,
    prophet::dsl::Context& ctx,
    const std::string& cache_prefix,
    const std::vector<std::string>& params,
    Func compute_func
) {
    // 1. 构建缓存键: prefix_param1_param2_...
    std::string cache_key = cache_prefix;
    for (const auto& p : params) {
        cache_key += "_" + p;
    }
    
    // 2. 尝试从缓存获取结果
    try {
        IndicatorResult cached = ctx.getIndicator(cache_key, call.timeframe);
        
        // 3. 根据subfield返回对应字段
        if (!call.subfield.empty()) {
            return FunctionResult::fromValue(cached.get(call.subfield));
        }
        // 如果没有subfield，返回默认的value字段
        return FunctionResult::fromValue(cached.get("value"));
        
    } catch (...) {
        // 4. 缓存未命中，执行计算
        IndicatorResult result = compute_func();
        
        // 5. 将结果存入缓存
        ctx.setIndicator(cache_key, call.timeframe, result);
        
        // 6. 返回请求的字段
        if (!call.subfield.empty()) {
            return FunctionResult::fromValue(result.get(call.subfield));
        }
        return FunctionResult::fromValue(result.get("value"));
    }
}

} // namespace

void register_data_functions(FunctionRegistry& registry) {
    registry.register_function("KLINE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int offset = 0;
        if (!call.arguments.empty()) {
            offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }
        IndicatorResult indicator = get_base().KLINE(data.open, data.high, data.low, data.close, data.volume, call.field, offset);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("HIGHEST", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().HIGHEST(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("LOWEST", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().LOWEST(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("AVERAGE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().AVERAGE(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("STD", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().STD(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("CHANGE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().CHANGE(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        
        // 支持通过 subfield 访问不同的值
        // CHANGE(5m).close(10).value - 绝对变化量（默认）
        // CHANGE(5m).close(10).pct   - 百分比变化率
        if (!call.subfield.empty()) {
            if (call.subfield == "pct" || call.subfield == "PCT") {
                return FunctionResult::fromValue(indicator.get("pct"));
            }
        }
        // 默认返回绝对变化量
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("SUM", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().SUM(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("SLOPE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().SLOPE(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("RANK", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().RANK(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        
        // 支持通过 subfield 访问不同的值
        // RANK(5m).close(20).value - 绝对排名（默认）
        // RANK(5m).close(20).pct   - 百分位排名
        if (!call.subfield.empty()) {
            if (call.subfield == "pct" || call.subfield == "PCT") {
                return FunctionResult::fromValue(indicator.get("pct"));
            }
        }
        // 默认返回绝对排名
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("MEDIAN", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().MEDIAN(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("VARIANCE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().VARIANCE(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("CROSS", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        
        // CROSS 需要一个阈值参数
        if (call.arguments.empty()) {
            throw std::runtime_error("CROSS requires a threshold argument");
        }
        
        double threshold = call.arguments[0]->evaluate(ctx).toNumber();
        IndicatorResult indicator = get_base().CROSS(data.open, data.high, data.low, data.close, data.volume, call.field, threshold);
        
        // 支持通过 subfield 访问不同的布尔字段
        // CROSS(5m).close(100).above  - 向上穿越
        // CROSS(5m).close(100).below  - 向下穿越
        // CROSS(5m).close(100).any    - 任意穿越
        if (!call.subfield.empty()) {
            std::string subfield_lower = call.subfield;
            std::transform(subfield_lower.begin(), subfield_lower.end(), subfield_lower.begin(), 
                [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
            
            if (subfield_lower == "above") {
                return FunctionResult::fromValue(indicator.get("above"));
            } else if (subfield_lower == "below") {
                return FunctionResult::fromValue(indicator.get("below"));
            } else if (subfield_lower == "any") {
                return FunctionResult::fromValue(indicator.get("any"));
            }
        }
        
        // 默认返回 above
        return FunctionResult::fromValue(indicator.get("above"));
    });

    registry.register_function("ZSCORE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().ZSCORE(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("PERCENTILE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        
        // PERCENTILE 需要两个参数：PERIOD 和 quantile
        if (call.arguments.size() < 2) {
            throw std::runtime_error("PERCENTILE requires two arguments: period and quantile");
        }
        
        int period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        double quantile = call.arguments[1]->evaluate(ctx).toNumber();
        
        IndicatorResult indicator = get_base().PERCENTILE(data.open, data.high, data.low, data.close, data.volume, call.field, period, quantile);
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("VOLA", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        IndicatorResult indicator = get_base().VOLA(data.open, data.high, data.low, data.close, data.volume, call.field, period);
        
        // 支持通过 subfield 访问不同的值
        // VOLA(5m).close(20).value - 绝对波动率（默认）
        // VOLA(5m).close(20).pct   - 百分比波动率
        if (!call.subfield.empty()) {
            if (call.subfield == "pct" || call.subfield == "PCT") {
                return FunctionResult::fromValue(indicator.get("pct"));
            }
        }
        // 默认返回绝对波动率
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("ATR", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        require_field(call);
        SeriesData data = get_series(call, ctx);
        int period = require_period(call, ctx);
        
        // ATR 特殊处理：不需要 field，使用 high/low/close
        IndicatorResult indicator = get_base().ATR(data.open, data.high, data.low, data.close, period);
        
        // 支持通过 subfield 访问不同的值
        // ATR(5m).close(14).value - 绝对ATR（默认）
        // ATR(5m).close(14).pct   - 百分比ATR
        if (!call.subfield.empty()) {
            if (call.subfield == "pct" || call.subfield == "PCT") {
                return FunctionResult::fromValue(indicator.get("pct"));
            }
        }
        // 默认返回绝对ATR
        return FunctionResult::fromValue(indicator.get("value"));
    });

    registry.register_function("PRICE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        require_field(call);
        
        // AVG, MED, TYP, WCL使用单K线计算
        int offset = 0;
        if (!call.arguments.empty()) {
            offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }
        Value value = ctx.computePrice(call.timeframe, call.field, offset);
        return FunctionResult::fromValue(value);
    });

    registry.register_function("HT", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        require_field(call);
        Value value = ctx.computeHilbertTransform(call.timeframe, call.field, call.subfield);
        return FunctionResult::fromValue(value);
    });

    registry.register_function("PATTERN", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        const std::string& pattern_name = require_field(call);
        double penetration = 0.3;
        if (!call.arguments.empty()) {
            penetration = call.arguments[0]->evaluate(ctx).toNumber();
            if (penetration < 0.0 || penetration > 1.0) {
                throw EvaluatorException("Penetration must be between 0.0 and 1.0");
            }
        }

        if (!ctx.hasKlines(call.timeframe)) {
            return FunctionResult::fromBoolean(false);
        }

        const auto& klines = ctx.getKlines(call.timeframe);
        try {
            bool detected = PatternRecognizer::recognize(klines, pattern_name, penetration);
            return FunctionResult::fromBoolean(detected);
        } catch (const std::exception&) {
            return FunctionResult::fromBoolean(false);
        }
    });


    // ========================================================================
    // POWER - 力量/动量函数组
    // ========================================================================
    registry.register_function("POWER", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        require_field(call);
        SeriesData data = get_series(call, ctx);

        int offset = 0;
        if (!call.arguments.empty()) {
            offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }

        if (call.field == "BOP") {
            IndicatorResult indicator = get_base().BOP(data.open, data.high, data.low, data.close, offset);
            return FunctionResult::fromValue(indicator.get("value"));
        }

        throw EvaluatorException("POWER: Unknown method '" + call.field + "'. Supported: BOP");
    });

    // ========================================================================
    // FVG - Fair Value Gap（公平价值缺口）检测和分析
    // ========================================================================
    registry.register_function("FVG", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            // 没有K线数据时的默认返回
            if (call.field == "bullish" || call.field == "bearish" || call.field == "any") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 1. 形态检测（返回布尔值）
        if (call.field == "bullish") {
            bool detected = FVGAnalyzer::detectBullishFVG(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        if (call.field == "bearish") {
            bool detected = FVGAnalyzer::detectBearishFVG(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        if (call.field == "any") {
            bool detected = FVGAnalyzer::detectAnyFVG(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        // 2. 区域信息（返回数值）- FVG(5m).top, FVG(5m).bottom, FVG(5m).mid, FVG(5m).size
        if (call.field == "top" || call.field == "bottom" || call.field == "mid" || call.field == "size") {
            // 获取lookback参数（默认0，表示最近的FVG）
            int lookback = 0;
            if (!call.arguments.empty()) {
                lookback = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            // 获取未回补的FVG列表
            auto unfilled_fvgs = FVGAnalyzer::getUnfilledFVGs(klines, 100, 0.0);
            
            if (unfilled_fvgs.empty() || lookback >= static_cast<int>(unfilled_fvgs.size())) {
                return FunctionResult::fromValue(Value::fromNumber(0.0));
            }
            
            const FVGZone& fvg = unfilled_fvgs[lookback];
            
            if (call.field == "top") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.top));
            }
            if (call.field == "bottom") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.bottom));
            }
            if (call.field == "mid") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.mid));
            }
            if (call.field == "size") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.size));
            }
        }
        
        // 3. 回补状态 - FVG(5m).filled.pct, FVG(5m).filled.isfull, FVG(5m).filled.ispartial
        if (call.field == "filled") {
            // 需要subfield
            if (call.subfield.empty()) {
                throw EvaluatorException("FVG.filled requires subfield: .pct, .isfull, or .ispartial");
            }
            
            // 获取lookback参数（默认0）
            int lookback = 0;
            if (!call.arguments.empty()) {
                lookback = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            auto unfilled_fvgs = FVGAnalyzer::getUnfilledFVGs(klines, 100, 0.0);
            
            if (unfilled_fvgs.empty() || lookback >= static_cast<int>(unfilled_fvgs.size())) {
                if (call.subfield == "pct") {
                    return FunctionResult::fromValue(Value::fromNumber(0.0));
                }
                return FunctionResult::fromBoolean(false);
            }
            
            const FVGZone& fvg = unfilled_fvgs[lookback];
            
            if (call.subfield == "pct") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.filled_pct));
            }
            if (call.subfield == "isfull") {
                return FunctionResult::fromBoolean(fvg.is_filled);
            }
            if (call.subfield == "ispartial") {
                bool is_partial = fvg.filled_pct > 0 && !fvg.is_filled;
                return FunctionResult::fromBoolean(is_partial);
            }
            
            throw EvaluatorException("FVG.filled: Unknown subfield '" + call.subfield + "'. Supported: pct, isfull, ispartial");
        }
        
        // 4. 剩余区域 - FVG(5m).remaining.top, FVG(5m).remaining.bottom, etc.
        if (call.field == "remaining") {
            if (call.subfield.empty()) {
                throw EvaluatorException("FVG.remaining requires subfield: .top, .bottom, .mid, or .size");
            }
            
            int lookback = 0;
            if (!call.arguments.empty()) {
                lookback = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            auto unfilled_fvgs = FVGAnalyzer::getUnfilledFVGs(klines, 100, 0.0);
            
            if (unfilled_fvgs.empty() || lookback >= static_cast<int>(unfilled_fvgs.size())) {
                return FunctionResult::fromValue(Value::fromNumber(0.0));
            }
            
            const FVGZone& fvg = unfilled_fvgs[lookback];
            
            if (call.subfield == "top") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.remaining_top));
            }
            if (call.subfield == "bottom") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.remaining_bottom));
            }
            if (call.subfield == "mid") {
                double remaining_mid = (fvg.remaining_top + fvg.remaining_bottom) / 2.0;
                return FunctionResult::fromValue(Value::fromNumber(remaining_mid));
            }
            if (call.subfield == "size") {
                return FunctionResult::fromValue(Value::fromNumber(fvg.remainingSize()));
            }
            
            throw EvaluatorException("FVG.remaining: Unknown subfield '" + call.subfield + "'. Supported: top, bottom, mid, size");
        }
        
        // 5. 统计功能 - FVG(5m).count(20), FVG(5m).count(20).bullish, etc.
        if (call.field == "count") {
            if (call.arguments.empty()) {
                throw EvaluatorException("FVG.count requires period argument: FVG(5m).count(20)");
            }
            
            int period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            
            std::string count_type = "all";
            if (!call.subfield.empty()) {
                count_type = call.subfield;  // bullish, bearish, unfilled
            }
            
            int count = FVGAnalyzer::countFVGs(klines, period, count_type);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
        }
        
        throw EvaluatorException("FVG: Unknown field '" + call.field + "'. Supported: bullish, bearish, any, top, bottom, mid, size, filled, remaining, count");
    });

    // ========================================================================
    // PREMIUM/DISCOUNT ZONES - 溢价/折价区分析
    // ========================================================================
    registry.register_function("PREMIUM", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            if (call.field == "premium" || call.field == "discount" || call.field == "equilibrium") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 获取PERIOD参数（默认100）
        int period = 100;
        if (!call.arguments.empty()) {
            period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            if (period < 1 || period > 100) {
                throw EvaluatorException(
                    "[VALIDATION] PREMIUM period must be in range [1, 100], got: " + 
                    std::to_string(period)
                );
            }
        }
        
        // 1. 区域判断（返回布尔值）
        if (call.field == "premium") {
            // PREMIUM(5m).premium(100) 或 PREMIUM(5m).premium
            bool is_premium = PremiumDiscountAnalyzer::isInPremium(klines, period);
            return FunctionResult::fromBoolean(is_premium);
        }
        
        if (call.field == "discount") {
            // PREMIUM(5m).discount(100)
            bool is_discount = PremiumDiscountAnalyzer::isInDiscount(klines, period);
            return FunctionResult::fromBoolean(is_discount);
        }
        
        if (call.field == "equilibrium") {
            // PREMIUM(5m).equilibrium(100)
            bool is_equilibrium = PremiumDiscountAnalyzer::isInEquilibrium(klines, period);
            return FunctionResult::fromBoolean(is_equilibrium);
        }
        
        // 2. 区域类型数值（返回数值：1=溢价，0=均衡，-1=折价）
        if (call.field == "zone") {
            // PREMIUM(5m).zone(100)
            int zone_value = PremiumDiscountAnalyzer::getZoneValue(klines, period);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(zone_value)));
        }
        
        // 3. 百分比位置（返回数值：0-100）
        if (call.field == "pct") {
            // PREMIUM(5m).pct(100)
            double percentage = PremiumDiscountAnalyzer::getPercentage(klines, period);
            return FunctionResult::fromValue(Value::fromNumber(percentage));
        }
        
        // 4. 均衡价格水平（返回数值）
        if (call.field == "level") {
            // PREMIUM(5m).level(100)
            double level = PremiumDiscountAnalyzer::getEquilibriumLevel(klines, period);
            return FunctionResult::fromValue(Value::fromNumber(level));
        }
        
        // 5. 区域强度（返回数值：0-100）
        if (call.field == "strength") {
            // PREMIUM(5m).strength(100)
            double strength = PremiumDiscountAnalyzer::getStrength(klines, period);
            return FunctionResult::fromValue(Value::fromNumber(strength));
        }
        
        // 6. 范围高点
        if (call.field == "high") {
            // PREMIUM(5m).high(100)
            double high = PremiumDiscountAnalyzer::getRangeHigh(klines, period);
            return FunctionResult::fromValue(Value::fromNumber(high));
        }
        
        // 7. 范围低点
        if (call.field == "low") {
            // PREMIUM(5m).low(100)
            double low = PremiumDiscountAnalyzer::getRangeLow(klines, period);
            return FunctionResult::fromValue(Value::fromNumber(low));
        }
        
        throw EvaluatorException("PREMIUM: Unknown field '" + call.field + "'. Supported: premium, discount, equilibrium, zone, pct, level, strength, high, low");
    });

    // ========================================================================
    // ORDER BLOCK - 订单块检测
    // ========================================================================
    registry.register_function("ORDERBLOCK", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            if (call.field == "bullish" || call.field == "bearish" || call.field == "any") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 1. 形态检测（返回布尔值）
        if (call.field == "bullish") {
            // ORDERBLOCK(5m).bullish
            bool detected = OrderBlockAnalyzer::detectBullishOrderBlock(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        if (call.field == "bearish") {
            // ORDERBLOCK(5m).bearish
            bool detected = OrderBlockAnalyzer::detectBearishOrderBlock(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        if (call.field == "any") {
            // ORDERBLOCK(5m).any
            bool detected = OrderBlockAnalyzer::detectAnyOrderBlock(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        // 获取类型过滤器和回看参数
        std::string type_filter = "any";
        int lookback = 0;
        
        // 检查是否有subfield作为类型过滤器
        if (!call.subfield.empty()) {
            type_filter = call.subfield;  // bullish, bearish
        }
        
        // 检查是否有参数作为lookback
        if (!call.arguments.empty()) {
            lookback = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }
        
        // 2. 区域信息（返回数值）
        if (call.field == "top") {
            // ORDERBLOCK(5m).top(0) 或 ORDERBLOCK(5m).top.bullish(0)
            double top = OrderBlockAnalyzer::getOrderBlockTop(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(top));
        }
        
        if (call.field == "bottom") {
            // ORDERBLOCK(5m).bottom(0)
            double bottom = OrderBlockAnalyzer::getOrderBlockBottom(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(bottom));
        }
        
        if (call.field == "mid") {
            // ORDERBLOCK(5m).mid(0)
            double mid = OrderBlockAnalyzer::getOrderBlockMid(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(mid));
        }
        
        // 3. 强度（返回数值）
        if (call.field == "strength") {
            // ORDERBLOCK(5m).strength(0)
            int strength = OrderBlockAnalyzer::getOrderBlockStrength(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(strength)));
        }
        
        // 4. 测试状态（返回布尔值或数值）
        if (call.field == "tested") {
            // ORDERBLOCK(5m).tested(0)
            bool tested = OrderBlockAnalyzer::isOrderBlockTested(klines, type_filter, lookback);
            return FunctionResult::fromBoolean(tested);
        }
        
        if (call.field == "testcount") {
            // ORDERBLOCK(5m).testcount(0)
            int test_count = OrderBlockAnalyzer::getOrderBlockTestCount(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(test_count)));
        }
        
        // 5. 突破状态（返回布尔值）
        if (call.field == "broken") {
            // ORDERBLOCK(5m).broken(0)
            bool broken = OrderBlockAnalyzer::isOrderBlockBroken(klines, type_filter, lookback);
            return FunctionResult::fromBoolean(broken);
        }
        
        // 6. 距离（返回数值）
        if (call.field == "distance") {
            // ORDERBLOCK(5m).distance(0)
            double distance = OrderBlockAnalyzer::getDistanceToOrderBlock(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(distance));
        }
        
        // 7. 统计功能
        if (call.field == "count") {
            // ORDERBLOCK(5m).count(20) 或 ORDERBLOCK(5m).count.bullish(20)
            if (call.arguments.empty()) {
                throw EvaluatorException("ORDERBLOCK.count requires period argument: ORDERBLOCK(5m).count(20)");
            }
            
            int period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            int count = OrderBlockAnalyzer::countOrderBlocks(klines, period, type_filter);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
        }
        
        throw EvaluatorException("ORDERBLOCK: Unknown field '" + call.field + "'. Supported: bullish, bearish, any, top, bottom, mid, strength, tested, testcount, broken, distance, count");
    });

    // ========================================================================
    // SWING - 摆动高低点检测
    // ========================================================================
    registry.register_function("SWING", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            if (call.field == "high" || call.field == "low") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 默认参数
        int left_bars = 5;
        int right_bars = 5;
        int lookback = 0;
        
        // 解析参数：SWING(5m).price(5, 5, 0) - left_bars, right_bars, lookback
        if (call.arguments.size() >= 1) {
            left_bars = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }
        if (call.arguments.size() >= 2) {
            right_bars = static_cast<int>(call.arguments[1]->evaluate(ctx).toNumber());
        }
        if (call.arguments.size() >= 3) {
            lookback = static_cast<int>(call.arguments[2]->evaluate(ctx).toNumber());
        }
        
        // 获取类型过滤器
        std::string type_filter = "any";
        if (!call.subfield.empty() && call.subfield != "count" && call.subfield != "value") {
            type_filter = call.subfield;  // high, low（用于price等字段）
        }
        
        // 1. 存在性检测（返回布尔值或对象）
        if (call.field == "high") {
            // SWING(5m).high(5, 5) - 返回布尔值
            // SWING(5m).high().count - 返回count（subfield）
            // SWING(5m).high().value(0) - 返回value(index)（submethod）
            
            if (!call.subfield.empty()) {
                if (call.subfield == "count") {
                    // SWING(5m).high().count
                    int count = SwingPointAnalyzer::countSwingPoints(klines, "high", left_bars, right_bars, 100);
                    return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
                } else if (call.subfield == "value") {
                    // SWING(5m).high().value(index)
                    int index = 0;
                    if (!call.submethod_params.empty()) {
                        index = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                    }
                    double value = SwingPointAnalyzer::getSwingPrice(klines, "high", left_bars, right_bars, index);
                    return FunctionResult::fromValue(Value::fromNumber(value));
                }
            }
            
            // 默认返回布尔值
            bool has_high = SwingPointAnalyzer::hasSwingHigh(klines, left_bars, right_bars);
            return FunctionResult::fromBoolean(has_high);
        }
        
        if (call.field == "low") {
            // SWING(5m).low(5, 5) - 返回布尔值
            // SWING(5m).low().count - 返回count（subfield）
            // SWING(5m).low().value(0) - 返回value(index)（submethod）
            
            if (!call.subfield.empty()) {
                if (call.subfield == "count") {
                    // SWING(5m).low().count
                    int count = SwingPointAnalyzer::countSwingPoints(klines, "low", left_bars, right_bars, 100);
                    return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
                } else if (call.subfield == "value") {
                    // SWING(5m).low().value(index)
                    int index = 0;
                    if (!call.submethod_params.empty()) {
                        index = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                    }
                    double value = SwingPointAnalyzer::getSwingPrice(klines, "low", left_bars, right_bars, index);
                    return FunctionResult::fromValue(Value::fromNumber(value));
                }
            }
            
            // 默认返回布尔值
            bool has_low = SwingPointAnalyzer::hasSwingLow(klines, left_bars, right_bars);
            return FunctionResult::fromBoolean(has_low);
        }
        
        // 2. 价格（返回数值）
        if (call.field == "price") {
            // SWING(5m).price(5, 5, 0) 或 SWING(5m).price.high(5, 5, 0)
            double price = SwingPointAnalyzer::getSwingPrice(klines, type_filter, left_bars, right_bars, lookback);
            return FunctionResult::fromValue(Value::fromNumber(price));
        }
        
        // 3. 强度（返回数值）
        if (call.field == "strength") {
            // SWING(5m).strength(5, 5, 0)
            int strength = SwingPointAnalyzer::getSwingStrength(klines, type_filter, left_bars, right_bars, lookback);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(strength)));
        }
        
        // 4. 突破状态（返回布尔值）
        if (call.field == "broken") {
            // SWING(5m).broken(5, 5, 0)
            bool broken = SwingPointAnalyzer::isSwingBroken(klines, type_filter, left_bars, right_bars, lookback);
            return FunctionResult::fromBoolean(broken);
        }
        
        // 5. 统计功能
        if (call.field == "count") {
            // SWING(5m).count(5, 5) - 前两个参数是left_bars和right_bars，第三个可选参数是period
            int period = 100;
            if (call.arguments.size() >= 3) {
                period = static_cast<int>(call.arguments[2]->evaluate(ctx).toNumber());
            }
            
            int count = SwingPointAnalyzer::countSwingPoints(klines, type_filter, left_bars, right_bars, period);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
        }
        
        // 6. 距离（返回数值）
        if (call.field == "distance") {
            // SWING(5m).distance(5, 5)
            double distance = SwingPointAnalyzer::getDistanceToSwing(klines, type_filter, left_bars, right_bars);
            return FunctionResult::fromValue(Value::fromNumber(distance));
        }
        
        throw EvaluatorException("SWING: Unknown field '" + call.field + "'. Supported: high, low, price, strength, broken, count, distance");
    });

    // ========================================================================
    // BOS - Break of Structure (结构突破)
    // ========================================================================
    registry.register_function("BOS", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            if (call.field == "bullish" || call.field == "bearish") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 默认参数
        int swing_left_bars = 5;
        int swing_right_bars = 5;
        int lookback = 0;
        
        // 解析参数：BOS(5m).price(5, 5, 0) - swing_left, swing_right, lookback
        if (call.arguments.size() >= 1) {
            swing_left_bars = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }
        if (call.arguments.size() >= 2) {
            swing_right_bars = static_cast<int>(call.arguments[1]->evaluate(ctx).toNumber());
        }
        if (call.arguments.size() >= 3) {
            lookback = static_cast<int>(call.arguments[2]->evaluate(ctx).toNumber());
        }
        
        // 1. 形态检测（返回布尔值）
        if (call.field == "bullish") {
            // BOS(5m).bullish(5, 5)
            bool detected = MarketStructureAnalyzer::detectBullishBOS(klines, swing_left_bars, swing_right_bars);
            return FunctionResult::fromBoolean(detected);
        }
        
        if (call.field == "bearish") {
            // BOS(5m).bearish(5, 5)
            bool detected = MarketStructureAnalyzer::detectBearishBOS(klines, swing_left_bars, swing_right_bars);
            return FunctionResult::fromBoolean(detected);
        }
        
        // 获取类型过滤器
        std::string type_filter = "bos";
        if (!call.subfield.empty()) {
            if (call.subfield == "bullish" || call.subfield == "bearish") {
                type_filter = call.subfield;
            }
        }
        
        // 2. 突破价格（返回数值）
        if (call.field == "price") {
            // BOS(5m).price(5, 5, 0) 或 BOS(5m).price.bullish(5, 5, 0)
            double price = MarketStructureAnalyzer::getStructurePrice(klines, type_filter, swing_left_bars, swing_right_bars, lookback);
            return FunctionResult::fromValue(Value::fromNumber(price));
        }
        
        // 3. 被突破的摆动点价格（返回数值）
        if (call.field == "swing") {
            // BOS(5m).swing(5, 5, 0)
            double swing_price = MarketStructureAnalyzer::getSwingPrice(klines, type_filter, swing_left_bars, swing_right_bars, lookback);
            return FunctionResult::fromValue(Value::fromNumber(swing_price));
        }
        
        // 4. 统计功能
        if (call.field == "count") {
            // BOS(5m).count(5, 5) - 前两个参数是swing_left和swing_right，第三个可选参数是period
            int period = 100;
            if (call.arguments.size() >= 3) {
                period = static_cast<int>(call.arguments[2]->evaluate(ctx).toNumber());
            }
            
            int count = MarketStructureAnalyzer::countStructureEvents(klines, type_filter, swing_left_bars, swing_right_bars, period);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
        }
        
        throw EvaluatorException("BOS: Unknown field '" + call.field + "'. Supported: bullish, bearish, price, swing, count");
    });

    // ========================================================================
    // CHOCH - Change of Character (特征改变)
    // ========================================================================
    registry.register_function("CHOCH", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            if (call.field == "bullish" || call.field == "bearish") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 默认参数
        int swing_left_bars = 5;
        int swing_right_bars = 5;
        int lookback = 0;
        
        // 解析参数：CHOCH(5m).price(5, 5, 0) - swing_left, swing_right, lookback
        if (call.arguments.size() >= 1) {
            swing_left_bars = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }
        if (call.arguments.size() >= 2) {
            swing_right_bars = static_cast<int>(call.arguments[1]->evaluate(ctx).toNumber());
        }
        if (call.arguments.size() >= 3) {
            lookback = static_cast<int>(call.arguments[2]->evaluate(ctx).toNumber());
        }
        
        // 1. 形态检测（返回布尔值）
        if (call.field == "bullish") {
            // CHOCH(5m).bullish(5, 5)
            bool detected = MarketStructureAnalyzer::detectBullishCHoCH(klines, swing_left_bars, swing_right_bars);
            return FunctionResult::fromBoolean(detected);
        }
        
        if (call.field == "bearish") {
            // CHOCH(5m).bearish(5, 5)
            bool detected = MarketStructureAnalyzer::detectBearishCHoCH(klines, swing_left_bars, swing_right_bars);
            return FunctionResult::fromBoolean(detected);
        }
        
        // 获取类型过滤器
        std::string type_filter = "choch";
        if (!call.subfield.empty()) {
            if (call.subfield == "bullish" || call.subfield == "bearish") {
                type_filter = call.subfield;
            }
        }
        
        // 2. 改变价格（返回数值）
        if (call.field == "price") {
            // CHOCH(5m).price(5, 5, 0) 或 CHOCH(5m).price.bullish(5, 5, 0)
            double price = MarketStructureAnalyzer::getStructurePrice(klines, type_filter, swing_left_bars, swing_right_bars, lookback);
            return FunctionResult::fromValue(Value::fromNumber(price));
        }
        
        // 3. 被突破的摆动点价格（返回数值）
        if (call.field == "swing") {
            // CHOCH(5m).swing(5, 5, 0)
            double swing_price = MarketStructureAnalyzer::getSwingPrice(klines, type_filter, swing_left_bars, swing_right_bars, lookback);
            return FunctionResult::fromValue(Value::fromNumber(swing_price));
        }
        
        // 4. 统计功能
        if (call.field == "count") {
            // CHOCH(5m).count(5, 5) - 前两个参数是swing_left和swing_right，第三个可选参数是period
            int period = 100;
            if (call.arguments.size() >= 3) {
                period = static_cast<int>(call.arguments[2]->evaluate(ctx).toNumber());
            }
            
            int count = MarketStructureAnalyzer::countStructureEvents(klines, type_filter, swing_left_bars, swing_right_bars, period);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
        }
        
        // 5. 当前趋势（返回数值）
        if (call.field == "trend") {
            // CHOCH(5m).trend(5, 5)
            int trend = MarketStructureAnalyzer::getCurrentTrend(klines, swing_left_bars, swing_right_bars);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(trend)));
        }
        
        throw EvaluatorException("CHOCH: Unknown field '" + call.field + "'. Supported: bullish, bearish, price, swing, count, trend");
    });

    // ========================================================================
    // LIQUIDITY - 流动性区域检测
    // ========================================================================
    registry.register_function("LIQUIDITY", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            if (call.field == "buyside" || call.field == "sellside") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 默认参数
        double price_tolerance = 0.5;
        int lookback = 0;
        
        // 解析参数：LIQUIDITY(5m).price(0.5, 0) - price_tolerance, lookback
        if (call.arguments.size() >= 1) {
            price_tolerance = call.arguments[0]->evaluate(ctx).toNumber();
        }
        if (call.arguments.size() >= 2) {
            lookback = static_cast<int>(call.arguments[1]->evaluate(ctx).toNumber());
        }
        
        // 获取类型过滤器
        std::string type_filter = "any";
        if (!call.subfield.empty()) {
            type_filter = call.subfield;  // buyside, sellside
        }
        
        // 1. 存在性检测（返回布尔值）
        if (call.field == "buyside") {
            // LIQUIDITY(5m).buyside(0.5)
            bool has_buyside = LiquidityAnalyzer::hasBuysideLiquidity(klines, price_tolerance);
            return FunctionResult::fromBoolean(has_buyside);
        }
        
        if (call.field == "sellside") {
            // LIQUIDITY(5m).sellside(0.5)
            bool has_sellside = LiquidityAnalyzer::hasSellsideLiquidity(klines, price_tolerance);
            return FunctionResult::fromBoolean(has_sellside);
        }
        
        // 2. 价格（返回数值）
        if (call.field == "price") {
            // LIQUIDITY(5m).price(0.5, 0) 或 LIQUIDITY(5m).price.buyside(0.5, 0)
            double price = LiquidityAnalyzer::getLiquidityPrice(klines, type_filter, price_tolerance, lookback);
            return FunctionResult::fromValue(Value::fromNumber(price));
        }
        
        // 3. 强度（返回数值）
        if (call.field == "strength") {
            // LIQUIDITY(5m).strength(0.5, 0)
            int strength = LiquidityAnalyzer::getLiquidityStrength(klines, type_filter, price_tolerance, lookback);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(strength)));
        }
        
        // 4. 扫荡状态（返回布尔值）
        if (call.field == "swept") {
            // LIQUIDITY(5m).swept(0.5, 0)
            bool swept = LiquidityAnalyzer::isLiquiditySwept(klines, type_filter, price_tolerance, lookback);
            return FunctionResult::fromBoolean(swept);
        }
        
        // 5. 统计功能
        if (call.field == "count") {
            // LIQUIDITY(5m).count(0.5) - 第一个参数是price_tolerance，第二个可选参数是period
            int period = 100;
            if (call.arguments.size() >= 2) {
                period = static_cast<int>(call.arguments[1]->evaluate(ctx).toNumber());
            }
            
            int count = LiquidityAnalyzer::countLiquidityZones(klines, type_filter, price_tolerance, period);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
        }
        
        // 6. 距离（返回数值）
        if (call.field == "distance") {
            // LIQUIDITY(5m).distance(0.5)
            double distance = LiquidityAnalyzer::getDistanceToLiquidity(klines, type_filter, price_tolerance);
            return FunctionResult::fromValue(Value::fromNumber(distance));
        }
        
        throw EvaluatorException("LIQUIDITY: Unknown field '" + call.field + "'. Supported: buyside, sellside, price, strength, swept, count, distance");
    });

    // ========================================================================
    // BREAKER - Breaker Block (突破块)
    // ========================================================================
    registry.register_function("BREAKER", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        require_timeframe(call);
        
        if (!ctx.hasKlines(call.timeframe)) {
            if (call.field == "bullish" || call.field == "bearish") {
                return FunctionResult::fromBoolean(false);
            }
            return FunctionResult::fromValue(Value::fromNumber(0.0));
        }
        
        const auto& klines = ctx.getKlines(call.timeframe);
        
        // 默认参数
        int lookback = 0;
        if (!call.arguments.empty()) {
            lookback = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        }
        
        // 获取类型过滤器
        std::string type_filter = "any";
        if (!call.subfield.empty()) {
            type_filter = call.subfield;  // bullish, bearish
        }
        
        // 1. 形态检测（返回布尔值）
        if (call.field == "bullish") {
            // BREAKER(5m).bullish
            bool detected = BreakerBlockAnalyzer::detectBullishBreakerBlock(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        if (call.field == "bearish") {
            // BREAKER(5m).bearish
            bool detected = BreakerBlockAnalyzer::detectBearishBreakerBlock(klines);
            return FunctionResult::fromBoolean(detected);
        }
        
        // 2. 区域信息（返回数值）
        if (call.field == "top") {
            // BREAKER(5m).top(0) 或 BREAKER(5m).top.bullish(0)
            double top = BreakerBlockAnalyzer::getBreakerBlockTop(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(top));
        }
        
        if (call.field == "bottom") {
            // BREAKER(5m).bottom(0)
            double bottom = BreakerBlockAnalyzer::getBreakerBlockBottom(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(bottom));
        }
        
        if (call.field == "mid") {
            // BREAKER(5m).mid(0)
            double mid = BreakerBlockAnalyzer::getBreakerBlockMid(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(mid));
        }
        
        // 3. 强度（返回数值）
        if (call.field == "strength") {
            // BREAKER(5m).strength(0)
            int strength = BreakerBlockAnalyzer::getBreakerBlockStrength(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(strength)));
        }
        
        // 4. 测试状态（返回布尔值或数值）
        if (call.field == "tested") {
            // BREAKER(5m).tested(0)
            bool tested = BreakerBlockAnalyzer::isBreakerBlockTested(klines, type_filter, lookback);
            return FunctionResult::fromBoolean(tested);
        }
        
        if (call.field == "testcount") {
            // BREAKER(5m).testcount(0)
            int test_count = BreakerBlockAnalyzer::getBreakerBlockTestCount(klines, type_filter, lookback);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(test_count)));
        }
        
        // 5. 失效状态（返回布尔值）
        if (call.field == "broken") {
            // BREAKER(5m).broken(0)
            bool broken = BreakerBlockAnalyzer::isBreakerBlockBroken(klines, type_filter, lookback);
            return FunctionResult::fromBoolean(broken);
        }
        
        // 6. 统计功能
        if (call.field == "count") {
            // BREAKER(5m).count - 可选参数是period
            int period = 100;
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            int count = BreakerBlockAnalyzer::countBreakerBlocks(klines, type_filter, period);
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(count)));
        }
        
        throw EvaluatorException("BREAKER: Unknown field '" + call.field + "'. Supported: bullish, bearish, top, bottom, mid, strength, tested, testcount, broken, count");
    });

    // ========================================================================
    // 序列条件函数 - CONSECUTIVE, COUNT
    // ========================================================================
    register_sequence_functions(registry);

}

} // namespace prophet::functions

