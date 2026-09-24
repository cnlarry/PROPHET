/*
 * ============================================================================
 * 文件名：evaluator.cpp
 * 功能说明：表达式求值器（Evaluator）实现
 * 
 * 什么是表达式求值？
 *   把抽象语法树（AST）"执行"，计算出具体结果的过程
 *   就像把"数学公式"代入具体数字，计算出答案
 * 
 * 举个例子：
 *   AST结构：BinaryOpNode { left: 2, op: +, right: 3 }
 *   求值结果：5
 * 
 *   复杂例子：
 *   AST：BinaryOpNode { 
 *     left: IndicatorRefNode("MACD", "5m", "value"),
 *     op: >,
 *     right: NumberNode(0)
 *   }
 *   求值：从Context获取MACD值(假设为0.5)，比较0.5 > 0，结果为true
 * 
 * 主要功能：
 * 1. 评估各种AST节点（数字、字符串、运算符、函数调用等）
 * 2. 从Context获取指标数据、K线数据
 * 3. 执行数学运算、逻辑运算
 * 4. 调用各种函数（数据函数、数学函数、信号函数）
 * 5. 评估交易规则，生成交易信号
 * 
 * 为什么需要求值器？
 * - Parser只是构建了语法结构，不会执行
 * - 需要根据实际数据计算出结果
 * - 这是从"静态规则"到"动态结果"的关键步骤
 * 
 * 简单理解：
 * - Lexer识别"单词"
 * - Parser组织"语法"
 * - Evaluator"执行计算"
 * - 就像从"公式"到"答案"的过程
 * ============================================================================
 */

#include "prophet/dsl/ast.hpp"
#include "prophet/dsl/custom_function_ast.hpp"
#include "prophet/dsl/evaluation_context.hpp"
#include "prophet/core/context.hpp"
#include "prophet/functions/Pattern.hpp"
#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/functions/Common.hpp"
#include <cmath>
#include <sstream>
#include <iostream>

