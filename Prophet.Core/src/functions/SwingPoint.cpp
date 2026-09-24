#include "prophet/functions/SwingPoint.hpp"
#include <algorithm>
#include <cmath>
#include <limits>

namespace prophet {
namespace functions {

std::vector<SwingPoint> SwingPointAnalyzer::detectSwingPoints(
    const std::vector<Kline>& klines,
    int left_bars,
    int right_bars,
    int period
) {
    std::vector<SwingPoint> swing_points;
    
    if (klines.size() < static_cast<size_t>(left_bars + right_bars + 1)) {
        return swing_points;
    }
    
    int actual_period = std::min(period, static_cast<int>(klines.size()) - right_bars);
    size_t start_index = klines.size() - actual_period;
    
    // 确保start_index至少为left_bars
    if (start_index < static_cast<size_t>(left_bars)) {
        start_index = left_bars;
    }
    
    // 遍历K线，寻找摆动点
    for (size_t i = start_index; i < klines.size() - right_bars; ++i) {
        // 检查摆动高点
        if (isSwingHigh(klines, i, left_bars, right_bars)) {
            SwingPoint sp;
            sp.type = SwingPointType::HIGH;
            sp.price = klines[i].high;
            sp.index = static_cast<int>(i);
            sp.left_bars = left_bars;
            sp.right_bars = right_bars;
            sp.strength = calculateStrength(klines, i, SwingPointType::HIGH, left_bars, right_bars);
            sp.is_broken = checkIfBroken(klines, i, SwingPointType::HIGH, sp.price);
            swing_points.push_back(sp);
        }
        
        // 检查摆动低点
        if (isSwingLow(klines, i, left_bars, right_bars)) {
            SwingPoint sp;
            sp.type = SwingPointType::LOW;
            sp.price = klines[i].low;
            sp.index = static_cast<int>(i);
            sp.left_bars = left_bars;
            sp.right_bars = right_bars;
            sp.strength = calculateStrength(klines, i, SwingPointType::LOW, left_bars, right_bars);
            sp.is_broken = checkIfBroken(klines, i, SwingPointType::LOW, sp.price);
            swing_points.push_back(sp);
        }
    }
    
    return swing_points;
}

std::vector<SwingPoint> SwingPointAnalyzer::detectSwingHighs(
    const std::vector<Kline>& klines,
    int left_bars,
    int right_bars,
    int period
) {
    auto all_swings = detectSwingPoints(klines, left_bars, right_bars, period);
    std::vector<SwingPoint> swing_highs;
    
    for (const auto& sp : all_swings) {
        if (sp.type == SwingPointType::HIGH) {
            swing_highs.push_back(sp);
        }
    }
    
    return swing_highs;
}

std::vector<SwingPoint> SwingPointAnalyzer::detectSwingLows(
    const std::vector<Kline>& klines,
    int left_bars,
    int right_bars,
    int period
) {
    auto all_swings = detectSwingPoints(klines, left_bars, right_bars, period);
    std::vector<SwingPoint> swing_lows;
    
    for (const auto& sp : all_swings) {
        if (sp.type == SwingPointType::LOW) {
            swing_lows.push_back(sp);
        }
    }
    
    return swing_lows;
}

bool SwingPointAnalyzer::hasSwingHigh(const std::vector<Kline>& klines, int left_bars, int right_bars) {
    auto swing_highs = detectSwingHighs(klines, left_bars, right_bars, 50);
    return !swing_highs.empty();
}

bool SwingPointAnalyzer::hasSwingLow(const std::vector<Kline>& klines, int left_bars, int right_bars) {
    auto swing_lows = detectSwingLows(klines, left_bars, right_bars, 50);
    return !swing_lows.empty();
}

SwingPoint SwingPointAnalyzer::getRecentSwingPoint(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int left_bars,
    int right_bars,
    int lookback
) {
    std::vector<SwingPoint> swings;
    
    if (type_filter == "high") {
        swings = detectSwingHighs(klines, left_bars, right_bars, 100);
    } else if (type_filter == "low") {
        swings = detectSwingLows(klines, left_bars, right_bars, 100);
    } else {
        swings = detectSwingPoints(klines, left_bars, right_bars, 100);
    }
    
    // 按索引排序（最近的在前）
    std::sort(swings.begin(), swings.end(), 
        [](const SwingPoint& a, const SwingPoint& b) {
            return a.index > b.index;
        });
    
    if (lookback >= static_cast<int>(swings.size())) {
        // 返回空的SwingPoint
        SwingPoint empty_sp;
        empty_sp.type = SwingPointType::NONE;
        empty_sp.price = 0.0;
        empty_sp.index = -1;
        empty_sp.left_bars = 0;
        empty_sp.right_bars = 0;
        empty_sp.strength = 0;
        empty_sp.is_broken = false;
        return empty_sp;
    }
    
    return swings[lookback];
}

double SwingPointAnalyzer::getSwingPrice(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int left_bars,
    int right_bars,
    int lookback
) {
    auto sp = getRecentSwingPoint(klines, type_filter, left_bars, right_bars, lookback);
    return sp.price;
}

int SwingPointAnalyzer::getSwingStrength(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int left_bars,
    int right_bars,
    int lookback
) {
    auto sp = getRecentSwingPoint(klines, type_filter, left_bars, right_bars, lookback);
    return sp.strength;
}

bool SwingPointAnalyzer::isSwingBroken(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int left_bars,
    int right_bars,
    int lookback
) {
    auto sp = getRecentSwingPoint(klines, type_filter, left_bars, right_bars, lookback);
    return sp.is_broken;
}

int SwingPointAnalyzer::countSwingPoints(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int left_bars,
    int right_bars,
    int period
) {
    std::vector<SwingPoint> swings;
    
    if (type_filter == "high") {
        swings = detectSwingHighs(klines, left_bars, right_bars, period);
    } else if (type_filter == "low") {
        swings = detectSwingLows(klines, left_bars, right_bars, period);
    } else {
        swings = detectSwingPoints(klines, left_bars, right_bars, period);
    }
    
    return static_cast<int>(swings.size());
}

double SwingPointAnalyzer::getDistanceToSwing(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int left_bars,
    int right_bars
) {
    auto sp = getRecentSwingPoint(klines, type_filter, left_bars, right_bars, 0);
    
    if (sp.type == SwingPointType::NONE || klines.empty()) {
        return 0.0;
    }
    
    double current_price = klines.back().close;
    double distance_pct = ((current_price - sp.price) / sp.price) * 100.0;
    
    return distance_pct;
}

bool SwingPointAnalyzer::isSwingHigh(const std::vector<Kline>& klines, size_t index, int left_bars, int right_bars) {
    if (index < static_cast<size_t>(left_bars) || index >= klines.size() - right_bars) {
        return false;
    }
    
    double current_high = klines[index].high;
    
    // 检查左侧K线
    for (int i = 1; i <= left_bars; ++i) {
        if (klines[index - i].high >= current_high) {
            return false;
        }
    }
    
    // 检查右侧K线
    for (int i = 1; i <= right_bars; ++i) {
        if (klines[index + i].high >= current_high) {
            return false;
        }
    }
    
    return true;
}

bool SwingPointAnalyzer::isSwingLow(const std::vector<Kline>& klines, size_t index, int left_bars, int right_bars) {
    if (index < static_cast<size_t>(left_bars) || index >= klines.size() - right_bars) {
        return false;
    }
    
    double current_low = klines[index].low;
    
    // 检查左侧K线
    for (int i = 1; i <= left_bars; ++i) {
        if (klines[index - i].low <= current_low) {
            return false;
        }
    }
    
    // 检查右侧K线
    for (int i = 1; i <= right_bars; ++i) {
        if (klines[index + i].low <= current_low) {
            return false;
        }
    }
    
    return true;
}

int SwingPointAnalyzer::calculateStrength(const std::vector<Kline>& klines, size_t index, SwingPointType type, int left_bars, int right_bars) {
    double extreme_price = 0.0;
    double avg_distance = 0.0;
    int count = 0;
    
    if (type == SwingPointType::HIGH) {
        extreme_price = klines[index].high;
        
        // 计算左右K线的平均距离
        for (int i = 1; i <= left_bars; ++i) {
            avg_distance += extreme_price - klines[index - i].high;
            count++;
        }
        for (int i = 1; i <= right_bars; ++i) {
            avg_distance += extreme_price - klines[index + i].high;
            count++;
        }
    } else if (type == SwingPointType::LOW) {
        extreme_price = klines[index].low;
        
        // 计算左右K线的平均距离
        for (int i = 1; i <= left_bars; ++i) {
            avg_distance += klines[index - i].low - extreme_price;
            count++;
        }
        for (int i = 1; i <= right_bars; ++i) {
            avg_distance += klines[index + i].low - extreme_price;
            count++;
        }
    }
    
    if (count > 0) {
        avg_distance /= count;
    }
    
    // 转换为百分比强度（0-100）
    double pct_distance = extreme_price > 0 ? (avg_distance / extreme_price) * 1000.0 : 0.0;
    int strength = static_cast<int>(std::min(100.0, pct_distance));
    
    return std::max(0, strength);
}

bool SwingPointAnalyzer::checkIfBroken(const std::vector<Kline>& klines, size_t swing_index, SwingPointType type, double price) {
    // 检查摆动点形成后，价格是否突破该点
    for (size_t i = swing_index + 1; i < klines.size(); ++i) {
        if (type == SwingPointType::HIGH) {
            // 摆动高点：如果价格收盘在该点以上，则被突破
            if (klines[i].close > price) {
                return true;
            }
        } else if (type == SwingPointType::LOW) {
            // 摆动低点：如果价格收盘在该点以下，则被突破
            if (klines[i].close < price) {
                return true;
            }
        }
    }
    
    return false;
}

} // namespace functions
} // namespace prophet

