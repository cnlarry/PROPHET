/*
 * ============================================================================
 * 文件名：calculator.hpp
 * 功能说明：技术指标计算器头文件
 * 
 * 定义了技术指标计算器Calculator类
 * 
 * Calculator的职责：
 * - 使用TA-Lib库计算各种技术指标
 * - 添加业务逻辑（趋势判断、穿越检测等）
 * - 返回IndicatorResult格式的结果
 * - 与DSL引擎无缝集成
 * 
 * 支持的指标（200+种）：
 * - 趋势指标：SMA、EMA、MACD、ADX、AROON、SAR等
 * - 动量指标：RSI、STOCH、CCI、MFI、ROC、MOM等
 * - 波动率指标：ATR、BBANDS、TRANGE等
 * - 成交量指标：OBV、AD、ADOSC等
 * - 统计指标：BETA、CORREL、STDDEV、VAR等
 * - 周期指标：HT_DCPERIOD、HT_TRENDLINE等
 * 
 * 每个指标方法都返回IndicatorResult，包含多个字段：
 * - 指标值（value、signal等）
 * - 业务字段（trend、cross等）
 * - 元数据（最后更新时间等）
 * ============================================================================
 */

#pragma once

#include "../common/types.hpp"
#include <vector>
#include <string>

namespace prophet::indicators {

/**
 * 指标计算器
 *
 * 使用 TA-Lib 计算技术指标，并添加业务逻辑（趋势、穿越检测等）
 * 返回 IndicatorResult（map），与 DSL 引擎无缝集成
 */
class Calculator {
public:
    Calculator() = default;

    // ========================================================================
    // MACD - 移动平均收敛发散指标
    // ========================================================================

    /**
     * MACD - 移动平均收敛发散指标
     *
     * @param close 收盘价序列
     * @param FAST_PERIOD 快速 EMA 周期
     * @param SLOW_PERIOD 慢速 EMA 周期
     * @param SIGNAL_PERIOD 信号线周期
     * @return IndicatorResult 包含以下字段：
     *   - macd: MACD 线值
     *   - signal: 信号线值
     *   - histogram: 柱状图值（macd - signal）
     *   - crossover_type: 交叉类型（GOLDEN_CROSS/DEATH_CROSS/NONE）
     *   - zero_cross: 是否零轴穿越
     *   - trend: 趋势方向（BULLISH/BEARISH/NEUTRAL）
     *   - momentum: 动量强度（0-1）
     */
    IndicatorResult MACD(
        const std::vector<double>& close,
        int FAST_PERIOD,
        int SLOW_PERIOD,
        int SIGNAL_PERIOD
    );

    // ========================================================================
    // RSI - 相对强弱指标
    // ========================================================================

    /**
     * RSI - 相对强弱指标
     *
     * @param close 收盘价序列
     * @param RSI_PERIOD RSI 周期
     * @param RSI_OVERBOUGHT_THRESHOLD 超买阈值（默认70）
     * @param RSI_OVERSOLD_THRESHOLD 超卖阈值（默认30）
     * @return IndicatorResult 包含以下字段：
     *   - value: RSI 值（0-100）
     *   - overbought: 是否超买（> RSI_OVERBOUGHT_THRESHOLD）
     *   - oversold: 是否超卖（< RSI_OVERSOLD_THRESHOLD）
     *   - trend: 趋势方向
     *   - momentum: 动量强度
     */
    IndicatorResult RSI(
        const std::vector<double>& close,
        int RSI_PERIOD,
        double RSI_OVERBOUGHT_THRESHOLD = 70.0,
        double RSI_OVERSOLD_THRESHOLD = 30.0
    );

    // ========================================================================
    // EMA - 指数移动平均
    // ========================================================================

    /**
     * EMA - 指数移动平均
     *
     * @param close 收盘价序列
     * @param EMA_PERIOD EMA 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: EMA 值
     *   - slope: 斜率（正/负表示上升/下降）
     *   - distance: 与当前价格的距离百分比
     */
    IndicatorResult EMA(
        const std::vector<double>& close,
        int EMA_PERIOD
    );

    // ========================================================================
    // SMA - 简单移动平均
    // ========================================================================

    /**
     * SMA - 简单移动平均
     *
     * @param close 收盘价序列
     * @param SMA_PERIOD SMA 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: SMA 值
     *   - slope: 斜率
     *   - distance: 与当前价格的距离百分比
     */
    IndicatorResult MA(
        const std::vector<double>& close,
        int MA_PERIOD
    );