namespace prophet::dsl {

namespace {

// ============================================================================
// 辅助函数：确保函数已注册
// 
// 这个函数使用单例模式，确保所有函数（数学函数、数据函数等）
// 只注册一次，避免重复注册
// ============================================================================
void ensure_functions_registered() {
    static bool initialized = false;  // 静态变量，程序运行期间只初始化一次
    if (!initialized) {
        functions::register_all_functions(functions::FunctionRegistry::instance());
        initialized = true;
    }
}

} // namespace

// ============================================================================
// 字面量节点实现
// 
// 字面量节点是最简单的节点，直接返回其存储的值
// 包括：数字（123、45.67）、字符串（"BULLISH"）、布尔值（true/false）
// ============================================================================

// 数字节点：直接返回数字值
Value NumberNode::evaluate(const Context& /* ctx */) const {
    return Value::fromNumber(value_);
}

std::string NumberNode::toString() const {
    return std::to_string(value_);
}

// 字符串节点：直接返回字符串值
Value StringNode::evaluate(const Context& /* ctx */) const {
    return Value::fromString(value_);
}

std::string StringNode::toString() const {
    return "\"" + value_ + "\"";
}

// 布尔节点：直接返回布尔值
Value BooleanNode::evaluate(const Context& /* ctx */) const {
    return Value::fromBoolean(value_);
}

std::string BooleanNode::toString() const {
    return value_ ? "true" : "false";
}

// ============================================================================
// 引用节点实现
// 
// 引用节点用于获取存储在Context中的数据
// 包括：指标数据（MACD、RSI）、环境变量（@CURRENT_PRICE）、参数等
// ============================================================================

// ============================================================================
// 函数：IndicatorRefNode::evaluate
// 功能：获取指标的值或调用数据函数
// 
// 这个函数会：
// 1. 检查是否为数据函数（如KLINE、PRICE、HT等）
// 2. 如果是数据函数，调用相应的函数处理器
// 3. 如果不是，自动计算指标（如果尚未计算）
// 4. 从Context获取指标的字段值
// ============================================================================
Value IndicatorRefNode::evaluate(const Context& ctx) const {
    ensure_functions_registered();

    // 多时间框架应该在parseComparison中已经展开了，这里只处理单时间框架
    // 但为了健壮性，如果遇到多时间框架，抛出错误
    if (hasMultipleTimeframes()) {
        throw EvaluatorException(
            "Multi-timeframe indicator reference should have been expanded during parsing. "
            "Use in comparison expressions only."
        );
    }

    const std::string& timeframe = getSingleTimeframe();

    // 🆕 v4.0: 如果指标有参数（indicator_params_），应该作为指标引用处理，而不是数据函数
    // 例如：$(5m).ATR(14).value 中的 14 是指标参数，不是函数参数
    // 数据函数调用（如 ATR(5m).close(14).value）中的 14 才是函数参数（在 field_params_ 中）
    if (hasIndicatorParams()) {
        // 有指标参数，跳过数据函数处理，直接作为指标引用处理
        // 这会在下面的自动指标计算部分处理
    } else {
        // 没有指标参数，尝试作为数据函数处理
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Data;
    call.name = indicator_;
    call.timeframe = timeframe;
    call.field = field_;
    call.subfield = subfield_;
    call.arguments = field_params_;
    call.submethod_params = submethod_params_;  // 传递子方法参数

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (handler) {
        Context& mutable_ctx = const_cast<Context&>(ctx);
        auto result = handler(call, mutable_ctx);
        if (!result.has_value) {
            throw EvaluatorException("Data function " + indicator_ + " returned no value");
        }
        return result.value;
        }
    }

    // ========================================================================
    // 自动指标计算（核心改进）
    // 在访问指标字段之前，先确保指标已经计算
    // 如果指标不存在，会自动调用Calculator计算
    // ========================================================================
    Context& mutable_ctx = const_cast<Context&>(ctx);
    try {
        // 检查是否有DSL参数（新语法）
        // Use new syntax: $(timeframe).INDICATOR(params).field
        if (hasIndicatorParams()) {
            mutable_ctx.getOrCalculateIndicator(indicator_, timeframe, indicator_params_);
        } else {
            // No DSL parameters: use JSON parameters or default values
            mutable_ctx.getOrCalculateIndicator(indicator_, timeframe);
        }
    } catch (const std::exception& e) {
        // 如果自动计算失败，抛出更详细的错误信息
        throw EvaluatorException(
            "Failed to auto-calculate indicator " + indicator_ + "(" + timeframe + "): " + e.what()
        );
    }

    // 现在指标一定存在了，可以安全地获取字段值
    // 处理字段参数：可能是偏移量（单个数字）或旧式的参数化字段名
    int offset = 0;  // 默认偏移量为0（最新值）
    std::string actual_field = field_;
    
    if (!field_params_.empty()) {
        // 检查第一个参数是否是数字（偏移量）
        // 如果是单个数字参数，且范围在[-100, 0]，则视为偏移量
        if (field_params_.size() == 1) {
            Value first_param = field_params_[0]->evaluate(ctx);
            if (first_param.isNumber()) {
                double offset_value = first_param.toNumber();
                // 检查是否是合理的偏移量（整数且在范围内）
                if (offset_value >= -100 && offset_value <= 0 && 
                    offset_value == static_cast<int>(offset_value)) {
                    offset = static_cast<int>(offset_value);
                    // 这是偏移量，使用原始字段名
                } else {
                    // 不是偏移量，可能是旧式的参数化字段名
                    actual_field = field_ + "_" + std::to_string(static_cast<int>(offset_value));
                }
            } else {
                // 非数字参数，使用旧式逻辑
                std::string param_field_name = field_;
                for (const auto& param : field_params_) {
                    Value param_value = param->evaluate(ctx);
                    param_field_name += "_" + std::to_string(static_cast<int>(param_value.toNumber()));
                }
                actual_field = param_field_name;
            }
        } else {
            // 多个参数，使用旧式逻辑（参数化字段名）
            std::string param_field_name = field_;
            for (const auto& param : field_params_) {
                Value param_value = param->evaluate(ctx);
                param_field_name += "_" + std::to_string(static_cast<int>(param_value.toNumber()));
            }
            actual_field = param_field_name;
        }
    }

    // 尝试获取字段值（支持偏移量）
    // 注意：必须把 indicator_params_ 一并传给 getIndicatorField，
    // 否则会命中默认参数缓存，导致 RSI(7) 返回 RSI(14) 的结果
    try {
        return ctx.getIndicatorField(indicator_, timeframe, actual_field, offset, indicator_params_);
    } catch (...) {
        // 如果字段不存在，尝试使用 "value" 字段
        if (actual_field != "value") {
            try {
                return ctx.getIndicatorField(indicator_, timeframe, "value", offset, indicator_params_);
            } catch (...) {
                throw EvaluatorException("Indicator field not found: " + indicator_ + "(" + timeframe + ")." + actual_field);
            }
        }
        throw;
    }
}

std::string IndicatorRefNode::toString() const {
    std::string result = indicator_;
    
    // 时间框架：单个用()，多个用[]
    if (timeframes_.size() == 1) {
        result += "(" + timeframes_[0] + ")";
    } else {
        result += "[";
        for (size_t i = 0; i < timeframes_.size(); i++) {
            if (i > 0) result += ", ";
            result += timeframes_[i];
        }
        result += "]";
    }
    
    result += "." + field_;
    
    // 添加子字段
    if (!subfield_.empty()) {
        result += "." + subfield_;
    }
    
    // 添加参数
    if (!field_params_.empty()) {
        result += "(";
        for (size_t i = 0; i < field_params_.size(); i++) {
            if (i > 0) result += ", ";
            result += field_params_[i]->toString();
        }
        result += ")";
    }
    
    return result;
}

Value ParamRefNode::evaluate(const Context& ctx) const {
    return ctx.getParameter(indicator_, timeframe_, param_name_);
}

std::string ParamRefNode::toString() const {
    return "$" + indicator_ + "(" + timeframe_ + ")." + param_name_;
}

// ============================================================================
// 运算符节点实现
// 
// 运算符节点执行各种运算操作
// 包括：算术运算（+、-、*、/）、比较运算（>、<、==）、逻辑运算（AND、OR）
// ============================================================================

// ============================================================================
// 函数：BinaryOpNode::evaluate
// 功能：执行二元运算（两个操作数的运算）
// 
// 支持的运算：
//   算术：+、-、*、/、%
//   比较：==、!=、>、<、>=、<=
//   逻辑：AND、OR
// 
// 工作流程：
//   1. 先计算左右两个操作数的值
//   2. 根据运算符类型执行相应的运算
//   3. 返回运算结果
// ============================================================================
Value BinaryOpNode::evaluate(const Context& ctx) const {
    // 第1步：计算左右操作数的值
    Value left_val = left_->evaluate(ctx);
    Value right_val = right_->evaluate(ctx);

    switch (op_) {
        // === 算术运算 ===
        case OpType::ADD:
        case OpType::SUB:
        case OpType::MUL:
        case OpType::DIV:
        case OpType::MOD: {
            // 类型检查：算术运算要求两个操作数都是数字
            if (left_val.type != ValueType::NUMBER || right_val.type != ValueType::NUMBER) {
                throw EvaluatorException("Arithmetic operation requires numeric operands");
            }
            
            double left_num = left_val.number_value;
            double right_num = right_val.number_value;
            
            switch (op_) {
                case OpType::ADD:
                    return Value::fromNumber(left_num + right_num);
                case OpType::SUB:
                    return Value::fromNumber(left_num - right_num);
                case OpType::MUL:
                    return Value::fromNumber(left_num * right_num);
                case OpType::DIV:
                    if (std::abs(right_num) < 1e-10) {
                        throw EvaluatorException("Division by zero");
                    }
                    return Value::fromNumber(left_num / right_num);
                case OpType::MOD:
                    if (std::abs(right_num) < 1e-10) {
                        throw EvaluatorException("Modulo by zero");
                    }
                    return Value::fromNumber(std::fmod(left_num, right_num));
                default:
                    break;
            }
        }

        // === 比较运算 ===
        // 支持字符串、数字、布尔值的比较
        case OpType::EQ:  // 等于（==）
            // 字符串比较
            if (left_val.type == ValueType::STRING && right_val.type == ValueType::STRING) {
                return Value::fromBoolean(left_val.string_value == right_val.string_value);
            }
            // 布尔值比较
            if (left_val.type == ValueType::BOOLEAN && right_val.type == ValueType::BOOLEAN) {
                return Value::fromBoolean(left_val.boolean_value == right_val.boolean_value);
            }
            // 数字比较（考虑浮点数精度问题）
            if (left_val.type == ValueType::NUMBER && right_val.type == ValueType::NUMBER) {
                return Value::fromBoolean(std::abs(left_val.number_value - right_val.number_value) < 1e-10);
            }
            // 类型不匹配，返回 false
            return Value::fromBoolean(false);

        case OpType::NE:
            if (left_val.type == ValueType::STRING && right_val.type == ValueType::STRING) {
                return Value::fromBoolean(left_val.string_value != right_val.string_value);
            }
            if (left_val.type == ValueType::BOOLEAN && right_val.type == ValueType::BOOLEAN) {
                return Value::fromBoolean(left_val.boolean_value != right_val.boolean_value);
            }
            if (left_val.type == ValueType::NUMBER && right_val.type == ValueType::NUMBER) {
                return Value::fromBoolean(std::abs(left_val.number_value - right_val.number_value) >= 1e-10);
            }
            // 类型不匹配，返回 true
            return Value::fromBoolean(true);

        case OpType::GT:
        case OpType::LT:
        case OpType::GE:
        case OpType::LE:
            // 大小比较要求数值类型
            if (left_val.type != ValueType::NUMBER || right_val.type != ValueType::NUMBER) {
                throw EvaluatorException("Comparison operation requires numeric operands");
            }
            switch (op_) {
                case OpType::GT:
                    return Value::fromBoolean(left_val.number_value > right_val.number_value);
                case OpType::LT:
                    return Value::fromBoolean(left_val.number_value < right_val.number_value);
                case OpType::GE:
                    return Value::fromBoolean(left_val.number_value >= right_val.number_value);
                case OpType::LE:
                    return Value::fromBoolean(left_val.number_value <= right_val.number_value);
                default:
                    break;
            }

        // === 逻辑运算符 ===
        case OpType::AND:  // 逻辑与：两个都为真才为真
            return Value::fromBoolean(left_val.toBool() && right_val.toBool());
        case OpType::OR:   // 逻辑或：至少一个为真就为真
            return Value::fromBoolean(left_val.toBool() || right_val.toBool());

        default:
            throw EvaluatorException("Unknown binary operator");
    }
}

std::string BinaryOpNode::toString() const {
    static const char* op_strings[] = {
        "+", "-", "*", "/", "%",
        "=", "!=", ">", "<", ">=", "<=",
        "AND", "OR"
    };
    return "(" + left_->toString() + " " +
           op_strings[static_cast<int>(op_)] + " " +
           right_->toString() + ")";
}

Value UnaryOpNode::evaluate(const Context& ctx) const {
    Value operand_val = operand_->evaluate(ctx);

    switch (op_) {
        case OpType::NEG:
            return Value::fromNumber(-operand_val.toNumber());
        case OpType::POS:
            return Value::fromNumber(operand_val.toNumber());
        default:
            throw EvaluatorException("Unknown unary operator");
    }
}

std::string UnaryOpNode::toString() const {
    return (op_ == OpType::NEG ? "-" : "+") + operand_->toString();
}

// ============================================================================
// 范围运算符节点实现
// ============================================================================

Value BetweenNode::evaluate(const Context& ctx) const {
    double val = value_->evaluate(ctx).toNumber();
    double min_val = min_->evaluate(ctx).toNumber();
    double max_val = max_->evaluate(ctx).toNumber();

    bool result = (val >= min_val && val <= max_val);
    if (negate_) {
        result = !result;
    }

    return Value::fromBoolean(result);
}

std::string BetweenNode::toString() const {
    std::string op = negate_ ? "NOT BETWEEN" : "BETWEEN";
    return value_->toString() + " " + op + "(" +
           min_->toString() + ", " + max_->toString() + ")";
}

Value InNode::evaluate(const Context& ctx) const {
    Value val = value_->evaluate(ctx);

    bool found = false;
    for (const auto& v : values_) {
        Value list_val = v->evaluate(ctx);

        // 字符串比较
        if (val.type == ValueType::STRING && list_val.type == ValueType::STRING) {
            if (val.string_value == list_val.string_value) {
                found = true;
                break;
            }
        }
        // 数值比较
        else if (std::abs(val.toNumber() - list_val.toNumber()) < 1e-10) {
            found = true;
            break;
        }
    }

    bool result = negate_ ? !found : found;
    return Value::fromBoolean(result);
}

std::string InNode::toString() const {
    std::string op = negate_ ? "NOT IN" : "IN";
    std::ostringstream oss;
    oss << value_->toString() << " " << op << "(";
    for (size_t i = 0; i < values_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << values_[i]->toString();
    }
    oss << ")";
    return oss.str();
}

// ============================================================================
// 逻辑运算符节点实现
// ============================================================================

Value NotNode::evaluate(const Context& ctx) const {
    Value cond_val = condition_->evaluate(ctx);
    return Value::fromBoolean(!cond_val.toBool());
}

std::string NotNode::toString() const {
    return "NOT(" + condition_->toString() + ")";
}

// ============================================================================
// 数学函数节点实现
// ============================================================================

Value MathFunctionNode::evaluate(const Context& ctx) const {
    ensure_functions_registered();
    using namespace functions;
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Math;
    call.name = name_;
    call.arguments = args_;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Math function not registered: " + call.name);
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    functions::FunctionResult result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Math function returned no value: " + call.name);
    }
    return result.value;
}

