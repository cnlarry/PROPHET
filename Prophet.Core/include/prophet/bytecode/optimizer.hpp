/*
 * ============================================================================
 * 文件名：optimizer.hpp
 * 功能说明：字节码优化器（阶段5优化）
 * 
 * 优化技术：
 * - 常量折叠（Constant Folding）
 * - 死代码消除（Dead Code Elimination）
 * - 强度削减（Strength Reduction）
 * ============================================================================
 */

#pragma once

#include "opcodes.hpp"
#include <vector>
#include <optional>

namespace prophet::bytecode {

/**
 * 字节码优化器
 * 
 * 在编译后对字节码进行优化，减少运行时计算
 */
class BytecodeOptimizer {
public:
    BytecodeOptimizer() = default;
    
    /**
     * 优化字节码序列
     * @param code 原始字节码
     * @return 优化后的字节码
     */
    std::vector<Instruction> optimize(const std::vector<Instruction>& code);
    
    /**
     * 获取优化统计
     */
    struct Statistics {
        size_t original_instructions = 0;
        size_t optimized_instructions = 0;
        size_t constants_folded = 0;
        size_t dead_code_eliminated = 0;
        
        double reduction_rate() const {
            if (original_instructions == 0) return 0.0;
            return 1.0 - static_cast<double>(optimized_instructions) / original_instructions;
        }
    };
    
    Statistics getStatistics() const { return stats_; }
    void resetStatistics() { stats_ = Statistics(); }

private:
    /**
     * 常量折叠优化
     * 
     * 识别编译期可计算的表达式并预先计算
     * 例如：3 + 5 → 8
     */
    std::vector<Instruction> constantFolding(const std::vector<Instruction>& code);
    
    /**
     * 死代码消除
     * 
     * 移除永远不会执行的代码
     */
    std::vector<Instruction> deadCodeElimination(const std::vector<Instruction>& code);
    
    /**
     * 强度削减
     * 
     * 用更快的操作替代慢的操作
     * 例如：x * 2 → x + x，x / 2 → x * 0.5
     */
    std::vector<Instruction> strengthReduction(const std::vector<Instruction>& code);
    
    /**
     * 尝试折叠二元运算
     */
    std::optional<Value> tryFoldBinaryOp(Opcode op, const Value& left, const Value& right) const;
    
    /**
     * 尝试折叠一元运算
     */
    std::optional<Value> tryFoldUnaryOp(Opcode op, const Value& operand) const;
    
    /**
     * 检查指令是否是常量加载
     */
    bool isConstantLoad(const Instruction& inst) const;
    
    /**
     * 检查指令是否是算术或比较运算
     */
    bool isArithmeticOp(Opcode op) const;
    bool isComparisonOp(Opcode op) const;
    bool isLogicalOp(Opcode op) const;

private:
    Statistics stats_;
};

} // namespace prophet::bytecode

