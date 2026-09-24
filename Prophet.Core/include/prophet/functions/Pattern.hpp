#pragma once

#include "../common/types.hpp"

#include <string>
#include <unordered_map>
#include <vector>

namespace prophet::functions {

// 使用 prophet 命名空间中的 Kline 定义
using Kline = prophet::Kline;

/**
 * K线形态识别器
 * 
 * 使用TA-Lib C库识别61个K线形态
 * 
 * 架构说明：
 * - Pattern.hpp/cpp：K线形态识别核心实现（本文件）
 * - Data.cpp：DSL集成层，将PATTERN函数注册到FunctionRegistry
 * 
 * 设计理由：
 * K线形态识别是数据函数（Data Function），而不是技术指标（Indicator）。
 * 因此归类在 functions 目录而非 indicators 目录。
 * 
 * 数据函数特点：
 * - 无需预配置参数
 * - 使用时动态计算
 * - 可在同一策略中多次调用不同形态
 * 
 * 技术指标特点：
 * - 需要预先配置参数（如MACD_FAST_PERIOD）
 * - 统一预计算后存储
 * - 每个指标每个时间框架只计算一次
 */
class PatternRecognizer {
public:
    /**
     * 识别指定的K线形态
     * 
     * @param klines K线数据（至少3根）
     * @param pattern_name 形态名称（如 hammer, doji, morning_star）
     * @param penetration 穿透度参数（0.0-1.0），仅部分形态使用
     * @return true=存在该形态，false=不存在
     */
    static bool recognize(const std::vector<Kline>& klines,
                          const std::string& pattern_name,
                          double penetration = 0.3);

    /**
     * 识别所有61个支持的形态
     * 
     * @param klines K线数据
     * @param penetration 穿透度参数
     * @return 形态名称 -> 是否存在的映射
     */
    static std::unordered_map<std::string, bool> recognizeAll(
        const std::vector<Kline>& klines,
        double penetration = 0.3);

private:
    // ========== 阶段1：10个核心形态 ==========
    static bool recognizeHammer(const std::vector<Kline>& klines);
    static bool recognizeDoji(const std::vector<Kline>& klines);
    static bool recognizeEngulfingBullish(const std::vector<Kline>& klines);
    static bool recognizeEngulfingBearish(const std::vector<Kline>& klines);
    static bool recognizeMorningStar(const std::vector<Kline>& klines, double penetration);
    static bool recognizeEveningStar(const std::vector<Kline>& klines, double penetration);
    static bool recognizeShootingStar(const std::vector<Kline>& klines);
    static bool recognizeHangingMan(const std::vector<Kline>& klines);
    static bool recognizeThreeWhiteSoldiers(const std::vector<Kline>& klines);
    static bool recognizeThreeBlackCrows(const std::vector<Kline>& klines);

    // ========== 阶段2：15个常用形态 ==========
    // 看涨形态（5个）
    static bool recognizeInvertedHammer(const std::vector<Kline>& klines);
    static bool recognizeMorningDojiStar(const std::vector<Kline>& klines, double penetration);
    static bool recognizePiercing(const std::vector<Kline>& klines);
    static bool recognizeThreeInsideUp(const std::vector<Kline>& klines);
    static bool recognizeThreeOutsideUp(const std::vector<Kline>& klines);

    // 看跌形态（4个）
    static bool recognizeEveningDojiStar(const std::vector<Kline>& klines, double penetration);
    static bool recognizeDarkCloudCover(const std::vector<Kline>& klines, double penetration);
    static bool recognizeThreeInsideDown(const std::vector<Kline>& klines);
    static bool recognizeThreeOutsideDown(const std::vector<Kline>& klines);

    // 中性/反转形态（6个）
    static bool recognizeLongLeggedDoji(const std::vector<Kline>& klines);
    static bool recognizeDragonflyDoji(const std::vector<Kline>& klines);
    static bool recognizeGravestoneDoji(const std::vector<Kline>& klines);
    static bool recognizeSpinningTop(const std::vector<Kline>& klines);
    static bool recognizeMarubozu(const std::vector<Kline>& klines);
    static bool recognizeHarami(const std::vector<Kline>& klines);

    // ========== 阶段3：36个完整形态 ==========
    // 看涨形态（14个）
    static bool recognizeAbandonedBaby(const std::vector<Kline>& klines, double penetration);
    static bool recognizeAdvanceBlock(const std::vector<Kline>& klines);
    static bool recognizeBeltHold(const std::vector<Kline>& klines);
    static bool recognizeBreakaway(const std::vector<Kline>& klines);
    static bool recognizeClosingMarubozu(const std::vector<Kline>& klines);
    static bool recognizeConcealBabysWall(const std::vector<Kline>& klines);
    static bool recognizeCounterAttack(const std::vector<Kline>& klines);
    static bool recognizeDojiStar(const std::vector<Kline>& klines);
    static bool recognizeGapSideSideWhite(const std::vector<Kline>& klines);
    static bool recognizeHaramiCross(const std::vector<Kline>& klines);
    static bool recognizeHomingPigeon(const std::vector<Kline>& klines);
    static bool recognizeKicking(const std::vector<Kline>& klines);
    static bool recognizeLadderBottom(const std::vector<Kline>& klines);
    static bool recognizeLongLine(const std::vector<Kline>& klines);

    // 看跌形态（12个）
    static bool recognizeHikkake(const std::vector<Kline>& klines);
    static bool recognizeHikkakeMod(const std::vector<Kline>& klines);
    static bool recognizeHighWave(const std::vector<Kline>& klines);
    static bool recognizeIdentical3Crows(const std::vector<Kline>& klines);
    static bool recognizeInNeck(const std::vector<Kline>& klines);
    static bool recognizeKickingByLength(const std::vector<Kline>& klines);
    static bool recognizeMatHold(const std::vector<Kline>& klines, double penetration);
    static bool recognizeMatchingLow(const std::vector<Kline>& klines);
    static bool recognizeOnNeck(const std::vector<Kline>& klines);
    static bool recognizeRickshawMan(const std::vector<Kline>& klines);
    static bool recognizeSeparatingLines(const std::vector<Kline>& klines);
    static bool recognizeShortLine(const std::vector<Kline>& klines);

    // 中性/反转形态（10个）
    static bool recognizeStalledPattern(const std::vector<Kline>& klines);
    static bool recognizeStickSandwich(const std::vector<Kline>& klines);
    static bool recognizeTakuri(const std::vector<Kline>& klines);
    static bool recognizeTasukiGap(const std::vector<Kline>& klines);
    static bool recognizeThrusting(const std::vector<Kline>& klines);
    static bool recognizeTristar(const std::vector<Kline>& klines);
    static bool recognizeUnique3River(const std::vector<Kline>& klines);
    static bool recognizeUpsideGap2Crows(const std::vector<Kline>& klines);
    static bool recognizeXSideGap3Methods(const std::vector<Kline>& klines);
    static bool recognize2Crows(const std::vector<Kline>& klines);

    // 辅助函数
    static void prepareOHLC(const std::vector<Kline>& klines,
                             std::vector<double>& open,
                             std::vector<double>& high,
                             std::vector<double>& low,
                             std::vector<double>& close);

    static bool recognizeMorningDojiStarPhase3(const std::vector<Kline>& klines, double penetration);
};

} // namespace prophet::functions