    // ========================================================================
    // BBANDS - 布林带
    // ========================================================================

    /**
     * BBANDS - 布林带（Bollinger Bands）
     *
     * @param close 收盘价序列
     * @param BBANDS_PERIOD 周期
     * @param BBANDS_STD_DEV 标准差倍数
     * @return IndicatorResult 包含以下字段：
     *   - upper: 上轨
     *   - middle: 中轨
     *   - lower: 下轨
     *   - width: 带宽
     *   - percent_b: %B 指标（价格在带中的位置）
     */
    IndicatorResult BOLL(
        const std::vector<double>& close,
        int BOLL_PERIOD,
        double BOLL_STD_DEV
    );

    // ========================================================================
    // ATR - 平均真实波幅
    // ========================================================================

    /**
     * ATR - 平均真实波幅（Average True Range）
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param period 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: ATR 值
     *   - volatility: 波动率等级（LOW/MEDIUM/HIGH）
     */
    IndicatorResult ATR(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int period
    );

    // ========================================================================
    // STOCHRSI - 随机 RSI
    // ========================================================================

    /**
     * STOCHRSI - 随机 RSI
     *
     * @param close 收盘价序列
     * @param RSI_PERIOD RSI 周期
     * @param STOCH_PERIOD Stoch 周期
     * @param K_PERIOD K 线平滑周期
     * @param D_PERIOD D 线平滑周期
     * @return IndicatorResult 包含以下字段：
     *   - k: %K 值
     *   - d: %D 值
     *   - crossover_type: K/D 交叉类型
     *   - overbought: 是否超买
     *   - oversold: 是否超卖
     */
    IndicatorResult STOCHRSI(
        const std::vector<double>& close,
        int RSI_PERIOD,
        int STOCH_PERIOD,
        int K_PERIOD,
        int D_PERIOD
    );

    // ========================================================================
    // CCI - 商品通道指标
    // ========================================================================

    /**
     * CCI - 商品通道指标（Commodity Channel Index）
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param CCI_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: CCI 值
     *   - overbought: 是否超买（> 100）
     *   - oversold: 是否超卖（< -100）
     *   - trend: 趋势方向
     */
    IndicatorResult CCI(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int CCI_PERIOD
    );

    // ========================================================================
    // ADX - 平均趋向指标
    // ========================================================================

    /**
     * ADX - 平均趋向指标（Average Directional Index）
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param ADX_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: ADX 值
     *   - trend_strength: 趋势强度（WEAK/MODERATE/STRONG/VERY_STRONG）
     */
    IndicatorResult ADX(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int ADX_PERIOD
    );

    // ========================================================================
    // WILLR - 威廉指标
    // ========================================================================

    /**
     * WILLR - 威廉指标（Williams %R）
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param WILLR_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: WILLR 值（-100 到 0）
     *   - overbought: 是否超买（> -20）
     *   - oversold: 是否超卖（< -80）
     */
    IndicatorResult WR(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int WR_PERIOD
    );

    // ========================================================================
    // MFI - 资金流量指标
    // ========================================================================

    /**
     * MFI - 资金流量指标（Money Flow Index）
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param MFI_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: MFI 值（0-100）
     *   - overbought: 是否超买（> 80）
     *   - oversold: 是否超卖（< 20）
     */
    IndicatorResult MFI(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        int MFI_PERIOD
    );


    // ========================================================================
    // SAR - 抛物线转向指标
    // ========================================================================

    /**
     * SAR - 抛物线转向指标（Parabolic SAR）
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param SAR_ACCELERATION 加速因子
     * @param SAR_MAXIMUM 最大加速因子
     * @return IndicatorResult 包含以下字段：
     *   - value: SAR 值
     *   - trend: 趋势方向（BULLISH/BEARISH）
     */
    IndicatorResult SAR(
        const std::vector<double>& high,
        const std::vector<double>& low,
        double SAR_ACCELERATION,
        double SAR_MAXIMUM
    );

    // ========================================================================
    // WMA - 加权移动平均
    // ========================================================================

    /**
     * WMA - 加权移动平均（Weighted Moving Average）
     *
     * @param close 收盘价序列
     * @param WMA_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: WMA 值
     *   - slope: 斜率
     *   - distance: 与当前价格的距离百分比
     */
    IndicatorResult WMA(
        const std::vector<double>& close,
        int WMA_PERIOD
    );

    // ========================================================================
    // ROC - 变动率指标
    // ========================================================================

