#include "prophet/cache/parameter_store.hpp"

namespace prophet {
namespace cache {

std::string ParameterStore::makeKey(const std::string& timeframe,
                                     const std::string& indicator_name,
                                     const std::string& param_name) const {
    return timeframe + ":" + indicator_name + ":" + param_name;
}

void ParameterStore::set(const std::string& timeframe,
                          const std::string& indicator_name,
                          const std::string& param_name,
                          const Value& value) {
    std::string key = makeKey(timeframe, indicator_name, param_name);

    std::unique_lock<std::shared_mutex> lock(mutex_);
    parameters_[key] = value;
}

Value ParameterStore::get(const std::string& timeframe,
                           const std::string& indicator_name,
                           const std::string& param_name,
                           const Value& default_value) const {
    std::string key = makeKey(timeframe, indicator_name, param_name);

    std::shared_lock<std::shared_mutex> lock(mutex_);

    auto it = parameters_.find(key);
    if (it != parameters_.end()) {
        return it->second;
    }

    return default_value;
}

bool ParameterStore::has(const std::string& timeframe,
                          const std::string& indicator_name,
                          const std::string& param_name) const {
    std::string key = makeKey(timeframe, indicator_name, param_name);

    std::shared_lock<std::shared_mutex> lock(mutex_);
    return parameters_.find(key) != parameters_.end();
}

void ParameterStore::clear(const std::string& timeframe,
                            const std::string& indicator_name) {
    std::unique_lock<std::shared_mutex> lock(mutex_);

    if (timeframe.empty()) {
        // 清除全部
        parameters_.clear();
        return;
    }

    std::string prefix;
    if (indicator_name.empty()) {
        // 清除指定时间周期的全部参数
        prefix = timeframe + ":";
    } else {
        // 清除指定指标的参数
        prefix = timeframe + ":" + indicator_name + ":";
    }

    for (auto it = parameters_.begin(); it != parameters_.end();) {
        if (it->first.find(prefix) == 0) {
            it = parameters_.erase(it);
        } else {
            ++it;
        }
    }
}

std::unordered_map<std::string, Value> ParameterStore::getAll() const {
    std::shared_lock<std::shared_mutex> lock(mutex_);
    return parameters_;
}

size_t ParameterStore::size() const {
    std::shared_lock<std::shared_mutex> lock(mutex_);
    return parameters_.size();
}

} // namespace cache
} // namespace prophet

