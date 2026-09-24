/*
 * ============================================================================
 * 文件名：vm.cpp
 * 功能说明：字节码虚拟机实现 - Day 2
 * 
 * 核心设计：
 * - 高效的switch-case指令分发
 * - 最小化栈操作开销
 * - 与Context无缝集成
 * 
 * P2优化核心组件
 * ============================================================================
 */

#include "prophet/bytecode/vm.hpp"
#include <sstream>
#include <stdexcept>

namespace prophet {
namespace bytecode {

// ============================================================================
// 构造函数
// ============================================================================

BytecodeVM::BytecodeVM() 
    : ip_(0)
    , max_observed_stack_size_(32) {  // P2.1.2优化：初始预分配大小
    stack_.reserve(max_observed_stack_size_);
    stats_ = {0, 0};
}

// ============================================================================
// 主执行接口
// ============================================================================

Value BytecodeVM::execute(const std::vector<Instruction>& code,
                          dsl::Context& context) {
    snapshots_collector_ = nullptr;  // 禁用快照收集
    return executeImpl(code, context);
}

Value BytecodeVM::executeWithSnapshots(const std::vector<Instruction>& code,
                                       dsl::Context& context,
                                       std::vector<IndicatorSnapshot>& snapshots_out) {
    snapshots_collector_ = &snapshots_out;  // 启用快照收集
    snapshots_out.clear();
    return executeImpl(code, context);
}

Value BytecodeVM::executeImpl(const std::vector<Instruction>& code,
                               dsl::Context& context) {
    // 重置状态 (P2.1优化：不清空stack_，只重置指针)
    stack_top_ = 0;
    ip_ = 0;
    stats_.instructions_executed = 0;
    
    // P2.1.2优化：使用历史最大值智能预分配
    // 避免执行过程中的重新分配
    if (stack_.capacity() < max_observed_stack_size_) {
        stack_.reserve(max_observed_stack_size_);
    }
    
    if (code.empty()) {
        return Value::fromBoolean(false);
    }
    
    // 主执行循环 - 性能关键路径
    while (ip_ < code.size()) {
        const auto& inst = code[ip_];
        stats_.instructions_executed++;
        
        // 指令分发 - 使用switch以便编译器优化
        switch (inst.opcode) {
            // ========== 加载指令 ==========
            case Opcode::LOAD_CONST:
                exec_load_const(inst);
                break;
                
            case Opcode::LOAD_INDICATOR:
                if (snapshots_collector_) {
                    exec_load_indicator_with_snapshot(inst, context);
                } else {
                    exec_load_indicator(inst, context);
                }
                break;
                
            case Opcode::LOAD_ENV:
                exec_load_env(inst, context);
                break;
            
            // ========== 算术指令 ==========
            case Opcode::ADD:
                exec_add();
                break;
                
            case Opcode::SUB:
                exec_sub();
                break;
                
            case Opcode::MUL:
                exec_mul();
                break;
                
            case Opcode::DIV:
                exec_div();
                break;
                
            case Opcode::MOD:
                exec_mod();
                break;
                
            case Opcode::NEG:
                exec_neg();
                break;
            
            // ========== 比较指令 ==========
            case Opcode::EQ:
                exec_eq();
                break;
                
            case Opcode::NE:
                exec_ne();
                break;
                
            case Opcode::GT:
                exec_gt();
                break;
                
            case Opcode::LT:
                exec_lt();
                break;
                
            case Opcode::GE:
                exec_ge();
                break;
                
            case Opcode::LE:
                exec_le();
                break;
            
            // ========== 逻辑指令 ==========
            case Opcode::AND:
                exec_and();
                break;
                
            case Opcode::OR:
                exec_or();
                break;
                
            case Opcode::NOT:
                exec_not();
                break;
            
            // ========== 聚合指令 ==========
            case Opcode::ALL:
                exec_all(static_cast<int>(inst.operand.toNumber()));
                break;
                
            case Opcode::ANY:
                exec_any(static_cast<int>(inst.operand.toNumber()));
                break;
                
            case Opcode::NONE:
                exec_none(static_cast<int>(inst.operand.toNumber()));
                break;
            
            // ========== 控制流指令 ==========
            case Opcode::RETURN:
                // 注意：stack_ 是复用存储（容量常驻），逻辑栈顶是 stack_top_，
                // 必须用 stack_top_ 判空，否则复用时会读到上一轮残留。
                if (stack_top_ == 0) {
                    return Value::fromBoolean(false);
                }
                return pop();
                
            case Opcode::POP:
                pop();
                break;
            
            default:
                throw VMException("Unknown opcode: " + 
                                std::to_string(static_cast<int>(inst.opcode)));
        }
        
        ip_++;
    }
    
    // P2.1.2优化：更新历史最大栈大小
    // 用于下次执行时的智能预分配
    if (stats_.max_stack_depth > max_observed_stack_size_) {
        max_observed_stack_size_ = stats_.max_stack_depth;
    }
    
    // 如果没有RETURN指令，返回栈顶值
    if (stack_top_ > 0) {
        return pop();
    }
    
    return Value::fromBoolean(false);
}

// ============================================================================
// 重置虚拟机状态
// ============================================================================

void BytecodeVM::reset() {
    stack_.clear();
    ip_ = 0;
    stats_ = {0, 0};
}

// ============================================================================
// 加载指令实现
// ============================================================================

void BytecodeVM::exec_load_const(const Instruction& inst) {
    push(inst.operand);
}

void BytecodeVM::exec_load_indicator(const Instruction& inst, dsl::Context& ctx) {
    // 解析编码字符串："5m|RSI|value"
    std::string encoded = inst.operand.toString();
    
    // 分割字符串
    size_t pos1 = encoded.find('|');
    size_t pos2 = encoded.find('|', pos1 + 1);
    
    if (pos1 == std::string::npos || pos2 == std::string::npos) {
        throw VMException("Invalid indicator encoding: " + encoded);
    }
    
    std::string timeframe = encoded.substr(0, pos1);
    std::string indicator = encoded.substr(pos1 + 1, pos2 - pos1 - 1);
    std::string field = encoded.substr(pos2 + 1);
    
    // 从Context获取或计算指标
    // 注意：getOrCalculateIndicator的参数顺序是 (indicator_name, timeframe)，不是 (timeframe, indicator)
    auto result = ctx.getOrCalculateIndicator(indicator, timeframe);
    
    // 提取字段值
    Value field_value = result.get(field);
    
    push(field_value);
}

void BytecodeVM::exec_load_indicator_with_snapshot(const Instruction& inst, dsl::Context& ctx) {
    // 解析编码字符串："5m|RSI|value"
    std::string encoded = inst.operand.toString();
    
    // 分割字符串
    size_t pos1 = encoded.find('|');
    size_t pos2 = encoded.find('|', pos1 + 1);
    
    if (pos1 == std::string::npos || pos2 == std::string::npos) {
        throw VMException("Invalid indicator encoding: " + encoded);
    }
    
    std::string timeframe = encoded.substr(0, pos1);
    std::string indicator = encoded.substr(pos1 + 1, pos2 - pos1 - 1);
    std::string field = encoded.substr(pos2 + 1);
    
    // 从Context获取或计算指标
    auto result = ctx.getOrCalculateIndicator(indicator, timeframe);
    
    // 提取字段值
    Value field_value = result.get(field);
    
    // 收集指标快照
    if (snapshots_collector_) {
        IndicatorSnapshot snapshot;
        snapshot.condition_id = encoded;  // 使用编码字符串作为ID
        snapshot.label = indicator + "(" + timeframe + ")." + field;
        snapshot.timeframe = timeframe;
        snapshot.indicator = indicator;
        snapshot.offset = 0;
        snapshot.status = "hit";  // 字节码VM执行时，指标已加载，视为命中
        
        // 收集指标结果
        for (const auto& [fname, series] : result.field_series) {
            if (!series.empty()) {
                snapshot.results[fname] = series.back();  // series已经是Value类型，不需要转换
            }
        }
        
        // 收集OHLCV快照
        try {
            if (ctx.hasKlines(timeframe)) {
                const auto& klines = ctx.getKlines(timeframe);
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
            // 忽略错误
        }
        
        snapshots_collector_->push_back(std::move(snapshot));
    }
    
    push(field_value);
}

void BytecodeVM::exec_load_env(const Instruction& inst, dsl::Context& ctx) {
    std::string var_name = inst.operand.toString();
    
    // 从Context获取环境变量
    if (var_name == "CURRENT_PRICE") {
        // 从Context获取当前价格
        push(Value::fromNumber(ctx.getCurrentPrice()));
    } else {
        // 尝试从Context的环境变量中获取
        try {
            Value val = ctx.getEnvVar(var_name);
            push(val);
        } catch (...) {
            throw VMException("Unknown environment variable: " + var_name);
        }
    }
}

// ============================================================================
// 算术指令实现
// ============================================================================

void BytecodeVM::exec_add() {
    auto b = pop();
    auto a = pop();
    push(Value::fromNumber(a.toNumber() + b.toNumber()));
}

void BytecodeVM::exec_sub() {
    auto b = pop();
    auto a = pop();
    push(Value::fromNumber(a.toNumber() - b.toNumber()));
}

void BytecodeVM::exec_mul() {
    auto b = pop();
    auto a = pop();
    push(Value::fromNumber(a.toNumber() * b.toNumber()));
}

void BytecodeVM::exec_div() {
    auto b = pop();
    auto a = pop();
    
    double divisor = b.toNumber();
    if (divisor == 0.0) {
        throw VMException("Division by zero");
    }
    
    push(Value::fromNumber(a.toNumber() / divisor));
}

void BytecodeVM::exec_mod() {
    auto b = pop();
    auto a = pop();
    
    double divisor = b.toNumber();
    if (divisor == 0.0) {
        throw VMException("Modulo by zero");
    }
    
    push(Value::fromNumber(std::fmod(a.toNumber(), divisor)));
}

void BytecodeVM::exec_neg() {
    auto a = pop();
    push(Value::fromNumber(-a.toNumber()));
}

// ============================================================================
// 比较指令实现
// ============================================================================

void BytecodeVM::exec_eq() {
    auto b = pop();
    auto a = pop();
    
    // 根据类型进行比较
    bool result = false;
    if (a.isNumber() && b.isNumber()) {
        result = (a.toNumber() == b.toNumber());
    } else if (a.isBoolean() && b.isBoolean()) {
        result = (a.toBool() == b.toBool());
    } else if (a.isString() && b.isString()) {
        result = (a.toString() == b.toString());
    }
    
    push(Value::fromBoolean(result));
}

void BytecodeVM::exec_ne() {
    auto b = pop();
    auto a = pop();
    
    bool result = false;
    if (a.isNumber() && b.isNumber()) {
        result = (a.toNumber() != b.toNumber());
    } else if (a.isBoolean() && b.isBoolean()) {
        result = (a.toBool() != b.toBool());
    } else if (a.isString() && b.isString()) {
        result = (a.toString() != b.toString());
    } else {
        result = true;  // 不同类型视为不等
    }
    
    push(Value::fromBoolean(result));
}

void BytecodeVM::exec_gt() {
    auto b = pop();
    auto a = pop();
    push(Value::fromBoolean(a.toNumber() > b.toNumber()));
}

void BytecodeVM::exec_lt() {
    auto b = pop();
    auto a = pop();
    push(Value::fromBoolean(a.toNumber() < b.toNumber()));
}

void BytecodeVM::exec_ge() {
    auto b = pop();
    auto a = pop();
    push(Value::fromBoolean(a.toNumber() >= b.toNumber()));
}

void BytecodeVM::exec_le() {
    auto b = pop();
    auto a = pop();
    push(Value::fromBoolean(a.toNumber() <= b.toNumber()));
}

// ============================================================================
// 逻辑指令实现
// ============================================================================

void BytecodeVM::exec_and() {
    auto b = pop();
    auto a = pop();
    push(Value::fromBoolean(a.toBool() && b.toBool()));
}

void BytecodeVM::exec_or() {
    auto b = pop();
    auto a = pop();
    push(Value::fromBoolean(a.toBool() || b.toBool()));
}

void BytecodeVM::exec_not() {
    auto a = pop();
    push(Value::fromBoolean(!a.toBool()));
}

// ============================================================================
// 聚合指令实现
// ============================================================================

void BytecodeVM::exec_all(int count) {
    if (count < 0) {
        throw VMException("ALL: invalid count");
    }
    
    // 用逻辑栈顶 stack_top_（而非容器容量 stack_.size()）做下溢检查
    if (static_cast<size_t>(count) > stack_top_) {
        throw VMException("ALL: stack underflow");
    }
    
    // 检查所有条件是否为真
    bool result = true;
    for (int i = 0; i < count; i++) {
        auto val = pop();
        if (!val.toBool()) {
            result = false;
            // 继续pop剩余的值
        }
    }
    
    push(Value::fromBoolean(result));
}

void BytecodeVM::exec_any(int count) {
    if (count < 0) {
        throw VMException("ANY: invalid count");
    }
    
    if (static_cast<size_t>(count) > stack_top_) {
        throw VMException("ANY: stack underflow");
    }
    
    // 检查是否至少有一个条件为真
    bool result = false;
    for (int i = 0; i < count; i++) {
        auto val = pop();
        if (val.toBool()) {
            result = true;
            // 继续pop剩余的值
        }
    }
    
    push(Value::fromBoolean(result));
}

void BytecodeVM::exec_none(int count) {
    if (count < 0) {
        throw VMException("NONE: invalid count");
    }
    
    if (static_cast<size_t>(count) > stack_top_) {
        throw VMException("NONE: stack underflow");
    }
    
    // 检查所有条件是否为假
    bool result = true;
    for (int i = 0; i < count; i++) {
        auto val = pop();
        if (val.toBool()) {
            result = false;
            // 继续pop剩余的值
        }
    }
    
    push(Value::fromBoolean(result));
}

} // namespace bytecode
} // namespace prophet

