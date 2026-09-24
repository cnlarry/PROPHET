#pragma once

#include <string>
#include <map>
#include <vector>
#include <functional>
#include <memory>
#include "prophet/common/types.hpp"

namespace prophet::indicators {

// 前向声明
class Calculator;

/**
 * 指标参数容器
 * 用于传递指标所需的参数
 */
class IndicatorParams {
public:
    IndicatorParams() = default;
    
    // 设置参数
    void set(const std::string& key, double value) {
        params_[key] = value;
    }
    
    // 获取整数参数
    int get_int(const std::string& key, int default_value) const {
        auto it = params_.find(key);
        if (it == params_.end()) {
            return default_value;
        }
        return static_cast<int>(it->second);
    }
    
    // 获取浮点数参数
    double get_double(const std::string& key, double default_value) const {
        auto it = params_.find(key);
        if (it == params_.end()) {
            return default_value;
        }
        return it->second;
    }
    
    // 检查参数是否存在
    bool has(const std::string& key) const {
        return params_.find(key) != params_.end();
    }
    
    // 获取所有参数
    const std::map<std::string, double>& getAll() const {
        return params_;
    }

private:
    std::map<std::string, double> params_;
};

/**
 * 指标计算函数类型
 * 
 * 参数：
 *   calculator - Calculator 实例的引用
 *   close - 收盘价序列
 *   high - 最高价序列
 *   low - 最低价序列
 *   volume - 成交量序列
 *   params - 指标参数
 * 
 * 返回：
 *   IndicatorResult - 指标计算结果
 */
using IndicatorCallable = std::function<IndicatorResult(
    Calculator& calculator,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    const IndicatorParams& params
)>;

/**
 * 指标注册表
 * 
 * 功能：
 * 1. 动态注册指标计算函数
 * 2. 查找和调用已注册的指标
 * 3. 列出所有可用指标
 * 
 * 优势：
 * - 无需修改 DSL 解析器代码
 * - 支持运行时扩展
 * - 降低耦合度
 * 
 * 使用方式：
 * 
 *   // 注册指标
 *   auto& registry = IndicatorRegistry::getInstance();
 *   registry.registerIndicator("MACD", [](
 *       Calculator& calc,
 *       const std::vector<double>& close,
 *       const std::vector<double>& high,
 *       const std::vector<double>& low,
 *       const std::vector<double>& volume,
 *       const IndicatorParams& params
 *   ) {
 *       int fast = params.get_int("FAST_PERIOD", 12);
 *       int slow = params.get_int("SLOW_PERIOD", 26);
 *       int signal = params.get_int("SIGNAL_PERIOD", 9);
 *       return calc.MACD(close, fast, slow, signal);
 *   });
 * 
 *   // 调用指标
 *   if (registry.hasIndicator("MACD")) {
 *       auto callable = registry.getIndicator("MACD");
 *       IndicatorResult result = callable(calc, close, high, low, volume, params);
 *   }
 */
class IndicatorRegistry {
public:
    /**
     * 获取单例实例
     * 
     * @return IndicatorRegistry& 注册表的全局唯一实例
     */
    static IndicatorRegistry& getInstance();
    
    /**
     * 注册指标
     * 
     * @param name 指标名称（如 "MACD"、"RSI"）
     * @param callable 指标计算函数
     * 
     * 注意：如果指标已存在，会覆盖原有注册
     */
    void registerIndicator(const std::string& name, IndicatorCallable callable);
    
    /**
     * 检查指标是否已注册
     * 
     * @param name 指标名称
     * @return bool 是否存在
     */
    bool hasIndicator(const std::string& name) const;
    
    /**
     * 获取指标计算函数
     * 
     * @param name 指标名称
     * @return IndicatorCallable 计算函数
     * @throws std::runtime_error 如果指标不存在
     */
    IndicatorCallable getIndicator(const std::string& name) const;
    
    /**
     * 获取所有已注册的指标名称
     * 
     * @return std::vector<std::string> 指标名称列表
     */
    std::vector<std::string> getAllIndicators() const;
    
    /**
     * 获取已注册指标的数量
     * 
     * @return size_t 指标数量
     */
    size_t count() const;
    
    /**
     * 清空所有注册（主要用于测试）
     */
    void clear();

private:
    IndicatorRegistry() = default;
    ~IndicatorRegistry() = default;
    
    // 禁止拷贝和赋值
    IndicatorRegistry(const IndicatorRegistry&) = delete;
    IndicatorRegistry& operator=(const IndicatorRegistry&) = delete;
    
    // 存储指标名称到计算函数的映射
    std::map<std::string, IndicatorCallable> indicators_;
};

/**
 * 指标自动注册辅助宏
 * 
 * 用法：
 *   REGISTER_INDICATOR(MACD, [](
 *       Calculator& calc,
 *       const std::vector<double>& close,
 *       const std::vector<double>& high,
 *       const std::vector<double>& low,
 *       const std::vector<double>& volume,
 *       const IndicatorParams& params
 *   ) {
 *       // 指标计算逻辑
 *       return calc.MACD(...);
 *   });
 * 
 * 原理：
 * - 创建一个静态对象
 * - 在程序启动时自动执行构造函数
 * - 构造函数中完成注册
 */
#define REGISTER_INDICATOR(indicator_name, callable) \
    namespace { \
        struct indicator_name##Registrar { \
            indicator_name##Registrar() { \
                prophet::indicators::IndicatorRegistry::getInstance() \
                    .registerIndicator(#indicator_name, callable); \
            } \
        }; \
        static indicator_name##Registrar indicator_name##RegistrarInstance; \
    }

} // namespace prophet::indicators

