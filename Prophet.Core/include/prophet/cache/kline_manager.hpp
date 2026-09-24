#pragma once

#include <string>
#include <vector>
#include <unordered_map>
#include <shared_mutex>
#include "prophet/functions/Pattern.hpp"

namespace prophet {
namespace cache {

/**
 * @brief K线数据管理器
 * 
 * P1优化：从Context中分离出来的专职K线管理
 * 
 * 职责：
 * - K线数据存储
 * - K线版本追踪（用于缓存失效）
 * - 线程安全的并发访问
 * 
 * 性能优化点：
 * - 版本号机制：K线更新时递增版本
 * - 缓存系统据此判断是否需要重新计算
 * - 读写锁支持高并发读取
 */
class KlineManager {
public:
    KlineManager() = default;
    ~KlineManager() = default;

    // 禁止拷贝，允许移动
    KlineManager(const KlineManager&) = delete;
    KlineManager& operator=(const KlineManager&) = delete;
    KlineManager(KlineManager&&) = default;
    KlineManager& operator=(KlineManager&&) = default;

    /**
     * @brief 设置K线数据
     * @param timeframe 时间周期
     * @param klines K线数据
     * @return 新的版本号
     */
    uint64_t setKlines(const std::string& timeframe,
                       const std::vector<prophet::functions::Kline>& klines);

    /**
     * @brief 获取K线数据
     * @param timeframe 时间周期
     * @return K线数据（如果不存在返回空vector）
     */
    std::vector<prophet::functions::Kline> getKlines(const std::string& timeframe) const;

    /**
     * @brief 检查K线是否存在
     */
    bool hasKlines(const std::string& timeframe) const;

    /**
     * @brief 获取K线版本号
     * @param timeframe 时间周期
     * @return 版本号（0表示不存在）
     */
    uint64_t getVersion(const std::string& timeframe) const;

    /**
     * @brief 获取所有时间周期
     */
    std::vector<std::string> getAllTimeframes() const;

    /**
     * @brief 清除K线数据
     * @param timeframe 时间周期（为空则清除全部）
     */
    void clear(const std::string& timeframe = "");

    /**
     * @brief 获取统计信息
     */
    struct Statistics {
        size_t timeframe_count;
        size_t total_klines;
        std::unordered_map<std::string, size_t> klines_per_timeframe;
        std::unordered_map<std::string, uint64_t> versions_per_timeframe;
    };

    Statistics getStatistics() const;

private:
    // K线数据存储
    std::unordered_map<std::string, std::vector<prophet::functions::Kline>> klines_;

    // K线版本追踪：每次更新递增
    std::unordered_map<std::string, uint64_t> versions_;

    // 读写锁
    mutable std::shared_mutex mutex_;
};

} // namespace cache
} // namespace prophet

