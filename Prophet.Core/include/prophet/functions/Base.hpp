#pragma once

#include "../common/types.hpp"
#include <vector>
#include <string>

namespace prophet::functions {

/**
 * 数据函数基础实现 - Base Data Functions Implementation
 *
 * 这个类提供了所有数据函数的核心算法实现。
 * 数据函数是DSL中用于访问和计算K线数据的基础函数。
 * 
 * 架构分层：
 * - Base.hpp/cpp：核心算法实现层（本文件）
 * - Data.cpp：DSL集成层（注册到FunctionRegistry）
 * - data_function_bindings.cpp：Python绑定层
 *
 * 特点：
 * - 无状态的纯函数实现
 * - 无需预先配置参数
 * - 使用时动态计算
 * - 返回 IndicatorResult 格式以便与 DSL 集成
 *
 * 数据函数清单：
 * - KLINE:      访问K线数据（当前和历史）
 * - HIGHEST:    n期最高值
 * - LOWEST:     n期最低值
 * - AVERAGE:    n期平均值
 * - STD:        n期标准差（波动率）
 * - CHANGE:     n期变化量（.value=绝对值, .pct=百分比）
 * - SUM:        n期累加和
 * - SLOPE:      n期线性回归斜率
 * - RANK:       当前值排名（.value=绝对排名, .pct=百分位）
 * - MEDIAN:     n期中位数
 * - VARIANCE:   n期方差
 * - CROSS:      交叉检测（.above/.below/.any）
 * - ZSCORE:     标准化得分
 * - PERCENTILE: 百分位数
 * - VOLA:       波动率分析（.value/.pct）
 * - ATR:        平均真实波幅（.value/.pct）
 */
class Base {
public:
    Base() = default;

    // ========================================================================
    // KLINE - 访问K线数据（当前和历史）
    // ========================================================================

    /**
     * KLINE - 访问K线数据（当前和历史）
     *
     * DSL 语法：KLINE(timeframe).field(offset)
     * 示例：
     *   KLINE(5m).close(0)  - 5分钟级别当前K线收盘价
     *   KLINE(5m).close(-1) - 5分钟级别前1根K线收盘价
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param offset 整数偏移量（0=当前，-1=前1根，-2=前2根，范围：[-100, 0]）
     * @return IndicatorResult 包含单个字段（K线数据值）
     *
     * 设计理由：
     * - 多时间框架系统必须显式指定时间框架
     * - offset=0 访问当前K线，offset<0 访问历史K线
     * - 偏移量范围统一为 [-100, 0]，与其他函数保持一致
     * - 统一K线数据访问接口，替代已废弃的环境变量
     * 
     * 应用场景：
     * - 当前K线实体大小判断
     * - 连续K线形态判断（连续阳线、阴线）
     * - 突破回踩确认
     * - 跨时间框架数据对比
     */
    IndicatorResult KLINE(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int offset
    );

    // ========================================================================
    // HIGHEST - n期最高值
    // ========================================================================

