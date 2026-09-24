/*
 * ============================================================================
 * 文件名：Pattern.cpp
 * 功能说明：K线形态识别实�?
 * 
 * 这个文件实现了各种经典的K线形态识�?
 * 使用TA-Lib库识别日本蜡烛图形�?
 * 
 * 支持的形态（60多种）：
 * - 反转形态：锤子线、吊颈线、吞没形态、启明星、黄昏星、十字星�?
 * - 持续形态：三白兵、三黑鸦、上升三法、下降三法等
 * - 中性形态：十字星、纺锤线�?
 * 
 * 形态识别在技术分析中用于预测价格转折�?
 * 
 * 使用示例�?
 *   PATTERN(5m).ENGULFING(0.3)  // 识别吞没形�?
 *   PATTERN(1h).HAMMER()  // 识别锤子�?
 * ============================================================================
 */

#include "prophet/functions/Pattern.hpp"

extern "C" {
#include "ta_libc.h"
}

#include <algorithm>
#include <climits>
#include <cstring>

// 辅助函数：安全地�?size_t 转换�?int（用�?TA-Lib 函数调用�?
static inline int safeSizeToInt(size_t size) {
    return static_cast<int>(size > static_cast<size_t>(INT_MAX) ? INT_MAX : size);
}

namespace prophet::functions {

// 辅助函数：准备OHLC数据
void PatternRecognizer::prepareOHLC(const std::vector<Kline>& klines,
                                    std::vector<double>& open,
                                    std::vector<double>& high,
                                    std::vector<double>& low,
                                    std::vector<double>& close) {
    size_t n = klines.size();
    open.resize(n);
    high.resize(n);
    low.resize(n);
    close.resize(n);

    for (size_t i = 0; i < n; ++i) {
        open[i] = klines[i].open;
        high[i] = klines[i].high;
        low[i] = klines[i].low;
        close[i] = klines[i].close;
    }
}

// ============================================================================
// 主识别函�?
// ============================================================================

bool PatternRecognizer::recognize(const std::vector<Kline>& klines,
                                  const std::string& pattern_name,
                                  double penetration) {
    if (klines.size() < 3) {
        return false;  // 大部分形态需要至�?根K�?
    }

    // 阶段1�?0个核心形�?
    if (pattern_name == "hammer") {
        return recognizeHammer(klines);
    } else if (pattern_name == "doji") {
        return recognizeDoji(klines);
    } else if (pattern_name == "engulfing_bullish") {
        return recognizeEngulfingBullish(klines);
    } else if (pattern_name == "engulfing_bearish") {
        return recognizeEngulfingBearish(klines);
    } else if (pattern_name == "morning_star") {
        return recognizeMorningStar(klines, penetration);
    } else if (pattern_name == "evening_star") {
        return recognizeEveningStar(klines, penetration);
    } else if (pattern_name == "shooting_star") {
        return recognizeShootingStar(klines);
    } else if (pattern_name == "hanging_man") {
        return recognizeHangingMan(klines);
    } else if (pattern_name == "three_white_soldiers") {
        return recognizeThreeWhiteSoldiers(klines);
    } else if (pattern_name == "three_black_crows") {
        return recognizeThreeBlackCrows(klines);
    }

    // 阶段2�?5个常用形�?
    // 看涨形�?
    else if (pattern_name == "inverted_hammer") {
        return recognizeInvertedHammer(klines);
    } else if (pattern_name == "morning_doji_star") {
        return recognizeMorningDojiStar(klines, penetration);
    } else if (pattern_name == "piercing") {
        return recognizePiercing(klines);
    } else if (pattern_name == "three_inside_up") {
        return recognizeThreeInsideUp(klines);
    } else if (pattern_name == "three_outside_up") {
        return recognizeThreeOutsideUp(klines);
    }

    // 看跌形�?
    else if (pattern_name == "evening_doji_star") {
        return recognizeEveningDojiStar(klines, penetration);
    } else if (pattern_name == "dark_cloud_cover") {
        return recognizeDarkCloudCover(klines, penetration);
    } else if (pattern_name == "three_inside_down") {
        return recognizeThreeInsideDown(klines);
    } else if (pattern_name == "three_outside_down") {
        return recognizeThreeOutsideDown(klines);
    }

    // 中�?反转形�?
    else if (pattern_name == "long_legged_doji") {
        return recognizeLongLeggedDoji(klines);
    } else if (pattern_name == "dragonfly_doji") {
        return recognizeDragonflyDoji(klines);
    } else if (pattern_name == "gravestone_doji") {
        return recognizeGravestoneDoji(klines);
    } else if (pattern_name == "spinning_top") {
        return recognizeSpinningTop(klines);
    } else if (pattern_name == "marubozu") {
        return recognizeMarubozu(klines);
    } else if (pattern_name == "harami") {
        return recognizeHarami(klines);
    }

    // 阶段3�?6个完整形�?
    // 看涨形�?
    else if (pattern_name == "abandoned_baby") {
        return recognizeAbandonedBaby(klines, penetration);
    } else if (pattern_name == "advance_block") {
        return recognizeAdvanceBlock(klines);
    } else if (pattern_name == "belt_hold") {
        return recognizeBeltHold(klines);
    } else if (pattern_name == "breakaway") {
        return recognizeBreakaway(klines);
    } else if (pattern_name == "closing_marubozu") {
        return recognizeClosingMarubozu(klines);
    } else if (pattern_name == "conceal_babys_wall") {
        return recognizeConcealBabysWall(klines);
    } else if (pattern_name == "counter_attack") {
        return recognizeCounterAttack(klines);
    } else if (pattern_name == "doji_star") {
        return recognizeDojiStar(klines);
    } else if (pattern_name == "gap_side_side_white") {
        return recognizeGapSideSideWhite(klines);
    } else if (pattern_name == "harami_cross") {
        return recognizeHaramiCross(klines);
    } else if (pattern_name == "homing_pigeon") {
        return recognizeHomingPigeon(klines);
    } else if (pattern_name == "kicking") {
        return recognizeKicking(klines);
    } else if (pattern_name == "ladder_bottom") {
        return recognizeLadderBottom(klines);
    } else if (pattern_name == "long_line") {
        return recognizeLongLine(klines);
    }

    // 看跌形�?
    else if (pattern_name == "hikkake") {
        return recognizeHikkake(klines);
    } else if (pattern_name == "hikkake_mod") {
        return recognizeHikkakeMod(klines);
    } else if (pattern_name == "high_wave") {
        return recognizeHighWave(klines);
    } else if (pattern_name == "identical_3_crows") {
        return recognizeIdentical3Crows(klines);
    } else if (pattern_name == "in_neck") {
        return recognizeInNeck(klines);
    } else if (pattern_name == "kicking_by_length") {
        return recognizeKickingByLength(klines);
    } else if (pattern_name == "mat_hold") {
        return recognizeMatHold(klines, penetration);
    } else if (pattern_name == "matching_low") {
        return recognizeMatchingLow(klines);
    } else if (pattern_name == "on_neck") {
        return recognizeOnNeck(klines);
    } else if (pattern_name == "rickshaw_man") {
        return recognizeRickshawMan(klines);
    } else if (pattern_name == "separating_lines") {
        return recognizeSeparatingLines(klines);
    } else if (pattern_name == "short_line") {
        return recognizeShortLine(klines);
    }

    // 中�?反转形�?
    else if (pattern_name == "stalled_pattern") {
        return recognizeStalledPattern(klines);
    } else if (pattern_name == "stick_sandwich") {
        return recognizeStickSandwich(klines);
    } else if (pattern_name == "takuri") {
        return recognizeTakuri(klines);
    } else if (pattern_name == "tasuki_gap") {
        return recognizeTasukiGap(klines);
    } else if (pattern_name == "thrusting") {
        return recognizeThrusting(klines);
    } else if (pattern_name == "tristar") {
        return recognizeTristar(klines);
    } else if (pattern_name == "unique_3_river") {
        return recognizeUnique3River(klines);
    } else if (pattern_name == "upside_gap_2_crows") {
        return recognizeUpsideGap2Crows(klines);
    } else if (pattern_name == "xside_gap_3_methods") {
        return recognizeXSideGap3Methods(klines);
    } else if (pattern_name == "two_crows") {
        return recognize2Crows(klines);
    }

    // 未知形�?
    return false;
}

std::unordered_map<std::string, bool> PatternRecognizer::recognizeAll(
    const std::vector<Kline>& klines,
    double penetration) {
    
    std::unordered_map<std::string, bool> results;

    // 所�?1个形�?
    std::vector<std::string> patterns = {
        // 阶段1: 10个核心形�?
        "hammer", "doji", "engulfing_bullish", "engulfing_bearish",
        "morning_star", "evening_star", "shooting_star", "hanging_man",
        "three_white_soldiers", "three_black_crows",
        // 阶段2: 15个常用形�?
        "inverted_hammer", "morning_doji_star", "piercing",
        "three_inside_up", "three_outside_up",
        "evening_doji_star", "dark_cloud_cover",
        "three_inside_down", "three_outside_down",
        "long_legged_doji", "dragonfly_doji", "gravestone_doji",
        "spinning_top", "marubozu", "harami",
        // 阶段3: 36个完整形�?
        "abandoned_baby", "advance_block", "belt_hold", "breakaway",
        "closing_marubozu", "conceal_babys_wall", "counter_attack", "doji_star",
        "gap_side_side_white", "harami_cross", "homing_pigeon", "kicking",
        "ladder_bottom", "long_line",
        "hikkake", "hikkake_mod", "high_wave", "identical_3_crows",
        "in_neck", "kicking_by_length", "mat_hold", "matching_low",
        "on_neck", "rickshaw_man", "separating_lines", "short_line",
        "stalled_pattern", "stick_sandwich", "takuri", "tasuki_gap",
        "thrusting", "tristar", "unique_3_river", "upside_gap_2_crows",
        "xside_gap_3_methods", "two_crows"
    };

    for (const auto& pattern : patterns) {
        results[pattern] = recognize(klines, pattern, penetration);
    }

    return results;
}

// ============================================================================
// 阶段1�?0个核心形态的实现
// ============================================================================

bool PatternRecognizer::recognizeHammer(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    int endIdx = safeSizeToInt(klines_size) - 1;
    TA_RetCode retCode = TA_CDLHAMMER(
        0, endIdx,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeDoji(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLDOJI(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeEngulfingBullish(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLENGULFING(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        // 正值表示看涨吞�?
        return outInteger[outNbElement - 1] > 0;
    }
    return false;
}

bool PatternRecognizer::recognizeEngulfingBearish(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLENGULFING(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        // 负值表示看跌吞�?
        return outInteger[outNbElement - 1] < 0;
    }
    return false;
}

bool PatternRecognizer::recognizeMorningStar(const std::vector<Kline>& klines, double penetration) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLMORNINGSTAR(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        penetration,
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeEveningStar(const std::vector<Kline>& klines, double penetration) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLEVENINGSTAR(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        penetration,
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeShootingStar(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLSHOOTINGSTAR(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeHangingMan(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLHANGINGMAN(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeThreeWhiteSoldiers(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDL3WHITESOLDIERS(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeThreeBlackCrows(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDL3BLACKCROWS(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

// ============================================================================
// 阶段2�?5个常用形态的实现
// ============================================================================

// 看涨形态（5个）

bool PatternRecognizer::recognizeInvertedHammer(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLINVERTEDHAMMER(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeMorningDojiStar(const std::vector<Kline>& klines, double penetration) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLMORNINGDOJISTAR(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        penetration,
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizePiercing(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLPIERCING(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeThreeInsideUp(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDL3INSIDE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        // 正值表示看涨（三内上升�?
        return outInteger[outNbElement - 1] > 0;
    }
    return false;
}

bool PatternRecognizer::recognizeThreeOutsideUp(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDL3OUTSIDE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        // 正值表示看涨（三外上升�?
        return outInteger[outNbElement - 1] > 0;
    }
    return false;
}

// 看跌形态（4个）

bool PatternRecognizer::recognizeEveningDojiStar(const std::vector<Kline>& klines, double penetration) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLEVENINGDOJISTAR(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        penetration,
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeDarkCloudCover(const std::vector<Kline>& klines, double penetration) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLDARKCLOUDCOVER(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        penetration,
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeThreeInsideDown(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDL3INSIDE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        // 负值表示看跌（三内下降�?
        return outInteger[outNbElement - 1] < 0;
    }
    return false;
}

bool PatternRecognizer::recognizeThreeOutsideDown(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDL3OUTSIDE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        // 负值表示看跌（三外下降�?
        return outInteger[outNbElement - 1] < 0;
    }
    return false;
}

// 中�?反转形态（6个）

bool PatternRecognizer::recognizeLongLeggedDoji(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLLONGLEGGEDDOJI(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeDragonflyDoji(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLDRAGONFLYDOJI(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeGravestoneDoji(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLGRAVESTONEDOJI(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeSpinningTop(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLSPINNINGTOP(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeMarubozu(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLMARUBOZU(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeHarami(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLHARAMI(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

// ============================================================================
// 阶段3�?6个完整形态的实现
// ============================================================================

// 看涨形态（14个）

bool PatternRecognizer::recognizeAbandonedBaby(const std::vector<Kline>& klines, double penetration) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLABANDONEDBABY(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        penetration,
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeAdvanceBlock(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLADVANCEBLOCK(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeBeltHold(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLBELTHOLD(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeBreakaway(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLBREAKAWAY(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeClosingMarubozu(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLCLOSINGMARUBOZU(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeConcealBabysWall(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLCONCEALBABYSWALL(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeCounterAttack(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLCOUNTERATTACK(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeDojiStar(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLDOJISTAR(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeGapSideSideWhite(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLGAPSIDESIDEWHITE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeHaramiCross(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLHARAMICROSS(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeHomingPigeon(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLHOMINGPIGEON(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeKicking(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLKICKING(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeLadderBottom(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLLADDERBOTTOM(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeLongLine(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLLONGLINE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

// 看跌形态（12个）

bool PatternRecognizer::recognizeHikkake(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLHIKKAKE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeHikkakeMod(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLHIKKAKEMOD(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeHighWave(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLHIGHWAVE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeIdentical3Crows(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLIDENTICAL3CROWS(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeInNeck(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLINNECK(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeKickingByLength(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLKICKINGBYLENGTH(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeMatHold(const std::vector<Kline>& klines, double penetration) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLMATHOLD(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        penetration,
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeMatchingLow(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLMATCHINGLOW(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeOnNeck(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLONNECK(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeRickshawMan(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLRICKSHAWMAN(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeSeparatingLines(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLSEPARATINGLINES(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeShortLine(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLSHORTLINE(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

// 中�?反转形态（10个）

bool PatternRecognizer::recognizeStalledPattern(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLSTALLEDPATTERN(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeStickSandwich(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLSTICKSANDWICH(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeTakuri(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLTAKURI(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeTasukiGap(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLTASUKIGAP(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeThrusting(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLTHRUSTING(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeTristar(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLTRISTAR(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeUnique3River(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLUNIQUE3RIVER(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeUpsideGap2Crows(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLUPSIDEGAP2CROWS(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognizeXSideGap3Methods(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDLXSIDEGAP3METHODS(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

bool PatternRecognizer::recognize2Crows(const std::vector<Kline>& klines) {
    std::vector<double> open, high, low, close;
    prepareOHLC(klines, open, high, low, close);

    int outBeg, outNbElement;
    size_t klines_size = klines.size();
    std::vector<int> outInteger(klines_size);

    TA_RetCode retCode = TA_CDL2CROWS(
        0, safeSizeToInt(klines.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBeg, &outNbElement, outInteger.data()
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        return outInteger[outNbElement - 1] != 0;
    }
    return false;
}

} // namespace prophet::functions
