#pragma once

#include "prophet/common/types.hpp"
#include "prophet/functions/SwingPoint.hpp"
#include <vector>
#include <string>

namespace prophet {
namespace functions {

/**
 * Market Structure (市场结构分析)
 * 
 * 包含BOS (Break of Structure) 和 CHoCH (Change of Character) 的检测。
 * 这些是Smart Money Concepts中用于识别趋势变化的核心工具。
 * 
 * BOS (Break of Structure - 结构突破):
 * - 在上升趋势中：价格突破前一个摆动高点
 * - 在下降趋势中：价格突破前一个摆动低点
 * - 表示当前趋势的延续和加强
 * 
 * CHoCH (Change of Character - 特征改变):
 * - 在上升趋势中：价格跌破前一个摆动低点
 * - 在下降趋势中：价格突破前一个摆动高点
 * - 表示趋势可能反转的早期信号
 */

enum class StructureType {
    NONE = 0,
    BOS_BULLISH = 1,    // 看涨结构突破
    BOS_BEARISH = -1,   // 看跌结构突破
    CHOCH_BULLISH = 2,  // 看涨特征改变
    CHOCH_BEARISH = -2  // 看跌特征改变
};

enum class TrendDirection {
    UNKNOWN = 0,
    UPTREND = 1,     // 上升趋势
    DOWNTREND = -1   // 下降趋势
};

struct StructureEvent {
    StructureType type;         // 事件类型
    double price;               // 突破/改变发生的价格
    int index;                  // 发生时的K线索引
    double swing_price;         // 被突破的摆动点价格
    int swing_index;            // 摆动点索引
    TrendDirection prev_trend;  // 之前的趋势
    TrendDirection new_trend;   // 新的趋势
};

class MarketStructureAnalyzer {
public:
    /**
     * 检测市场结构事件（BOS和CHoCH）
     * @param klines K线数据
     * @param swing_left_bars 摆动点左侧K线数
     * @param swing_right_bars 摆动点右侧K线数
     * @param period 回看周期
     * @return 检测到的结构事件列表
     */
    static std::vector<StructureEvent> detectStructureEvents(
        const std::vector<Kline>& klines,
        int swing_left_bars = 5,
        int swing_right_bars = 5,
        int period = 100
    );
    
    /**
     * 检测是否发生看涨BOS
     */
    static bool detectBullishBOS(const std::vector<Kline>& klines, int swing_left_bars = 5, int swing_right_bars = 5);
    
    /**
     * 检测是否发生看跌BOS
     */
    static bool detectBearishBOS(const std::vector<Kline>& klines, int swing_left_bars = 5, int swing_right_bars = 5);
    
    /**
     * 检测是否发生看涨CHoCH
     */
    static bool detectBullishCHoCH(const std::vector<Kline>& klines, int swing_left_bars = 5, int swing_right_bars = 5);
    
    /**
     * 检测是否发生看跌CHoCH
     */
    static bool detectBearishCHoCH(const std::vector<Kline>& klines, int swing_left_bars = 5, int swing_right_bars = 5);
    
    /**
     * 获取最近的结构事件
     * @param klines K线数据
     * @param type_filter 类型过滤（"bos"/"choch"/"bullish"/"bearish"/"any"）
     * @param swing_left_bars 摆动点左侧K线数
     * @param swing_right_bars 摆动点右侧K线数
     * @param lookback 回看索引
     * @return 结构事件
     */
    static StructureEvent getRecentStructureEvent(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int swing_left_bars = 5,
        int swing_right_bars = 5,
        int lookback = 0
    );
    
    /**
     * 获取结构事件的价格
     */
    static double getStructurePrice(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int swing_left_bars = 5,
        int swing_right_bars = 5,
        int lookback = 0
    );
    
    /**
     * 获取被突破的摆动点价格
     */
    static double getSwingPrice(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int swing_left_bars = 5,
        int swing_right_bars = 5,
        int lookback = 0
    );
    
    /**
     * 统计结构事件数量
     */
    static int countStructureEvents(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int swing_left_bars = 5,
        int swing_right_bars = 5,
        int period = 100
    );
    
    /**
     * 获取当前趋势方向
     */
    static int getCurrentTrend(
        const std::vector<Kline>& klines,
        int swing_left_bars = 5,
        int swing_right_bars = 5
    );

private:
    /**
     * 确定当前趋势方向
     */
    static TrendDirection determineTrend(const std::vector<SwingPoint>& swing_points);
    
    /**
     * 检测BOS
     */
    static bool detectBOS(
        const std::vector<Kline>& klines,
        const std::vector<SwingPoint>& swing_points,
        TrendDirection trend,
        size_t current_index
    );
    
    /**
     * 检测CHoCH
     */
    static bool detectCHoCH(
        const std::vector<Kline>& klines,
        const std::vector<SwingPoint>& swing_points,
        TrendDirection trend,
        size_t current_index
    );
};

} // namespace functions
} // namespace prophet

