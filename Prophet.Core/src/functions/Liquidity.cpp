#include "prophet/functions/Liquidity.hpp"
#include <algorithm>
#include <cmath>
#include <map>

namespace prophet {
namespace functions {

std::vector<LiquidityZone> LiquidityAnalyzer::detectLiquidityZones(
    const std::vector<Kline>& klines,
    double price_tolerance,
    int min_touches,
    int period
) {
    std::vector<LiquidityZone> zones;
    
    if (klines.size() < 10) {
        return zones;
    }
    
    // 检测摆动点
    auto swing_highs = SwingPointAnalyzer::detectSwingHighs(klines, 5, 5, period);
    auto swing_lows = SwingPointAnalyzer::detectSwingLows(klines, 5, 5, period);
    
    // 查找等高点（买方流动性）
    auto buyside_zones = findEqualHighs(klines, swing_highs, price_tolerance, min_touches);
    zones.insert(zones.end(), buyside_zones.begin(), buyside_zones.end());
    
    // 查找等低点（卖方流动性）
    auto sellside_zones = findEqualLows(klines, swing_lows, price_tolerance, min_touches);
    zones.insert(zones.end(), sellside_zones.begin(), sellside_zones.end());
    
    return zones;
}

std::vector<LiquidityZone> LiquidityAnalyzer::detectBuysideLiquidity(
    const std::vector<Kline>& klines,
    double price_tolerance,
    int min_touches,
    int period
) {
    auto swing_highs = SwingPointAnalyzer::detectSwingHighs(klines, 5, 5, period);
    return findEqualHighs(klines, swing_highs, price_tolerance, min_touches);
}

std::vector<LiquidityZone> LiquidityAnalyzer::detectSellsideLiquidity(
    const std::vector<Kline>& klines,
    double price_tolerance,
    int min_touches,
    int period
) {
    auto swing_lows = SwingPointAnalyzer::detectSwingLows(klines, 5, 5, period);
    return findEqualLows(klines, swing_lows, price_tolerance, min_touches);
}

bool LiquidityAnalyzer::hasBuysideLiquidity(const std::vector<Kline>& klines, double price_tolerance) {
    auto zones = detectBuysideLiquidity(klines, price_tolerance, 2, 50);
    return !zones.empty();
}

bool LiquidityAnalyzer::hasSellsideLiquidity(const std::vector<Kline>& klines, double price_tolerance) {
    auto zones = detectSellsideLiquidity(klines, price_tolerance, 2, 50);
    return !zones.empty();
}

LiquidityZone LiquidityAnalyzer::getRecentLiquidityZone(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    double price_tolerance,
    int lookback
) {
    std::vector<LiquidityZone> zones;
    
    if (type_filter == "buyside") {
        zones = detectBuysideLiquidity(klines, price_tolerance, 2, 100);
    } else if (type_filter == "sellside") {
        zones = detectSellsideLiquidity(klines, price_tolerance, 2, 100);
    } else {
        zones = detectLiquidityZones(klines, price_tolerance, 2, 100);
    }
    
    // 按形成索引排序（最近的在前）
    std::sort(zones.begin(), zones.end(),
        [](const LiquidityZone& a, const LiquidityZone& b) {
            return a.formed_index > b.formed_index;
        });
    
    if (lookback >= static_cast<int>(zones.size())) {
        // 返回空区域
        LiquidityZone empty_zone;
        empty_zone.type = LiquidityType::NONE;
        empty_zone.price = 0.0;
        empty_zone.top = 0.0;
        empty_zone.bottom = 0.0;
        empty_zone.strength = 0;
        empty_zone.formed_index = -1;
        empty_zone.is_swept = false;
        empty_zone.sweep_index = -1;
        return empty_zone;
    }
    
    return zones[lookback];
}

double LiquidityAnalyzer::getLiquidityPrice(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    double price_tolerance,
    int lookback
) {
    auto zone = getRecentLiquidityZone(klines, type_filter, price_tolerance, lookback);
    return zone.price;
}

int LiquidityAnalyzer::getLiquidityStrength(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    double price_tolerance,
    int lookback
) {
    auto zone = getRecentLiquidityZone(klines, type_filter, price_tolerance, lookback);
    return zone.strength;
}

bool LiquidityAnalyzer::isLiquiditySwept(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    double price_tolerance,
    int lookback
) {
    auto zone = getRecentLiquidityZone(klines, type_filter, price_tolerance, lookback);
    return zone.is_swept;
}

int LiquidityAnalyzer::countLiquidityZones(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    double price_tolerance,
    int period
) {
    std::vector<LiquidityZone> zones;
    
    if (type_filter == "buyside") {
        zones = detectBuysideLiquidity(klines, price_tolerance, 2, period);
    } else if (type_filter == "sellside") {
        zones = detectSellsideLiquidity(klines, price_tolerance, 2, period);
    } else {
        zones = detectLiquidityZones(klines, price_tolerance, 2, period);
    }
    
    return static_cast<int>(zones.size());
}

double LiquidityAnalyzer::getDistanceToLiquidity(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    double price_tolerance
) {
    auto zone = getRecentLiquidityZone(klines, type_filter, price_tolerance, 0);
    
    if (zone.type == LiquidityType::NONE || klines.empty()) {
        return 0.0;
    }
    
    double current_price = klines.back().close;
    double distance_pct = ((current_price - zone.price) / zone.price) * 100.0;
    
    return distance_pct;
}

bool LiquidityAnalyzer::isPriceWithinTolerance(double price1, double price2, double tolerance_pct) {
    double avg_price = (price1 + price2) / 2.0;
    double diff_pct = std::abs(price1 - price2) / avg_price * 100.0;
    return diff_pct <= tolerance_pct;
}

std::vector<LiquidityZone> LiquidityAnalyzer::findEqualHighs(
    const std::vector<Kline>& klines,
    const std::vector<SwingPoint>& swing_highs,
    double price_tolerance,
    int min_touches
) {
    std::vector<LiquidityZone> zones;
    
    if (swing_highs.size() < 2) {
        return zones;
    }
    
    // 按价格分组摆动高点
    std::map<double, std::vector<SwingPoint>> price_groups;
    
    for (const auto& sp : swing_highs) {
        bool found_group = false;
        
        // 检查是否可以加入现有分组
        for (auto& [key_price, group] : price_groups) {
            if (isPriceWithinTolerance(sp.price, key_price, price_tolerance)) {
                group.push_back(sp);
                found_group = true;
                break;
            }
        }
        
        // 创建新分组
        if (!found_group) {
            price_groups[sp.price].push_back(sp);
        }
    }
    
    // 为每个有足够触及次数的分组创建流动性区域
    for (const auto& [key_price, group] : price_groups) {
        if (static_cast<int>(group.size()) >= min_touches) {
            LiquidityZone zone;
            zone.type = LiquidityType::BUYSIDE;
            
            // 计算平均价格
            double sum_price = 0.0;
            double max_price = group[0].price;
            double min_price = group[0].price;
            int earliest_index = group[0].index;
            
            for (const auto& sp : group) {
                sum_price += sp.price;
                max_price = std::max(max_price, sp.price);
                min_price = std::min(min_price, sp.price);
                earliest_index = std::min(earliest_index, sp.index);
            }
            
            zone.price = sum_price / group.size();
            zone.top = max_price;
            zone.bottom = min_price;
            zone.strength = static_cast<int>(group.size());
            zone.formed_index = earliest_index;
            zone.is_swept = checkIfSwept(klines, zone, earliest_index);
            zone.sweep_index = -1;
            
            // 收集触及索引
            for (const auto& sp : group) {
                zone.touch_indices.push_back(sp.index);
            }
            
            zones.push_back(zone);
        }
    }
    
    return zones;
}

std::vector<LiquidityZone> LiquidityAnalyzer::findEqualLows(
    const std::vector<Kline>& klines,
    const std::vector<SwingPoint>& swing_lows,
    double price_tolerance,
    int min_touches
) {
    std::vector<LiquidityZone> zones;
    
    if (swing_lows.size() < 2) {
        return zones;
    }
    
    // 按价格分组摆动低点
    std::map<double, std::vector<SwingPoint>> price_groups;
    
    for (const auto& sp : swing_lows) {
        bool found_group = false;
        
        // 检查是否可以加入现有分组
        for (auto& [key_price, group] : price_groups) {
            if (isPriceWithinTolerance(sp.price, key_price, price_tolerance)) {
                group.push_back(sp);
                found_group = true;
                break;
            }
        }
        
        // 创建新分组
        if (!found_group) {
            price_groups[sp.price].push_back(sp);
        }
    }
    
    // 为每个有足够触及次数的分组创建流动性区域
    for (const auto& [key_price, group] : price_groups) {
        if (static_cast<int>(group.size()) >= min_touches) {
            LiquidityZone zone;
            zone.type = LiquidityType::SELLSIDE;
            
            // 计算平均价格
            double sum_price = 0.0;
            double max_price = group[0].price;
            double min_price = group[0].price;
            int earliest_index = group[0].index;
            
            for (const auto& sp : group) {
                sum_price += sp.price;
                max_price = std::max(max_price, sp.price);
                min_price = std::min(min_price, sp.price);
                earliest_index = std::min(earliest_index, sp.index);
            }
            
            zone.price = sum_price / group.size();
            zone.top = max_price;
            zone.bottom = min_price;
            zone.strength = static_cast<int>(group.size());
            zone.formed_index = earliest_index;
            zone.is_swept = checkIfSwept(klines, zone, earliest_index);
            zone.sweep_index = -1;
            
            // 收集触及索引
            for (const auto& sp : group) {
                zone.touch_indices.push_back(sp.index);
            }
            
            zones.push_back(zone);
        }
    }
    
    return zones;
}

bool LiquidityAnalyzer::checkIfSwept(
    const std::vector<Kline>& klines,
    const LiquidityZone& zone,
    size_t zone_formed_index
) {
    // 检查流动性区域形成后，价格是否扫荡该区域
    for (size_t i = zone_formed_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        if (zone.type == LiquidityType::BUYSIDE) {
            // 买方流动性：如果价格突破上方后回落
            if (kline.high > zone.top) {
                return true;
            }
        } else if (zone.type == LiquidityType::SELLSIDE) {
            // 卖方流动性：如果价格突破下方后反弹
            if (kline.low < zone.bottom) {
                return true;
            }
        }
    }
    
    return false;
}

} // namespace functions
} // namespace prophet