    /**
     * ROC - 变动率指标（Rate of Change）
     *
     * @param close 收盘价序列
     * @param ROC_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: ROC 值（百分比）
     *   - trend: 趋势方向
     */
    IndicatorResult ROC(
        const std::vector<double>& close,
        int ROC_PERIOD
    );

    // ========================================================================
    // AROON - 阿隆指标
    // ========================================================================

    /**
     * AROON - 阿隆指标
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param AROON_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - aroon_up: Aroon Up 值（0-100）
     *   - aroon_down: Aroon Down 值（0-100）
     *   - oscillator: Aroon Oscillator（aroon_up - aroon_down）
     *   - trend: 趋势方向
     */
    IndicatorResult AROON(
        const std::vector<double>& high,
        const std::vector<double>& low,
        int AROON_PERIOD
    );

    // ========================================================================
    // VWAP - Volume Weighted Average Price
    // ========================================================================
    /**
     * @brief 成交量加权平均价格
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param VWAP_PERIOD 计算周期（默认使用所有数据，0表示全部）
     * @return IndicatorResult 包含以下字段：
     *   - value: VWAP值
     *   - distance: 当前价格与VWAP的距离百分比
     *   - above_vwap: 价格是否在VWAP上方
     */
    IndicatorResult VWAP(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        int VWAP_PERIOD
    );

    // ========================================================================
    // TRIX - 三重指数平滑移动平均
    // ========================================================================

    /**
     * TRIX - 三重指数平滑移动平均（Triple Exponential Average）
     *
     * @param close 收盘价序列
     * @param period 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: TRIX 值（百分比）
     *   - signal: TRIX 信号线（9周期EMA）
     *   - crossover_type: TRIX与信号线交叉类型
     *   - zero_cross: 是否穿越零轴
     *   - trend: 趋势方向
     */
    IndicatorResult TRIX(
        const std::vector<double>& close,
        int period
    );

    // ========================================================================
    // Ichimoku - Ichimoku Cloud
    // ========================================================================
    /**
     * @brief 一目均衡表（日本云图）
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param ICHIMOKU_TENKAN 转换线周期（默认9）
     * @param ICHIMOKU_KIJUN 基准线周期（默认26）
     * @param ICHIMOKU_SENKOU 先行跨度B周期（默认52）
     * @return IndicatorResult 包含以下字段：
     *   - tenkan: 转换线
     *   - kijun: 基准线
     *   - senkou_a: 先行跨度A
     *   - senkou_b: 先行跨度B
     *   - chikou: 滞后跨度
     */
    IndicatorResult Ichimoku(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int ICHIMOKU_TENKAN,
        int ICHIMOKU_KIJUN,
        int ICHIMOKU_SENKOU
    );

    // ========================================================================
    // DMI - 趋向指标
    // ========================================================================

    /**
     * DMI - 趋向指标（Directional Movement Index）
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param DMI_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - plus_di: +DI 值（0-100）
     *   - minus_di: -DI 值（0-100）
     *   - adx: ADX 值（趋势强度）
     *   - crossover_type: +DI/-DI 交叉类型
     *   - trend: 趋势方向
     *   - trend_strength: 趋势强度等级
     */
    IndicatorResult DMI(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int DMI_PERIOD
    );

    // ========================================================================
    // CMO - 钱德动量摆动指标
    // ========================================================================

    /**
     * CMO - 钱德动量摆动指标（Chande Momentum Oscillator）
     *
     * @param close 收盘价序列
     * @param CMO_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: CMO 值（-100 到 100）
     *   - overbought: 是否超买（> 50）
     *   - oversold: 是否超卖（< -50）
     *   - zero_cross: 是否穿越零轴
     *   - trend: 趋势方向
     */
    IndicatorResult CMO(
        const std::vector<double>& close,
        int CMO_PERIOD
    );

    // ========================================================================
    // TEMA - 三重指数移动平均
    // ========================================================================

    /**
     * TEMA - 三重指数移动平均（Triple Exponential Moving Average）
     *
     * @param close 收盘价序列
     * @param TEMA_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: TEMA 值
     *   - slope: 斜率
     *   - distance: 与当前价格的距离百分比
     *   - trend: 趋势方向
     */
    IndicatorResult TEMA(
        const std::vector<double>& close,
        int TEMA_PERIOD
    );

    // ========================================================================
    // DEMA - 双重指数移动平均
    // ========================================================================

