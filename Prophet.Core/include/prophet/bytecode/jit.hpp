/*
 * ============================================================================
 * 文件名：jit.hpp
 * 功能说明：简化JIT编译器 - P2.2优化
 * 
 * 核心思想：
 * - 不生成机器码（太复杂）
 * - 而是生成优化的C++ Lambda
 * - 跳过字节码VM，直接访问Context
 * 
 * 预期提升：+10-20%
 * ============================================================================
 */

#pragma once

#include <functional>
#include <memory>
#include <string>
#include <vector>
#include "opcodes.hpp"
#include "prophet/core/context.hpp"

namespace prophet {
namespace bytecode {

/**
 * JIT函数类型
 * 输入：Context引用
 * 输出：规则评估结果（true/false）
 */
using JITFunc = std::function<bool(dsl::Context&)>;

/**
 * 可优化的DSL模式
 */
enum class JITPattern {
    UNKNOWN,                    // 无法识别/优化
    INDICATOR_CMP_CONST,        // $(5m).RSI().value < 30
    INDICATOR_CMP_INDICATOR,    // $(5m).MACD().histogram > $(5m).MACD().signal
    SIMPLE_ALL_2,               // ALL{cond1, cond2}
    SIMPLE_ANY_2,               // ANY{cond1, cond2}
    // 可扩展更多模式...
};

/**
 * 模式参数（用于Lambda生成）
 */
struct PatternParams {
    // INDICATOR_CMP_CONST
    std::string timeframe;
    std::string indicator;
    std::string field;
    double constant;
    Opcode cmp_op;
    
    // INDICATOR_CMP_INDICATOR
    std::string timeframe2;
    std::string indicator2;
    std::string field2;
    
    // ALL/ANY
    std::vector<JITFunc> sub_funcs;
};

/**
 * 简化JIT编译器
 * 
 * 职责：
 * 1. 识别字节码模式
 * 2. 生成优化Lambda
 * 3. 回退机制（无法优化时返回nullptr）
 */
class JITCompiler {
public:
    JITCompiler() = default;
    
    /**
     * 尝试编译字节码为JIT函数
     * @param code 字节码指令序列
     * @return JIT函数（失败返回nullptr）
     */
    JITFunc compile(const std::vector<Instruction>& code);
    
    /**
     * 获取编译统计
     */
    struct Statistics {
        size_t total_attempts;      // 尝试编译次数
        size_t successful_compiles;  // 成功编译次数
        size_t pattern_hits[10];     // 各模式命中次数
    };
    Statistics getStatistics() const { return stats_; }
    
private:
    Statistics stats_;
    
    /**
     * 识别字节码模式
     */
    JITPattern detectPattern(const std::vector<Instruction>& code);
    
    /**
     * 提取模式参数
     */
    PatternParams extractParams(JITPattern pattern, const std::vector<Instruction>& code);
    
    /**
     * 生成JIT函数
     */
    JITFunc generateLambda(JITPattern pattern, const PatternParams& params);
    
    // ========== 特定模式的Lambda生成器 ==========
    JITFunc generate_indicator_cmp_const(const PatternParams& params);
    JITFunc generate_indicator_cmp_indicator(const PatternParams& params);
    JITFunc generate_simple_all_2(const PatternParams& params);
    JITFunc generate_simple_any_2(const PatternParams& params);
};

} // namespace bytecode
} // namespace prophet

