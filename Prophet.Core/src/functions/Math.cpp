/*
 * ============================================================================
 * 文件名：Math.cpp
 * 功能说明：数学函数实现
 * 
 * 这个文件提供各种数学计算函数，用于DSL表达式中的数值运算
 * 
 * 支持的函数：
 * - 单参数函数：ABS（绝对值）、ROUND（四舍五入）、SQRT（平方根）、
 *              EXP（指数）、LOG（对数）、SIN/COS/TAN（三角函数）
 * - 双参数函数：POW（幂运算）
 * - 多参数函数：MAX（最大值）、MIN（最小值）
 * 
 * 使用示例：
 *   ABS(-5) -> 5
 *   POW(2, 3) -> 8
 *   MAX(1, 5, 3) -> 5
 * ============================================================================
 */

#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/core/context.hpp"

#include <cmath>

namespace prophet::functions {

void register_math_functions(FunctionRegistry& registry) {
    auto register_unary = [&](const std::string& name, auto func) {
        registry.register_function(name, [name, func](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
            if (call.arguments.size() != 1) {
                throw EvaluatorException(name + "() requires exactly 1 argument");
            }
            double value = call.arguments[0]->evaluate(ctx).toNumber();
            return FunctionResult::fromNumber(func(value));
        });
    };

    auto register_binary = [&](const std::string& name, auto func) {
        registry.register_function(name, [name, func](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
            if (call.arguments.size() != 2) {
                throw EvaluatorException(name + "() requires exactly 2 arguments");
            }
            double a = call.arguments[0]->evaluate(ctx).toNumber();
            double b = call.arguments[1]->evaluate(ctx).toNumber();
            return FunctionResult::fromNumber(func(a, b));
        });
    };

    register_unary("ABS", [](double v) { return std::abs(v); });
    // 注意：MAX/MIN作为数学函数与Frame函数冲突，暂不注册
    // 如需使用，可注册为 MATH_MAX / MATH_MIN
    // register_binary("MAX", [](double a, double b) { return std::max(a, b); });
    // register_binary("MIN", [](double a, double b) { return std::min(a, b); });
    registry.register_function("ROUND", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        if (call.arguments.size() != 2) {
            throw EvaluatorException("ROUND() requires exactly 2 arguments");
        }
        double value = call.arguments[0]->evaluate(ctx).toNumber();
        int precision = static_cast<int>(call.arguments[1]->evaluate(ctx).toNumber());
        double multiplier = std::pow(10.0, precision);
        return FunctionResult::fromNumber(std::round(value * multiplier) / multiplier);
    });
    register_unary("SQRT", [](double v) {
        if (v < 0) {
            throw EvaluatorException("SQRT() requires non-negative argument");
        }
        return std::sqrt(v);
    });
    register_binary("POW", [](double base, double exp) {
        double result = std::pow(base, exp);
        if (std::isinf(result) || std::isnan(result)) {
            throw EvaluatorException("POW() result overflow or invalid");
        }
        return result;
    });
    register_unary("SIN", [](double v) { return std::sin(v); });
    register_unary("COS", [](double v) { return std::cos(v); });
    register_unary("TAN", [](double v) { return std::tan(v); });
    register_unary("ASIN", [](double v) {
        if (v < -1.0 || v > 1.0) {
            throw EvaluatorException("ASIN() requires argument in [-1, 1]");
        }
        return std::asin(v);
    });
    register_unary("ACOS", [](double v) {
        if (v < -1.0 || v > 1.0) {
            throw EvaluatorException("ACOS() requires argument in [-1, 1]");
        }
        return std::acos(v);
    });
    register_unary("ATAN", [](double v) { return std::atan(v); });
    register_unary("SINH", [](double v) { return std::sinh(v); });
    register_unary("COSH", [](double v) { return std::cosh(v); });
    register_unary("TANH", [](double v) { return std::tanh(v); });
    register_unary("EXP", [](double v) {
        double result = std::exp(v);
        if (std::isinf(result)) {
            throw EvaluatorException("EXP() result overflow");
        }
        return result;
    });
    register_unary("LN", [](double v) {
        if (v <= 0) {
            throw EvaluatorException("LN() requires positive argument");
        }
        return std::log(v);
    });
    register_unary("LOG10", [](double v) {
        if (v <= 0) {
            throw EvaluatorException("LOG10() requires positive argument");
        }
        return std::log10(v);
    });
    register_unary("CEIL", [](double v) { return std::ceil(v); });
    register_unary("FLOOR", [](double v) { return std::floor(v); });
}

} // namespace prophet::functions

