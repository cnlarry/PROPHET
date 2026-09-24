#include "prophet/cache/kline_manager.hpp"

namespace prophet {
namespace cache {

uint64_t KlineManager::setKlines(const std::string& timeframe,
                                  const std::vector<prophet::functions::Kline>& klines) {
    std::unique_lock<std::shared_mutex> lock(mutex_);

    // 更新K线数据
    klines_[timeframe] = klines;

    // 递增版本号
    uint64_t& version = versions_[timeframe];
    ++version;

    return version;
}

std::vector<prophet::functions::Kline> KlineManager::getKlines(const std::string& timeframe) const {
    std::shared_lock<std::shared_mutex> lock(mutex_);

    auto it = klines_.find(timeframe);
    if (it != klines_.end()) {
        return it->second;
    }

    return {};
}

bool KlineManager::hasKlines(const std::string& timeframe) const {
    std::shared_lock<std::shared_mutex> lock(mutex_);
    return klines_.find(timeframe) != klines_.end();
}

uint64_t KlineManager::getVersion(const std::string& timeframe) const {
    std::shared_lock<std::shared_mutex> lock(mutex_);

    auto it = versions_.find(timeframe);
    if (it != versions_.end()) {
        return it->second;
    }

    return 0;
}

std::vector<std::string> KlineManager::getAllTimeframes() const {
    std::shared_lock<std::shared_mutex> lock(mutex_);

    std::vector<std::string> timeframes;
    timeframes.reserve(klines_.size());

    for (const auto& pair : klines_) {
        timeframes.push_back(pair.first);
    }

    return timeframes;
}

void KlineManager::clear(const std::string& timeframe) {
    std::unique_lock<std::shared_mutex> lock(mutex_);

    if (timeframe.empty()) {
        // 清除全部
        klines_.clear();
        versions_.clear();
    } else {
        // 清除指定时间周期
        klines_.erase(timeframe);
        versions_.erase(timeframe);
    }
}

KlineManager::Statistics KlineManager::getStatistics() const {
    std::shared_lock<std::shared_mutex> lock(mutex_);

    Statistics stats;
    stats.timeframe_count = klines_.size();
    stats.total_klines = 0;

    for (const auto& pair : klines_) {
        const std::string& tf = pair.first;
        size_t count = pair.second.size();

        stats.klines_per_timeframe[tf] = count;
        stats.total_klines += count;

        auto version_it = versions_.find(tf);
        if (version_it != versions_.end()) {
            stats.versions_per_timeframe[tf] = version_it->second;
        }
    }

    return stats;
}

} // namespace cache
} // namespace prophet

