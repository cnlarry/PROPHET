/*
 * ============================================================================
 * 文件名：optimizer.cpp
 * 功能说明：字节码优化器实现（阶段5优化）
 * ============================================================================
 */

#include "prophet/bytecode/optimizer.hpp"
#include <stack>
#include <cmath>

namespace prophet::bytecode {

// ============================================================================
// 主优化接口
// ============================================================================

std::vector<Instruction> BytecodeOptimizer::optimize(const std::vector<Instruction>& code) {
    stats_.original_instructions = code.size();
    
    // 优化流水线
    auto optimized = constantFolding(code);
    optimized = deadCodeElimination(optimized);
    optimized = strengthReduction(optimized);
    
    stats_.optimized_instructions = optimized.size();
    
    return optimized;
}

// ============================================================================
// 常量折叠
// ============================================================================

std::vector<Instruction> BytecodeOptimizer::constantFolding(const std::vector<Instruction>& code) {
    std::vector<Instruction> optimized;
    optimized.reserve(code.size());
    
    // 使用栈模拟常量传播
    std::stack<Value> const_stack;
    
    for (size_t i = 0; i < code.size(); ++i) {
        const auto& inst = code[i];
        
        // 如果是常量加载，压入栈
        if (inst.opcode == Opcode::LOAD_CONST) {
            const_stack.push(inst.operand);
            optimized.push_back(inst);
        }
        // 如果是二元运算，尝试折叠
        else if (isArithmeticOp(inst.opcode) || isComparisonOp(inst.opcode) || isLogicalOp(inst.opcode)) {
            if (const_stack.size() >= 2) {
                Value right = const_stack.top(); const_stack.pop();
                Value left = const_stack.top(); const_stack.pop();
                
                auto folded = tryFoldBinaryOp(inst.opcode, left, right);
                if (folded.has_value()) {
                    // 成功折叠，用常量替代
                    optimized.pop_back();  // 移除右操作数的LOAD_CONST
                    optimized.pop_back();  // 移除左操作数的LOAD_CONST
                    optimized.push_back(Instruction{Opcode::LOAD_CONST, *folded});
                    const_stack.push(*folded);
                    stats_.constants_folded++;
                } else {
                    // 无法折叠，保持原样
                    optimized.push_back(inst);
                    const_stack = std::stack<Value>();  // 清空栈
                }
            } else {
                // 栈中常量不足，保持原样
                optimized.push_back(inst);
                const_stack = std::stack<Value>();
            }
        }
        // 如果是一元运算，尝试折叠
        else if (inst.opcode == Opcode::NEG || inst.opcode == Opcode::NOT) {
            if (!const_stack.empty()) {
                Value operand = const_stack.top(); const_stack.pop();
                
                auto folded = tryFoldUnaryOp(inst.opcode, operand);
                if (folded.has_value()) {
                    optimized.pop_back();  // 移除操作数的LOAD_CONST
                    optimized.push_back(Instruction{Opcode::LOAD_CONST, *folded});
                    const_stack.push(*folded);
                    stats_.constants_folded++;
                } else {
                    optimized.push_back(inst);
                    const_stack = std::stack<Value>();
                }
            } else {
                optimized.push_back(inst);
                const_stack = std::stack<Value>();
            }
        }
        // 其他指令，清空常量栈
        else {
            optimized.push_back(inst);
            const_stack = std::stack<Value>();
        }
    }
    
    return optimized;
}

// ============================================================================
// 死代码消除
// ============================================================================

std::vector<Instruction> BytecodeOptimizer::deadCodeElimination(const std::vector<Instruction>& code) {
    // 简化实现：暂时不做复杂的控制流分析
    // 只移除明显的死代码模式
    return code;
}

// ============================================================================
// 强度削减
// ============================================================================

std::vector<Instruction> BytecodeOptimizer::strengthReduction(const std::vector<Instruction>& code) {
    std::vector<Instruction> optimized;
    optimized.reserve(code.size());
    
    for (size_t i = 0; i < code.size(); ++i) {
        const auto& inst = code[i];
        
        // 优化：x * 2 → x + x（加法比乘法快）
        if (inst.opcode == Opcode::MUL && i > 0) {
            const auto& prev = optimized.back();
            if (prev.opcode == Opcode::LOAD_CONST && prev.operand.isNumber()) {
                double val = prev.operand.toNumber();
                if (val == 2.0) {
                    // 替换为 DUP + ADD
                    // 暂时保持原样，后续可以添加DUP指令
                }
            }
        }
        
        optimized.push_back(inst);
    }
    
    return optimized;
}

// ============================================================================
// 辅助方法
// ============================================================================

std::optional<Value> BytecodeOptimizer::tryFoldBinaryOp(
    Opcode op, const Value& left, const Value& right
) const {
    // 只折叠数字运算
    if (!left.isNumber() || !right.isNumber()) {
        // 对于布尔运算
        if (left.isBoolean() && right.isBoolean()) {
            bool l = left.toBool();
            bool r = right.toBool();
            
            switch (op) {
                case Opcode::AND: return Value::fromBoolean(l && r);
                case Opcode::OR:  return Value::fromBoolean(l || r);
                case Opcode::EQ:  return Value::fromBoolean(l == r);
                case Opcode::NE:  return Value::fromBoolean(l != r);
                default: return std::nullopt;
            }
        }
        return std::nullopt;
    }
    
    double l = left.toNumber();
    double r = right.toNumber();
    
    switch (op) {
        // 算术运算
        case Opcode::ADD: return Value::fromNumber(l + r);
        case Opcode::SUB: return Value::fromNumber(l - r);
        case Opcode::MUL: return Value::fromNumber(l * r);
        case Opcode::DIV:
            if (r == 0.0) return std::nullopt;  // 避免除零
            return Value::fromNumber(l / r);
        case Opcode::MOD:
            if (r == 0.0) return std::nullopt;
            return Value::fromNumber(std::fmod(l, r));
        
        // 比较运算
        case Opcode::EQ: return Value::fromBoolean(l == r);
        case Opcode::NE: return Value::fromBoolean(l != r);
        case Opcode::GT: return Value::fromBoolean(l > r);
        case Opcode::LT: return Value::fromBoolean(l < r);
        case Opcode::GE: return Value::fromBoolean(l >= r);
        case Opcode::LE: return Value::fromBoolean(l <= r);
        
        default:
            return std::nullopt;
    }
}

std::optional<Value> BytecodeOptimizer::tryFoldUnaryOp(
    Opcode op, const Value& operand
) const {
    switch (op) {
        case Opcode::NEG:
            if (operand.isNumber()) {
                return Value::fromNumber(-operand.toNumber());
            }
            break;
        
        case Opcode::NOT:
            if (operand.isBoolean()) {
                return Value::fromBoolean(!operand.toBool());
            }
            break;
        
        default:
            break;
    }
    
    return std::nullopt;
}

bool BytecodeOptimizer::isConstantLoad(const Instruction& inst) const {
    return inst.opcode == Opcode::LOAD_CONST;
}

bool BytecodeOptimizer::isArithmeticOp(Opcode op) const {
    return op == Opcode::ADD || op == Opcode::SUB || 
           op == Opcode::MUL || op == Opcode::DIV || op == Opcode::MOD;
}

bool BytecodeOptimizer::isComparisonOp(Opcode op) const {
    return op == Opcode::EQ || op == Opcode::NE ||
           op == Opcode::GT || op == Opcode::LT ||
           op == Opcode::GE || op == Opcode::LE;
}

bool BytecodeOptimizer::isLogicalOp(Opcode op) const {
    return op == Opcode::AND || op == Opcode::OR;
}

} // namespace prophet::bytecode

