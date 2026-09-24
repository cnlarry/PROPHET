#pragma once

#include "prophet/common/types.hpp"

#include <string>
#include <vector>
#include <deque>

namespace prophet::functions {

// 使用 prophet 命名空间中的 Kline 定义
using Kline = prophet::Kline;

/**
 * FVG（Fair Value Gap）区域信息
 * 
 * FVG是价格快速移动时形成的"价格空白区域"，常被视为市场失衡
 * 价格往往会回补这些区域，因此FVG是重要的支撑/阻力位
 */
struct FVGZone {
    double top;              // 上边界
    double bottom;           // 下边界
    double mid;              // 中间价 = (top + bottom) / 2
    double size;             // FVG大小 = top - bottom
    std::string type;        // "bullish" 或 "bearish"
    int formed_index;        // FVG形成时的K线索引（在原始数组中的位置）
    int64_t formed_time;     // FVG形成时间戳
    bool is_filled;          // 是否被完全回补
    double filled_pct;       // 回补百分比 (0-100)
    double remaining_top;    // 剩余区域上边界
    double remaining_bottom; // 剩余区域下边界
    double remaining_mid;    // 剩余区域中点 = (remaining_top + remaining_bottom) / 2 【派生字段】
    double remaining_size;   // 剩余区域大小 = remaining_top - remaining_bottom 【派生字段】
    
    /**
     * 构造函数
     */
    FVGZone() : 
        top(0), bottom(0), mid(0), size(0), 
        type(""), formed_index(-1), formed_time(0),
        is_filled(false), filled_pct(0),
        remaining_top(0), remaining_bottom(0),
        remaining_mid(0), remaining_size(0) {}
    
    /**
     * 计算剩余区域大小（已废弃，使用 remaining_size 字段代替）
     * @deprecated 保留此方法以兼容旧代码，建议使用 remaining_size 字段
     */
    double remainingSize() const {
        return remaining_size;
    }
};

/**
 * FVG分析器
 * 
 * 使用最近3根K线检测FVG形成，并追踪回补状态
 * 
 * 架构说明：
 * - FVG.hpp/cpp：FVG检测和分析核心实现（本文件）
 * - Data.cpp：DSL集成层，将FVG函数注册到FunctionRegistry
 * 
 * 设计理由：
 * FVG是数据函数（Data Function），而不是技术指标（Indicator）。
 * 因此归类在 functions 目录而非 indicators 目录。
 * 
 * 数据函数特点：
 * - 无需预配置参数
 * - 使用时动态计算
 * - 可在同一策略中多次调用
 * 
 * FVG检测原理：
 * - 看涨FVG：K线1的high < K线3的low（中间留有向上的空隙）
 * - 看跌FVG：K线1的low > K线3的high（中间留有向下的空隙）
 */
class FVGAnalyzer {
public:
    FVGAnalyzer() = default;
    
    // ========================================================================
    // 主要接口
    // ========================================================================
    
    /**
     * 检测当前是否形成看涨FVG
     * 
     * @param klines K线数据（至少3根）
     * @return true=形成看涨FVG，false=未形成
     * 
     * 检测逻辑：
     * - 使用最近3根K线：[n-2], [n-1], [n]
     * - 如果 klines[n-2].high < klines[n].low，形成看涨FVG
     * - 空隙区域：[klines[n-2].high, klines[n].low]
     */
    static bool detectBullishFVG(const std::vector<Kline>& klines);
    
    /**
     * 检测当前是否形成看跌FVG
     * 
     * @param klines K线数据（至少3根）
     * @return true=形成看跌FVG，false=未形成
     * 
     * 检测逻辑：
     * - 使用最近3根K线：[n-2], [n-1], [n]
     * - 如果 klines[n-2].low > klines[n].high，形成看跌FVG
     * - 空隙区域：[klines[n].high, klines[n-2].low]
     */
    static bool detectBearishFVG(const std::vector<Kline>& klines);
    
    /**
     * 检测当前是否形成任意类型的FVG
     * 
     * @param klines K线数据（至少3根）
     * @return true=形成FVG，false=未形成
     */
    static bool detectAnyFVG(const std::vector<Kline>& klines);
    
    /**
     * 获取最近的未回补FVG列表
     * 
     * @param klines K线数据
     * @param max_lookback 最大回溯期数（默认100）
     * @param min_fvg_size 最小FVG大小过滤（默认0，不过滤）
     * @return 未回补的FVG列表（按时间倒序：最新的在前）
     * 
     * 使用场景：
     * - 获取最近的FVG：getUnfilledFVGs(klines)[0]
     * - 获取前1个FVG：getUnfilledFVGs(klines)[1]
     * - 统计FVG数量：getUnfilledFVGs(klines).size()
     */
    static std::vector<FVGZone> getUnfilledFVGs(
        const std::vector<Kline>& klines,
        int max_lookback = 100,
        double min_fvg_size = 0.0
    );
    
    /**
     * 统计指定周期内形成的FVG数量
     * 
     * @param klines K线数据
     * @param period 统计周期
     * @param type "all"=所有类型, "bullish"=看涨, "bearish"=看跌, "unfilled"=未回补
     * @return FVG数量
     */
    static int countFVGs(
        const std::vector<Kline>& klines,
        int period,
        const std::string& type = "all"
    );
    
    // ========================================================================
    // 辅助函数
    // ========================================================================
    
    /**
     * 创建FVG区域对象
     * 
     * @param klines K线数据
     * @param index FVG形成的位置（指向第3根K线）
     * @param type FVG类型（"bullish" 或 "bearish"）
     * @return FVGZone对象
     */
    static FVGZone createFVGZone(
        const std::vector<Kline>& klines,
        size_t index,
        const std::string& type
    );
    
    /**
     * 计算FVG的回补状态
     * 
     * @param fvg FVG区域对象
     * @param subsequent_klines FVG形成后的K线数据
     * @return 回补百分比（0-100）
     * 
     * 回补计算逻辑：
     * - 看涨FVG：价格向下回补，检测low是否进入FVG区域
     * - 看跌FVG：价格向上回补，检测high是否进入FVG区域
     * - 回补百分比 = (已回补部分 / FVG总大小) * 100
     */
    static void updateFillStatus(
        FVGZone& fvg,
        const std::vector<Kline>& subsequent_klines
    );
    
private:
    /**
     * 验证K线数据是否足够
     */
    static bool validateKlines(const std::vector<Kline>& klines, size_t min_size = 3);
};

} // namespace prophet::functions

