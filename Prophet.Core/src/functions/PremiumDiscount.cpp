#include "prophet/functions/PremiumDiscount.hpp"
#include <algorithm>
#include <cmath>
#include <limits>

namespace prophet {
namespace functions {

PremiumDiscountInfo PremiumDiscountAnalyzer::analyze(const std::vector<Kline>& klines, int period) {
    PremiumDiscountInfo info;
    
    if (klines.size() < 3) {
        // 数据不足，返回默认值
        info.zone = PremiumDiscountZone::EQUILIBRIUM;
        info.percentage = 50.0;
        info.level = 0.0;
        info.high = 0.0;
        info.low = 0.0;
        info.current_price = 0.0;
        info.strength = 0.0;
        return info;
    }
    
    // 确定实际回看周期
    int actual_period = std::min(period, static_cast<int>(klines.size()));
    
    // 找到回看周期内的最高价和最低价
    double range_high = std::numeric_limits<double>::lowest();
    double range_low = std::numeric_limits<double>::max();
    
    for (int i = static_cast<int>(klines.size()) - actual_period; i < static_cast<int>(klines.size()); ++i) {
        range_high = std::max(range_high, klines[i].high);
        range_low = std::min(range_low, klines[i].low);
    }
    
    // 获取当前价格（最新K线的收盘价）
    double current_price = klines.back().close;
    
    // 计算均衡价格（50%水平）
    double equilibrium = (range_high + range_low) / 2.0;
    
    // 计算在范围内的百分比位置
    double percentage = calculatePercentageInRange(current_price, range_high, range_low);
    
    // 确定所在区域
    PremiumDiscountZone zone = determineZone(percentage);
    
    // 计算区域强度
    double strength = calculateStrength(percentage, zone);
    
    // 填充结果
    info.zone = zone;
    info.percentage = percentage;
    info.level = equilibrium;
    info.high = range_high;
    info.low = range_low;
    info.current_price = current_price;
    info.strength = strength;
    
    return info;
}

bool PremiumDiscountAnalyzer::isInPremium(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.zone == PremiumDiscountZone::PREMIUM;
}

bool PremiumDiscountAnalyzer::isInDiscount(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.zone == PremiumDiscountZone::DISCOUNT;
}

bool PremiumDiscountAnalyzer::isInEquilibrium(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.zone == PremiumDiscountZone::EQUILIBRIUM;
}

double PremiumDiscountAnalyzer::getPercentage(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.percentage;
}

double PremiumDiscountAnalyzer::getEquilibriumLevel(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.level;
}

double PremiumDiscountAnalyzer::getRangeHigh(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.high;
}

double PremiumDiscountAnalyzer::getRangeLow(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.low;
}

double PremiumDiscountAnalyzer::getStrength(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return info.strength;
}

int PremiumDiscountAnalyzer::getZoneValue(const std::vector<Kline>& klines, int period) {
    auto info = analyze(klines, period);
    return static_cast<int>(info.zone);
}

double PremiumDiscountAnalyzer::calculatePercentageInRange(double price, double high, double low) {
    if (high <= low) {
        return 50.0; // 范围无效，返回中间值
    }
    
    double range = high - low;
    double position = price - low;
    double percentage = (position / range) * 100.0;
    
    // 限制在0-100范围内
    percentage = std::max(0.0, std::min(100.0, percentage));
    
    return percentage;
}

PremiumDiscountZone PremiumDiscountAnalyzer::determineZone(double percentage) {
    if (percentage >= 60.0) {
        return PremiumDiscountZone::PREMIUM;
    } else if (percentage <= 40.0) {
        return PremiumDiscountZone::DISCOUNT;
    } else {
        return PremiumDiscountZone::EQUILIBRIUM;
    }
}

double PremiumDiscountAnalyzer::calculateStrength(double percentage, PremiumDiscountZone zone) {
    switch (zone) {
        case PremiumDiscountZone::PREMIUM:
            // 在溢价区，60-100，强度从0到100
            // percentage = 60 -> strength = 0
            // percentage = 100 -> strength = 100
            return ((percentage - 60.0) / 40.0) * 100.0;
            
        case PremiumDiscountZone::DISCOUNT:
            // 在折价区，0-40，强度从100到0
            // percentage = 0 -> strength = 100
            // percentage = 40 -> strength = 0
            return ((40.0 - percentage) / 40.0) * 100.0;
            
        case PremiumDiscountZone::EQUILIBRIUM:
            // 在均衡区，强度较低
            // percentage = 40或60 -> strength = 0
            // percentage = 50 -> strength = 100
            {
                double distance_from_center = std::abs(percentage - 50.0);
                return (1.0 - distance_from_center / 10.0) * 100.0;
            }
            
        default:
            return 0.0;
    }
}

} // namespace functions
} // namespace prophet

