/*
 * ============================================================================
 * 文件名：indicator_registrations.cpp
 * 功能说明：所有指标的自动注册
 * 
 * 这个文件包含所有指标的注册代码
 * 当程序启动时，这些注册会自动执行
 * 
 * 优势：
 * - 添加新指标无需修改 DSL 代码
 * - 集中管理所有指标注册
 * - 编译时自动执行
 * ============================================================================
 */

 #include "prophet/indicators/registry.hpp"
 #include "prophet/indicators/calculator.hpp"
 
 namespace prophet::indicators {
 
 // ============================================================================
 // 动量指标注册
 // ============================================================================
 
 // MACD - 移动平均收敛发散指标
 REGISTER_INDICATOR(MACD, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int fast = params.get_int("FAST_PERIOD", 12);
     int slow = params.get_int("SLOW_PERIOD", 26);
     int signal = params.get_int("SIGNAL_PERIOD", 9);
     return calc.MACD(close, fast, slow, signal);
 });

// RSI - 相对强弱指标
REGISTER_INDICATOR(RSI, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    double overbought = params.get_double("OVERBOUGHT", 70.0);
    double oversold = params.get_double("OVERSOLD", 30.0);
    return calc.RSI(close, period, overbought, oversold);
});
 
// STOCHRSI - 随机RSI
 REGISTER_INDICATOR(STOCHRSI, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int RSI_PERIOD = params.get_int("RSI_PERIOD", 14);
     int STOCH_PERIOD = params.get_int("STOCH_PERIOD", 14);
     int K_PERIOD = params.get_int("K_PERIOD", 3);
     int D_PERIOD = params.get_int("D_PERIOD", 3);
     return calc.STOCHRSI(close, RSI_PERIOD, STOCH_PERIOD, K_PERIOD, D_PERIOD);
 });

// CCI - 商品通道指标
REGISTER_INDICATOR(CCI, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.CCI(high, low, close, period);
});
 
// MFI - 资金流量指标
REGISTER_INDICATOR(MFI, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.MFI(high, low, close, volume, period);
});
 
// WILLR - 威廉指标
REGISTER_INDICATOR(WR, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.WR(high, low, close, period);
});
 
// ROC - 变化率
REGISTER_INDICATOR(ROC, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 10);
    return calc.ROC(close, period);
});
 
// CMO - Chande动量摆动
REGISTER_INDICATOR(CMO, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.CMO(close, period);
});
 
 // ============================================================================
 // 趋势指标注册
 // ============================================================================
 
 // SMA - 简单移动平均
 REGISTER_INDICATOR(MA, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 20);
     return calc.MA(close, period);
 });
 
 // EMA - 指数移动平均
 REGISTER_INDICATOR(EMA, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 20);
     return calc.EMA(close, period);
 });
 
 // WMA - 加权移动平均
 REGISTER_INDICATOR(WMA, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 20);
     return calc.WMA(close, period);
 });
 
 // DEMA - 双指数移动平均
 REGISTER_INDICATOR(DEMA, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 30);
     return calc.DEMA(close, period);
 });
 
 // TEMA - 三指数移动平均
 REGISTER_INDICATOR(TEMA, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 30);
     return calc.TEMA(close, period);
 });
 
 // KAMA - Kaufman自适应移动平均
 REGISTER_INDICATOR(KAMA, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 30);
     return calc.KAMA(close, period);
 });
 
 // T3 - T3移动平均
 REGISTER_INDICATOR(T3, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 5);
     return calc.T3(close, period);
 });
 
 // MAMA - MESA自适应移动平均
 REGISTER_INDICATOR(MAMA, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     double fastlimit = params.get_double("FASTLIMIT", 0.5);
     double slowlimit = params.get_double("SLOWLIMIT", 0.05);
     return calc.MAMA(close, fastlimit, slowlimit);
 });
 
 // ADX - 平均趋向指标
 REGISTER_INDICATOR(ADX, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 14);
     return calc.ADX(high, low, close, period);
 });
 
 // SAR - 抛物线转向
 REGISTER_INDICATOR(SAR, [](
     Calculator& calc,
     const std::vector<double>& /* close */,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     double acceleration = params.get_double("ACCELERATION", 0.02);
     double maximum = params.get_double("MAXIMUM", 0.2);
     return calc.SAR(high, low, acceleration, maximum);
 });

// AROON - 阿隆指标
REGISTER_INDICATOR(AROON, [](
    Calculator& calc,
    const std::vector<double>& /* close */,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.AROON(high, low, period);
});
 
 // Ichimoku - 一目均衡表
 REGISTER_INDICATOR(Ichimoku, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int tenkan = params.get_int("TENKAN", 9);
     int kijun = params.get_int("KIJUN", 26);
     int senkou = params.get_int("SENKOU", 52);
     return calc.Ichimoku(high, low, close, tenkan, kijun, senkou);
 });

// DMI - 趋向指标
REGISTER_INDICATOR(DMI, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.DMI(high, low, close, period);
});
 
 // ============================================================================
 // 波动率指标注册
 // ============================================================================

