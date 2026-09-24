/*
 * ============================================================================
 * 文件名：Weight.cpp
 * 功能说明：加权函数实现
 * 
 * 这个文件实现了WEIGHTED信号函数的权重计算逻辑
 * 
 * WEIGHTED函数说明：
 * - 为每组条件分配权重
 * - 满足的条件组权重累加
 * - 总权重达到阈值时触发信号
 * 
 * 使用示例：
 *   WEIGHTED(0.7) {
 *     WEIGHT(MACD > 0, RSI < 70) = 0.6,
 *     WEIGHT(MA5 > MA10) = 0.4
 *   } = BUY
 *   // 当总权重>=0.7时触发买入
 * ============================================================================
 */

#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/dsl/ast.hpp"
#include "prophet/dsl/custom_function_ast.hpp"
#include "prophet/dsl/evaluation_context.hpp"
#include "prophet/core/context.hpp"
#include "prophet/common/types.hpp"
#include <stdexcept>
#include <cmath>
#include <algorithm>

namespace prophet::functions {

// 🆕 v4.0: 辅助函数：递归评估AST节点，支持自定义函数调用
// 如果节点包含自定义函数调用，使用EvaluationContext评估
static prophet::Value evaluateWithCustomFunctionSupport(
    const prophet::dsl::ASTNodePtr& node,
    prophet::dsl::Context& ctx,
    prophet::dsl::FunctionRegistry* func_registry
) {
    // 首先尝试用Context评估（适用于大多数情况）
    try {
        return node->evaluate(ctx);
    } catch (const std::runtime_error& e) {
        // 如果抛出异常，检查是否是自定义函数调用错误
        std::string error_msg = e.what();
        if (error_msg.find("can only be called with EvaluationContext") != std::string::npos) {
            // 需要EvaluationContext，创建它并重新评估
            if (func_registry == nullptr) {
                throw std::runtime_error("Custom function call requires FunctionRegistry, but it is not set in Context");
            }
            
            prophet::dsl::EvaluationContext eval_ctx(ctx, *func_registry);
            
            // 检查节点类型，使用相应的评估方法
            if (auto func_call = std::dynamic_pointer_cast<prophet::dsl::FunctionCallNode>(node)) {
                // 直接的函数调用节点
                return func_call->evaluateWithContext(eval_ctx);
            } else if (auto binary_op = std::dynamic_pointer_cast<prophet::dsl::BinaryOpNode>(node)) {
                // 二元运算符节点：递归评估左右操作数
                auto left_val = evaluateWithCustomFunctionSupport(binary_op->getLeft(), ctx, func_registry);
                auto right_val = evaluateWithCustomFunctionSupport(binary_op->getRight(), ctx, func_registry);
                
                // 执行二元运算
                switch (binary_op->getOp()) {
                    case prophet::dsl::BinaryOpNode::OpType::GT:
                        return prophet::Value::fromBoolean(left_val.toNumber() > right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::LT:
                        return prophet::Value::fromBoolean(left_val.toNumber() < right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::GE:
                        return prophet::Value::fromBoolean(left_val.toNumber() >= right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::LE:
                        return prophet::Value::fromBoolean(left_val.toNumber() <= right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::EQ: {
                        // 相等性比较：支持字符串、数字、布尔值
                        if (left_val.type == prophet::ValueType::STRING && right_val.type == prophet::ValueType::STRING) {
                            return prophet::Value::fromBoolean(left_val.string_value == right_val.string_value);
                        }
                        if (left_val.type == prophet::ValueType::BOOLEAN && right_val.type == prophet::ValueType::BOOLEAN) {
                            return prophet::Value::fromBoolean(left_val.boolean_value == right_val.boolean_value);
                        }
                        if (left_val.type == prophet::ValueType::NUMBER && right_val.type == prophet::ValueType::NUMBER) {
                            // 数字比较（考虑浮点数精度问题）
                            return prophet::Value::fromBoolean(std::abs(left_val.number_value - right_val.number_value) < 1e-10);
                        }
                        // 类型不匹配，返回 false
                        return prophet::Value::fromBoolean(false);
                    }
                    case prophet::dsl::BinaryOpNode::OpType::NE: {
                        // 不等性比较：支持字符串、数字、布尔值
                        if (left_val.type == prophet::ValueType::STRING && right_val.type == prophet::ValueType::STRING) {
                            return prophet::Value::fromBoolean(left_val.string_value != right_val.string_value);
                        }
                        if (left_val.type == prophet::ValueType::BOOLEAN && right_val.type == prophet::ValueType::BOOLEAN) {
                            return prophet::Value::fromBoolean(left_val.boolean_value != right_val.boolean_value);
                        }
                        if (left_val.type == prophet::ValueType::NUMBER && right_val.type == prophet::ValueType::NUMBER) {
                            // 数字比较（考虑浮点数精度问题）
                            return prophet::Value::fromBoolean(std::abs(left_val.number_value - right_val.number_value) >= 1e-10);
                        }
                        // 类型不匹配，返回 true
                        return prophet::Value::fromBoolean(true);
                    }
                    case prophet::dsl::BinaryOpNode::OpType::AND:
                        return prophet::Value::fromBoolean(left_val.toBool() && right_val.toBool());
                    case prophet::dsl::BinaryOpNode::OpType::OR:
                        return prophet::Value::fromBoolean(left_val.toBool() || right_val.toBool());
                    case prophet::dsl::BinaryOpNode::OpType::ADD:
                        return prophet::Value::fromNumber(left_val.toNumber() + right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::SUB:
                        return prophet::Value::fromNumber(left_val.toNumber() - right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::MUL:
                        return prophet::Value::fromNumber(left_val.toNumber() * right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::DIV:
                        if (right_val.toNumber() == 0.0) {
                            throw std::runtime_error("Division by zero");
                        }
                        return prophet::Value::fromNumber(left_val.toNumber() / right_val.toNumber());
                    case prophet::dsl::BinaryOpNode::OpType::MOD:
                        return prophet::Value::fromNumber(std::fmod(left_val.toNumber(), right_val.toNumber()));
                    default:
                        throw std::runtime_error("Unsupported binary operator in custom function evaluation");
                }
            } else if (auto unary_op = std::dynamic_pointer_cast<prophet::dsl::UnaryOpNode>(node)) {
                // 一元运算符节点：递归评估操作数
                auto operand_val = evaluateWithCustomFunctionSupport(unary_op->getOperand(), ctx, func_registry);
                if (unary_op->getOp() == prophet::dsl::UnaryOpNode::OpType::NEG) {
                    return prophet::Value::fromNumber(-operand_val.toNumber());
                }
                return operand_val;
            } else {
                // 其他类型的节点，尝试用EvaluationContext评估
                // 如果节点内部有自定义函数调用，会在递归调用中处理
                throw std::runtime_error("Unsupported node type in custom function evaluation: " + node->toString());
            }
        } else {
            // 其他异常，重新抛出
            throw;
        }
    }
}

void register_weight_functions(FunctionRegistry& registry) {
    registry.register_function("WEIGHT", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        double total_weight = 0.0;

        // 🆕 v4.0: 获取函数注册表（用于自定义函数调用）
        auto* func_registry = ctx.getFunctionRegistry();
        
        for (const auto& wc : call.weight_conditions) {
            bool all_satisfied = true;
            for (const auto& condition : wc.conditions) {
                try {
                    // 🆕 v4.0: 使用支持自定义函数的评估函数
                    if (!evaluateWithCustomFunctionSupport(condition, ctx, func_registry).toBool()) {
                        all_satisfied = false;
                        break;
                    }
                } catch (const std::exception&) {
                    // 评估失败，条件不满足
                    all_satisfied = false;
                    break;
                }
            }

            if (all_satisfied) {
                total_weight += wc.weight;
            }
        }

        return FunctionResult::fromNumber(total_weight);
    });
}

} // namespace prophet::functions

