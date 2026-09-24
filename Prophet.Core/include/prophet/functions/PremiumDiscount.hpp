#pragma once

#include "prophet/common/types.hpp"
#include <vector>
#include <string>

namespace prophet {
namespace functions {

/**
 * Premium/Discount Zones (溢价/折价区)
 * 
 * Premium/Discount区域是Smart Money Concepts中的核心概念，用于识别价格相对于某个基准范围的位置。
 * 通常基于某个周期的高低点范围，将其分为：
 * - Premium Zone（溢价区）：价格位于范围上半部分（50%-100%），表示价格偏高
 * - Equilibrium（均衡区）：价格位于范围中部（40%-60%），表示价格平衡
 * - Discount Zone（折价区）：价格位于范围下半部分（0%-50%），表示价格偏低
 * 
 * SMC交易者倾向于在折价区寻找买入机会，在溢价区寻找卖出机会。
 */

enum class PremiumDiscountZone {
    DISCOUNT = -1,    // 折价区
    EQUILIBRIUM = 0,  // 均衡区
    PREMIUM = 1       // 溢价区
};

struct PremiumDiscountInfo {
    PremiumDiscountZone zone;  // 当前所在区域
    double percentage;         // 在范围内的百分比位置 (0-100)
    double level;             // 50%水平线价格（均衡价）
    double high;              // 范围高点
    double low;               // 范围低点
    double current_price;     // 当前价格
    double strength;          // 区域强度 (0-100)，表示距离边界的远近
};

class PremiumDiscountAnalyzer {
public:
    /**
     * 分析当前价格在Premium/Discount区域的位置
     * @param klines K线数据
     * @param period 回看周期，用于确定高低点范围
     * @return PremiumDiscountInfo 区域信息
     */
    static PremiumDiscountInfo analyze(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 判断当前是否在溢价区
     */
    static bool isInPremium(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 判断当前是否在折价区
     */
    static bool isInDiscount(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 判断当前是否在均衡区
     */
    static bool isInEquilibrium(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 获取当前在范围内的百分比位置 (0-100)
     * 0 = 范围最低点
     * 50 = 范围中间（均衡）
     * 100 = 范围最高点
     */
    static double getPercentage(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 获取均衡价格水平（50%位置）
     */
    static double getEquilibriumLevel(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 获取范围高点
     */
    static double getRangeHigh(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 获取范围低点
     */
    static double getRangeLow(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 获取区域强度 (0-100)
     * 在Premium区：值越高表示越接近顶部
     * 在Discount区：值越高表示越接近底部
     */
    static double getStrength(const std::vector<Kline>& klines, int period = 100);
    
    /**
     * 获取区域类型的数值表示
     * 1 = Premium, 0 = Equilibrium, -1 = Discount
     */
    static int getZoneValue(const std::vector<Kline>& klines, int period = 100);

private:
    /**
     * 计算在范围内的百分比位置
     */
    static double calculatePercentageInRange(double price, double high, double low);
    
    /**
     * 根据百分比确定所在区域
     */
    static PremiumDiscountZone determineZone(double percentage);
    
    /**
     * 计算区域强度
     */
    static double calculateStrength(double percentage, PremiumDiscountZone zone);
};

} // namespace functions
} // namespace prophet

