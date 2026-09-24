/*
 * ============================================================================
 * 文件名：Logic.cpp
 * 功能说明：逻辑函数实现
 * 
 * 这个文件实现了逻辑运算函数
 * 
 * 支持的逻辑函数：
 * - NOT：逻辑非，将真变假，假变真
 * 
 * 使用示例：
 *   NOT(MACD > 0)  // MACD不大于0
 * ============================================================================
 */

#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/dsl/ast.hpp"
#include "prophet/core/context.hpp"

namespace prophet::functions {

void register_logic_functions(FunctionRegistry& registry) {
    registry.register_function("NOT", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        if (call.arguments.size() != 1) {
            throw EvaluatorException("NOT() requires exactly 1 argument");
        }
        Value value = call.arguments[0]->evaluate(ctx);
        return FunctionResult::fromBoolean(!value.toBool());
    });
}

} // namespace prophet::functions

