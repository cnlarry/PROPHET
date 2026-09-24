#pragma once

#include "prophet/common/types.hpp"
#include <vector>
#include <string>

namespace prophet {
namespace functions {

/**
 * Swing High/Low (摆动高低点)
 * 
 * 摆动高低点是技术分析中的基础概念，用于识别价格的局部极值点。
 * 在Smart Money Concepts中，摆动点用于：
 * - 识别市场结构（Market Structure）
 * - 确定BOS（Break of Structure）和CHoCH（Change of Character）
 * - 绘制趋势线和支撑阻力位
 * 
 * 定义：
 * - Swing High（摆动高点）：一个高点，其左右N根K线的高点都低于它
 * - Swing Low（摆动低点）：一个低点，其左右N根K线的低点都高于它
 */

enum class SwingPointType {
    NONE = 0,
    HIGH = 1,   // 摆动高点
    LOW = -1    // 摆动低点
};

struct SwingPoint {
    SwingPointType type;      // 摆动点类型
    double price;             // 摆动点价格
    int index;                // 摆动点在K线数组中的索引
    int left_bars;            // 左侧K线数量
    int right_bars;           // 右侧K线数量
    int strength;             // 强度（基于左右K线的价格差距）
    bool is_broken;           // 是否被突破（对于高点，被向上突破；对于低点，被向下突破）
};

class SwingPointAnalyzer {
public:
    /**
     * 检测摆动点
     * @param klines K线数据
     * @param left_bars 左侧K线数量
     * @param right_bars 右侧K线数量
     * @param period 回看周期
     * @return 检测到的摆动点列表
     */
    static std::vector<SwingPoint> detectSwingPoints(
        const std::vector<Kline>& klines,
        int left_bars = 5,
        int right_bars = 5,
        int period = 100
    );
    
    /**
     * 检测摆动高点
     */
    static std::vector<SwingPoint> detectSwingHighs(
        const std::vector<Kline>& klines,
        int left_bars = 5,
        int right_bars = 5,
        int period = 100
    );
    
    /**
     * 检测摆动低点
     */
    static std::vector<SwingPoint> detectSwingLows(
        const std::vector<Kline>& klines,
        int left_bars = 5,
        int right_bars = 5,
        int period = 100
    );
    
    /**
     * 检查是否存在摆动高点
     */
    static bool hasSwingHigh(const std::vector<Kline>& klines, int left_bars = 5, int right_bars = 5);
    
    /**
     * 检查是否存在摆动低点
     */
    static bool hasSwingLow(const std::vector<Kline>& klines, int left_bars = 5, int right_bars = 5);
    
    /**
     * 获取最近的摆动点
     * @param klines K线数据
     * @param type_filter 类型过滤（"high"/"low"/"any"）
     * @param left_bars 左侧K线数量
     * @param right_bars 右侧K线数量
     * @param lookback 回看索引（0=最近的）
     * @return 摆动点信息
     */
    static SwingPoint getRecentSwingPoint(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int left_bars = 5,
        int right_bars = 5,
        int lookback = 0
    );
    
    /**
     * 获取摆动点价格
     */
    static double getSwingPrice(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int left_bars = 5,
        int right_bars = 5,
        int lookback = 0
    );
    
    /**
     * 获取摆动点强度
     */
    static int getSwingStrength(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int left_bars = 5,
        int right_bars = 5,
        int lookback = 0
    );
    
    /**
     * 检查摆动点是否被突破
     */
    static bool isSwingBroken(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int left_bars = 5,
        int right_bars = 5,
        int lookback = 0
    );
    
    /**
     * 统计摆动点数量
     */
    static int countSwingPoints(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int left_bars = 5,
        int right_bars = 5,
        int period = 100
    );
    
    /**
     * 获取当前价格距离最近摆动点的距离百分比
     */
    static double getDistanceToSwing(
        const std::vector<Kline>& klines,
        const std::string& type_filter = "any",
        int left_bars = 5,
        int right_bars = 5
    );

private:
    /**
     * 检测指定位置是否为摆动高点
     */
    static bool isSwingHigh(const std::vector<Kline>& klines, size_t index, int left_bars, int right_bars);
    
    /**
     * 检测指定位置是否为摆动低点
     */
    static bool isSwingLow(const std::vector<Kline>& klines, size_t index, int left_bars, int right_bars);
    
    /**
     * 计算摆动点强度
     */
    static int calculateStrength(const std::vector<Kline>& klines, size_t index, SwingPointType type, int left_bars, int right_bars);
    
    /**
     * 检查摆动点是否被突破
     */
    static bool checkIfBroken(const std::vector<Kline>& klines, size_t swing_index, SwingPointType type, double price);
};

} // namespace functions
} // namespace prophet