// ATR - 真实波动幅度
REGISTER_INDICATOR(ATR, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.ATR(high, low, close, period);
});
 
// BBANDS - 布林带
REGISTER_INDICATOR(BOLL, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    double nbdev = params.get_double("STD_DEV", 2.0);
    return calc.BOLL(close, period, nbdev);
});
 
 // ============================================================================
 // 价格指标
 // ============================================================================
 
 // VWAP - Volume Weighted Average Price
 REGISTER_INDICATOR(VWAP, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& volume,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 0);  // 0 = use all data
     return calc.VWAP(high, low, close, volume, period);
 });

// TRIX - 三重指数
REGISTER_INDICATOR(TRIX, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 15);
    return calc.TRIX(close, period);
});
 
 // ============================================================================
 // 动量指标 - 补充
 // ============================================================================
 
 // KDJ - KDJ随机指标
 REGISTER_INDICATOR(KDJ, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int n_period = params.get_int("N_PERIOD", 9);
     int m1_period = params.get_int("M1_PERIOD", 3);
     int m2_period = params.get_int("M2_PERIOD", 3);
     return calc.KDJ(high, low, close, n_period, m1_period, m2_period);
 });

// STOCHF - 快速随机指标
REGISTER_INDICATOR(STOCHF, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    // STOCHF 需要 (high, low, close, fastk, fastd)
    // 参数签名是 (close, high, low, volume)，需要正确使用close参数
    int fastk = params.get_int("FASTK_PERIOD", 5);
    int fastd = params.get_int("FASTD_PERIOD", 3);
    // 使用正确的close参数（第一个参数）
    return calc.STOCHF(high, low, close, fastk, fastd);
});
 
 // ULTOSC - 终极振荡器
 REGISTER_INDICATOR(ULTOSC, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int PERIOD1 = params.get_int("PERIOD1", 7);
     int PERIOD2 = params.get_int("PERIOD2", 14);
     int PERIOD3 = params.get_int("PERIOD3", 28);
     return calc.ULTOSC(high, low, close, PERIOD1, PERIOD2, PERIOD3);
 });
 
 // PPO - 价格百分比振荡器
 REGISTER_INDICATOR(PPO, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int fast = params.get_int("FAST_PERIOD", 12);
     int slow = params.get_int("SLOW_PERIOD", 26);
     int signal = params.get_int("SIGNAL_PERIOD", 9);
     return calc.PPO(close, fast, slow, signal);
 });

// MTM - 动量
REGISTER_INDICATOR(MTM, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 10);
    return calc.MTM(close, period);
});
 
 // APO - 绝对价格振荡器
 REGISTER_INDICATOR(APO, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& /* high */,
     const std::vector<double>& /* low */,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int fast = params.get_int("FAST_PERIOD", 12);
     int slow = params.get_int("SLOW_PERIOD", 26);
     return calc.APO(close, fast, slow);
 });

// Keltner - 肯特纳通道
REGISTER_INDICATOR(Keltner, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    double multiplier = params.get_double("MULTIPLIER", 2.0);
    return calc.Keltner(high, low, close, period, multiplier);
});
 
 // NATR - 标准化平均真实波幅
 REGISTER_INDICATOR(NATR, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& /* volume */,
     const IndicatorParams& params
 ) {
     int period = params.get_int("PERIOD", 14);
     return calc.NATR(high, low, close, period);
 });
 
 // ============================================================================
 // 成交量指标
 // ============================================================================

// CMF - Chaikin Money Flow
REGISTER_INDICATOR(CMF, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    return calc.CMF(high, low, close, volume, period);
});

// EMV - Ease of Movement
REGISTER_INDICATOR(EMV, [](
    Calculator& calc,
    const std::vector<double>& /* close */,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.EMV(high, low, volume, period);
});

// OBV - On Balance Volume
REGISTER_INDICATOR(OBV, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& volume,
    const IndicatorParams& /* params */
) {
    return calc.OBV(close, volume);
});
 
 // ADOSC - Chaikin A/D 振荡器
 REGISTER_INDICATOR(ADOSC, [](
     Calculator& calc,
     const std::vector<double>& close,
     const std::vector<double>& high,
     const std::vector<double>& low,
     const std::vector<double>& volume,
     const IndicatorParams& params
 ) {
     int fast = params.get_int("FAST_PERIOD", 3);
     int slow = params.get_int("SLOW_PERIOD", 10);
    return calc.ADOSC(high, low, close, volume, fast, slow);
});

// ============================================================================
// 新增指标（第一阶段）
// ============================================================================

// Supertrend - 超级趋势
REGISTER_INDICATOR(Supertrend, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 10);
    double multiplier = params.get_double("MULTIPLIER", 3.0);
    return calc.Supertrend(high, low, close, period, multiplier);
});

// DonchianChannel - 唐奇安通道
REGISTER_INDICATOR(DonchianChannel, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    return calc.DonchianChannel(high, low, close, period);
});

