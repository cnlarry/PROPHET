/*
 * ============================================================================
 * 文件名：Frame.cpp
 * 功能说明：信号框架函数实现
 * 
 * 这个文件实现了交易策略的核心信号组合函数
 * 这些函数用于组合多个条件，生成交易信号
 * 
 * 支持的信号函数：
 * - ALL：所有条件都满足才触发（逻辑与）
 * - ANY：任一条件满足就触发（逻辑或）
 * - NONE：所有条件都不满足才触发（逻辑非）
 * - MIN(n)：至少n个条件满足才触发
 * - COUNT(n)：恰好n个条件满足才触发
 * - MAX(n)：最多n个条件满足才触发
 * - WEIGHTED(threshold)：加权条件，总权重达到阈值才触发
 * 
 * 使用示例：
 *   ALL{MACD > 0, RSI < 70} = BUY  // MACD和RSI都满足才买入
 *   ANY{MA5 > MA10, MACD > 0} = BUY  // 任一满足就买入
 * ============================================================================
 */

#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/functions/Common.hpp"
#include "prophet/dsl/ast.hpp"
#include "prophet/core/context.hpp"

#include <iostream>
#include "prophet/common/types.hpp"

#include <algorithm>

namespace prophet::functions {

void register_frame_functions(FunctionRegistry& registry) {
    // ALL
    registry.register_function("ALL", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        int total = static_cast<int>(call.conditions.size());
        if (total == 0) {
            return FunctionResult::fromNumber(0.0);
        }
        int satisfied = 0;
        for (const auto& cond : call.conditions) {
            try {
                if (cond->evaluate(ctx).toBool()) {
                    satisfied++;
                }
            } catch (const std::exception&) {
                // 如果某个条件评估失败（例如数据不足），视为不满足
                // 记录警告但不中断整个评估过程
                continue;
            }
        }
        return FunctionResult::fromNumber(satisfied == total ? 1.0 : 0.0);
    });

    // ANY
    registry.register_function("ANY", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        int total = static_cast<int>(call.conditions.size());
        if (total == 0) {
            return FunctionResult::fromNumber(0.0);
        }
        int satisfied = 0;
        for (const auto& cond : call.conditions) {
            try {
                if (cond->evaluate(ctx).toBool()) {
                    satisfied++;
                }
            } catch (const std::exception&) {
                // 如果某个条件评估失败（例如数据不足），视为不满足
                continue;
            }
        }
        return FunctionResult::fromNumber(static_cast<double>(satisfied) / static_cast<double>(total));
    });

    // NONE
    registry.register_function("NONE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        int total = static_cast<int>(call.conditions.size());
        if (total == 0) {
            return FunctionResult::fromNumber(1.0);
        }
        for (const auto& cond : call.conditions) {
            if (cond->evaluate(ctx).toBool()) {
                return FunctionResult::fromNumber(0.0);
            }
        }
        return FunctionResult::fromNumber(1.0);
    });

    // MIN
    registry.register_function("MIN", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        int total = static_cast<int>(call.conditions.size());
        if (total == 0) {
            return FunctionResult::fromNumber(0.0);
        }
        if (!call.has_numeric_arg) {
            throw EvaluatorException("MIN() requires numeric parameter");
        }
        int min_count = static_cast<int>(call.numeric_arg);
        int satisfied = 0;
        for (const auto& cond : call.conditions) {
            try {
                if (cond->evaluate(ctx).toBool()) {
                    satisfied++;
                }
            } catch (const std::exception&) {
                // 数据不足时视为条件不满足
                continue;
            }
        }
        if (satisfied < min_count) {
            return FunctionResult::fromNumber(0.0);
        }
        return FunctionResult::fromNumber(static_cast<double>(satisfied) / static_cast<double>(total));
    });

    // COUNT
    registry.register_function("COUNT", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        int total = static_cast<int>(call.conditions.size());
        if (total == 0) {
            return FunctionResult::fromNumber(0.0);
        }
        if (!call.has_numeric_arg) {
            throw EvaluatorException("COUNT() requires numeric parameter");
        }
        int target = static_cast<int>(call.numeric_arg);
        int satisfied = 0;
        for (const auto& cond : call.conditions) {
            if (cond->evaluate(ctx).toBool()) {
                satisfied++;
            }
        }
        if (satisfied == target) {
            return FunctionResult::fromNumber(1.0);
        }
        int deviation = std::abs(satisfied - target);
        return FunctionResult::fromNumber(std::max(0.0, 1.0 - (static_cast<double>(deviation) / static_cast<double>(total))));
    });

    // MAX
    registry.register_function("MAX", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        int total = static_cast<int>(call.conditions.size());
        if (total == 0) {
            return FunctionResult::fromNumber(1.0);
        }
        if (!call.has_numeric_arg) {
            throw EvaluatorException("MAX() requires numeric parameter");
        }
        int max_count = static_cast<int>(call.numeric_arg);
        int satisfied = 0;
        for (const auto& cond : call.conditions) {
            if (cond->evaluate(ctx).toBool()) {
                satisfied++;
            }
        }
        if (satisfied > max_count) {
            return FunctionResult::fromNumber(0.0);
        }
        return FunctionResult::fromNumber(static_cast<double>(max_count + 1 - satisfied) / static_cast<double>(max_count + 1));
    });

    // VOTE - 投票机制（支持数量和百分比）
    registry.register_function("VOTE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        int total = static_cast<int>(call.conditions.size());
        if (total == 0) {
            return FunctionResult::fromNumber(0.0);
        }
        if (!call.has_numeric_arg) {
            throw EvaluatorException("VOTE() requires numeric parameter");
        }
        
        double threshold = call.numeric_arg;
        
        // 计算满足的条件数量
        int satisfied = 0;
        for (const auto& cond : call.conditions) {
            if (cond->evaluate(ctx).toBool()) {
                satisfied++;
            }
        }
        
        // 判断是否达到阈值
        bool pass = false;
        if (threshold > 1.0) {
            // 整数模式：至少n个
            pass = (satisfied >= static_cast<int>(threshold));
        } else {
            // 百分比模式：至少x%
            double required_ratio = threshold;
            double actual_ratio = static_cast<double>(satisfied) / static_cast<double>(total);
            pass = (actual_ratio >= required_ratio);
        }
        
        if (!pass) {
            return FunctionResult::fromNumber(0.0);
        }
        
        // 返回满意度：满足的条件数 / 总条件数
        return FunctionResult::fromNumber(static_cast<double>(satisfied) / static_cast<double>(total));
    });

    registry.register_function("WEIGHTED", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        if (call.weight_conditions.empty()) {
            return FunctionResult::fromNumber(0.0);
        }

        auto& registry = FunctionRegistry::instance();
        auto weight_handler = registry.resolve("WEIGHT");
        if (!weight_handler) {
            throw EvaluatorException("WEIGHT handler not registered");
        }

        FunctionCall weight_call = call;
        weight_call.category = FunctionCategory::Weight;

        FunctionResult weight_result = weight_handler(weight_call, ctx);
        if (!weight_result.has_value) {
            return FunctionResult::fromNumber(0.0);
        }

        double total_score = weight_result.value.toNumber();
        double max_score = 0.0;
        for (const auto& wc : call.weight_conditions) {
            max_score += wc.weight;
        }

        if (total_score < call.threshold) {
            return FunctionResult::fromNumber(0.0);
        }

        double confidence = (max_score > 0.0) ? (total_score / max_score) : 0.0;
        return FunctionResult::fromNumber(confidence);
    });
}

} // namespace prophet::functions

