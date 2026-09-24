/*
 * ============================================================================
 * 文件名：context.hpp
 * 功能说明：策略上下文头文件
 * 
 * 定义了策略运行时的数据管理类Context
 * 
 * Context类的职责：
 * 1. 存储和管理指标数据（MACD、RSI等技术指标的计算结果）
 * 2. 存储和管理参数数据（指标的配置参数）
 * 3. 存储和管理环境变量（当前价格、时间等运行时信息）
 * 4. 存储和管理K线数据（蜡烛图历史数据）
 * 5. 提供特殊计算功能（PRICE、希尔伯特变换等）
 * 
 * 数据结构：
 * - indicators_：存储指标结果的嵌套映射
 * - parameters_：存储参数的嵌套映射
 * - klines_：存储K线数据
 * ============================================================================
 */

#pragma once

#include "../common/types.hpp"
#include "../functions/Pattern.hpp"
#include "../functions/Base.hpp"
#include "../indicators/calculator.hpp"
#include "../indicators/registry.hpp"  // 需要包含此文件以使用 IndicatorParams
// P1优化：引入独立管理器
#include "../cache/indicator_cache.hpp"
#include "../cache/parameter_store.hpp"
#include "../cache/kline_manager.hpp"
#include <string>
#include <unordered_map>
#include <vector>
#include <cstdint>
#include <memory>

namespace prophet {
namespace dsl {

// 前向声明
class FunctionRegistry;

class Context {
public:

    Context();
    
    // P1优化：显式允许移动语义（因为缓存管理器默认允许移动）
    Context(Context&&) = default;
    Context& operator=(Context&&) = default;
    
    // 禁止拷贝（因为缓存管理器禁止拷贝）
    Context(const Context&) = delete;
    Context& operator=(const Context&) = delete;

    void setIndicator(const std::string& indicator_name, const std::string& timeframe, const IndicatorResult& result);

    IndicatorResult getIndicator(const std::string& indicator_name, const std::string& timeframe) const;

    Value getIndicatorField(const std::string& indicator_name, const std::string& timeframe, const std::string& field, int offset = 0) const;

    /**
     * 获取或自动计算指标字段（带 DSL 参数版本）
     * 
     * 当 indicator_params 非空时，直接走带参数的 getOrCalculateIndicator，
     * 避免无参缓存 key 命中默认参数结果导致参数丢失（RSI(7) 返回 RSI(14) 的问题）。
     * 
     * @param indicator_name 指标名称
     * @param timeframe 时间框架
     * @param field 字段名（如"value"、"histogram"）
     * @param offset 偏移量（0=最新，-1=前一根，范围[-100,0]）
     * @param indicator_params DSL 中指定的指标参数（如 RSI(7) 中的 [7]）
     * @return 字段值
     */
    Value getIndicatorField(const std::string& indicator_name,
                            const std::string& timeframe,
                            const std::string& field,
                            int offset,
                            const std::vector<Value>& indicator_params) const;

    /**
     * 获取或自动计算指标
     * 如果指标数据不存在，会自动调用Calculator计算并缓存结果
     * 
     * @param indicator_name 指标名称（如"MACD"、"RSI"）
     * @param timeframe 时间框架（如"5m"、"1h"）
     * @return 指标结果
     */
    IndicatorResult getOrCalculateIndicator(const std::string& indicator_name, const std::string& timeframe);
    
    /**
     * 获取或自动计算指标（新语法，支持参数列表）
     * 参数优先级：DSL参数 > JSON参数 > 默认值
     * 
     * @param indicator_name 指标名称
     * @param timeframe 时间框架
     * @param dsl_params DSL中指定的参数列表（位置参数）
     * @return 指标结果
     */
    IndicatorResult getOrCalculateIndicator(const std::string& indicator_name, 
                                           const std::string& timeframe,
                                           const std::vector<Value>& dsl_params);

    void setParameter(const std::string& indicator_name, const std::string& timeframe, const std::string& param_name, const Value& value);

    Value getParameter(const std::string& indicator_name, const std::string& timeframe, const std::string& param_name) const;

    void clearParameter(const std::string& indicator_name, const std::string& timeframe);