std::string MathFunctionNode::toString() const {
    std::ostringstream oss;
    oss << name_ << "(";
    for (size_t i = 0; i < args_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << args_[i]->toString();
    }
    oss << ")";
    return oss.str();
}

// ============================================================================
// 数据函数节点实现
// ============================================================================

Value PatternNode::evaluate(const Context& ctx) const {
    ensure_functions_registered();

    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Data;
    call.name = "PATTERN";
    call.timeframe = timeframe_;
    call.field = pattern_name_;
    if (penetration_param_) {
        call.arguments.push_back(penetration_param_);
    }

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Data function not registered: PATTERN");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        return Value::fromBoolean(false);
    }
    return result.value;
}

std::string PatternNode::toString() const {
    std::ostringstream oss;
    oss << "PATTERN(" << timeframe_ << ")." << pattern_name_ << "(";
    if (penetration_param_) {
        oss << penetration_param_->toString();
    } else {
        oss << "0.3";  // 默认值
    }
    oss << ")";
    return oss.str();
}

// ============================================================================
// 信号函数节点实现
// ============================================================================

// 辅助函数：从条件节点提取指标快照信息
namespace {
    IndicatorSnapshot extractIndicatorSnapshot(const ASTNodePtr& cond, const Context& ctx, bool satisfied) {
        IndicatorSnapshot snapshot;
        snapshot.condition_id = cond->toString();  // 使用条件字符串作为ID
        snapshot.label = cond->toString();         // 默认使用toString作为label
        snapshot.status = satisfied ? "hit" : "miss";
        snapshot.comparison = cond->toString();
        
        // 尝试从条件中提取指标引用信息
        // 这里简化处理，只处理常见的比较节点
        if (auto* binop = dynamic_cast<BinaryOpNode*>(cond.get())) {
            // 尝试从左右操作数中提取指标信息
            auto extractFromNode = [&](const ASTNodePtr& node) {
                if (auto* indref = dynamic_cast<IndicatorRefNode*>(node.get())) {
                    snapshot.indicator = indref->getIndicator();
                    snapshot.timeframe = indref->getSingleTimeframe();
                    snapshot.offset = 0;  // 默认偏移量
                    
                    // 尝试获取指标结果
                    try {
                        auto indicator_result = ctx.getIndicator(snapshot.indicator, snapshot.timeframe);
                        
                        // 获取所有字段值
                        for (const auto& [fname, series] : indicator_result.field_series) {
                            if (!series.empty()) {
                                snapshot.results[fname] = series.back();  // 最新值
                            }
                        }
                        
                        // 获取OHLCV快照（如果有K线数据）
                        if (ctx.hasKlines(snapshot.timeframe)) {
                            const auto& klines = ctx.getKlines(snapshot.timeframe);
                            if (!klines.empty()) {
                                const auto& latest = klines.back();
                                snapshot.ohlcv["open"] = Value::fromNumber(latest.open);
                                snapshot.ohlcv["high"] = Value::fromNumber(latest.high);
                                snapshot.ohlcv["low"] = Value::fromNumber(latest.low);
                                snapshot.ohlcv["close"] = Value::fromNumber(latest.close);
                                snapshot.ohlcv["volume"] = Value::fromNumber(latest.volume);
                            }
                        }
                    } catch (...) {
                        // 忽略错误，继续处理
                    }
                }
            };
            
            // 从左右操作数提取
            extractFromNode(binop->getLeft());
            if (snapshot.indicator.empty()) {
                extractFromNode(binop->getRight());
            }
        }
        
        return snapshot;
    }
}

