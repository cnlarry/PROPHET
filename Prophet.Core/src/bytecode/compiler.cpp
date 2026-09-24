/*
 * ============================================================================
 * 文件名：compiler.cpp
 * 功能说明：字节码编译器实现 - Day 1
 * 
 * 编译策略：
 * 1. 深度优先遍历AST
 * 2. 后序生成指令（先编译操作数，后生成操作符）
 * 3. 基于栈的求值模型
 * 
 * P2优化核心组件
 * ============================================================================
 */

#include "prophet/bytecode/compiler.hpp"
#include "prophet/dsl/ast.hpp"
#include "prophet/functions/Common.hpp"
#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/indicators/registry.hpp"
#include <sstream>
#include <iomanip>
#include <stdexcept>

namespace prophet {
namespace bytecode {

// ============================================================================
// 构造函数
// ============================================================================

BytecodeCompiler::BytecodeCompiler() {
    code_.reserve(64);  // 预分配空间，减少重分配
}

// ============================================================================
// 主编译接口
// ============================================================================

std::vector<Instruction> BytecodeCompiler::compile(const dsl::ASTNodePtr& condition) {
    code_.clear();
    
    if (!condition) {
        throw std::runtime_error("BytecodeCompiler::compile: null condition");
    }
    
    // 编译条件表达式
    compile_node(condition);
    
    // 生成RETURN指令
    emit(Opcode::RETURN);
    
    return code_;
}

// ============================================================================
// 节点分发器 - 根据节点类型调用对应的编译函数
// ============================================================================

void BytecodeCompiler::compile_node(const dsl::ASTNodePtr& node) {
    if (!node) {
        throw std::runtime_error("BytecodeCompiler::compile_node: null node");
    }
    
    // 使用dynamic_cast判断节点类型并分发
    
    // 1. 字面量节点
    if (auto* num_node = dynamic_cast<const dsl::NumberNode*>(node.get())) {
        compile_number(num_node);
        return;
    }
    
    if (auto* bool_node = dynamic_cast<const dsl::BooleanNode*>(node.get())) {
        compile_boolean(bool_node);
        return;
    }
    
    if (auto* str_node = dynamic_cast<const dsl::StringNode*>(node.get())) {
        compile_string(str_node);
        return;
    }
    
    // 2. 引用节点
    if (auto* indicator_node = dynamic_cast<const dsl::IndicatorRefNode*>(node.get())) {
        compile_indicator_ref(indicator_node);
        return;
    }
    
    // 3. 运算符节点
    if (auto* binary_node = dynamic_cast<const dsl::BinaryOpNode*>(node.get())) {
        compile_binary_op(binary_node);
        return;
    }
    
    if (auto* unary_node = dynamic_cast<const dsl::UnaryOpNode*>(node.get())) {
        compile_unary_op(unary_node);
        return;
    }
    
    // 4. 信号函数节点
    if (auto* all_node = dynamic_cast<const dsl::AllNode*>(node.get())) {
        compile_signal_function(all_node);
        return;
    }
    
    if (auto* any_node = dynamic_cast<const dsl::AnyNode*>(node.get())) {
        compile_signal_function(any_node);
        return;
    }
    
    if (auto* none_node = dynamic_cast<const dsl::NoneNode*>(node.get())) {
        compile_signal_function(none_node);
        return;
    }
    
    // 其他节点类型暂不支持字节码编译
    throw std::runtime_error("BytecodeCompiler: unsupported AST node type: " + node->toString());
}

// ============================================================================
// 编译字面量节点
// ============================================================================

void BytecodeCompiler::compile_number(const dsl::NumberNode* node) {
    // 使用访问器获取值
    double value = node->getValue();
    emit(Opcode::LOAD_CONST, Value::fromNumber(value));
}

void BytecodeCompiler::compile_boolean(const dsl::BooleanNode* node) {
    // 使用访问器获取值
    bool value = node->getValue();
    emit(Opcode::LOAD_CONST, Value::fromBoolean(value));
}

void BytecodeCompiler::compile_string(const dsl::StringNode* node) {
    // 使用访问器获取值
    const std::string& value = node->getValue();
    emit(Opcode::LOAD_CONST, Value::fromString(value));
}

// ============================================================================
// 编译二元运算符 - Day 1核心逻辑
// ============================================================================

void BytecodeCompiler::compile_binary_op(const dsl::BinaryOpNode* node) {
    // 后序遍历：先编译左右操作数，再生成操作符指令
    
    // 1. 编译左操作数
    compile_node(node->getLeft());
    
    // 2. 编译右操作数
    compile_node(node->getRight());
    
    // 3. 生成操作符指令
    switch (node->getOp()) {
        case dsl::BinaryOpNode::OpType::ADD:
            emit(Opcode::ADD);
            break;
        case dsl::BinaryOpNode::OpType::SUB:
            emit(Opcode::SUB);
            break;
        case dsl::BinaryOpNode::OpType::MUL:
            emit(Opcode::MUL);
            break;
        case dsl::BinaryOpNode::OpType::DIV:
            emit(Opcode::DIV);
            break;
        case dsl::BinaryOpNode::OpType::MOD:
            emit(Opcode::MOD);
            break;
        case dsl::BinaryOpNode::OpType::EQ:
            emit(Opcode::EQ);
            break;
        case dsl::BinaryOpNode::OpType::NE:
            emit(Opcode::NE);
            break;
        case dsl::BinaryOpNode::OpType::GT:
            emit(Opcode::GT);
            break;
        case dsl::BinaryOpNode::OpType::LT:
            emit(Opcode::LT);
            break;
        case dsl::BinaryOpNode::OpType::GE:
            emit(Opcode::GE);
            break;
        case dsl::BinaryOpNode::OpType::LE:
            emit(Opcode::LE);
            break;
        case dsl::BinaryOpNode::OpType::AND:
            emit(Opcode::AND);
            break;
        case dsl::BinaryOpNode::OpType::OR:
            emit(Opcode::OR);
            break;
        default:
            throw std::runtime_error("BytecodeCompiler: unknown binary operator");
    }
}

// ============================================================================
// 编译一元运算符
// ============================================================================

void BytecodeCompiler::compile_unary_op(const dsl::UnaryOpNode* node) {
    // 1. 编译操作数
    compile_node(node->getOperand());
    
    // 2. 生成操作符指令
    switch (node->getOp()) {
        case dsl::UnaryOpNode::OpType::NEG:
            emit(Opcode::NEG);
            break;
        case dsl::UnaryOpNode::OpType::POS:
            // 正号不需要任何操作
            break;
        default:
            throw std::runtime_error("BytecodeCompiler: unknown unary operator");
    }
}

// ============================================================================
// 编译指标引用
// ============================================================================

void BytecodeCompiler::compile_indicator_ref(const dsl::IndicatorRefNode* node) {
    // LOAD_INDICATOR指令需要3个参数：timeframe, indicator, field
    // 将它们编码到operand中
    
    std::string indicator = node->getIndicator();
    
    // 检查是否为时间序列函数（不需要timeframe，通过函数注册表处理）
    // 时间序列函数：CURRENT, FEARGREED, FUNDINGRATE等
    if (indicator == "CURRENT" || indicator == "FEARGREED" || indicator == "FUNDINGRATE") {
        // 时间序列函数不支持字节码编译，抛出异常让其回退到AST评估
        throw std::runtime_error("Time series functions not supported in bytecode: " + indicator);
    }
    
    // 检查是否为数据函数（KLINE, HIGHEST, CHANGE, AVERAGE等）
    // 数据函数通过 functions::FunctionRegistry 处理（见 IndicatorRefNode::evaluate），
    // 字节码 VM 的 exec_load_indicator 只会走指标注册表，无法处理数据函数，
    // 必须在编译期识别并回退到 AST 评估，否则运行时会报
    // "Unsupported indicator for auto-calculation: KLINE"。
    // 注意：部分名字同时注册为数据函数和真实指标（如 ATR），这类应走
    // 指标路径（LOAD_INDICATOR），只有"是数据函数且不是真实指标"才回退。
    functions::register_all_functions(functions::FunctionRegistry::instance());
    bool is_data_function = functions::FunctionRegistry::instance().resolve(indicator) != nullptr;
    bool is_real_indicator = indicators::IndicatorRegistry::getInstance().hasIndicator(indicator);
    if (is_data_function && !is_real_indicator) {
        throw std::runtime_error("Data function not supported in bytecode: " + indicator);
    }
    
    // 使用第一个时间框架（多时间框架在字节码中暂不支持）
    std::string tf = node->getSingleTimeframe();
    std::string field = node->getField();

    // ========================================================================
    // 正确性护栏：字节码只处理它能保证与 AST 结果一致的场景。
    // 带指标参数（$(5m).RSI(7).value）、带字段偏移（$(5m).RSI().value(-1)）、
    // 多时间框架的引用，统一在编译期抛异常回退到 AST 评估，
    // 避免字节码 VM 用默认参数/默认偏移算出不同结果。
    // ========================================================================
    if (node->hasMultipleTimeframes()) {
        throw std::runtime_error("Multi-timeframe indicator not supported in bytecode: " + indicator);
    }
    if (node->hasIndicatorParams()) {
        throw std::runtime_error("Indicator with DSL params not supported in bytecode: " + indicator
                                 + " (falling back to AST)");
    }
    if (!node->getFieldParams().empty()) {
        throw std::runtime_error("Indicator field offset not supported in bytecode: " + indicator
                                 + "." + field + " (falling back to AST)");
    }

    // 使用分隔符编码（简单方案）
    std::string encoded = tf + "|" + indicator + "|" + field;
    emit(Opcode::LOAD_INDICATOR, Value::fromString(encoded));
}

// ============================================================================
// 编译信号函数 (ALL/ANY/NONE)
// ============================================================================

void BytecodeCompiler::compile_signal_function(const dsl::SignalFunctionNode* node) {
    // 1. 获取所有条件
    const auto& conditions = node->getConditions();
    
    // 2. 编译每个条件（结果依次压栈）
    for (const auto& cond : conditions) {
        compile_node(cond);
    }
    
    // 3. 生成聚合指令
    int count = static_cast<int>(conditions.size());
    
    if (dynamic_cast<const dsl::AllNode*>(node)) {
        emit(Opcode::ALL, Value::fromNumber(count));
    } else if (dynamic_cast<const dsl::AnyNode*>(node)) {
        emit(Opcode::ANY, Value::fromNumber(count));
    } else if (dynamic_cast<const dsl::NoneNode*>(node)) {
        emit(Opcode::NONE, Value::fromNumber(count));
    } else {
        throw std::runtime_error("BytecodeCompiler: unsupported signal function type");
    }
}

// ============================================================================
// 辅助方法：生成指令
// ============================================================================

void BytecodeCompiler::emit(Opcode opcode) {
    code_.push_back(Instruction(opcode));
}

void BytecodeCompiler::emit(Opcode opcode, const Value& operand) {
    code_.push_back(Instruction(opcode, operand));
}

void BytecodeCompiler::emit(Opcode opcode, int jump_offset) {
    code_.push_back(Instruction(opcode, jump_offset));
}

// ============================================================================
// 反汇编器 - Day 3功能
// ============================================================================

std::string BytecodeCompiler::disassemble(const std::vector<Instruction>& code) const {
    std::ostringstream oss;
    
    for (size_t i = 0; i < code.size(); i++) {
        const auto& inst = code[i];
        
        // 打印指令索引
        oss << std::setw(4) << i << ": ";
        
        // 打印指令
        oss << instructionToString(inst);
        
        oss << "\n";
    }
    
    return oss.str();
}

} // namespace bytecode
} // namespace prophet

