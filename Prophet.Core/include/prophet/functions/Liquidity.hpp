#pragma once

#include "prophet/common/types.hpp"
#include "prophet/functions/SwingPoint.hpp"
#include <vector>
#include <string>

namespace prophet {
namespace functions {

/**
 * Liquidity Zones (流动性区域)
 * 
 * 流动性区域是Smart Money Concepts中的重要概念，代表市场中订单聚集的区域。
 * 这些区域通常是：
 * - Equal Highs（等高点）：多个相近的高点，代表买入止损订单聚集
 * - Equal Lows（等低点）：多个相近的低点，代表卖出止损订单聚集
 * - 历史重要价格水平
 * 
 * Smart Money会在这些区域"扫荡流动性"（Liquidity Sweep），触发止损订单后反向交易。
 */

enum class LiquidityType {
    NONE = 0,
    BUYSIDE = 1,   // 买方流动性（高点，止损在上方）
    SELLSIDE = -1  // 卖方流动性（低点，止损在下方）
};

struct LiquidityZone {
    LiquidityType type;       // 流动性类型
    double price;             // 流动性价格水平
    double top;               // 区域上边界
    double bottom;            // 区域下边界
    int strength;             // 强度（基于重复次数）
    int formed_index;         // 形成时的K线索引
    bool is_swept;            // 是否被扫荡
    int sweep_index;          // 扫荡发生的K线索引
    std::vector<int> touch_indices;  // 触及该区域的K线索引
};

class LiquidityAnalyzer {
public:
    /**
     * 检测流动性区域
     * @param klines K线数据
     * @param price_tolerance 价格容差百分比（用于判断等高/等低）
     * @param min_touches 最少触及次数
     * @param period 回看周期
     * @return 检测到的流动性区域列表
     */
    static std::vector<LiquidityZone> detectLiquidityZones(
        const std::vector<Kline>& klines,
        double price_tolerance = 0.5,
        int min_touches = 2,
        int period = 100
    );
    
    /**
     * 检测买方流动性区域（等高点）
     */
    static std::vector<LiquidityZone> detectBuysideLiquidity(
        const std::vector<Kline>& klines,
        double price_tolerance = 0.5,
        int min_touches = 2,
        int period = 100
    );
    
    /**
     * 检测卖方流动性区域（等低点）
     */
    static std::vector<LiquidityZone> detectSellsideLiquidity(
        const std::vector<Kline>& klines,
        double price_tolerance = 0.5,
        int min_touches = 2,
        int period = 100
    );
    
    /**
     * 检查是否存在买方流动性
     */
    static bool hasBuysideLiquidity(const std::vector<Kline>& klines, double price_tolerance = 0.5);
    
    /**
     * 检查是否存在卖方流动性
     */
    static bool hasSellsideLiquidity(const std::vector<Kline>& klines, double price_tolerance = 0.5);
    
    /**
     * 获取最近的流动性区域
     * @param klines K线数据
     * @param type_filter 类型过滤（"buyside"/"sellside"/"any"）
     * @param price_tolerance 价格容差
     * @param lookback 回看索引
     * @return 流动性区域
     */
    static LiquidityZone getRecentLiquidityZone(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        double price_tolerance = 0.5,
        int lookback = 0
    );
    
    /**
     * 获取流动性区域的价格
     */
    static double getLiquidityPrice(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        double price_tolerance = 0.5,
        int lookback = 0
    );
    
    /**
     * 获取流动性区域的强度
     */
    static int getLiquidityStrength(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        double price_tolerance = 0.5,
        int lookback = 0
    );
    
    /**
     * 检查流动性是否被扫荡
     */
    static bool isLiquiditySwept(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        double price_tolerance = 0.5,
        int lookback = 0
    );
    
    /**
     * 统计流动性区域数量
     */
    static int countLiquidityZones(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        double price_tolerance = 0.5,
        int period = 100
    );
    
    /**
     * 获取当前价格距离最近流动性区域的距离百分比
     */
    static double getDistanceToLiquidity(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        double price_tolerance = 0.5
    );

private:
    /**
     * 检查价格是否在容差范围内
     */
    static bool isPriceWithinTolerance(double price1, double price2, double tolerance_pct);
    
    /**
     * 查找等高点
     */
    static std::vector<LiquidityZone> findEqualHighs(
        const std::vector<Kline>& klines,
        const std::vector<SwingPoint>& swing_highs,
        double price_tolerance,
        int min_touches
    );
    
    /**
     * 查找等低点
     */
    static std::vector<LiquidityZone> findEqualLows(
        const std::vector<Kline>& klines,
        const std::vector<SwingPoint>& swing_lows,
        double price_tolerance,
        int min_touches
    );
    
    /**
     * 检查流动性区域是否被扫荡
     */
    static bool checkIfSwept(
        const std::vector<Kline>& klines,
        const LiquidityZone& zone,
        size_t zone_formed_index
    );
};

} // namespace functions
} // namespace prophet