    /**
     * 清空所有指标缓存
     * 在K线数据更新后应该调用此方法，确保指标基于最新数据重新计算
     * 
     * @deprecated 不再需要手动清除缓存，系统会自动基于K线版本号失效缓存
     */
    void clearIndicators();
    
    /**
     * P2优化：开始新的evaluate会话
     * 清空上次会话的缓存，为新的一次evaluate做准备
     */
    void beginSession();
    
    /**
     * P2优化：结束当前evaluate会话
     * 清空会话缓存，释放内存
     */
    void endSession();

    // 局部变量管理（用于自定义函数的 @myvar）
    void setEnvVar(const std::string& var_name, const Value& value);
    Value getEnvVar(const std::string& var_name) const;
    void clearEnvVar(const std::string& var_name);
    
    // 🆕 v4.1: 全局变量管理
    void declareGlobalVariable(const std::string& var_name);  // 声明全局变量（未赋值）
    void setGlobalVariable(const std::string& var_name, const Value& value);  // 设置全局变量值
    Value getGlobalVariable(const std::string& var_name) const;  // 获取全局变量值
    bool hasGlobalVariable(const std::string& var_name) const;  // 检查全局变量是否存在
    bool isGlobalVariableAssigned(const std::string& var_name) const;  // 检查全局变量是否已赋值
    void clearGlobalVariables();  // 清空所有全局变量（每次GetSignal前调用）

    void setCurrentPrice(double price);

    double getCurrentPrice() const;

    void setCurrentTime(int64_t timestamp);

    int64_t getCurrentTime() const;

    // K线数据管理（v10.0 重构：固定300根窗口 + 增量更新）
    
    /**
     * 设置K线数据（全量设置，用于初始化）
     * @param timeframe 时间框架（如"5m", "1h", "1d"）
     * @param klines K线数据数组（如果超过300根，只保留最后300根）
     */
    void setKlines(const std::string& timeframe, const std::vector<Kline>& klines);
    
    /**
     * 增量追加单根K线（高性能，回测循环使用）
     * 自动维护300根窗口：追加新K线，丢弃最旧的
     * @param timeframe 时间框架
     * @param kline 新K线数据
     */
    void appendKline(const std::string& timeframe, const Kline& kline);
    
    /**
     * 增量追加多根K线（批量追加，处理跳跃场景）
     * @param timeframe 时间框架
     * @param klines 新K线数据数组
     */
    void appendKlines(const std::string& timeframe, const std::vector<Kline>& klines);

    const std::vector<Kline>& getKlines(const std::string& timeframe) const;

    bool hasKlines(const std::string& timeframe) const;
    
    size_t getKlineCount(const std::string& timeframe) const;

    Value computePrice(const std::string& timeframe, const std::string& method, int offset = 0) const;

    Value computeHilbertTransform(const std::string& timeframe, const std::string& method, const std::string& subfield = "") const;

    std::vector<std::string> getAllTimeframes() const;

    void convertAndSetKlines(const std::string& source_timeframe, const std::string& target_timeframe, int period = 0);

    // ====================================================================
    // 时间序列数据管理（用于FEARGREED等函数）
    // ====================================================================
    
    /**
     * 设置恐惧与贪婪指数序列数据
     * 
     * @param series 恐惧与贪婪指数数据序列（按日期降序排序，最新的在前）
     */
    void setFearGreedSeries(const std::vector<FearGreedData>& series);
    
    /**
     * 获取恐惧与贪婪指数序列数据
     * 
     * @return 恐惧与贪婪指数数据序列
     */
    const std::vector<FearGreedData>& getFearGreedSeries() const;
    
    /**
     * 检查是否有恐惧与贪婪指数数据
     * 
     * @return 如果有数据返回true，否则返回false
     */
    bool hasFearGreedData() const;

    /**
     * 设置资金费率序列数据
     * 
     * @param series 资金费率数据序列（按时间戳降序排序，最新的在前）
     */
    void setFundingRateSeries(const std::vector<FundingRateData>& series);
    
    /**
     * 获取资金费率序列数据
     * 
     * @return 资金费率数据序列
     */
    const std::vector<FundingRateData>& getFundingRateSeries() const;
    
