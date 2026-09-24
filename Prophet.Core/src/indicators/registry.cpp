/*
 * ============================================================================
 * 文件名：registry.cpp
 * 功能说明：指标注册表实现
 * 
 * 这个文件实现了指标的动态注册机制
 * 是实现"添加新指标无需修改 DSL 代码"的核心
 * ============================================================================
 */

#include "prophet/indicators/registry.hpp"
#include <stdexcept>
#include <algorithm>

namespace prophet::indicators {

// ============================================================================
// IndicatorRegistry 实现
// ============================================================================

IndicatorRegistry& IndicatorRegistry::getInstance() {
    // 使用局部静态变量实现线程安全的单例模式（C++11 保证）
    static IndicatorRegistry instance;
    return instance;
}

void IndicatorRegistry::registerIndicator(
    const std::string& name, 
    IndicatorCallable callable
) {
    // 注册指标（如果已存在则覆盖）
    indicators_[name] = callable;
}

bool IndicatorRegistry::hasIndicator(const std::string& name) const {
    return indicators_.find(name) != indicators_.end();
}

IndicatorCallable IndicatorRegistry::getIndicator(const std::string& name) const {
    auto it = indicators_.find(name);
    if (it == indicators_.end()) {
        throw std::runtime_error("Indicator not registered: " + name);
    }
    return it->second;
}

std::vector<std::string> IndicatorRegistry::getAllIndicators() const {
    std::vector<std::string> names;
    names.reserve(indicators_.size());
    
    for (const auto& [name, _] : indicators_) {
        names.push_back(name);
    }
    
    // 按字母顺序排序
    std::sort(names.begin(), names.end());
    
    return names;
}

size_t IndicatorRegistry::count() const {
    return indicators_.size();
}

void IndicatorRegistry::clear() {
    indicators_.clear();
}

} // namespace prophet::indicators