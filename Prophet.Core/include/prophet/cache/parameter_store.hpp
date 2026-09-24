#pragma once

#include <string>
#include <unordered_map>
#include <shared_mutex>
#include "prophet/common/types.hpp"

namespace prophet {
namespace cache {

/**
 * @brief 参数存储管理器
 * 
 * P1优化：从Context中分离出来的专职参数存储
 * 
 * 职责：
 * - 指标参数存储与查询
 * - 扁平化的高效访问
 * - 线程安全的并发访问
 * 
 * 性能优化点：
 * - P0已实现：单层Map替代三层嵌套
 * - 复合键："timeframe:indicator:param"
 * - 读写锁支持高并发读取
 */
class ParameterStore {
public:
    ParameterStore() = default;
    ~ParameterStore() = default;

    // 禁止拷贝，允许移动
    ParameterStore(const ParameterStore&) = delete;
    ParameterStore& operator=(const ParameterStore&) = delete;
    ParameterStore(ParameterStore&&) = default;
    ParameterStore& operator=(ParameterStore&&) = default;

    /**
     * @brief 设置参数
     * @param timeframe 时间周期
     * @param indicator_name 指标名称
     * @param param_name 参数名称
     * @param value 参数值
     */
    void set(const std::string& timeframe,
             const std::string& indicator_name,
             const std::string& param_name,
             const Value& value);

    /**
     * @brief 获取参数
     * @param timeframe 时间周期
     * @param indicator_name 指标名称
     * @param param_name 参数名称
     * @param default_value 默认值
     * @return 参数值
     */
    Value get(const std::string& timeframe,
              const std::string& indicator_name,
              const std::string& param_name,
              const Value& default_value = Value()) const;

    /**
     * @brief 检查参数是否存在
     */
    bool has(const std::string& timeframe,
             const std::string& indicator_name,
             const std::string& param_name) const;

    /**
     * @brief 清除参数
     * @param timeframe 时间周期（为空则清除全部）
     * @param indicator_name 指标名称（为空则清除该周期全部）
     */
    void clear(const std::string& timeframe = "",
               const std::string& indicator_name = "");

    /**
     * @brief 获取所有参数（用于调试）
     */
    std::unordered_map<std::string, Value> getAll() const;

    /**
     * @brief 获取参数数量
     */
    size_t size() const;

private:
    /**
     * @brief 生成参数键
     */
    std::string makeKey(const std::string& timeframe,
                        const std::string& indicator_name,
                        const std::string& param_name) const;

private:
    // 扁平化的参数存储：key = "timeframe:indicator:param"
    std::unordered_map<std::string, Value> parameters_;

    // 读写锁
    mutable std::shared_mutex mutex_;
};

} // namespace cache
} // namespace prophet