int SignalFunctionNode::countSatisfiedConditions(const Context& ctx) const {
    int count = 0;
    for (const auto& cond : conditions_) {
        Value result = cond->evaluate(ctx);
        if (result.toBool()) {
            count++;
        }
    }
    return count;
}

// 收集所有条件的快照（命中+未命中）
std::vector<IndicatorSnapshot> SignalFunctionNode::collectIndicatorSnapshots(const Context& ctx) const {
    std::vector<IndicatorSnapshot> snapshots;
    
    for (const auto& cond : conditions_) {
        try {
            Value result = cond->evaluate(ctx);
            bool satisfied = result.toBool();
            
            // 提取快照
            auto snapshot = extractIndicatorSnapshot(cond, ctx, satisfied);
            if (!snapshot.indicator.empty() || !snapshot.comparison.empty()) {
                snapshots.push_back(std::move(snapshot));
            }
        } catch (...) {
            // 忽略单个条件的错误，继续处理其他条件
        }
    }
    
    return snapshots;
}

double AllNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "ALL";
    call.conditions = conditions_;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: ALL");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function ALL returned no value");
    }
    return result.value.toNumber();
}

std::string AllNode::toString() const {
    std::ostringstream oss;
    oss << "ALL{";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << conditions_[i]->toString();
    }
    oss << "}";
    return oss.str();
}

double AnyNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "ANY";
    call.conditions = conditions_;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: ANY");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function ANY returned no value");
    }
    return result.value.toNumber();
}

std::string AnyNode::toString() const {
    std::ostringstream oss;
    oss << "ANY{";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << conditions_[i]->toString();
    }
    oss << "}";
    return oss.str();
}

double NoneNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "NONE";
    call.conditions = conditions_;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: NONE");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function NONE returned no value");
    }
    return result.value.toNumber();
}

std::string NoneNode::toString() const {
    std::ostringstream oss;
    oss << "NONE{";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << conditions_[i]->toString();
    }
    oss << "}";
    return oss.str();
}

double MinNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "MIN";
    call.conditions = conditions_;
    call.numeric_arg = static_cast<double>(min_count_);
    call.has_numeric_arg = true;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: MIN");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function MIN returned no value");
    }
    return result.value.toNumber();
}

std::string MinNode::toString() const {
    std::ostringstream oss;
    oss << "MIN(" << min_count_ << "){";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << conditions_[i]->toString();
    }
    oss << "}";
    return oss.str();
}

double CountNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "COUNT";
    call.conditions = conditions_;
    call.numeric_arg = static_cast<double>(target_count_);
    call.has_numeric_arg = true;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: COUNT");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function COUNT returned no value");
    }
    return result.value.toNumber();
}

