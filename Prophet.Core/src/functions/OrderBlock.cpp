#include "prophet/functions/OrderBlock.hpp"
#include <algorithm>
#include <cmath>
#include <limits>

namespace prophet {
namespace functions {

std::vector<OrderBlock> OrderBlockAnalyzer::detectOrderBlocks(
    const std::vector<Kline>& klines, 
    int period,
    int min_candles,
    int strength_threshold
) {
    std::vector<OrderBlock> order_blocks;
    
    if (klines.size() < static_cast<size_t>(min_candles + 3)) {
        return order_blocks;
    }
    
    int actual_period = std::min(period, static_cast<int>(klines.size()) - min_candles);
    size_t start_index = klines.size() - actual_period;
    
    // 遍历K线，寻找订单块
    for (size_t i = start_index; i < klines.size() - min_candles; ++i) {
        OrderBlockType type = OrderBlockType::NONE;
        
        // 检查是否是看涨订单块
        if (isBullishOrderBlock(klines, i)) {
            type = OrderBlockType::BULLISH;
        }
        // 检查是否是看跌订单块
        else if (isBearishOrderBlock(klines, i)) {
            type = OrderBlockType::BEARISH;
        }
        
        if (type != OrderBlockType::NONE) {
            // 计算强度
            int strength = calculateStrength(klines, i, type);
            
            if (strength >= strength_threshold) {
                OrderBlock ob;
                ob.type = type;
                ob.top = klines[i].high;
                ob.bottom = klines[i].low;
                ob.mid = (ob.top + ob.bottom) / 2.0;
                ob.formed_index = static_cast<int>(i);
                ob.strength = strength;
                ob.is_tested = checkIfTested(klines, i, ob.top, ob.bottom);
                ob.test_count = countTests(klines, i, ob.top, ob.bottom);
                ob.is_broken = checkIfBroken(klines, i, type, ob.top, ob.bottom);
                ob.distance_pct = calculateDistance(klines.back().close, ob.top, ob.bottom, type);
                
                order_blocks.push_back(ob);
            }
        }
    }
    
    return order_blocks;
}

bool OrderBlockAnalyzer::detectBullishOrderBlock(const std::vector<Kline>& klines) {
    auto obs = detectOrderBlocks(klines, 50, 3, 30);
    for (const auto& ob : obs) {
        if (ob.type == OrderBlockType::BULLISH && !ob.is_broken) {
            return true;
        }
    }
    return false;
}

bool OrderBlockAnalyzer::detectBearishOrderBlock(const std::vector<Kline>& klines) {
    auto obs = detectOrderBlocks(klines, 50, 3, 30);
    for (const auto& ob : obs) {
        if (ob.type == OrderBlockType::BEARISH && !ob.is_broken) {
            return true;
        }
    }
    return false;
}

bool OrderBlockAnalyzer::detectAnyOrderBlock(const std::vector<Kline>& klines) {
    auto obs = detectOrderBlocks(klines, 50, 3, 30);
    for (const auto& ob : obs) {
        if (!ob.is_broken) {
            return true;
        }
    }
    return false;
}

OrderBlock OrderBlockAnalyzer::getRecentOrderBlock(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto obs = detectOrderBlocks(klines, 100, 3, 30);
    
    // 过滤类型并移除已突破的订单块
    std::vector<OrderBlock> filtered_obs;
    for (const auto& ob : obs) {
        if (ob.is_broken) continue;
        
        if (type_filter == "bullish" && ob.type != OrderBlockType::BULLISH) continue;
        if (type_filter == "bearish" && ob.type != OrderBlockType::BEARISH) continue;
        
        filtered_obs.push_back(ob);
    }
    
    // 按形成时间排序（最近的在前）
    std::sort(filtered_obs.begin(), filtered_obs.end(), 
        [](const OrderBlock& a, const OrderBlock& b) {
            return a.formed_index > b.formed_index;
        });
    
    if (lookback >= static_cast<int>(filtered_obs.size())) {
        // 返回空的OrderBlock
        OrderBlock empty_ob;
        empty_ob.type = OrderBlockType::NONE;
        empty_ob.top = 0.0;
        empty_ob.bottom = 0.0;
        empty_ob.mid = 0.0;
        empty_ob.formed_index = -1;
        empty_ob.strength = 0;
        empty_ob.is_tested = false;
        empty_ob.test_count = 0;
        empty_ob.is_broken = false;
        empty_ob.distance_pct = 0.0;
        return empty_ob;
    }
    
    return filtered_obs[lookback];
}

double OrderBlockAnalyzer::getOrderBlockTop(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.top;
}

double OrderBlockAnalyzer::getOrderBlockBottom(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.bottom;
}

double OrderBlockAnalyzer::getOrderBlockMid(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.mid;
}

int OrderBlockAnalyzer::getOrderBlockStrength(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.strength;
}

bool OrderBlockAnalyzer::isOrderBlockTested(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.is_tested;
}

int OrderBlockAnalyzer::getOrderBlockTestCount(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.test_count;
}

bool OrderBlockAnalyzer::isOrderBlockBroken(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.is_broken;
}

double OrderBlockAnalyzer::getDistanceToOrderBlock(const std::vector<Kline>& klines, const std::string& type_filter, int lookback) {
    auto ob = getRecentOrderBlock(klines, type_filter, lookback);
    return ob.distance_pct;
}

int OrderBlockAnalyzer::countOrderBlocks(const std::vector<Kline>& klines, int period, const std::string& type_filter) {
    auto obs = detectOrderBlocks(klines, period, 3, 30);
    
    int count = 0;
    for (const auto& ob : obs) {
        if (ob.is_broken) continue;
        
        if (type_filter == "bullish" && ob.type != OrderBlockType::BULLISH) continue;
        if (type_filter == "bearish" && ob.type != OrderBlockType::BEARISH) continue;
        
        count++;
    }
    
    return count;
}

bool OrderBlockAnalyzer::isBullishOrderBlock(const std::vector<Kline>& klines, size_t index) {
    if (index < 2 || index >= klines.size() - 3) {
        return false;
    }
    
    const auto& current = klines[index];
    
    // 检查当前K线是否是看跌的（收盘价低于开盘价）
    if (current.close >= current.open) {
        return false;
    }
    
    // 检查之前是否有下降趋势
    bool has_downtrend = true;
    for (size_t i = index - 2; i < index; ++i) {
        if (klines[i].close >= klines[i + 1].close) {
            has_downtrend = false;
            break;
        }
    }
    
    if (!has_downtrend) {
        return false;
    }
    
    // 检查之后是否有上升趋势（至少3根K线）
    bool has_uptrend = true;
    for (size_t i = index + 1; i < std::min(index + 4, klines.size()); ++i) {
        if (klines[i].close <= current.high) {
            has_uptrend = false;
            break;
        }
    }
    
    return has_uptrend;
}

bool OrderBlockAnalyzer::isBearishOrderBlock(const std::vector<Kline>& klines, size_t index) {
    if (index < 2 || index >= klines.size() - 3) {
        return false;
    }
    
    const auto& current = klines[index];
    
    // 检查当前K线是否是看涨的（收盘价高于开盘价）
    if (current.close <= current.open) {
        return false;
    }
    
    // 检查之前是否有上升趋势
    bool has_uptrend = true;
    for (size_t i = index - 2; i < index; ++i) {
        if (klines[i].close <= klines[i + 1].close) {
            has_uptrend = false;
            break;
        }
    }
    
    if (!has_uptrend) {
        return false;
    }
    
    // 检查之后是否有下降趋势（至少3根K线）
    bool has_downtrend = true;
    for (size_t i = index + 1; i < std::min(index + 4, klines.size()); ++i) {
        if (klines[i].close >= current.low) {
            has_downtrend = false;
            break;
        }
    }
    
    return has_downtrend;
}

int OrderBlockAnalyzer::calculateStrength(const std::vector<Kline>& klines, size_t index, OrderBlockType type) {
    // 显式标记type参数未使用（当前实现强度计算不依赖类型）
    (void)type;
    
    const auto& ob_kline = klines[index];
    
    // 基础强度：K线实体大小
    double body_size = std::abs(ob_kline.close - ob_kline.open);
    double range = ob_kline.high - ob_kline.low;
    double body_ratio = range > 0 ? (body_size / range) * 100.0 : 0.0;
    
    // 成交量强度（相对于平均成交量）
    double avg_volume = 0.0;
    int volume_period = 20;
    size_t start = index >= static_cast<size_t>(volume_period) ? index - volume_period : 0;
    for (size_t i = start; i <= index; ++i) {
        avg_volume += klines[i].volume;
    }
    avg_volume /= (index - start + 1);
    
    double volume_ratio = avg_volume > 0 ? (ob_kline.volume / avg_volume) * 100.0 : 100.0;
    
    // 综合强度（0-100）
    int strength = static_cast<int>((body_ratio * 0.5 + volume_ratio * 0.5) / 2.0);
    strength = std::max(0, std::min(100, strength));
    
    return strength;
}

bool OrderBlockAnalyzer::checkIfTested(const std::vector<Kline>& klines, size_t ob_index, double top, double bottom) {
    // 检查订单块形成后，价格是否回到该区域
    for (size_t i = ob_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        // 如果价格进入订单块区域
        if (kline.low <= top && kline.high >= bottom) {
            return true;
        }
    }
    
    return false;
}

int OrderBlockAnalyzer::countTests(const std::vector<Kline>& klines, size_t ob_index, double top, double bottom) {
    int count = 0;
    bool in_zone = false;
    
    for (size_t i = ob_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        bool current_in_zone = (kline.low <= top && kline.high >= bottom);
        
        // 如果从区域外进入区域，计数+1
        if (current_in_zone && !in_zone) {
            count++;
        }
        
        in_zone = current_in_zone;
    }
    
    return count;
}

bool OrderBlockAnalyzer::checkIfBroken(const std::vector<Kline>& klines, size_t ob_index, OrderBlockType type, double top, double bottom) {
    // 检查订单块是否被突破
    for (size_t i = ob_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        if (type == OrderBlockType::BULLISH) {
            // 看涨订单块：如果价格收盘在底部以下，则被突破
            if (kline.close < bottom) {
                return true;
            }
        } else if (type == OrderBlockType::BEARISH) {
            // 看跌订单块：如果价格收盘在顶部以上，则被突破
            if (kline.close > top) {
                return true;
            }
        }
    }
    
    return false;
}

double OrderBlockAnalyzer::calculateDistance(double current_price, double top, double bottom, OrderBlockType type) {
    // 显式标记type参数未使用（当前实现距离计算不依赖类型）
    (void)type;
    
    if (top <= bottom) {
        return 0.0;
    }
    
    double mid = (top + bottom) / 2.0;
    
    // 如果价格在订单块内，距离为0
    if (current_price >= bottom && current_price <= top) {
        return 0.0;
    }
    
    // 计算距离百分比
    double distance = 0.0;
    if (current_price > top) {
        distance = ((current_price - top) / mid) * 100.0;
    } else {
        distance = ((bottom - current_price) / mid) * 100.0;
    }
    
    return distance;
}

} // namespace functions
} // namespace prophet

