/**
 * @file Sequence.hpp
 * @brief 序列条件函数声明 - CONSECUTIVE, COUNT
 * @date 2025-10-31
 */

#pragma once

#include "prophet/common/types.hpp"
#include <vector>
#include <string>

// Forward declarations
namespace prophet::functions {
    class FunctionRegistry;
}

namespace prophet::functions {

/**
 * @brief 序列条件评估器
 * 
 * 用于评估时间序列数据是否满足特定条件：
 * - CONSECUTIVE模式：检查连续N期是否都满足条件
 * - COUNT模式：统计N期中有多少期满足条件
 * 
 * 注意：内部实现使用SeriesData，但不在头文件中暴露
 */
class SequenceConditionEvaluator {
public:
    /**
     * @brief 评估模式
     */
    enum class Mode {
        CONSECUTIVE,  ///< 连续满足模式
        COUNT         ///< 计数模式
    };
    
    /**
     * @brief 支持的条件类型
     */
    enum class Condition {
        RISING,          ///< 连续上涨（严格>）
        FALLING,         ///< 连续下跌（严格<）
        INCREASING,      ///< 连续增加（>=，适合volume）
        DECREASING,      ///< 连续减少（<=）
        ABOVE,           ///< 持续高于阈值
        BELOW,           ///< 持续低于阈值
        BETWEEN,         ///< 持续在区间内
        CROSSES_ABOVE,   ///< 向上穿越阈值
        CROSSES_BELOW    ///< 向下穿越阈值
    };
    
    /**
     * @brief 评估序列条件（内部方法，不对外暴露SeriesData）
     * 
     * @param data K线数据（内部使用SeriesData）
     * @param field 字段名（close, open, high, low, volume）
     * @param periods 期数
     * @param mode 评估模式（CONSECUTIVE或COUNT）
     * @param condition 条件类型
     * @param params 条件参数（如阈值）
     * @return IndicatorResult 包含boolean（CONSECUTIVE）或number（COUNT）
     */
    static IndicatorResult evaluateInternal(
        const std::vector<double>& values,
        int periods,
        Mode mode,
        Condition condition,
        const std::vector<double>& params
    );
    
    /**
     * @brief 从字符串解析条件类型
     * 
     * @param name 条件名称（如"rising", "above"等）
     * @return Condition 条件枚举值
     * @throws std::runtime_error 如果条件名称无效
     */
    static Condition parseCondition(const std::string& name);

private:
    /**
     * @brief 评估连续条件（CONSECUTIVE模式）
     * 
     * @param values 字段值序列
     * @param periods 期数
     * @param condition 条件类型
     * @param params 条件参数
     * @return bool 是否连续满足
     */
    static bool evaluateConsecutive(
        const std::vector<double>& values,
        int periods,
        Condition condition,
        const std::vector<double>& params
    );
    
    /**
     * @brief 评估计数条件（COUNT模式）
     * 
     * @param values 字段值序列
     * @param periods 期数
     * @param condition 条件类型
     * @param params 条件参数
     * @return int 满足条件的期数
     */
    static int evaluateCount(
        const std::vector<double>& values,
        int periods,
        Condition condition,
        const std::vector<double>& params
    );
    
    /**
     * @brief 评估单个周期是否满足条件
     * 
     * @param values 字段值序列
     * @param idx 当前周期索引
     * @param condition 条件类型
     * @param params 条件参数
     * @return bool 当前周期是否满足
     */
    static bool evaluateSinglePeriod(
        const std::vector<double>& values,
        size_t idx,
        Condition condition,
        const std::vector<double>& params
    );
};

/**
 * @brief 注册序列条件函数到函数注册表
 * 
 * 注册以下函数：
 * - CONSECUTIVE: 连续条件函数
 * - CONSEC: CONSECUTIVE的别名
 * - COUNT_DATA: 条件计数函数（数据函数，非信号函数）
 * 
 * @param registry 函数注册表实例
 */
void register_sequence_functions(FunctionRegistry& registry);

} // namespace prophet::functions