std::string CountNode::toString() const {
    std::ostringstream oss;
    oss << "COUNT(" << target_count_ << "){";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << conditions_[i]->toString();
    }
    oss << "}";
    return oss.str();
}

double MaxNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "MAX";
    call.conditions = conditions_;
    call.numeric_arg = static_cast<double>(max_count_);
    call.has_numeric_arg = true;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: MAX");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function MAX returned no value");
    }
    return result.value.toNumber();
}

std::string MaxNode::toString() const {
    std::ostringstream oss;
    oss << "MAX(" << max_count_ << "){";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << conditions_[i]->toString();
    }
    oss << "}";
    return oss.str();
}

// ============================================================================
// VoteNode - VOTE(n){} 信号函数
// 功能：投票机制，支持数量模式和百分比模式
// ============================================================================

double VoteNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "VOTE";
    call.conditions = conditions_;
    call.numeric_arg = threshold_;
    call.has_numeric_arg = true;

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: VOTE");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function VOTE returned no value");
    }
    return result.value.toNumber();
}

std::string VoteNode::toString() const {
    std::ostringstream oss;
    oss << "VOTE(" << threshold_ << "){";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << conditions_[i]->toString();
    }
    oss << "}";
    return oss.str();
}

double WeightedNode::evaluateSignal(const Context& ctx) const {
    ensure_functions_registered();
    functions::FunctionCall call;
    call.category = functions::FunctionCategory::Frame;
    call.name = "WEIGHTED";
    call.threshold = threshold_;
    call.weight_conditions.reserve(conditions_.size());
    for (const auto& wc : conditions_) {
        functions::WeightedConditionCall call_wc;
        call_wc.weight = wc.weight;
        call_wc.conditions = wc.conditions;
        call.weight_conditions.push_back(std::move(call_wc));
    }

    auto handler = functions::FunctionRegistry::instance().resolve(call.name);
    if (!handler) {
        throw EvaluatorException("Frame function not registered: WEIGHTED");
    }

    Context& mutable_ctx = const_cast<Context&>(ctx);
    auto result = handler(call, mutable_ctx);
    if (!result.has_value) {
        throw EvaluatorException("Frame function WEIGHTED returned no value");
    }
    return result.value.toNumber();
}

