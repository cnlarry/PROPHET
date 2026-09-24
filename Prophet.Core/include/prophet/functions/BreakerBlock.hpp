#pragma once

#include "prophet/common/types.hpp"
#include "prophet/functions/OrderBlock.hpp"
#include <vector>
#include <string>

namespace prophet {
namespace functions {

/**
 * Breaker Block (突破块)
 * 
 * Breaker Block是Smart Money Concepts中的高级概念，是Order Block的"失效"形态。
 * 当一个Order Block被突破后，它会转变为Breaker Block，极性反转：
 * - Bullish Order Block被突破 -> Bearish Breaker Block（阻力）
 * - Bearish Order Block被突破 -> Bullish Breaker Block（支撑）
 * 
 * Breaker Block通常是强力的支撑/阻力区域，因为它代表：
 * 1. 原Order Block的失效
 * 2. 市场结构的改变
 * 3. Smart Money的新仓位建立
 */

enum class BreakerBlockType {
    NONE = 0,
    BULLISH = 1,   // 看涨突破块（原看跌OB被突破）
    BEARISH = -1   // 看跌突破块（原看涨OB被突破）
};

struct BreakerBlock {
    BreakerBlockType type;       // 突破块类型
    double top;                  // 突破块上边界
    double bottom;               // 突破块下边界
    double mid;                  // 突破块中间价
    int formed_index;            // 形成时的K线索引（OB被突破的时刻）
    int ob_index;                // 原Order Block的索引
    OrderBlockType original_ob_type;  // 原Order Block类型
    int strength;                // 强度
    bool is_tested;              // 是否被测试过
    int test_count;              // 测试次数
    bool is_broken;              // 是否被突破（失效）
};

class BreakerBlockAnalyzer {
public:
    /**
     * 检测突破块
     * @param klines K线数据
     * @param period 回看周期
     * @return 检测到的突破块列表
     */
    static std::vector<BreakerBlock> detectBreakerBlocks(
        const std::vector<Kline>& klines,
        int period = 100
    );
    
    /**
     * 检测看涨突破块
     */
    static bool detectBullishBreakerBlock(const std::vector<Kline>& klines);
    
    /**
     * 检测看跌突破块
     */
    static bool detectBearishBreakerBlock(const std::vector<Kline>& klines);
    
    /**
     * 获取最近的突破块
     * @param klines K线数据
     * @param type_filter 类型过滤（"bullish"/"bearish"/"any"）
     * @param lookback 回看索引
     * @return 突破块信息
     */
    static BreakerBlock getRecentBreakerBlock(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 获取突破块的上边界
     */
    static double getBreakerBlockTop(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 获取突破块的下边界
     */
    static double getBreakerBlockBottom(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 获取突破块的中间价
     */
    static double getBreakerBlockMid(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 获取突破块的强度
     */
    static int getBreakerBlockStrength(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 检查突破块是否被测试过
     */
    static bool isBreakerBlockTested(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 获取突破块被测试的次数
     */
    static int getBreakerBlockTestCount(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 检查突破块是否被突破（失效）
     */
    static bool isBreakerBlockBroken(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int lookback = 0
    );
    
    /**
     * 统计突破块数量
     */
    static int countBreakerBlocks(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int period = 100
    );

private:
    /**
     * 从Order Block转换为Breaker Block
     */
    static BreakerBlock convertFromOrderBlock(
        const std::vector<Kline>& klines,
        const OrderBlock& ob,
        size_t break_index
    );
    
    /**
     * 检查Order Block是否被突破
     */
    static bool isOrderBlockBroken(
        const std::vector<Kline>& klines,
        const OrderBlock& ob
    );
    
    /**
     * 查找Order Block被突破的索引
     */
    static int findBreakIndex(
        const std::vector<Kline>& klines,
        const OrderBlock& ob
    );
    
    /**
     * 检查Breaker Block是否被测试
     */
    static bool checkIfTested(
        const std::vector<Kline>& klines,
        size_t bb_index,
        double top,
        double bottom
    );
    
    /**
     * 计算测试次数
     */
    static int countTests(
        const std::vector<Kline>& klines,
        size_t bb_index,
        double top,
        double bottom
    );
    
    /**
     * 检查Breaker Block是否被突破（失效）
     */
    static bool checkIfBroken(
        const std::vector<Kline>& klines,
        size_t bb_index,
        BreakerBlockType type,
        double top,
        double bottom
    );
};

} // namespace functions
} // namespace prophet

