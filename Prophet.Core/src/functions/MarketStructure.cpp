#include "prophet/functions/MarketStructure.hpp"
#include <algorithm>
#include <cmath>

namespace prophet {
namespace functions {

std::vector<StructureEvent> MarketStructureAnalyzer::detectStructureEvents(
    const std::vector<Kline>& klines,
    int swing_left_bars,
    int swing_right_bars,
    int period
) {
    std::vector<StructureEvent> events;
    
    if (klines.size() < 20) {
        return events;
    }
    
    // 检测所有摆动点
    auto swing_points = SwingPointAnalyzer::detectSwingPoints(klines, swing_left_bars, swing_right_bars, period);
    
    if (swing_points.size() < 3) {
        return events;
    }
    
    // 确定初始趋势
    TrendDirection current_trend = determineTrend(swing_points);
    
    // 遍历K线，检测结构事件
    for (size_t i = swing_right_bars; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        // 检测BOS
        if (current_trend == TrendDirection::UPTREND) {
            // 在上升趋势中，检测是否突破前一个摆动高点
            for (const auto& sp : swing_points) {
                if (sp.type == SwingPointType::HIGH && sp.index < static_cast<int>(i)) {
                    // 价格突破摆动高点
                    if (kline.close > sp.price && klines[i-1].close <= sp.price) {
                        StructureEvent event;
                        event.type = StructureType::BOS_BULLISH;
                        event.price = kline.close;
                        event.index = static_cast<int>(i);
                        event.swing_price = sp.price;
                        event.swing_index = sp.index;
                        event.prev_trend = current_trend;
                        event.new_trend = TrendDirection::UPTREND;
                        events.push_back(event);
                        break;
                    }
                }
            }
            
            // 检测CHoCH：价格跌破前一个摆动低点
            for (const auto& sp : swing_points) {
                if (sp.type == SwingPointType::LOW && sp.index < static_cast<int>(i)) {
                    if (kline.close < sp.price && klines[i-1].close >= sp.price) {
                        StructureEvent event;
                        event.type = StructureType::CHOCH_BEARISH;
                        event.price = kline.close;
                        event.index = static_cast<int>(i);
                        event.swing_price = sp.price;
                        event.swing_index = sp.index;
                        event.prev_trend = current_trend;
                        event.new_trend = TrendDirection::DOWNTREND;
                        events.push_back(event);
                        current_trend = TrendDirection::DOWNTREND;
                        break;
                    }
                }
            }
        } else if (current_trend == TrendDirection::DOWNTREND) {
            // 在下降趋势中，检测是否突破前一个摆动低点
            for (const auto& sp : swing_points) {
                if (sp.type == SwingPointType::LOW && sp.index < static_cast<int>(i)) {
                    if (kline.close < sp.price && klines[i-1].close >= sp.price) {
                        StructureEvent event;
                        event.type = StructureType::BOS_BEARISH;
                        event.price = kline.close;
                        event.index = static_cast<int>(i);
                        event.swing_price = sp.price;
                        event.swing_index = sp.index;
                        event.prev_trend = current_trend;
                        event.new_trend = TrendDirection::DOWNTREND;
                        events.push_back(event);
                        break;
                    }
                }
            }
            
            // 检测CHoCH：价格突破前一个摆动高点
            for (const auto& sp : swing_points) {
                if (sp.type == SwingPointType::HIGH && sp.index < static_cast<int>(i)) {
                    if (kline.close > sp.price && klines[i-1].close <= sp.price) {
                        StructureEvent event;
                        event.type = StructureType::CHOCH_BULLISH;
                        event.price = kline.close;
                        event.index = static_cast<int>(i);
                        event.swing_price = sp.price;
                        event.swing_index = sp.index;
                        event.prev_trend = current_trend;
                        event.new_trend = TrendDirection::UPTREND;
                        events.push_back(event);
                        current_trend = TrendDirection::UPTREND;
                        break;
                    }
                }
            }
        }
    }
    
    return events;
}

bool MarketStructureAnalyzer::detectBullishBOS(const std::vector<Kline>& klines, int swing_left_bars, int swing_right_bars) {
    auto events = detectStructureEvents(klines, swing_left_bars, swing_right_bars, 50);
    
    for (const auto& event : events) {
        if (event.type == StructureType::BOS_BULLISH) {
            return true;
        }
    }
    
    return false;
}

bool MarketStructureAnalyzer::detectBearishBOS(const std::vector<Kline>& klines, int swing_left_bars, int swing_right_bars) {
    auto events = detectStructureEvents(klines, swing_left_bars, swing_right_bars, 50);
    
    for (const auto& event : events) {
        if (event.type == StructureType::BOS_BEARISH) {
            return true;
        }
    }
    
    return false;
}

bool MarketStructureAnalyzer::detectBullishCHoCH(const std::vector<Kline>& klines, int swing_left_bars, int swing_right_bars) {
    auto events = detectStructureEvents(klines, swing_left_bars, swing_right_bars, 50);
    
    for (const auto& event : events) {
        if (event.type == StructureType::CHOCH_BULLISH) {
            return true;
        }
    }
    
    return false;
}

bool MarketStructureAnalyzer::detectBearishCHoCH(const std::vector<Kline>& klines, int swing_left_bars, int swing_right_bars) {
    auto events = detectStructureEvents(klines, swing_left_bars, swing_right_bars, 50);
    
    for (const auto& event : events) {
        if (event.type == StructureType::CHOCH_BEARISH) {
            return true;
        }
    }
    
    return false;
}

StructureEvent MarketStructureAnalyzer::getRecentStructureEvent(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int swing_left_bars,
    int swing_right_bars,
    int lookback
) {
    auto events = detectStructureEvents(klines, swing_left_bars, swing_right_bars, 100);
    
    // 过滤事件
    std::vector<StructureEvent> filtered_events;
    for (const auto& event : events) {
        if (type_filter == "bos" && (event.type == StructureType::BOS_BULLISH || event.type == StructureType::BOS_BEARISH)) {
            filtered_events.push_back(event);
        } else if (type_filter == "choch" && (event.type == StructureType::CHOCH_BULLISH || event.type == StructureType::CHOCH_BEARISH)) {
            filtered_events.push_back(event);
        } else if (type_filter == "bullish" && (event.type == StructureType::BOS_BULLISH || event.type == StructureType::CHOCH_BULLISH)) {
            filtered_events.push_back(event);
        } else if (type_filter == "bearish" && (event.type == StructureType::BOS_BEARISH || event.type == StructureType::CHOCH_BEARISH)) {
            filtered_events.push_back(event);
        } else if (type_filter == "any") {
            filtered_events.push_back(event);
        }
    }
    
    // 按索引排序（最近的在前）
    std::sort(filtered_events.begin(), filtered_events.end(),
        [](const StructureEvent& a, const StructureEvent& b) {
            return a.index > b.index;
        });
    
    if (lookback >= static_cast<int>(filtered_events.size())) {
        // 返回空事件
        StructureEvent empty_event;
        empty_event.type = StructureType::NONE;
        empty_event.price = 0.0;
        empty_event.index = -1;
        empty_event.swing_price = 0.0;
        empty_event.swing_index = -1;
        empty_event.prev_trend = TrendDirection::UNKNOWN;
        empty_event.new_trend = TrendDirection::UNKNOWN;
        return empty_event;
    }
    
    return filtered_events[lookback];
}

double MarketStructureAnalyzer::getStructurePrice(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int swing_left_bars,
    int swing_right_bars,
    int lookback
) {
    auto event = getRecentStructureEvent(klines, type_filter, swing_left_bars, swing_right_bars, lookback);
    return event.price;
}

double MarketStructureAnalyzer::getSwingPrice(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int swing_left_bars,
    int swing_right_bars,
    int lookback
) {
    auto event = getRecentStructureEvent(klines, type_filter, swing_left_bars, swing_right_bars, lookback);
    return event.swing_price;
}

int MarketStructureAnalyzer::countStructureEvents(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int swing_left_bars,
    int swing_right_bars,
    int period
) {
    auto events = detectStructureEvents(klines, swing_left_bars, swing_right_bars, period);
    
    int count = 0;
    for (const auto& event : events) {
        if (type_filter == "bos" && (event.type == StructureType::BOS_BULLISH || event.type == StructureType::BOS_BEARISH)) {
            count++;
        } else if (type_filter == "choch" && (event.type == StructureType::CHOCH_BULLISH || event.type == StructureType::CHOCH_BEARISH)) {
            count++;
        } else if (type_filter == "bullish" && (event.type == StructureType::BOS_BULLISH || event.type == StructureType::CHOCH_BULLISH)) {
            count++;
        } else if (type_filter == "bearish" && (event.type == StructureType::BOS_BEARISH || event.type == StructureType::CHOCH_BEARISH)) {
            count++;
        } else if (type_filter == "any") {
            count++;
        }
    }
    
    return count;
}

int MarketStructureAnalyzer::getCurrentTrend(
    const std::vector<Kline>& klines,
    int swing_left_bars,
    int swing_right_bars
) {
    auto swing_points = SwingPointAnalyzer::detectSwingPoints(klines, swing_left_bars, swing_right_bars, 100);
    
    if (swing_points.size() < 3) {
        return static_cast<int>(TrendDirection::UNKNOWN);
    }
    
    TrendDirection trend = determineTrend(swing_points);
    return static_cast<int>(trend);
}

TrendDirection MarketStructureAnalyzer::determineTrend(const std::vector<SwingPoint>& swing_points) {
    if (swing_points.size() < 3) {
        return TrendDirection::UNKNOWN;
    }
    
    // 获取最近的摆动高点和低点
    std::vector<SwingPoint> highs;
    std::vector<SwingPoint> lows;
    
    for (const auto& sp : swing_points) {
        if (sp.type == SwingPointType::HIGH) {
            highs.push_back(sp);
        } else if (sp.type == SwingPointType::LOW) {
            lows.push_back(sp);
        }
    }
    
    if (highs.size() < 2 || lows.size() < 2) {
        return TrendDirection::UNKNOWN;
    }
    
    // 按索引排序（最近的在后）
    std::sort(highs.begin(), highs.end(), [](const SwingPoint& a, const SwingPoint& b) {
        return a.index < b.index;
    });
    std::sort(lows.begin(), lows.end(), [](const SwingPoint& a, const SwingPoint& b) {
        return a.index < b.index;
    });
    
    // 检查高点和低点的趋势
    bool highs_rising = highs[highs.size()-1].price > highs[highs.size()-2].price;
    bool lows_rising = lows[lows.size()-1].price > lows[lows.size()-2].price;
    
    if (highs_rising && lows_rising) {
        return TrendDirection::UPTREND;
    } else if (!highs_rising && !lows_rising) {
        return TrendDirection::DOWNTREND;
    }
    
    return TrendDirection::UNKNOWN;
}

bool MarketStructureAnalyzer::detectBOS(
    const std::vector<Kline>& klines,
    const std::vector<SwingPoint>& swing_points,
    TrendDirection trend,
    size_t current_index
) {
    // 显式标记未使用的参数（当前实现暂时返回默认值）
    (void)klines;
    (void)swing_points;
    (void)trend;
    (void)current_index;
    
    // 实现省略，已在detectStructureEvents中实现
    return false;
}

bool MarketStructureAnalyzer::detectCHoCH(
    const std::vector<Kline>& klines,
    const std::vector<SwingPoint>& swing_points,
    TrendDirection trend,
    size_t current_index
) {
    // 显式标记未使用的参数（当前实现暂时返回默认值）
    (void)klines;
    (void)swing_points;
    (void)trend;
    (void)current_index;
    
    // 实现省略，已在detectStructureEvents中实现
    return false;
}

} // namespace functions
} // namespace prophet

