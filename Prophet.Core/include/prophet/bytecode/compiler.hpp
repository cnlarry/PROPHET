/*
 * ============================================================================
 * 文件名：compiler.hpp
 * 功能说明：字节码编译器 - 将AST编译为字节码
 * 
 * 编译流程：
 * AST → 遍历节点 → 生成指令序列 → 字节码
 * 
 * P2优化核心组件
 * ============================================================================
 */

#pragma once

#include "opcodes.hpp"
#include "prophet/dsl/ast.hpp"
#include <vector>
#include <memory>

namespace prophet {
namespace bytecode {

/**
 * 字节码编译器
 * 
 * 职责：
 * 1. 遍历AST树
 * 2. 为每个节点生成对应的字节码指令
 * 3. 优化指令序列（可选）
 */
class BytecodeCompiler {
public:
    BytecodeCompiler();
    
    /**
     * 编译规则条件为字节码
     * @param condition 规则的条件表达式（AST）
     * @return 字节码指令序列
     */
    std::vector<Instruction> compile(const dsl::ASTNodePtr& condition);
    
    /**
     * 获取最后一次编译的指令数量
     */
    size_t getInstructionCount() const { return code_.size(); }
    
    /**
     * 反汇编：字节码 → 可读文本（用于调试）
     */
    std::string disassemble(const std::vector<Instruction>& code) const;

private:
    std::vector<Instruction> code_;  // 生成的字节码
    
    // 编译各种AST节点类型
    void compile_node(const dsl::ASTNodePtr& node);
    void compile_binary_op(const dsl::BinaryOpNode* node);
    void compile_unary_op(const dsl::UnaryOpNode* node);
    void compile_indicator_ref(const dsl::IndicatorRefNode* node);
    void compile_number(const dsl::NumberNode* node);
    void compile_boolean(const dsl::BooleanNode* node);
    void compile_string(const dsl::StringNode* node);
    void compile_signal_function(const dsl::SignalFunctionNode* node);
    
    // 辅助方法：生成指令
    void emit(Opcode opcode);
    void emit(Opcode opcode, const Value& operand);
    void emit(Opcode opcode, int jump_offset);
};

} // namespace bytecode
} // namespace prophet

