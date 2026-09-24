#include "prophet/functions/BreakerBlock.hpp"
#include <algorithm>
#include <cmath>

namespace prophet {
namespace functions {

std::vector<BreakerBlock> BreakerBlockAnalyzer::detectBreakerBlocks(
    const std::vector<Kline>& klines,
    int period
) {
    std::vector<BreakerBlock> breaker_blocks;
    
    if (klines.size() < 20) {
        return breaker_blocks;
    }
    
    // 首先检测所有Order Blocks
    auto order_blocks = OrderBlockAnalyzer::detectOrderBlocks(klines, period, 3, 30);
    
    // 检查每个Order Block是否被突破，转换为Breaker Block
    for (const auto& ob : order_blocks) {
        if (isOrderBlockBroken(klines, ob)) {
            int break_index = findBreakIndex(klines, ob);
            if (break_index > 0) {
                BreakerBlock bb = convertFromOrderBlock(klines, ob, break_index);
                breaker_blocks.push_back(bb);
            }
        }
    }
    
    return breaker_blocks;
}

bool BreakerBlockAnalyzer::detectBullishBreakerBlock(const std::vector<Kline>& klines) {
    auto bbs = detectBreakerBlocks(klines, 50);
    
    for (const auto& bb : bbs) {
        if (bb.type == BreakerBlockType::BULLISH && !bb.is_broken) {
            return true;
        }
    }
    
    return false;
}

bool BreakerBlockAnalyzer::detectBearishBreakerBlock(const std::vector<Kline>& klines) {
    auto bbs = detectBreakerBlocks(klines, 50);
    
    for (const auto& bb : bbs) {
        if (bb.type == BreakerBlockType::BEARISH && !bb.is_broken) {
            return true;
        }
    }
    
    return false;
}

BreakerBlock BreakerBlockAnalyzer::getRecentBreakerBlock(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bbs = detectBreakerBlocks(klines, 100);
    
    // 过滤类型并移除已失效的突破块
    std::vector<BreakerBlock> filtered_bbs;
    for (const auto& bb : bbs) {
        if (bb.is_broken) continue;
        
        if (type_filter == "bullish" && bb.type != BreakerBlockType::BULLISH) continue;
        if (type_filter == "bearish" && bb.type != BreakerBlockType::BEARISH) continue;
        
        filtered_bbs.push_back(bb);
    }
    
    // 按形成时间排序（最近的在前）
    std::sort(filtered_bbs.begin(), filtered_bbs.end(),
        [](const BreakerBlock& a, const BreakerBlock& b) {
            return a.formed_index > b.formed_index;
        });
    
    if (lookback >= static_cast<int>(filtered_bbs.size())) {
        // 返回空的BreakerBlock
        BreakerBlock empty_bb;
        empty_bb.type = BreakerBlockType::NONE;
        empty_bb.top = 0.0;
        empty_bb.bottom = 0.0;
        empty_bb.mid = 0.0;
        empty_bb.formed_index = -1;
        empty_bb.ob_index = -1;
        empty_bb.original_ob_type = OrderBlockType::NONE;
        empty_bb.strength = 0;
        empty_bb.is_tested = false;
        empty_bb.test_count = 0;
        empty_bb.is_broken = false;
        return empty_bb;
    }
    
    return filtered_bbs[lookback];
}

double BreakerBlockAnalyzer::getBreakerBlockTop(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bb = getRecentBreakerBlock(klines, type_filter, lookback);
    return bb.top;
}

double BreakerBlockAnalyzer::getBreakerBlockBottom(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bb = getRecentBreakerBlock(klines, type_filter, lookback);
    return bb.bottom;
}

double BreakerBlockAnalyzer::getBreakerBlockMid(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bb = getRecentBreakerBlock(klines, type_filter, lookback);
    return bb.mid;
}

int BreakerBlockAnalyzer::getBreakerBlockStrength(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bb = getRecentBreakerBlock(klines, type_filter, lookback);
    return bb.strength;
}

bool BreakerBlockAnalyzer::isBreakerBlockTested(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bb = getRecentBreakerBlock(klines, type_filter, lookback);
    return bb.is_tested;
}

int BreakerBlockAnalyzer::getBreakerBlockTestCount(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bb = getRecentBreakerBlock(klines, type_filter, lookback);
    return bb.test_count;
}

bool BreakerBlockAnalyzer::isBreakerBlockBroken(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int lookback
) {
    auto bb = getRecentBreakerBlock(klines, type_filter, lookback);
    return bb.is_broken;
}

int BreakerBlockAnalyzer::countBreakerBlocks(
    const std::vector<Kline>& klines,
    const std::string& type_filter,
    int period
) {
    auto bbs = detectBreakerBlocks(klines, period);
    
    int count = 0;
    for (const auto& bb : bbs) {
        if (bb.is_broken) continue;
        
        if (type_filter == "bullish" && bb.type != BreakerBlockType::BULLISH) continue;
        if (type_filter == "bearish" && bb.type != BreakerBlockType::BEARISH) continue;
        
        count++;
    }
    
    return count;
}

BreakerBlock BreakerBlockAnalyzer::convertFromOrderBlock(
    const std::vector<Kline>& klines,
    const OrderBlock& ob,
    size_t break_index
) {
    BreakerBlock bb;
    
    // 极性反转
    if (ob.type == OrderBlockType::BULLISH) {
        bb.type = BreakerBlockType::BEARISH;
    } else if (ob.type == OrderBlockType::BEARISH) {
        bb.type = BreakerBlockType::BULLISH;
    } else {
        bb.type = BreakerBlockType::NONE;
    }
    
    bb.top = ob.top;
    bb.bottom = ob.bottom;
    bb.mid = ob.mid;
    bb.formed_index = static_cast<int>(break_index);
    bb.ob_index = ob.formed_index;
    bb.original_ob_type = ob.type;
    bb.strength = ob.strength;
    bb.is_tested = checkIfTested(klines, break_index, bb.top, bb.bottom);
    bb.test_count = countTests(klines, break_index, bb.top, bb.bottom);
    bb.is_broken = checkIfBroken(klines, break_index, bb.type, bb.top, bb.bottom);
    
    return bb;
}

bool BreakerBlockAnalyzer::isOrderBlockBroken(
    const std::vector<Kline>& klines,
    const OrderBlock& ob
) {
    // 检查Order Block形成后，价格是否突破该块
    for (size_t i = ob.formed_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        if (ob.type == OrderBlockType::BULLISH) {
            // 看涨OB：如果价格收盘在底部以下，则被突破
            if (kline.close < ob.bottom) {
                return true;
            }
        } else if (ob.type == OrderBlockType::BEARISH) {
            // 看跌OB：如果价格收盘在顶部以上，则被突破
            if (kline.close > ob.top) {
                return true;
            }
        }
    }
    
    return false;
}

int BreakerBlockAnalyzer::findBreakIndex(
    const std::vector<Kline>& klines,
    const OrderBlock& ob
) {
    // 找到Order Block被突破的K线索引
    for (size_t i = ob.formed_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        if (ob.type == OrderBlockType::BULLISH) {
            if (kline.close < ob.bottom) {
                return static_cast<int>(i);
            }
        } else if (ob.type == OrderBlockType::BEARISH) {
            if (kline.close > ob.top) {
                return static_cast<int>(i);
            }
        }
    }
    
    return -1;
}

bool BreakerBlockAnalyzer::checkIfTested(
    const std::vector<Kline>& klines,
    size_t bb_index,
    double top,
    double bottom
) {
    // 检查Breaker Block形成后，价格是否回到该区域
    for (size_t i = bb_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        if (kline.low <= top && kline.high >= bottom) {
            return true;
        }
    }
    
    return false;
}

int BreakerBlockAnalyzer::countTests(
    const std::vector<Kline>& klines,
    size_t bb_index,
    double top,
    double bottom
) {
    int count = 0;
    bool in_zone = false;
    
    for (size_t i = bb_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        bool current_in_zone = (kline.low <= top && kline.high >= bottom);
        
        if (current_in_zone && !in_zone) {
            count++;
        }
        
        in_zone = current_in_zone;
    }
    
    return count;
}

bool BreakerBlockAnalyzer::checkIfBroken(
    const std::vector<Kline>& klines,
    size_t bb_index,
    BreakerBlockType type,
    double top,
    double bottom
) {
    // 检查Breaker Block是否被突破（失效）
    for (size_t i = bb_index + 1; i < klines.size(); ++i) {
        const auto& kline = klines[i];
        
        if (type == BreakerBlockType::BULLISH) {
            // 看涨BB：如果价格收盘在底部以下，则失效
            if (kline.close < bottom) {
                return true;
            }
        } else if (type == BreakerBlockType::BEARISH) {
            // 看跌BB：如果价格收盘在顶部以上，则失效
            if (kline.close > top) {
                return true;
            }
        }
    }
    
    return false;
}

} // namespace functions
} // namespace prophet