    /**
     * HIGHEST - n期最高值指标
     *
     * DSL 语法：HIGHEST(timeframe).field(n)
     * 示例：HIGHEST(5m).high(20) - 5分钟K线近20根的最高价
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期（从 DSL 中的 field(n) 解析得到）
     * @return IndicatorResult 包含单个字段（字段名与 field 参数相同）
     *
     * 注意：此函数返回的 IndicatorResult 只包含一个字段，
     *      字段名为 field 参数指定的名称，值为对应的 n 期最高值
     */
    IndicatorResult HIGHEST(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // LOWEST - n期最低值
    // ========================================================================

    /**
     * LOWEST - n期最低值指标
     *
     * DSL 语法：LOWEST(timeframe).field(n)
     * 示例：LOWEST(1d).low(10) - 日线近10根的最低价
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段
     */
    IndicatorResult LOWEST(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // AVERAGE - n期平均值
    // ========================================================================

    /**
     * AVERAGE - n期平均值指标
     *
     * DSL 语法：AVERAGE(timeframe).field(n)
     * 示例：AVERAGE(1h).close(30) - 1小时K线近30根的收盘价平均
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段
     *
     * 注意：AVERAGE(tf).close(n) 等价于 SMA(tf).value（当 SMA_PERIOD=n 时）
     */
    IndicatorResult AVERAGE(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // STD - n期标准差（波动率）
    // ========================================================================

    /**
     * STD - n期标准差指标
     *
     * DSL 语法：STD(timeframe).field(n)
     * 示例：STD(5m).close(20) - 5分钟K线近20根的收盘价标准差
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（样本标准差）
     */
    IndicatorResult STD(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // CHANGE - n期绝对变化量
    // ========================================================================

    /**
     * CHANGE - n期绝对变化量
     *
     * DSL 语法：CHANGE(timeframe).field(n)
     * 示例：CHANGE(5m).close(1) - 5分钟K线相对1根K线前的绝对变化量
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（绝对变化量）
     *
     * 计算公式：当前值 - n期前值
     * 返回值单位与原字段相同（价格返回点数，成交量返回绝对量）
     */
    IndicatorResult CHANGE(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================

    // ========================================================================
    // SUM - n期累加和
    // ========================================================================

    /**
     * SUM - n期累加和指标
     *
     * DSL 语法：SUM(timeframe).field(n)
     * 示例：SUM(1d).volume(5) - 1日K线近5根的成交量累加和
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（累加和）
     */
    IndicatorResult SUM(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // SLOPE - n期线性回归斜率
    // ========================================================================

    /**
     * SLOPE - n期线性回归斜率指标
     *
     * DSL 语法：SLOPE(timeframe).field(n)
     * 示例：SLOPE(5m).close(10) - 5分钟K线近10根收盘价的线性回归斜率
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（线性回归斜率）
     *
     * 计算方法：最小二乘法拟合直线，返回斜率
     * 正值表示上升趋势，负值表示下降趋势，绝对值越大趋势越陡峭
     */
    IndicatorResult SLOPE(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // RANK - 当前值在n期中的绝对排名
    // ========================================================================

    /**
     * RANK - 当前值在n期中的绝对排名
     *
     * DSL 语法：RANK(timeframe).field(n)
     * 示例：RANK(5m).close(20) >= 18 - 价格排名前三
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（排名，1=最低，n=最高）
     *
     * 计算方法：排序后返回当前值的位置
     * 用途：判断相对位置，价格/成交量处于什么水平
     */
    IndicatorResult RANK(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================

    // ========================================================================
    // MEDIAN - n期中位数（排序后中间的值）
    // ========================================================================

    /**
     * MEDIAN - n期中位数
     *
     * DSL 语法：MEDIAN(timeframe).field(n)
     * 示例：MEDIAN(5m).volume(20) * 1.5 - 成交量超过中位数1.5倍
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（中位数）
     *
     * 计算方法：排序后取中间值
     * 优势：抗异常值，比平均值更能反映"正常情况"
     */
    IndicatorResult MEDIAN(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // VARIANCE - n期方差（标准差的平方）
    // ========================================================================

    /**
     * VARIANCE - n期方差
     *
     * DSL 语法：VARIANCE(timeframe).field(n)
     * 示例：VARIANCE(5m).close(20) < 2500 - 等价于 STD < 50
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（方差）
     *
     * 计算方法：标准差的平方
     * 注意：单位是"平方点"，日常使用建议用STD（标准差）
     */
    IndicatorResult VARIANCE(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // CROSS - 交叉检测
    // ========================================================================

    /**
     * CROSS - 交叉检测
     *
     * DSL 语法：CROSS(timeframe).field(threshold).above  - 向上穿越
     *           CROSS(timeframe).field(threshold).below  - 向下穿越
     *           CROSS(timeframe).field(threshold).any    - 任意穿越
     * 示例：CROSS(5m).close(100).above - 价格向上穿越100
     *       CROSS(5m).close(ma20).below - 价格死叉MA20
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param threshold 阈值（可以是固定值或动态值如均线）
     * @return IndicatorResult 包含三个布尔字段（above, below, any）
     *
     * 计算逻辑：
     *   above: 前一期 <= 阈值 && 当前期 > 阈值
     *   below: 前一期 >= 阈值 && 当前期 < 阈值
     *   any: above || below
     * 用途：金叉死叉、突破检测、信号触发
     */
    IndicatorResult CROSS(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        double threshold
    );

    // ========================================================================
    // ZSCORE - 标准化得分
    // ========================================================================

    /**
     * ZSCORE - 标准化得分
     *
     * DSL 语法：ZSCORE(timeframe).field(period)
     * 示例：ZSCORE(5m).close(50) < -2 - 价格低于均值2个标准差（超卖）
     *       ZSCORE(5m).close(20) > 2  - 价格高于均值2个标准差（超买）
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含单个字段（Z-Score值）
     *
     * 计算公式：(current - mean) / std_dev
     * 解读：
     *   Z = 0: 等于均值
     *   Z > 2: 显著高于均值（超买）
     *   Z < -2: 显著低于均值（超卖）
     * 用途：均值回归策略、异常值检测、标准化比较
     */
    IndicatorResult ZSCORE(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // PERCENTILE - 百分位数
    // ========================================================================

    /**
     * PERCENTILE - 百分位数
     *
     * DSL 语法：PERCENTILE(timeframe).field(period, quantile)
     * 示例：PERCENTILE(5m).high(100, 0.95) - 最近100期高点的95分位数
     *       PERCENTILE(5m).low(100, 0.05)  - 最近100期低点的5分位数
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @param quantile 百分位数（0.0-1.0，如0.95表示95%分位数）
     * @return IndicatorResult 包含单个字段（百分位值）
     *
     * 计算方法：线性插值法
     * 示例值：
     *   quantile = 0.5: 中位数
     *   quantile = 0.95: 95%分位数（只有5%的值大于此值）
     *   quantile = 0.05: 5%分位数（只有5%的值小于此值）
     * 用途：动态止损、风险管理、分位数回归
     */
    IndicatorResult PERCENTILE(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period,
        double quantile
    );

    // ========================================================================
    // VOLA - 波动率分析（Volatility）
    // ========================================================================

    /**
     * VOLA - 波动率分析
     *
     * DSL 语法：VOLA(timeframe).field(period).value      - 绝对波动率（标准差）
     *           VOLA(timeframe).field(period).pct        - 百分比波动率（变异系数）
     * 示例：VOLA(5m).close(20).value - 20期价格标准差
     *       VOLA(5m).close(20).pct   - 20期变异系数（归一化波动率）
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param volume 成交量序列
     * @param field 字段名称（"open"/"high"/"low"/"close"/"volume"）
     * @param period 回溯周期
     * @return IndicatorResult 包含两个字段（value=绝对波动率, pct=百分比波动率）
     *
     * 计算方法：
     *   value: 标准差 = √[Σ(x - mean)² / n]
     *   pct: 变异系数 = (std_dev / mean) * 100
     * 
     * 用途：
     *   .value - 衡量绝对波动幅度，适合单品种分析
     *   .pct   - 归一化波动率，适合跨品种比较
     * 
     * 应用场景：
     *   - 波动率突破策略
     *   - 低波动率待突破
     *   - 波动率收缩/扩张
     *   - 跨品种波动率比较
     */
    IndicatorResult VOLA(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        const std::vector<double>& volume,
        const std::string& field,
        int period
    );

    // ========================================================================
    // ATR - 平均真实波幅（Average True Range）
    // ========================================================================

    /**
     * ATR - 平均真实波幅
     *
     * DSL 语法：ATR(timeframe).close(period).value  - 绝对ATR
     *           ATR(timeframe).close(period).pct    - 百分比ATR
     * 示例：ATR(5m).close(14).value - 14期ATR绝对值
     *       ATR(5m).close(14).pct   - ATR占价格的百分比
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param period 回溯周期
     * @return IndicatorResult 包含两个字段（value=绝对ATR, pct=百分比ATR）
     *
     * 计算方法：
     *   True Range = max(high-low, abs(high-prev_close), abs(low-prev_close))
     *   ATR = average(True Range, period)
     *   pct = (ATR / current_price) * 100
     * 
     * 用途：
     *   .value - 绝对波动幅度，用于止损距离计算
     *   .pct   - 相对波动幅度，适合跨品种比较
     * 
     * 应用场景：
     *   - 动态止损止盈
     *   - ATR通道构建
     *   - 波动率突破
     *   - 仓位管理（基于波动率）
     * 
     * 注意：ATR考虑了跳空缺口，比标准差更全面
     */
    IndicatorResult ATR(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int period
    );

    // ========================================================================
    // BOP - 力量平衡（Balance of Power）- POWER函数组
    // ========================================================================

    /**
     * BOP - 力量平衡指标
     *
     * DSL 语法：POWER(timeframe).BOP
     *           POWER(timeframe).BOP(offset)
     * 示例：POWER(5m).BOP > 0 - 多头占优
     *       POWER(5m).BOP > POWER(5m).BOP(1) - 力量增强
     *
     * @param open 开盘价序列
     * @param high 最高价序列
     * @param low 最低价序列
     * @param close 收盘价序列
     * @param offset 偏移量（0=当前，1=前1根）
     * @return IndicatorResult 包含单个字段 "value"（BOP值，-1到1之间）
     *
     * 计算公式：(close - open) / (high - low)
     * 用途：判断多空力量对比，值域[-1, 1]
     */
    IndicatorResult BOP(
        const std::vector<double>& open,
        const std::vector<double>& high,
        const std::vector<double>& low,
        const std::vector<double>& close,
        int offset = 0
    );
};

} // namespace prophet::functions

