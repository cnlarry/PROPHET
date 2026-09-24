/*
 * ============================================================================
 * 文件名：jit.cpp
 * 功能说明：简化JIT编译器实现 - P2.2优化
 * ============================================================================
 */

#include "prophet/bytecode/jit.hpp"
#include <sstream>

namespace prophet {
namespace bytecode {

// ============================================================================
// 主编译接口
// ============================================================================

JITFunc JITCompiler::compile(const std::vector<Instruction>& code) {
    stats_.total_attempts++;
    
    // 1. 识别模式
    JITPattern pattern = detectPattern(code);
    
    if (pattern == JITPattern::UNKNOWN) {
        return nullptr;  // 无法优化
    }
    
    // 2. 提取参数
    PatternParams params = extractParams(pattern, code);
    
    // 3. 生成Lambda
    JITFunc func = generateLambda(pattern, params);
    
    if (func) {
        stats_.successful_compiles++;
        stats_.pattern_hits[static_cast<int>(pattern)]++;
    }
    
    return func;
}

// ============================================================================
// 模式识别
// ============================================================================

JITPattern JITCompiler::detectPattern(const std::vector<Instruction>& code) {
    if (code.size() < 3) {
        return JITPattern::UNKNOWN;
    }
    
    // Pattern 1: LOAD_INDICATOR + LOAD_CONST + CMP + RETURN
    // 示例: $(5m).RSI().value < 30
    if (code.size() == 4 &&
        code[0].opcode == Opcode::LOAD_INDICATOR &&
        code[1].opcode == Opcode::LOAD_CONST &&
        (code[2].opcode == Opcode::LT || code[2].opcode == Opcode::GT ||
         code[2].opcode == Opcode::LE || code[2].opcode == Opcode::GE ||
         code[2].opcode == Opcode::EQ || code[2].opcode == Opcode::NE) &&
        code[3].opcode == Opcode::RETURN) {
        return JITPattern::INDICATOR_CMP_CONST;
    }
    
    // Pattern 2: LOAD_INDICATOR + LOAD_INDICATOR + CMP + RETURN
    // 示例: $(5m).MACD().histogram > $(5m).MACD().signal
    if (code.size() == 4 &&
        code[0].opcode == Opcode::LOAD_INDICATOR &&
        code[1].opcode == Opcode::LOAD_INDICATOR &&
        (code[2].opcode == Opcode::LT || code[2].opcode == Opcode::GT ||
         code[2].opcode == Opcode::LE || code[2].opcode == Opcode::GE) &&
        code[3].opcode == Opcode::RETURN) {
        return JITPattern::INDICATOR_CMP_INDICATOR;
    }
    
    // TODO: 更多模式...
    
    return JITPattern::UNKNOWN;
}

// ============================================================================
// 参数提取
// ============================================================================

PatternParams JITCompiler::extractParams(JITPattern pattern, const std::vector<Instruction>& code) {
    PatternParams params;
    
    switch (pattern) {
        case JITPattern::INDICATOR_CMP_CONST: {
            // 解析 "timeframe|indicator|field"
            std::string encoded = code[0].operand.toString();
            size_t pos1 = encoded.find('|');
            size_t pos2 = encoded.find('|', pos1 + 1);
            
            params.timeframe = encoded.substr(0, pos1);
            params.indicator = encoded.substr(pos1 + 1, pos2 - pos1 - 1);
            params.field = encoded.substr(pos2 + 1);
            params.constant = code[1].operand.toNumber();
            params.cmp_op = code[2].opcode;
            break;
        }
        
        case JITPattern::INDICATOR_CMP_INDICATOR: {
            // 第一个指标
            std::string encoded1 = code[0].operand.toString();
            size_t pos1 = encoded1.find('|');
            size_t pos2 = encoded1.find('|', pos1 + 1);
            
            params.timeframe = encoded1.substr(0, pos1);
            params.indicator = encoded1.substr(pos1 + 1, pos2 - pos1 - 1);
            params.field = encoded1.substr(pos2 + 1);
            
            // 第二个指标
            std::string encoded2 = code[1].operand.toString();
            pos1 = encoded2.find('|');
            pos2 = encoded2.find('|', pos1 + 1);
            
            params.timeframe2 = encoded2.substr(0, pos1);
            params.indicator2 = encoded2.substr(pos1 + 1, pos2 - pos1 - 1);
            params.field2 = encoded2.substr(pos2 + 1);
            params.cmp_op = code[2].opcode;
            break;
        }
        
        default:
            break;
    }
    
    return params;
}

// ============================================================================
// Lambda生成
// ============================================================================

JITFunc JITCompiler::generateLambda(JITPattern pattern, const PatternParams& params) {
    switch (pattern) {
        case JITPattern::INDICATOR_CMP_CONST:
            return generate_indicator_cmp_const(params);
        case JITPattern::INDICATOR_CMP_INDICATOR:
            return generate_indicator_cmp_indicator(params);
        default:
            return nullptr;
    }
}

// ============================================================================
// 特定模式的Lambda生成器
// ============================================================================

JITFunc JITCompiler::generate_indicator_cmp_const(const PatternParams& params) {
    // 捕获参数到lambda
    std::string tf = params.timeframe;
    std::string ind = params.indicator;
    std::string fld = params.field;
    double threshold = params.constant;
    Opcode op = params.cmp_op;
    
    // 生成优化lambda
        return [=](dsl::Context& ctx) -> bool {
            // 直接访问指标缓存（跳过字节码VM）
            auto result = ctx.getOrCalculateIndicator(ind, tf);
            double value = result.get(fld).toNumber();
            
            // 直接比较（内联优化）
            switch (op) {
                case Opcode::LT: return value < threshold;
                case Opcode::GT: return value > threshold;
                case Opcode::LE: return value <= threshold;
                case Opcode::GE: return value >= threshold;
                case Opcode::EQ: return value == threshold;
                case Opcode::NE: return value != threshold;
                default: return false;
            }
        };
}

JITFunc JITCompiler::generate_indicator_cmp_indicator(const PatternParams& params) {
    // 捕获参数
    std::string tf1 = params.timeframe;
    std::string ind1 = params.indicator;
    std::string fld1 = params.field;
    std::string tf2 = params.timeframe2;
    std::string ind2 = params.indicator2;
    std::string fld2 = params.field2;
    Opcode op = params.cmp_op;
    
    return [=](dsl::Context& ctx) -> bool {
        auto result1 = ctx.getOrCalculateIndicator(ind1, tf1);
        double value1 = result1.get(fld1).toNumber();
        
        auto result2 = ctx.getOrCalculateIndicator(ind2, tf2);
        double value2 = result2.get(fld2).toNumber();
        
        switch (op) {
            case Opcode::LT: return value1 < value2;
            case Opcode::GT: return value1 > value2;
            case Opcode::LE: return value1 <= value2;
            case Opcode::GE: return value1 >= value2;
            default: return false;
        }
    };
}

JITFunc JITCompiler::generate_simple_all_2(const PatternParams& params) {
    // TODO: 实现ALL{cond1, cond2}的优化
    (void)params;  // 避免未使用参数警告
    return nullptr;
}

JITFunc JITCompiler::generate_simple_any_2(const PatternParams& params) {
    // TODO: 实现ANY{cond1, cond2}的优化
    (void)params;  // 避免未使用参数警告
    return nullptr;
}

} // namespace bytecode
} // namespace prophet

