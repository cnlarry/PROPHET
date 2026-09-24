/*
 * ============================================================================
 * 文件名：FVG.cpp
 * 功能说明：Fair Value Gap（公平价值缺口）检测和分析实现
 * 
 * FVG是价格快速移动时形成的"价格空白区域"，在技术分析中被视为：
 * - 市场失衡的标志
 * - 重要的支撑/阻力位
 * - 价格磁铁（价格往往会回补这些区域）
 * 
 * 应用场景：
 * - SMC（Smart Money Concepts）交易策略
 * - 订单块（Order Block）分析
 * - 机构交易痕迹识别
 * ============================================================================
 */

#include "prophet/functions/FVG.hpp"

#include <algorithm>
#include <cmath>
#include <limits>

namespace prophet::functions {

// ============================================================================
// 主要接口实现
// ============================================================================

bool FVGAnalyzer::detectBullishFVG(const std::vector<Kline>& klines) {
    if (!validateKlines(klines, 3)) {
        return false;
    }
    
    size_t n = klines.size();
    
    // 使用最近3根K线：[n-3], [n-2], [n-1]
    // 注意：索引从0开始，所以最后一根是 klines[n-1]
    const Kline& k1 = klines[n - 3];  // 第1根K线
    // const Kline& k2 = klines[n - 2];  // 第2根K线（中间K线）- FVG检测不需要使用k2
    const Kline& k3 = klines[n - 1];  // 第3根K线（最新K线）
    
    // 看涨FVG条件：K1的最高价 < K3的最低价
    // 这意味着K1和K3之间有一个向上的价格空隙，K2没有填补这个空隙
    return k1.high < k3.low;
}

bool FVGAnalyzer::detectBearishFVG(const std::vector<Kline>& klines) {
    if (!validateKlines(klines, 3)) {
        return false;
    }
    
    size_t n = klines.size();
    
    // 使用最近3根K线
    const Kline& k1 = klines[n - 3];  // 第1根K线
    // const Kline& k2 = klines[n - 2];  // 第2根K线（中间K线）- FVG检测不需要使用k2
    const Kline& k3 = klines[n - 1];  // 第3根K线（最新K线）
    
    // 看跌FVG条件：K1的最低价 > K3的最高价
    // 这意味着K1和K3之间有一个向下的价格空隙，K2没有填补这个空隙
    return k1.low > k3.high;
}

bool FVGAnalyzer::detectAnyFVG(const std::vector<Kline>& klines) {
    return detectBullishFVG(klines) || detectBearishFVG(klines);
}

std::vector<FVGZone> FVGAnalyzer::getUnfilledFVGs(
    const std::vector<Kline>& klines,
    int max_lookback,
    double min_fvg_size
) {
    std::vector<FVGZone> unfilled_fvgs;
    
    if (!validateKlines(klines, 3)) {
        return unfilled_fvgs;
    }
    
    size_t n = klines.size();
    
    // 限制回溯范围
    int lookback = std::min(max_lookback, static_cast<int>(n) - 2);
    if (lookback < 1) {
        return unfilled_fvgs;
    }
    
    // 从最近的位置开始向前扫描，寻找FVG
    // 注意：我们需要至少3根K线来检测FVG，所以从 n-1 开始（第3根K线位置）
    for (int i = static_cast<int>(n) - 1; i >= 2 && static_cast<int>(n) - i <= lookback; --i) {
        const Kline& k1 = klines[i - 2];  // 第1根K线
        // const Kline& k2 = klines[i - 1];  // 第2根K线 - FVG检测不需要使用k2
        const Kline& k3 = klines[i];      // 第3根K线
        
        FVGZone fvg;
        bool is_fvg = false;
        
        // 检测看涨FVG
        if (k1.high < k3.low) {
            fvg = createFVGZone(klines, i, "bullish");
            is_fvg = true;
        }
        // 检测看跌FVG
        else if (k1.low > k3.high) {
            fvg = createFVGZone(klines, i, "bearish");
            is_fvg = true;
        }
        
        if (is_fvg) {
            // 过滤太小的FVG
            if (fvg.size < min_fvg_size) {
                continue;
            }
            
            // 计算FVG形成后的回补状态
            std::vector<Kline> subsequent_klines(klines.begin() + i + 1, klines.end());
            updateFillStatus(fvg, subsequent_klines);
            
            // 只保留未完全回补的FVG
            if (!fvg.is_filled) {
                unfilled_fvgs.push_back(fvg);
            }
        }
    }
    
    // 结果已经是按时间倒序（最新的在前）
    return unfilled_fvgs;
}

int FVGAnalyzer::countFVGs(
    const std::vector<Kline>& klines,
    int period,
    const std::string& type
) {
    if (!validateKlines(klines, 3) || period < 1) {
        return 0;
    }
    
    size_t n = klines.size();
    int count = 0;
    
    // 限制统计范围
    int lookback = std::min(period, static_cast<int>(n) - 2);
    if (lookback < 1) {
        return 0;
    }
    
    // 扫描指定周期内的FVG
    for (int i = static_cast<int>(n) - 1; i >= 2 && static_cast<int>(n) - i <= lookback; --i) {
        const Kline& k1 = klines[i - 2];
        // const Kline& k2 = klines[i - 1];  // FVG检测不需要使用k2
        const Kline& k3 = klines[i];
        
        bool is_bullish = k1.high < k3.low;
        bool is_bearish = k1.low > k3.high;
        
        // 根据类型统计
        if (type == "bullish" && is_bullish) {
            count++;
        } else if (type == "bearish" && is_bearish) {
            count++;
        } else if (type == "all" && (is_bullish || is_bearish)) {
            count++;
        } else if (type == "unfilled") {
            // 对于unfilled类型，需要检查回补状态
            if (is_bullish || is_bearish) {
                FVGZone fvg = createFVGZone(klines, i, is_bullish ? "bullish" : "bearish");
                std::vector<Kline> subsequent_klines(klines.begin() + i + 1, klines.end());
                updateFillStatus(fvg, subsequent_klines);
                
                if (!fvg.is_filled) {
                    count++;
                }
            }
        }
    }
    
    return count;
}

// ============================================================================
// 辅助函数实现
// ============================================================================

FVGZone FVGAnalyzer::createFVGZone(
    const std::vector<Kline>& klines,
    size_t index,
    const std::string& type
) {
    FVGZone fvg;
    
    if (index < 2 || index >= klines.size()) {
        return fvg;  // 返回空FVG
    }
    
    const Kline& k1 = klines[index - 2];
    const Kline& k3 = klines[index];
    
    fvg.type = type;
    fvg.formed_index = static_cast<int>(index);
    fvg.formed_time = k3.open_time;  // 使用open_time替代timestamp
    
    if (type == "bullish") {
        // 看涨FVG：空隙在上方
        fvg.bottom = k1.high;
        fvg.top = k3.low;
    } else if (type == "bearish") {
        // 看跌FVG：空隙在下方
        fvg.bottom = k3.high;
        fvg.top = k1.low;
    }
    
    fvg.mid = (fvg.top + fvg.bottom) / 2.0;
    fvg.size = fvg.top - fvg.bottom;
    
    // 初始状态：未回补
    fvg.is_filled = false;
    fvg.filled_pct = 0.0;
    fvg.remaining_top = fvg.top;
    fvg.remaining_bottom = fvg.bottom;
    
    // ✅ 计算派生字段
    fvg.remaining_mid = (fvg.remaining_top + fvg.remaining_bottom) / 2.0;
    fvg.remaining_size = fvg.remaining_top - fvg.remaining_bottom;
    
    return fvg;
}

void FVGAnalyzer::updateFillStatus(
    FVGZone& fvg,
    const std::vector<Kline>& subsequent_klines
) {
    if (subsequent_klines.empty()) {
        return;  // 没有后续K线，无法计算回补
    }
    
    double fvg_size = fvg.size;
    if (fvg_size <= 0) {
        return;  // 无效的FVG大小
    }
    
    // 追踪最深的回补程度
    double max_fill_penetration = 0.0;  // 从FVG边界开始计算的最大渗透距离
    
    if (fvg.type == "bullish") {
        // 看涨FVG：价格从上方向下回补
        // 检测后续K线的low是否进入FVG区域
        
        for (const auto& kline : subsequent_klines) {
            if (kline.low < fvg.top) {
                // 价格进入FVG区域
                double penetration = fvg.top - kline.low;
                
                // 但不能超过FVG的底部
                penetration = std::min(penetration, fvg_size);
                
                // 更新最大渗透深度
                max_fill_penetration = std::max(max_fill_penetration, penetration);
            }
        }
        
        // 计算回补百分比
        fvg.filled_pct = (max_fill_penetration / fvg_size) * 100.0;
        
        // 更新剩余区域
        if (fvg.filled_pct >= 100.0) {
            fvg.is_filled = true;
            fvg.filled_pct = 100.0;
            fvg.remaining_top = fvg.bottom;
            fvg.remaining_bottom = fvg.bottom;
        } else {
            fvg.is_filled = false;
            fvg.remaining_top = fvg.top;
            fvg.remaining_bottom = fvg.top - max_fill_penetration;
        }
        
        // ✅ 更新派生字段
        fvg.remaining_mid = (fvg.remaining_top + fvg.remaining_bottom) / 2.0;
        fvg.remaining_size = fvg.remaining_top - fvg.remaining_bottom;
        
    } else if (fvg.type == "bearish") {
        // 看跌FVG：价格从下方向上回补
        // 检测后续K线的high是否进入FVG区域
        
        for (const auto& kline : subsequent_klines) {
            if (kline.high > fvg.bottom) {
                // 价格进入FVG区域
                double penetration = kline.high - fvg.bottom;
                
                // 但不能超过FVG的顶部
                penetration = std::min(penetration, fvg_size);
                
                // 更新最大渗透深度
                max_fill_penetration = std::max(max_fill_penetration, penetration);
            }
        }
        
        // 计算回补百分比
        fvg.filled_pct = (max_fill_penetration / fvg_size) * 100.0;
        
        // 更新剩余区域
        if (fvg.filled_pct >= 100.0) {
            fvg.is_filled = true;
            fvg.filled_pct = 100.0;
            fvg.remaining_top = fvg.top;
            fvg.remaining_bottom = fvg.top;
        } else {
            fvg.is_filled = false;
            fvg.remaining_top = fvg.bottom + max_fill_penetration;
            fvg.remaining_bottom = fvg.bottom;
        }
        
        // ✅ 更新派生字段
        fvg.remaining_mid = (fvg.remaining_top + fvg.remaining_bottom) / 2.0;
        fvg.remaining_size = fvg.remaining_top - fvg.remaining_bottom;
    }
}

bool FVGAnalyzer::validateKlines(const std::vector<Kline>& klines, size_t min_size) {
    return klines.size() >= min_size;
}

} // namespace prophet::functions