std::string WeightedNode::toString() const {
    std::ostringstream oss;
    oss << "WEIGHTED(" << threshold_ << "){";
    for (size_t i = 0; i < conditions_.size(); i++) {
        if (i > 0) oss << ", ";
        oss << "WEIGHT(";

        // 输出多个条件（逗号分隔）
        for (size_t j = 0; j < conditions_[i].conditions.size(); j++) {
            if (j > 0) oss << ", ";
            oss << conditions_[i].conditions[j]->toString();
        }

        oss << ")=" << conditions_[i].weight;
    }
    oss << "}";
    return oss.str();
}

// ============================================================================
// COUNT和CONSECUTIVE已重构为functions/系统实现
// 不再需要专门的evaluate实现
// ============================================================================

// ============================================================================
// 赋值节点实现（v3.0.2）
// ============================================================================

Value AssignmentNode::evaluate(const Context& /* ctx */) const {
    // 赋值节点不应在表达式中被求值
    throw EvaluatorException("Assignment cannot be used in expressions");
}

std::string AssignmentNode::toString() const {
    return "$(" + timeframe_ + ")." + indicator_ + "." + param_name_ + " = " + value_expr_->toString();
}

void AssignmentNode::executeAssignment(Context& ctx) const {
    // 计算右侧表达式的值
    Value value = value_expr_->evaluate(ctx);
    
    // 设置参数值
    // 注意：Context 需要是非 const 的
    const_cast<Context&>(ctx).setParameter(indicator_, timeframe_, param_name_, value);
}

