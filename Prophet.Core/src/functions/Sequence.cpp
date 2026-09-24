/**
 * @file Sequence.cpp
 * @brief 序列条件函数实现 - CONSECUTIVE, COUNT
 * @date 2025-10-31
 * 
 * 重构说明：
 * 将原本在DSL层（parser/evaluator）实现的CONSECUTIVE和COUNT函数
 * 迁移到functions/系统中，实现架构统一。
 */

#include "prophet/functions/Sequence.hpp"
#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/core/context.hpp"
#include <algorithm>
#include <sstream>

namespace prophet::functions {

// ============================================================================
// 辅助数据结构
// ============================================================================

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
    
    for (const auto& kline : klines) {
        data.open.push_back(kline.open);
        data.high.push_back(kline.high);
        data.low.push_back(kline.low);
        data.close.push_back(kline.close);
        data.volume.push_back(kline.volume);
    }
    
    return data;
}

SeriesData get_series(const FunctionCall& call, prophet::dsl::Context& ctx) {
    if (!ctx.hasKlines(call.timeframe)) {
        throw std::runtime_error("K-line data unavailable for timeframe: " + call.timeframe);
    }
    const auto& klines = ctx.getKlines(call.timeframe);
    return extract_series(klines);
}

// ============================================================================
// 序列条件评估器实现
// ============================================================================

IndicatorResult SequenceConditionEvaluator::evaluateInternal(
    const std::vector<double>& values,
    int periods,
    Mode mode,
    Condition condition,
    const std::vector<double>& params)
{
    IndicatorResult result;

    // 1. 检查数据充足性
    if (values.size() < static_cast<size_t>(periods)) {
        if (mode == Mode::CONSECUTIVE) {
            result.set("value", Value::fromBoolean(false));
        } else { // COUNT
            result.set("value", Value::fromNumber(0.0));
        }
        return result;
    }

    // 2. 根据模式评估
    bool consecutive_result = false;
    int count_result = 0;

    if (mode == Mode::CONSECUTIVE) {
        consecutive_result = evaluateConsecutive(values, periods, condition, params);
        result.set("value", Value::fromBoolean(consecutive_result));
    } else { // COUNT
        count_result = evaluateCount(values, periods, condition, params);
        result.set("value", Value::fromNumber(static_cast<double>(count_result)));
    }

    return result;
}

bool SequenceConditionEvaluator::evaluateConsecutive(
    const std::vector<double>& values,
    int periods,
    Condition condition,
    const std::vector<double>& params)
{
    size_t size = values.size();
    
    // 检查最近periods期是否连续满足条件
    for (int i = 0; i < periods; i++) {
        size_t idx = size - 1 - i;  // 从最新到最旧
        
        bool satisfied = evaluateSinglePeriod(
            values, idx, condition, params
        );
        
        if (!satisfied) {
            return false;  // 任何一期不满足，返回false
        }
    }
    
    return true;  // 所有期都满足
}

int SequenceConditionEvaluator::evaluateCount(
    const std::vector<double>& values,
    int periods,
    Condition condition,
    const std::vector<double>& params)
{
    size_t size = values.size();
    int count = 0;
    
    // 统计最近periods期中满足条件的次数
    for (int i = 0; i < periods; i++) {
        size_t idx = size - 1 - i;  // 从最新到最旧
        
        bool satisfied = evaluateSinglePeriod(
            values, idx, condition, params
        );
        
        if (satisfied) {
            count++;
        }
    }
    
    return count;
}