    /**
     * DEMA - 双重指数移动平均（Double Exponential Moving Average）
     *
     * @param close 收盘价序列
     * @param DEMA_PERIOD 周期
     * @return IndicatorResult 包含以下字段：
     *   - value: DEMA 值
     *   - slope: 斜率
     *   - distance: 与当前价格的距离百分比
     *   - trend: 趋势方向
     */
    IndicatorResult DEMA(
        const std::vector<double>& close,
        int DEMA_PERIOD
    );

    // ========================================================================
    // KAMA - Kaufman自适应移动平均
    // ========================================================================

    /**
     * KAMA - Kaufman自适应移动平均
     *
     * @param close 收盘价序列
     * @param KAMA_PERIOD 效率比计算周期
     * @return IndicatorResult 包含以下字段：
     *   - value: KAMA 值
     *   - slope: 斜率
     *   - distance: 与当前价格的距离百分比
     *   - trend: 趋势方向（BULLISH/BEARISH/NEUTRAL）
     */
    IndicatorResult KAMA(
        const std::vector<double>& close,
        int KAMA_PERIOD
    );

    // ========================================================================
    // T3 - Triple Exponential Moving Average (Tillson)
    // ========================================================================

    /**
     * T3 - Triple Exponential Moving Average
     *
     * @param close 收盘价序列
     * @param T3_PERIOD T3周期
     * @return IndicatorResult 包含以下字段：
     *   - value: T3 值
     *   - slope: 斜率
     *   - distance: 与当前价格的距离百分比
     *   - trend: 趋势方向
     */
    IndicatorResult T3(
        const std::vector<double>& close,
        int T3_PERIOD
    );

    // ========================================================================
    // HT_TRENDLINE - 已移除，使用 HT 数据函数替代
    // 在DSL中使用: HT(tf).TRENDLINE
    // ========================================================================

    // ========================================================================
    // MAMA - MESA Adaptive Moving Average
    // ========================================================================

    /**
     * MAMA - MESA自适应移动平均
     *
     * @param close 收盘价序列
     * @param MAMA_FASTLIMIT 快速限制
     * @param MAMA_SLOWLIMIT 慢速限制
     * @return IndicatorResult 包含以下字段：
     *   - mama: MAMA线
     *   - fama: FAMA线
     *   - crossover_type: 交叉类型
     *   - trend: 趋势方向
     */
    IndicatorResult MAMA(
        const std::vector<double>& close,
        double MAMA_FASTLIMIT,
        double MAMA_SLOWLIMIT
    );


    // ========================================================================
    // KDJ - 随机指标（中国版）
    // ========================================================================
    /**
     * @brief KDJ随机指标
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param KDJ_N_PERIOD N周期（默认9）
     * @param KDJ_M1_PERIOD M1周期（K值平滑，默认3）
     * @param KDJ_M2_PERIOD M2周期（D值平滑，默认3）
     * @return IndicatorResult 包含以下字段：
     *   - k: K值
     *   - d: D值
     *   - j: J值 (3*K - 2*D)
     *   - overbought: 是否超买（K>80, D>80）
     *   - oversold: 是否超卖（K<20, D<20）
     */
    IndicatorResult KDJ(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int KDJ_N_PERIOD,
        int KDJ_M1_PERIOD,
        int KDJ_M2_PERIOD
    );

    // ========================================================================
    // STOCHF - Fast Stochastic
    // ========================================================================

    /**
     * STOCHF - 快速随机指标
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param STOCHF_FASTK_PERIOD FastK周期
     * @param STOCHF_FASTD_PERIOD FastD周期
     * @return IndicatorResult 包含以下字段：
     *   - k: FastK值
     *   - d: FastD值
     *   - crossover_type: 交叉类型
     *   - overbought: 是否超买
     *   - oversold: 是否超卖
     */
    IndicatorResult STOCHF(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int STOCHF_FASTK_PERIOD,
        int STOCHF_FASTD_PERIOD
    );

    // ========================================================================
    // ULTOSC - Ultimate Oscillator
    // ========================================================================

    /**
     * ULTOSC - 终极振荡器
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param PERIOD1 周期1
     * @param PERIOD2 周期2
     * @param PERIOD3 周期3
     * @return IndicatorResult 包含以下字段：
     *   - value: UO值
     *   - overbought: 是否超买
     *   - oversold: 是否超卖
     *   - trend: 趋势方向
     */
    IndicatorResult ULTOSC(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int PERIOD1,
        int PERIOD2,
        int PERIOD3
    );

    // ========================================================================
    // PPO - Percentage Price Oscillator
    // ========================================================================

