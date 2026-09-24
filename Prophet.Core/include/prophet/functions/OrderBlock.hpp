#pragma once

#include "prophet/common/types.hpp"
#include <vector>
#include <string>

namespace prophet {
namespace functions {

/**
 * Order Block (订单块)
 * 
 * Order Block是Smart Money Concepts中的核心概念，代表机构订单的"足迹"。
 * 它通常是趋势反转前的最后一个相反方向的K线，表示大量订单聚集的区域。
 * 
 * 特征：
 * - Bullish Order Block（看涨订单块）：下降趋势中的最后一个看跌K线，之后价格开始上涨
 * - Bearish Order Block（看跌订单块）：上升趋势中的最后一个看涨K线，之后价格开始下跌
 * 
 * Order Block通常被视为强支撑/阻力区域，价格回到这些区域时可能反弹。
 */

enum class OrderBlockType {
    NONE = 0,
    BULLISH = 1,   // 看涨订单块（支撑）
    BEARISH = -1   // 看跌订单块（阻力）
};

struct OrderBlock {
    OrderBlockType type;       // 订单块类型
    double top;               // 订单块上边界
    double bottom;            // 订单块下边界
    double mid;               // 订单块中间价
    int formed_index;         // 形成时的K线索引
    int strength;             // 强度（基于成交量和价格变动）
    bool is_tested;           // 是否被测试过（价格回到该区域）
    int test_count;           // 被测试的次数
    bool is_broken;           // 是否被突破（价格穿过并收在另一侧）
    double distance_pct;      // 当前价格距离订单块的百分比距离
};

class OrderBlockAnalyzer {
public:
    /**
     * 检测最近的订单块
     * @param klines K线数据
     * @param min_candles 形成订单块后的最小K线数量（默认3）
     * @param strength_threshold 强度阈值（默认50）
     * @return 检测到的订单块列表
     */
    static std::vector<OrderBlock> detectOrderBlocks(
        const std::vector<Kline>& klines, 
        int period = 100,
        int min_candles = 3,
        int strength_threshold = 50
    );
    
    /**
     * 检测是否存在看涨订单块
     */
    static bool detectBullishOrderBlock(const std::vector<Kline>& klines);
    
    /**
     * 检测是否存在看跌订单块
     */
    static bool detectBearishOrderBlock(const std::vector<Kline>& klines);
    
    /**
     * 检测是否存在任何订单块
     */
    static bool detectAnyOrderBlock(const std::vector<Kline>& klines);
    
    /**
     * 获取最近的订单块
     * @param klines K线数据
     * @param type_filter 类型过滤（"bullish"/"bearish"/"any"）
     * @param lookback 回看索引（0=最近的）
     * @return 订单块信息，如果不存在返回空的OrderBlock
     */
    static OrderBlock getRecentOrderBlock(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 获取订单块的上边界
     */
    static double getOrderBlockTop(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 获取订单块的下边界
     */
    static double getOrderBlockBottom(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 获取订单块的中间价
     */
    static double getOrderBlockMid(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 获取订单块的强度
     */
    static int getOrderBlockStrength(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 检查订单块是否被测试过
     */
    static bool isOrderBlockTested(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 获取订单块被测试的次数
     */
    static int getOrderBlockTestCount(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 检查订单块是否被突破
     */
    static bool isOrderBlockBroken(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 获取当前价格距离订单块的百分比距离
     */
    static double getDistanceToOrderBlock(const std::vector<Kline>& klines, const std::string& type_filter = "any", int lookback = 0);
    
    /**
     * 统计订单块数量
     */
    static int countOrderBlocks(const std::vector<Kline>& klines, int period, const std::string& type_filter = "any");

private:
    /**
     * 判断是否是看涨订单块
     */
    static bool isBullishOrderBlock(const std::vector<Kline>& klines, size_t index);
    
    /**
     * 判断是否是看跌订单块
     */
    static bool isBearishOrderBlock(const std::vector<Kline>& klines, size_t index);
    
    /**
     * 计算订单块强度
     */
    static int calculateStrength(const std::vector<Kline>& klines, size_t index, OrderBlockType type);
    
    /**
     * 检查订单块是否被测试
     */
    static bool checkIfTested(const std::vector<Kline>& klines, size_t ob_index, double top, double bottom);
    
    /**
     * 计算测试次数
     */
    static int countTests(const std::vector<Kline>& klines, size_t ob_index, double top, double bottom);
    
    /**
     * 检查订单块是否被突破
     */
    static bool checkIfBroken(const std::vector<Kline>& klines, size_t ob_index, OrderBlockType type, double top, double bottom);
    
    /**
     * 计算距离百分比
     */
    static double calculateDistance(double current_price, double top, double bottom, OrderBlockType type);
};

} // namespace functions
} // namespace prophet