bool SequenceConditionEvaluator::evaluateSinglePeriod(
    const std::vector<double>& values,
    size_t idx,
    Condition condition,
    const std::vector<double>& params)
{
    double current_value = values[idx];
    double prev_value = (idx > 0) ? values[idx - 1] : 0.0;
    
    switch (condition) {
        case Condition::RISING:
            // 严格上涨（需要前一期数据）
            return (idx > 0) && (current_value > prev_value);
            
        case Condition::FALLING:
            // 严格下跌（需要前一期数据）
            return (idx > 0) && (current_value < prev_value);
            
        case Condition::INCREASING:
            // 增加或相等（需要前一期数据）
            return (idx > 0) && (current_value >= prev_value);
            
        case Condition::DECREASING:
            // 减少或相等（需要前一期数据）
            return (idx > 0) && (current_value <= prev_value);
            
        case Condition::ABOVE: {
            // 高于阈值
            if (params.empty()) {
                throw std::runtime_error("ABOVE condition requires threshold parameter");
            }
            double threshold = params[0];
            return current_value > threshold;
        }
            
        case Condition::BELOW: {
            // 低于阈值
            if (params.empty()) {
                throw std::runtime_error("BELOW condition requires threshold parameter");
            }
            double threshold = params[0];
            return current_value < threshold;
        }
            
        case Condition::BETWEEN: {
            // 在区间内
            if (params.size() < 2) {
                throw std::runtime_error("BETWEEN condition requires min and max parameters");
            }
            double min_val = params[0];
            double max_val = params[1];
            return (current_value >= min_val) && (current_value <= max_val);
        }
            
        case Condition::CROSSES_ABOVE: {
            // 向上穿越：前一期低于阈值，当前期高于阈值
            if (params.empty()) {
                throw std::runtime_error("CROSSES_ABOVE condition requires threshold parameter");
            }
            if (idx == 0) {
                return false;  // 第一期无法判断穿越
            }
            double threshold = params[0];
            return (prev_value <= threshold) && (current_value > threshold);
        }
            
        case Condition::CROSSES_BELOW: {
            // 向下穿越：前一期高于阈值，当前期低于阈值
            if (params.empty()) {
                throw std::runtime_error("CROSSES_BELOW condition requires threshold parameter");
            }
            if (idx == 0) {
                return false;  // 第一期无法判断穿越
            }
            double threshold = params[0];
            return (prev_value >= threshold) && (current_value < threshold);
        }
            
        default:
            throw std::runtime_error("Unknown condition type");
    }
}

SequenceConditionEvaluator::Condition 
SequenceConditionEvaluator::parseCondition(const std::string& name)
{
    if (name == "rising") return Condition::RISING;
    if (name == "falling") return Condition::FALLING;
    if (name == "increasing") return Condition::INCREASING;
    if (name == "decreasing") return Condition::DECREASING;
    if (name == "above") return Condition::ABOVE;
    if (name == "below") return Condition::BELOW;
    if (name == "between") return Condition::BETWEEN;
    if (name == "crosses_above") return Condition::CROSSES_ABOVE;
    if (name == "crosses_below") return Condition::CROSSES_BELOW;
    
    throw std::runtime_error(
        "Unknown sequence condition '" + name + "'. " +
        "Supported: rising, falling, increasing, decreasing, above, below, between, crosses_above, crosses_below"
    );
}

// ============================================================================
// 函数注册
// ============================================================================