// PivotPoints - 枢轴点
REGISTER_INDICATOR(PivotPoints, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& /* params */
) {
    return calc.PivotPoints(high, low, close);
});

// StdDev - 标准差
REGISTER_INDICATOR(StdDev, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    double nbdev = params.get_double("NBDEV", 1.0);
    return calc.StdDev(close, period, nbdev);
});

// VWMA - 成交量加权移动平均
REGISTER_INDICATOR(VWMA, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& volume,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    return calc.VWMA(close, volume, period);
});

// ============================================================================
// 新增指标（第二阶段）
// ============================================================================

// SwingHL - 摆动高低点
REGISTER_INDICATOR(SwingHL, [](
    Calculator& calc,
    const std::vector<double>& /* close */,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 5);
    return calc.SwingHL(high, low, period);
});

// VWAPBands - VWAP通道
REGISTER_INDICATOR(VWAPBands, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    const IndicatorParams& params
) {
    double std_dev = params.get_double("STD_DEV", 2.0);
    return calc.VWAPBands(high, low, close, volume, std_dev);
});

// HV - 历史波动率
REGISTER_INDICATOR(HV, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    return calc.HV(close, period);
});

// ChandelierExit - 吊灯止损
REGISTER_INDICATOR(ChandelierExit, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 22);
    double multiplier = params.get_double("MULTIPLIER", 3.0);
    return calc.ChandelierExit(high, low, close, period, multiplier);
});

// ChoppinessIndex - 震荡指标
REGISTER_INDICATOR(ChoppinessIndex, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.ChoppinessIndex(high, low, close, period);
});

// TSI - 真实强度指标
REGISTER_INDICATOR(TSI, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int LONG_PERIOD = params.get_int("LONG_PERIOD", 25);
    int SHORT_PERIOD = params.get_int("SHORT_PERIOD", 13);
    int SIGNAL_PERIOD = params.get_int("SIGNAL_PERIOD", 7);
    return calc.TSI(close, LONG_PERIOD, SHORT_PERIOD, SIGNAL_PERIOD);
});

// BollingerBW - 布林带宽度
REGISTER_INDICATOR(BollingerBW, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 20);
    double std_dev = params.get_double("STD_DEV", 2.0);
    return calc.BollingerBW(close, period, std_dev);
});

// MSB - 市场结构破坏
REGISTER_INDICATOR(MSB, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int lookback = params.get_int("LOOKBACK", 20);
    return calc.MSB(high, low, close, lookback);
});

// ADL - 累积派发线
REGISTER_INDICATOR(ADL, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    const IndicatorParams& /* params */
) {
    return calc.ADL(high, low, close, volume);
});

// HHLL - 更高高点/更低低点
REGISTER_INDICATOR(HHLL, [](
    Calculator& calc,
    const std::vector<double>& /* close */,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 10);
    return calc.HHLL(high, low, period);
});

// ============================================================================
// Phase 3: Advanced Indicators (10 indicators)
// ============================================================================

REGISTER_INDICATOR(ElderRay, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 13);
    return calc.ElderRay(high, low, close, period);
});

REGISTER_INDICATOR(LinearRegSlope, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.LinearRegSlope(close, period);
});

REGISTER_INDICATOR(ForceIndex, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& volume,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 13);
    return calc.ForceIndex(close, volume, period);
});

REGISTER_INDICATOR(SchaffTrendCycle, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int FAST_PERIOD = params.get_int("FAST_PERIOD", 23);
    int SLOW_PERIOD = params.get_int("SLOW_PERIOD", 50);
    int cycle_period = params.get_int("CYCLE_PERIOD", 10);
    return calc.SchaffTrendCycle(close, FAST_PERIOD, SLOW_PERIOD, cycle_period);
});

REGISTER_INDICATOR(Alligator, [](
    Calculator& calc,
    const std::vector<double>& /* close */,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int jaw_period = params.get_int("JAW_PERIOD", 13);
    int teeth_period = params.get_int("TEETH_PERIOD", 8);
    int lips_period = params.get_int("LIPS_PERIOD", 5);
    return calc.Alligator(high, low, jaw_period, teeth_period, lips_period);
});

REGISTER_INDICATOR(FisherTransform, [](
    Calculator& calc,
    const std::vector<double>& /* close */,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 10);
    return calc.FisherTransform(high, low, period);
});

REGISTER_INDICATOR(McGinleyDynamic, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 14);
    return calc.McGinleyDynamic(close, period);
});

REGISTER_INDICATOR(VolumePriceTrend, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& volume,
    const IndicatorParams& /* params */
) {
    return calc.VolumePriceTrend(close, volume);
});

REGISTER_INDICATOR(KaufmanER, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int period = params.get_int("PERIOD", 10);
    return calc.KaufmanER(close, period);
});

REGISTER_INDICATOR(ParabolicTP, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    double acceleration = params.get_double("ACCELERATION", 0.02);
    double maximum = params.get_double("MAXIMUM", 0.2);
    return calc.ParabolicTP(high, low, close, acceleration, maximum);
});

 
} // namespace prophet::indicators