    /**
     * PPO - 价格百分比振荡器
     *
     * @param close 收盘价序列
     * @param FAST_PERIOD 快线周期
     * @param SLOW_PERIOD 慢线周期
     * @param SIGNAL_PERIOD 信号线周期
     * @return IndicatorResult 包含以下字段：
     *   - ppo: PPO值
     *   - signal: 信号线
     *   - histogram: 柱状图
     *   - crossover_type: 交叉类型
     *   - trend: 趋势方向
     */
    IndicatorResult PPO(
        const std::vector<double>& close,
        int FAST_PERIOD,
        int SLOW_PERIOD,
        int SIGNAL_PERIOD
    );


    // ========================================================================
    // MTM - Momentum
    // ========================================================================
    /**
     * @brief 动量指标
     * @param close 收盘价序列
     * @param MTM_PERIOD 周期（默认10）
     * @return IndicatorResult 包含以下字段：
     *   - value: MTM 值
     *   - is_positive: 是否为正值
     *   - trend: 趋势（"up", "down", "neutral"）
     */
    IndicatorResult MTM(
        const std::vector<double>& close,
        int MTM_PERIOD
    );

    // ========================================================================
    // APO - Absolute Price Oscillator
    // ========================================================================

    /**
     * APO - 绝对价格振荡器
     *
     * @param close 收盘价序列
     * @param APO_FAST_PERIOD 快线周期
     * @param APO_SLOW_PERIOD 慢线周期
     * @return IndicatorResult 包含以下字段：
     *   - value: APO值
     *   - trend: 趋势方向
     */
    IndicatorResult APO(
        const std::vector<double>& close,
        int APO_FAST_PERIOD,
        int APO_SLOW_PERIOD
    );


    // ========================================================================
    // CMF - Chaikin Money Flow
    // ========================================================================
    /**
     * @brief 蔡金资金流量指标
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param CMF_PERIOD 周期（默认20）
     * @return IndicatorResult 包含以下字段：
     *   - value: CMF 值（-1 到 1）
     *   - is_positive: 是否为正值
     *   - is_strong: 是否为强势（abs > 0.25）
     */
    IndicatorResult CMF(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        int CMF_PERIOD
    );

    // ========================================================================
    // EMV - Ease of Movement
    // ========================================================================
    /**
     * @brief 简易波动指标
     * @param high 最高价序列
     * @param low 最低价序列
     * @param volume 成交量序列
     * @param EMV_PERIOD 移动平均周期（默认14）
     * @return IndicatorResult 包含以下字段：
     *   - value: EMV 值
     *   - signal: 信号线（EMV的MA）
     *   - is_positive: 是否为正值
     */
    IndicatorResult EMV(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& volume,
        int EMV_PERIOD
    );

    // ========================================================================
    // OBV - On Balance Volume
    // ========================================================================
    /**
     * @brief 能量潮指标
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @return IndicatorResult 包含以下字段：
     *   - value: OBV 值
     *   - trend: 趋势（"up", "down", "neutral"）
     *   - change: 变化量
     */
    IndicatorResult OBV(
        const std::vector<double>& close,
        const std::vector<double>& volume
    );

    // ========================================================================
    // ADOSC - Chaikin A/D Oscillator
    // ========================================================================

    /**
     * ADOSC - Chaikin累积派发振荡器
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param FAST_PERIOD 快速周期
     * @param SLOW_PERIOD 慢速周期
     * @return IndicatorResult 包含以下字段：
     *   - value: ADOSC值
     *   - crossover_type: 零轴穿越
     *   - trend: 趋势方向
     */
    IndicatorResult ADOSC(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        int FAST_PERIOD,
        int SLOW_PERIOD
    );

    // ========================================================================
    // Keltner - Keltner Channel
    // ========================================================================
    /**
     * @brief 肯特纳通道
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param KELTNER_PERIOD EMA周期（默认20）
     * @param KELTNER_MULTIPLIER ATR倍数（默认2.0）
     * @return IndicatorResult 包含以下字段：
     *   - upper: 上轨
     *   - middle: 中轨（EMA）
     *   - lower: 下轨
     *   - width: 通道宽度
     *   - percent_k: 价格在通道中的位置百分比
     */
    IndicatorResult Keltner(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int KELTNER_PERIOD,
        double KELTNER_MULTIPLIER
    );

    // ========================================================================
    // NATR - Normalized Average True Range
    // ========================================================================

    /**
     * NATR - 标准化平均真实波幅
     *
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param NATR_PERIOD 计算周期
     * @return IndicatorResult 包含以下字段：
     *   - value: NATR值（百分比）
     *   - volatility: 波动率等级
     */
    IndicatorResult NATR(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int NATR_PERIOD
    );