void register_sequence_functions(FunctionRegistry& registry) {
    
    // ========================================================================
    // CONSECUTIVE / CONSEC - 连续条件函数
    // ========================================================================
    
    registry.register_function("CONSECUTIVE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        // 语法：CONSECUTIVE(timeframe).field(periods).condition([params...])
        
        // 验证参数
        if (call.timeframe.empty()) {
            throw std::runtime_error("CONSECUTIVE requires timeframe");
        }
        if (call.field.empty()) {
            throw std::runtime_error("CONSECUTIVE requires field name");
        }
        if (call.arguments.empty()) {
            throw std::runtime_error("CONSECUTIVE requires periods argument");
        }
        if (call.subfield.empty()) {
            throw std::runtime_error("CONSECUTIVE requires condition (e.g., rising, above, etc.)");
        }
        
        // 解析周期数
        int periods = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        if (periods < 1 || periods > 100) {
            throw std::runtime_error("CONSECUTIVE period must be in range [1, 100], got: " + std::to_string(periods));
        }
        
        // 解析条件类型
        auto condition = SequenceConditionEvaluator::parseCondition(call.subfield);
        
        // 解析条件参数（可选）
        std::vector<double> condition_params;
        for (size_t i = 1; i < call.arguments.size(); i++) {
            condition_params.push_back(call.arguments[i]->evaluate(ctx).toNumber());
        }
        
        // 获取K线数据并提取字段
        SeriesData data = get_series(call, ctx);
        
        // 提取指定字段的数据
        std::vector<double> values;
        if (call.field == "close") {
            values = data.close;
        } else if (call.field == "open") {
            values = data.open;
        } else if (call.field == "high") {
            values = data.high;
        } else if (call.field == "low") {
            values = data.low;
        } else if (call.field == "volume") {
            values = data.volume;
        } else {
            throw std::runtime_error("Invalid field for CONSECUTIVE: " + call.field);
        }
        
        // 评估条件
        IndicatorResult result = SequenceConditionEvaluator::evaluateInternal(
            values,
            periods,
            SequenceConditionEvaluator::Mode::CONSECUTIVE,
            condition,
            condition_params
        );
        
        return FunctionResult::fromValue(result.get("value"));
    });
    
    // CONSEC 是 CONSECUTIVE 的别名
    registry.register_function("CONSEC", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        // 直接调用CONSECUTIVE的实现
        FunctionCall consecutive_call = call;
        consecutive_call.name = "CONSECUTIVE";
        auto handler = FunctionRegistry::instance().resolve("CONSECUTIVE");
        return handler(consecutive_call, ctx);
    });
    
    // ========================================================================
    // COUNT - 条件计数函数（数据函数版本，非信号函数）
    // ========================================================================
    
    registry.register_function("COUNT_DATA", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        // 语法：COUNT(timeframe).field(periods).condition([params...])
        // 注意：这个COUNT是数据函数，与信号函数COUNT(n){...}不同
        
        // 验证参数（与CONSECUTIVE相同）
        if (call.timeframe.empty()) {
            throw std::runtime_error("COUNT requires timeframe");
        }
        if (call.field.empty()) {
            throw std::runtime_error("COUNT requires field name");
        }
        if (call.arguments.empty()) {
            throw std::runtime_error("COUNT requires periods argument");
        }
        if (call.subfield.empty()) {
            throw std::runtime_error("COUNT requires condition (e.g., rising, above, etc.)");
        }
        
        // 解析周期数
        int periods = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
        if (periods < 1 || periods > 100) {
            throw std::runtime_error("COUNT period must be in range [1, 100], got: " + std::to_string(periods));
        }
        
        // 解析条件类型
        auto condition = SequenceConditionEvaluator::parseCondition(call.subfield);
        
        // 解析条件参数
        std::vector<double> condition_params;
        for (size_t i = 1; i < call.arguments.size(); i++) {
            condition_params.push_back(call.arguments[i]->evaluate(ctx).toNumber());
        }
        
        // 获取K线数据并提取字段
        SeriesData data = get_series(call, ctx);
        
        // 提取指定字段的数据
        std::vector<double> values;
        if (call.field == "close") {
            values = data.close;
        } else if (call.field == "open") {
            values = data.open;
        } else if (call.field == "high") {
            values = data.high;
        } else if (call.field == "low") {
            values = data.low;
        } else if (call.field == "volume") {
            values = data.volume;
        } else {
            throw std::runtime_error("Invalid field for COUNT: " + call.field);
        }
        
        // 评估条件（使用COUNT模式）
        IndicatorResult result = SequenceConditionEvaluator::evaluateInternal(
            values,
            periods,
            SequenceConditionEvaluator::Mode::COUNT,
            condition,
            condition_params
        );
        
        return FunctionResult::fromValue(result.get("value"));
    });
}

} // namespace prophet::functions

