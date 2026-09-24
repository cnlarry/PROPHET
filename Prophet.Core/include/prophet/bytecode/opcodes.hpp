/*
 * ============================================================================
 * 文件名：opcodes.hpp
 * 功能说明：字节码指令集定义 - P2优化
 * 
 * 目标：将DSL规则编译为字节码，提升执行效率30-50%
 * 
 * 设计原则：
 * 1. 简单高效 - 基于栈的虚拟机
 * 2. 完整性 - 覆盖所有DSL语义
 * 3. 可扩展 - 易于添加新指令
 * 
 * P2优化核心组件
 * ============================================================================
 */

#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include "prophet/common/types.hpp"

namespace prophet {
namespace bytecode {

/**
 * 操作码（Opcode）- 字节码指令类型
 * 
 * 基于栈的虚拟机设计：
 * - 所有操作从栈顶获取操作数
 * - 结果压回栈顶
 * - 最终栈顶为规则评估结果（true/false）
 */
enum class Opcode : uint8_t {
    // ========== 数据加载指令 ==========
    
    /**
     * LOAD_CONST <value>
     * 将常量值压入栈
     * 栈变化: [] → [value]
     */
    LOAD_CONST = 0x01,
    
    /**
     * LOAD_INDICATOR <timeframe> <indicator> <field>
     * 加载指标字段值
     * 示例: $(5m).RSI().value → LOAD_INDICATOR "5m" "RSI" "value"
     * 栈变化: [] → [indicator_value]
     */
    LOAD_INDICATOR = 0x02,
    
    /**
     * LOAD_ENV <var_name>
     * 加载环境变量
     * 示例: @CURRENT_PRICE → LOAD_ENV "CURRENT_PRICE"
     * 栈变化: [] → [env_value]
     */
    LOAD_ENV = 0x03,
    
    // ========== 算术运算指令 ==========
    
    /**
     * ADD
     * 栈顶两个值相加
     * 栈变化: [a, b] → [a + b]
     */
    ADD = 0x10,
    
    /**
     * SUB
     * 栈顶两个值相减
     * 栈变化: [a, b] → [a - b]
     */
    SUB = 0x11,
    
    /**
     * MUL
     * 栈顶两个值相乘
     * 栈变化: [a, b] → [a * b]
     */
    MUL = 0x12,
    
    /**
     * DIV
     * 栈顶两个值相除
     * 栈变化: [a, b] → [a / b]
     */
    DIV = 0x13,
    
    /**
     * MOD
     * 栈顶两个值取模
     * 栈变化: [a, b] → [a % b]
     */
    MOD = 0x14,
    
    /**
     * NEG
     * 栈顶值取负
     * 栈变化: [a] → [-a]
     */
    NEG = 0x15,
    
    // ========== 比较运算指令 ==========
    
    /**
     * EQ (==)
     * 栈变化: [a, b] → [a == b]
     */
    EQ = 0x20,
    
    /**
     * NE (!=)
     * 栈变化: [a, b] → [a != b]
     */
    NE = 0x21,
    
    /**
     * GT (>)
     * 栈变化: [a, b] → [a > b]
     */
    GT = 0x22,
    
    /**
     * LT (<)
     * 栈变化: [a, b] → [a < b]
     */
    LT = 0x23,
    
    /**
     * GE (>=)
     * 栈变化: [a, b] → [a >= b]
     */
    GE = 0x24,
    
    /**
     * LE (<=)
     * 栈变化: [a, b] → [a <= b]
     */
    LE = 0x25,
    
    // ========== 逻辑运算指令 ==========
    
    /**
     * AND (&&)
     * 栈变化: [a, b] → [a && b]
     */
    AND = 0x30,
    
    /**
     * OR (||)
     * 栈变化: [a, b] → [a || b]
     */
    OR = 0x31,
    
    /**
     * NOT (!)
     * 栈变化: [a] → [!a]
     */
    NOT = 0x32,
    
    // ========== 聚合函数指令 ==========
    
    /**
     * ALL
     * 检查栈上N个条件是否全部为真
     * 参数: N (条件数量)
     * 栈变化: [c1, c2, ..., cN] → [c1 && c2 && ... && cN]
     */
    ALL = 0x40,
    