    // ========================================================================
    // 数据函数已迁移到 prophet::data::Functions 类
    // 详见: Prophet.Core/include/prophet/data/functions.hpp
    // ========================================================================

    // ========================================================================
    // 中期规划指标（批次4-8）：20个专业指标
    // ========================================================================

    // --------------------------------------------------------------------
    // 批次4：高级趋势指标（5个）
    // --------------------------------------------------------------------

    /**
     * LINEARREG - 线性回归
     * 
     * @param close 收盘价序列
     * @param params 参数 {LINEARREG_PERIOD: 默认14}
     * @return {value, trend}
     */
    IndicatorResult LINEARREG(
        const std::vector<double>& close,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * LINEARREG_SLOPE - 线性回归斜率
     * 
     * @param close 收盘价序列
     * @param params 参数 {LINEARREG_SLOPE_PERIOD: 默认14}
     * @return {value, trend}
     */
    IndicatorResult LINEARREG_SLOPE(
        const std::vector<double>& close,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * LINEARREG_ANGLE - 线性回归角度
     * 
     * @param close 收盘价序列
     * @param params 参数 {LINEARREG_ANGLE_PERIOD: 默认14}
     * @return {value}
     */
    IndicatorResult LINEARREG_ANGLE(
        const std::vector<double>& close,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * LINEARREG_INTERCEPT - 线性回归截距
     * 
     * @param close 收盘价序列
     * @param params 参数 {LINEARREG_INTERCEPT_PERIOD: 默认14}
     * @return {value}
     */
    IndicatorResult LINEARREG_INTERCEPT(
        const std::vector<double>& close,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * TSF - 时间序列预测
     * 
     * @param close 收盘价序列
     * @param params 参数 {TSF_PERIOD: 默认14}
     * @return {value, deviation}
     */
    IndicatorResult TSF(
        const std::vector<double>& close,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    // --------------------------------------------------------------------
    // 批次5：Hilbert变换 - 已移除，使用 HT 数据函数替代
    // 在DSL中使用: HT(tf).DCPERIOD, HT(tf).DCPHASE, 
    //              HT(tf).PHASOR.inphase, HT(tf).PHASOR.quadrature
    //              HT(tf).SINE.sine, HT(tf).SINE.lead
    // --------------------------------------------------------------------

    // --------------------------------------------------------------------
    // 批次6：价格变换 - 已移除，使用 PRICE 数据函数替代
    // PRICE(tf).AVG, PRICE(tf).MED, PRICE(tf).TYP, PRICE(tf).WCL
    // --------------------------------------------------------------------

    // --------------------------------------------------------------------
    // 批次7：动量指标补充（4个）
    // --------------------------------------------------------------------


    /**
     * MOM - 动量
     * 
     * @param close 收盘价序列
     * @param params 参数 {MOM_PERIOD: 默认10}
     * @return {value, trend}
     */
    IndicatorResult MOM(
        const std::vector<double>& close,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    // --------------------------------------------------------------------
    // 批次8：统计指标（3个）
    // --------------------------------------------------------------------


    /**
     * STDDEV - 标准差
     * 
     * @param close 收盘价序列
     * @param params 参数 {STDDEV_PERIOD: 默认5, STDDEV_NBDEV: 默认1.0}
     * @return {value}
     */
    IndicatorResult STDDEV(
        const std::vector<double>& close,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    // ========================================================================
    // 批次13：远期规划 - 其他重要TA-Lib指标
    // ========================================================================

    /**
     * ADXR - Average Directional Movement Index Rating
     * ADX评级，ADX的平滑版本
     */
    IndicatorResult ADXR(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * MIDPRICE - Midpoint Price over period
     * 周期中间价格 = (最高价 + 最低价) / 2
     */
    IndicatorResult MIDPRICE(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * TRIMA - Triangular Moving Average
     * 三角移动平均，对SMA的SMA
     */
    IndicatorResult TRIMA(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * SAREXT - Parabolic SAR Extended
     * 扩展抛物线转向指标，更多参数控制
     */
    IndicatorResult SAREXT(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    // HT_TRENDMODE - 已移除，使用 HT 数据函数替代
    // 在DSL中使用: HT(tf).TRENDMODE

    /**
     * ROCP - Rate of change Percentage
     * 变动率百分比 = [(price - prevPrice) / prevPrice] * 100
     */
    IndicatorResult ROCP(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * ROCR - Rate of change ratio
     * 变动率比率 = (price / prevPrice)
     */
    IndicatorResult ROCR(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * ROCR100 - Rate of change ratio 100 scale
     * 变动率比率×100 = (price / prevPrice) * 100
     */
    IndicatorResult ROCR100(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * AROONOSC - Aroon Oscillator
     * Aroon振荡器 = AroonUp - AroonDown
     */
    IndicatorResult AROONOSC(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * MACDEXT - MACD with controllable MA type
     * MACD扩展版，可控制MA类型
     */
    IndicatorResult MACDEXT(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * MACDFIX - Moving Average Convergence/Divergence Fix 12/26
     * MACD固定参数版（12/26）
     */
    IndicatorResult MACDFIX(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * PLUS_DM - Plus Directional Movement
     * 正向动量，DMI系统的基础组件
     */
    IndicatorResult PLUS_DM(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * MINUS_DM - Minus Directional Movement
     * 负向动量，DMI系统的基础组件
     */
    IndicatorResult MINUS_DM(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const IndicatorParams& params
    );

    /**
     * MA - Moving Average
     * 通用移动平均，支持多种MA类型（SMA/EMA/WMA/DEMA/TEMA等）
     */
    IndicatorResult MA(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    // ========================================================================
    // 批次10：数学函数 - 已移除，使用DSL内置函数替代
    // 在DSL中直接使用：SIN(), COS(), TAN(), ASIN(), ACOS(), ATAN()
    //                  SINH(), COSH(), TANH(), EXP(), LN(), LOG10()
    //                  CEIL(), FLOOR(), SQRT()
    // ========================================================================

    // ========================================================================
    // 批次11：远期规划 - 数学运算符
    // ========================================================================

    /**
     * MAX - Highest value over a specified period
     * 周期内最高值
     */
    IndicatorResult MAX(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * MIN - Lowest value over a specified period
     * 周期内最低值
     */
    IndicatorResult MIN(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * MAXINDEX - Index of highest value over a specified period
     * 周期内最高值的索引位置
     */
    IndicatorResult MAXINDEX(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * MININDEX - Index of lowest value over a specified period
     * 周期内最低值的索引位置
     */
    IndicatorResult MININDEX(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * MINMAX - Lowest and highest values over a specified period
     * 周期内的最低值和最高值
     */
    IndicatorResult MINMAX(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    /**
     * MINMAXINDEX - Indexes of lowest and highest values over a specified period
     * 周期内最低值和最高值的索引位置
     */
    IndicatorResult MINMAXINDEX(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    // ========================================================================
    // 批次12：远期规划 - 高级统计指标
    // ========================================================================

    /**
     * VAR - Variance (TA-Lib版本)
     * 方差，衡量数据的离散程度
     */
    IndicatorResult VAR(
        const std::vector<double>& close,
        const IndicatorParams& params
    );

    // ========================================================================
    // 新增指标（第一阶段）
    // ========================================================================

    /**
     * Supertrend - 超级趋势指标
     */
    IndicatorResult Supertrend(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int SUPERTREND_PERIOD,
        double SUPERTREND_MULTIPLIER
    );

    /**
     * Donchian Channel - 唐奇安通道
     */
    IndicatorResult DonchianChannel(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int DONCHIAN_PERIOD
    );

    /**
     * Pivot Points - 枢轴点
     */
    IndicatorResult PivotPoints(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close
    );

    /**
     * Standard Deviation - 标准差
     */
    IndicatorResult StdDev(
        const std::vector<double>& close,
        int STDDEV_PERIOD,
        double STDDEV_NBDEV
    );

    /**
     * VWMA - 成交量加权移动平均
     */
    IndicatorResult VWMA(
        const std::vector<double>& close,
        const std::vector<double>& volume,
        int VWMA_PERIOD
    );

    // ========================================================================
    // 新增指标（第二阶段）
    // ========================================================================

    /**
     * Swing High/Low - 摆动高低点
     */
    IndicatorResult SwingHL(
        const std::vector<double>& high,
        const std::vector<double>& low,
        int SWING_PERIOD
    );

    /**
     * VWAP Bands - VWAP通道
     */
    IndicatorResult VWAPBands(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        double VWAPBANDS_STD_DEV
    );

    /**
     * Historical Volatility - 历史波动率
     */
    IndicatorResult HV(
        const std::vector<double>& close,
        int HV_PERIOD
    );

    /**
     * Chandelier Exit - 吊灯止损
     */
    IndicatorResult ChandelierExit(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int CHANDELIER_PERIOD,
        double CHANDELIER_MULTIPLIER
    );

    /**
     * Choppiness Index - 震荡指标
     */
    IndicatorResult ChoppinessIndex(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int CHOP_PERIOD
    );

    /**
     * TSI - 真实强度指标
     */
    IndicatorResult TSI(
        const std::vector<double>& close,
        int LONG_PERIOD,
        int SHORT_PERIOD,
        int SIGNAL_PERIOD
    );

    /**
     * Bollinger Bandwidth - 布林带宽度
     */
    IndicatorResult BollingerBW(
        const std::vector<double>& close,
        int BBWIDTH_PERIOD,
        double BBWIDTH_STD_DEV
    );

    /**
     * Market Structure Break - 市场结构破坏
     */
    IndicatorResult MSB(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int MSB_LOOKBACK
    );

    /**
     * ADL - 累积派发线
     */
    IndicatorResult ADL(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume
    );

    /**
     * Higher Highs/Lower Lows - 更高高点/更低低点
     */
    IndicatorResult HHLL(
        const std::vector<double>& high,
        const std::vector<double>& low,
        int HHLL_PERIOD
    );

    // ========================================================================
    // Phase 3: Advanced Indicators (10 indicators)
    // ========================================================================

    /**
     * Elder Ray - Bull Power & Bear Power (Alexander Elder)
     */
    IndicatorResult ElderRay(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int period = 13
    ) const;

    /**
     * Linear Regression Slope - 线性回归斜率
     */
    IndicatorResult LinearRegSlope(
        const std::vector<double>& close,
        int period = 14
    ) const;

    /**
     * Force Index - 力量指数 (Alexander Elder)
     */
    IndicatorResult ForceIndex(
        const std::vector<double>& close,
        const std::vector<double>& volume,
        int period = 13
    ) const;

    /**
     * Schaff Trend Cycle - 沙夫趋势周期
     */
    IndicatorResult SchaffTrendCycle(
        const std::vector<double>& close,
        int FAST_PERIOD = 23,
        int SLOW_PERIOD = 50,
        int cycle_period = 10
    ) const;

    /**
     * Alligator - 鳄鱼指标 (Bill Williams)
     */
    IndicatorResult Alligator(
        const std::vector<double>& high,
        const std::vector<double>& low,
        int jaw_period = 13,
        int teeth_period = 8,
        int lips_period = 5
    ) const;

    /**
     * Fisher Transform - 费舍尔变换
     */
    IndicatorResult FisherTransform(
        const std::vector<double>& high,
        const std::vector<double>& low,
        int period = 10
    ) const;

    /**
     * McGinley Dynamic - 麦吉尼动态指标
     */
    IndicatorResult McGinleyDynamic(
        const std::vector<double>& close,
        int period = 14
    ) const;

    /**
     * Volume Price Trend - 量价趋势
     */
    IndicatorResult VolumePriceTrend(
        const std::vector<double>& close,
        const std::vector<double>& volume
    ) const;

    /**
     * Kaufman Efficiency Ratio - 考夫曼效率比率
     */
    IndicatorResult KaufmanER(
        const std::vector<double>& close,
        int period = 10
    ) const;

    /**
     * Parabolic Time/Price - 抛物线时间/价格系统
     */
    IndicatorResult ParabolicTP(
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        double acceleration = 0.02,
        double maximum = 0.2
    ) const;

private:
    // ========================================================================
    // 辅助函数
    // ========================================================================

    /**
     * 检测两条线的穿越类型
     *
     * @param line1 线1（如 MACD 线）
     * @param line2 线2（如信号线）
     * @return GOLDEN_CROSS/DEATH_CROSS/NONE
     */
    std::string detectCrossover(
        const std::vector<double>& line1,
        const std::vector<double>& line2
    );

    /**
     * 检测零轴穿越
     *
     * @param values 数值序列
     * @return true 如果当前穿越零轴
     */
    bool detectZeroCross(const std::vector<double>& values);

    /**
     * 判断趋势方向
     *
     * @param values 数值序列
     * @return BULLISH/BEARISH/NEUTRAL
     */
    std::string determineTrend(const std::vector<double>& values);

    /**
     * 计算动量强度
     *
     * @param values 数值序列
     * @return 0.0-1.0 之间的动量值
     */
    double calculateMomentum(const std::vector<double>& values);

    /**
     * 计算斜率
     *
     * @param values 数值序列
     * @param lookback 回看周期（默认 3）
     * @return 斜率值
     */
    double calculateSlope(const std::vector<double>& values, int lookback = 3);
};

} // namespace prophet::indicators
