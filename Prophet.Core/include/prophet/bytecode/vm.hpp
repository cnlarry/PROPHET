/*
 * ============================================================================
 * 文件名：vm.hpp
 * 功能说明：字节码虚拟机 - Day 2
 * 
 * 设计：
 * - 基于栈的虚拟机
 * - 高效的指令执行循环
 * - 最小化开销
 * 
 * P2优化核心组件
 * ============================================================================
 */

#pragma once

#include "opcodes.hpp"
#include "prophet/core/context.hpp"
#include "prophet/common/signal.hpp"
#include <vector>
#include <stdexcept>

namespace prophet {
namespace bytecode {

/**
 * 字节码虚拟机
 * 
 * 职责：
 * 1. 执行字节码指令序列
 * 2. 管理操作数栈
 * 3. 与Context交互（加载指标、环境变量）
 * 
 * 性能关键：
 * - 指令执行循环需要高效
 * - 栈操作需要内联
 * - 最小化分支预测失败
 */
class BytecodeVM {
public:
    BytecodeVM();
    
    /**
     * 执行字节码，返回结果
     * @param code 字节码指令序列
     * @param context 执行上下文（用于获取指标、参数等）
     * @return 规则评估结果（通常为boolean）
     */
    Value execute(const std::vector<Instruction>& code,
                  dsl::Context& context);
    
    /**
     * 执行字节码并收集指标快照
     * @param code 字节码指令序列
     * @param context 执行上下文
     * @param snapshots_out 输出的指标快照列表（会在执行过程中填充）
     * @return 规则评估结果（通常为boolean）
     */
    Value executeWithSnapshots(const std::vector<Instruction>& code,
                               dsl::Context& context,
                               std::vector<IndicatorSnapshot>& snapshots_out);
    
private:
    /**
     * 内部执行实现（execute和executeWithSnapshots的公共逻辑）
     */
    Value executeImpl(const std::vector<Instruction>& code,
                      dsl::Context& context);
    
    /**
     * 重置虚拟机状态
     */
    void reset();
    
    /**
     * 获取执行统计（用于性能分析）
     */
    struct Statistics {
        size_t instructions_executed;  // 执行的指令数
        size_t max_stack_depth;        // 最大栈深度
    };
    Statistics getStatistics() const { return stats_; }

private:
    std::vector<Value> stack_;       // 操作数栈
    size_t stack_top_;               // P2.1优化：栈顶指针（避免clear）
    size_t ip_;                      // 指令指针
    Statistics stats_;               // 统计信息
    size_t max_observed_stack_size_; // P2.1.2优化：历史最大栈大小（用于智能预分配）
    
    // 指标快照收集（可选）
    std::vector<IndicatorSnapshot>* snapshots_collector_;  // 指向外部快照收集器（如果启用）
    
    // ========== 栈操作（内联以提高性能） ==========
    // P2.1优化：使用栈顶指针，避免vector的clear/resize
    
    inline void push(const Value& value) {
        if (stack_top_ >= stack_.size()) {
            stack_.push_back(value);
        } else {
            stack_[stack_top_] = value;
        }
        stack_top_++;
        if (stack_top_ > stats_.max_stack_depth) {
            stats_.max_stack_depth = stack_top_;
        }
    }
    
    inline Value pop() {
        if (stack_top_ == 0) {
            throw std::runtime_error("BytecodeVM: stack underflow");
        }
        return stack_[--stack_top_];
    }
    
    inline Value& peek() {
        if (stack_top_ == 0) {
            throw std::runtime_error("BytecodeVM: stack empty");
        }
        return stack_[stack_top_ - 1];
    }
    
    // ========== 指令执行方法 ==========
    
    // 加载指令
    void exec_load_const(const Instruction& inst);
    void exec_load_indicator(const Instruction& inst, dsl::Context& ctx);
    void exec_load_indicator_with_snapshot(const Instruction& inst, dsl::Context& ctx);
    void exec_load_env(const Instruction& inst, dsl::Context& ctx);
    
    // 算术指令
    void exec_add();
    void exec_sub();
    void exec_mul();
    void exec_div();
    void exec_mod();
    void exec_neg();
    
    // 比较指令
    void exec_eq();
    void exec_ne();
    void exec_gt();
    void exec_lt();
    void exec_ge();
    void exec_le();
    
    // 逻辑指令
    void exec_and();
    void exec_or();
    void exec_not();
    
    // 聚合指令
    void exec_all(int count);
    void exec_any(int count);
    void exec_none(int count);
};

/**
 * 虚拟机异常
 */
class VMException : public std::runtime_error {
public:
    explicit VMException(const std::string& msg)
        : std::runtime_error("BytecodeVM: " + msg) {}
};

} // namespace bytecode
} // namespace prophet