void AssignmentStatement::execute(Context& ctx) const {
    assignment_->executeAssignment(ctx);
}

std::string AssignmentStatement::toString() const {
    return assignment_->toString();
}

// ============================================================================
// 规则节点实现
// ============================================================================

// ============================================================================
// 函数：RuleNode::evaluateRule
// 功能：评估交易规则，生成交易信号
// 
// 这是交易策略的核心！
// 
// 工作流程：
//   1. 评估信号函数（ALL、ANY等），得到置信度（0.0-1.0）
//   2. 如果置信度 <= 0，返回HOLD信号（不交易）
//   3. 如果置信度 > 0，返回对应的交易动作（BUY/SELL）
// 
// 返回值：
//   Signal对象，包含：
//     - action：交易动作（BUY/SELL/HOLD）
//     - confidence：置信度（0.0-1.0）
//     - reason：触发原因
// ============================================================================
Signal RuleNode::evaluateRule(const Context& ctx) const {
    double confidence = 0.0;
    std::vector<IndicatorSnapshot> snapshots;

    // 尝试将信号函数转换为具体的节点类型并评估
    if (auto sig_func = std::dynamic_pointer_cast<SignalFunctionNode>(signal_func_)) {
        confidence = sig_func->evaluateSignal(ctx);
        // 收集指标快照
        snapshots = sig_func->collectIndicatorSnapshots(ctx);
    }
    // 尝试转换为加权节点
    else if (auto weighted = std::dynamic_pointer_cast<WeightedNode>(signal_func_)) {
        confidence = weighted->evaluateSignal(ctx);
        // TODO: WeightedNode 的快照收集可以后续添加
    }
    else {
        throw EvaluatorException("Invalid signal function type");
    }

    // 置信度 <= 0 表示条件未满足，返回HOLD信号
    if (confidence <= 0.0) {
        Signal hold_signal("HOLD", 0.0, "Conditions not met");
        hold_signal.indicator_snapshots = std::move(snapshots);
        return hold_signal;
    }

    // 生成信号：条件满足，执行对应的交易动作
    std::string reason = signal_func_->toString();
    Signal signal(action_, confidence, reason);
    signal.indicator_snapshots = std::move(snapshots);
    
    // 计算止盈止损（如果提供了表达式）
    // 支持：BUY, BUY(tp), BUY(tp, sl)
    if (tp_expr_) {
        try {
            Value tp_val = tp_expr_->evaluate(ctx);
            signal.tp = tp_val.toNumber();
        } catch (const std::exception&) {
            // 计算失败，使用默认值 0（Python层会重新计算）
            signal.tp = 0.0;
        }
    } else {
        signal.tp = 0.0;  // 未提供止盈表达式，使用默认值0
    }
    
    if (sl_expr_) {
        try {
            Value sl_val = sl_expr_->evaluate(ctx);
            signal.sl = sl_val.toNumber();
        } catch (const std::exception&) {
            // 计算失败，使用默认值 0
            signal.sl = 0.0;
        }
    } else {
        signal.sl = 0.0;  // 未提供止损表达式，使用默认值0
    }
    
    return signal;
}

std::string RuleNode::toString() const {
    std::string result = signal_func_->toString() + " = " + action_;  // action_已经是字符串了
    
    // 如果有止盈止损参数，添加到字符串表示中
    if (tp_expr_ || sl_expr_) {
        result += "(";
        if (tp_expr_) {
            result += tp_expr_->toString();
        }
        if (sl_expr_) {
            result += ", " + sl_expr_->toString();
        }
        result += ")";
    }
    
    return result;
}

} // namespace prophet::dsl