    /**
     * 检查是否有资金费率数据
     * 
     * @return 如果有数据返回true，否则返回false
     */
    bool hasFundingRateData() const;

    /**
     * 设置多空比序列数据
     * 
     * @param series 多空比数据序列（按时间戳降序排序，最新的在前）
     */
    void setLongShortRatioSeries(const std::vector<LongShortRatioData>& series);
    
    /**
     * 获取多空比序列数据
     * 
     * @return 多空比数据序列
     */
    const std::vector<LongShortRatioData>& getLongShortRatioSeries() const;
    
    /**
     * 检查是否有多空比数据
     * 
     * @return 如果有数据返回true，否则返回false
     */
    bool hasLongShortRatioData() const;

    /**
     * 🆕 v4.0: 设置函数注册表（用于自定义函数调用）
     * 
     * @param func_reg 函数注册表指针（可以为nullptr）
     */
    void setFunctionRegistry(dsl::FunctionRegistry* func_reg) {
        function_registry_ = func_reg;
    }

    /**
     * 🆕 v4.0: 获取函数注册表（用于自定义函数调用）
     * 
     * @return 函数注册表指针（可能为nullptr）
     */
    dsl::FunctionRegistry* getFunctionRegistry() const {
        return function_registry_;
    }

private:
    // =======================================================================
    // P1优化：使用独立的缓存管理器（职责分离 + 线程安全）
    // =======================================================================
    
    // P1：指标缓存管理器（替代indicator_cache_）
    cache::IndicatorCache indicator_cache_;
    
    // P1：参数存储管理器（替代parameters_flat_）
    cache::ParameterStore parameter_store_;
    
    // P1：K线数据管理器（替代klines_和kline_versions_）
    cache::KlineManager kline_manager_;
    
    // v10.0：固定K线窗口大小
    static constexpr size_t KLINE_WINDOW_SIZE = 300;
    
    // =======================================================================
    // JSON参数存储（用于优先级查找）
    // =======================================================================
    // 注意：JSON参数通过setParameter()设置到parameter_store_中
    // 但为了支持优先级查找（DSL > JSON > 默认值），我们需要区分JSON参数
    // 这里存储JSON参数的原始映射，用于getParameter()的优先级查找
    std::unordered_map<std::string, std::unordered_map<std::string, std::unordered_map<std::string, Value>>> json_parameters_;

    // =======================================================================
    // 其他成员变量
    // =======================================================================
    
    // 局部变量存储（用于自定义函数的 @myvar，不用于全局环境变量）
    std::unordered_map<std::string, Value> env_vars_;
    
    // 🆕 v4.1: 全局变量存储
    std::unordered_map<std::string, Value> global_variables_;
    
    // 🆕 v4.1: 全局变量赋值状态跟踪（true = 已赋值，false = 仅声明未赋值）
    std::unordered_map<std::string, bool> global_var_assigned_;
    
    double current_price_;
    int64_t current_time_;
    
    // 时间序列数据
    std::vector<FearGreedData> fear_greed_series_;
    std::vector<FundingRateData> funding_rate_series_;
    std::vector<LongShortRatioData> long_short_series_;

    // 指标计算器，用于自动计算指标
    indicators::Calculator calculator_;
    
    // 🆕 v4.0: 函数注册表指针（用于自定义函数调用）
    FunctionRegistry* function_registry_;
    
    // ========================================================================
    // 辅助函数：数据验证
    // ========================================================================
    
    /**
     * 计算指标所需的最小K线数量
     * @param indicator_name 指标名称
     * @param params 指标参数
     * @return 所需的最小K线数量
     */
    int getMinRequiredKlines(const std::string& indicator_name, 
                             const indicators::IndicatorParams& params) const;
    
    /**
     * 验证指标参数的有效性
     * @param indicator_name 指标名称
     * @param params 指标参数
     * @throws EvaluatorException 如果参数无效
     */
    void validateIndicatorParams(const std::string& indicator_name,
                                const indicators::IndicatorParams& params) const;
};

} // namespace dsl
} // namespace prophet