    /**
     * ANY
     * 检查栈上N个条件是否至少一个为真
     * 参数: N (条件数量)
     * 栈变化: [c1, c2, ..., cN] → [c1 || c2 || ... || cN]
     */
    ANY = 0x41,
    
    /**
     * NONE
     * 检查栈上N个条件是否全部为假
     * 参数: N (条件数量)
     * 栈变化: [c1, c2, ..., cN] → [!(c1 || c2 || ... || cN)]
     */
    NONE = 0x42,
    
    // ========== 控制流指令 ==========
    
    /**
     * JUMP <offset>
     * 无条件跳转
     * 参数: offset (相对偏移量)
     */
    JUMP = 0x50,
    
    /**
     * JUMP_IF_FALSE <offset>
     * 条件跳转：如果栈顶为false，跳转
     * 参数: offset (相对偏移量)
     * 栈变化: [cond] → []
     */
    JUMP_IF_FALSE = 0x51,
    
    /**
     * RETURN
     * 返回栈顶值
     * 栈变化: [result] → []（虚拟机停止）
     */
    RETURN = 0x60,
    
    /**
     * POP
     * 弹出栈顶值
     * 栈变化: [value] → []
     */
    POP = 0x61,
};

/**
 * 字节码指令
 * 
 * 包含：
 * - opcode: 操作码
 * - operand: 操作数（可选）
 * - jump_offset: 跳转偏移（用于JUMP指令）
 */
struct Instruction {
    Opcode opcode;
    Value operand;           // 操作数（用于LOAD_CONST, LOAD_INDICATOR等）
    int jump_offset;         // 跳转偏移（用于JUMP, JUMP_IF_FALSE）
    
    Instruction()
        : opcode(Opcode::RETURN)
        , operand(Value::fromNumber(0))
        , jump_offset(0) {}
    
    Instruction(Opcode op)
        : opcode(op)
        , operand(Value::fromNumber(0))
        , jump_offset(0) {}
    
    Instruction(Opcode op, const Value& val)
        : opcode(op)
        , operand(val)
        , jump_offset(0) {}
    
    Instruction(Opcode op, int offset)
        : opcode(op)
        , operand(Value::fromNumber(0))
        , jump_offset(offset) {}
};

/**
 * 辅助函数：Opcode转字符串（用于调试）
 */
inline std::string opcodeToString(Opcode opcode) {
    switch (opcode) {
        case Opcode::LOAD_CONST: return "LOAD_CONST";
        case Opcode::LOAD_INDICATOR: return "LOAD_INDICATOR";
        case Opcode::LOAD_ENV: return "LOAD_ENV";
        case Opcode::ADD: return "ADD";
        case Opcode::SUB: return "SUB";
        case Opcode::MUL: return "MUL";
        case Opcode::DIV: return "DIV";
        case Opcode::MOD: return "MOD";
        case Opcode::NEG: return "NEG";
        case Opcode::EQ: return "EQ";
        case Opcode::NE: return "NE";
        case Opcode::GT: return "GT";
        case Opcode::LT: return "LT";
        case Opcode::GE: return "GE";
        case Opcode::LE: return "LE";
        case Opcode::AND: return "AND";
        case Opcode::OR: return "OR";
        case Opcode::NOT: return "NOT";
        case Opcode::ALL: return "ALL";
        case Opcode::ANY: return "ANY";
        case Opcode::NONE: return "NONE";
        case Opcode::JUMP: return "JUMP";
        case Opcode::JUMP_IF_FALSE: return "JUMP_IF_FALSE";
        case Opcode::RETURN: return "RETURN";
        case Opcode::POP: return "POP";
        default: return "UNKNOWN";
    }
}

/**
 * 辅助函数：打印指令（用于调试）
 */
inline std::string instructionToString(const Instruction& inst) {
    std::string result = opcodeToString(inst.opcode);
    
    if (inst.opcode == Opcode::LOAD_CONST) {
        result += " " + inst.operand.toString();
    } else if (inst.opcode == Opcode::JUMP || inst.opcode == Opcode::JUMP_IF_FALSE) {
        result += " " + std::to_string(inst.jump_offset);
    }
    
    return result;
}

} // namespace bytecode
} // namespace prophet

