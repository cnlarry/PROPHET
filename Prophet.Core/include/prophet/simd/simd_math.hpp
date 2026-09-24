/*
 * ============================================================================
 * 文件名：simd_math.hpp
 * 功能说明：SIMD数学运算库 - P3优化核心
 * 
 * 提供高性能的向量化数学运算，包括：
 * - 基础运算：加减乘除
 * - 统计运算：求和、平均、标准差
 * - 自动回退：如果SIMD不可用，自动使用标量实现
 * 
 * 性能目标：比标量实现快3-4倍（AVX2）
 * ============================================================================
 */

#pragma once

#include <cstddef>
#include <vector>

namespace prophet {
namespace simd {

/**
 * ============================================================================
 * 基础向量运算
 * ============================================================================
 */

/**
 * 向量加法：result[i] = a[i] + b[i]
 * 
 * @param a 输入向量A
 * @param b 输入向量B
 * @param result 输出向量（长度必须 >= length）
 * @param length 向量长度
 * 
 * 性能：AVX2可实现4倍加速
 */
void add(const double* a, const double* b, double* result, size_t length);

/**
 * 向量减法：result[i] = a[i] - b[i]
 */
void sub(const double* a, const double* b, double* result, size_t length);

/**
 * 向量乘法：result[i] = a[i] * b[i]
 */
void mul(const double* a, const double* b, double* result, size_t length);

/**
 * 向量除法：result[i] = a[i] / b[i]
 * 
 * 注意：不检查除零错误，调用者需确保b[i] != 0
 */
void div(const double* a, const double* b, double* result, size_t length);

/**
 * 标量加法：result[i] = a[i] + scalar
 */
void add_scalar(const double* a, double scalar, double* result, size_t length);

/**
 * 标量乘法：result[i] = a[i] * scalar
 */
void mul_scalar(const double* a, double scalar, double* result, size_t length);

/**
 * ============================================================================
 * 统计运算
 * ============================================================================
 */

/**
 * 向量求和：sum(a[0] + a[1] + ... + a[length-1])
 * 
 * @param data 输入数据
 * @param length 数据长度
 * @return 求和结果
 * 
 * 性能：AVX2可实现4倍加速
 */
double sum(const double* data, size_t length);

/**
 * 向量平均值：mean = sum / length
 */
double mean(const double* data, size_t length);

/**
 * 向量方差：var = sum((x - mean)^2) / length
 * 
 * @param data 输入数据
 * @param length 数据长度
 * @param mean_value 如果已知均值，可传入避免重复计算；否则传0
 * @return 方差
 */
double variance(const double* data, size_t length, double mean_value = 0.0);

/**
 * 向量标准差：stddev = sqrt(variance)
 */
double stddev(const double* data, size_t length, double mean_value = 0.0);

/**
 * 向量最小值
 */
double min(const double* data, size_t length);

/**
 * 向量最大值
 */
double max(const double* data, size_t length);

/**
 * ============================================================================
 * 高级运算（为指标计算优化）
 * ============================================================================
 */

/**
 * 滑动窗口求和
 * 
 * 计算每个位置的窗口和：result[i] = sum(data[i-period+1] ... data[i])
 * 
 * @param data 输入数据
 * @param length 数据长度
 * @param period 窗口大小
 * @param result 输出结果（长度必须 >= length）
 * 
 * 优化：使用累加法，避免重复计算
 * result[i] = result[i-1] - data[i-period] + data[i]
 */
void rolling_sum(const double* data, size_t length, int period, double* result);

/**
 * 滑动窗口平均（SMA核心）
 * 
 * result[i] = mean(data[i-period+1] ... data[i])
 */
void rolling_mean(const double* data, size_t length, int period, double* result);

/**
 * 滑动窗口标准差
 */
void rolling_stddev(const double* data, size_t length, int period, double* result);

/**
 * 向量绝对值：result[i] = |a[i]|
 */
void abs(const double* a, double* result, size_t length);

/**
 * 向量平方：result[i] = a[i] * a[i]
 */
void square(const double* a, double* result, size_t length);

/**
 * 向量平方根：result[i] = sqrt(a[i])
 */
void sqrt(const double* a, double* result, size_t length);

/**
 * 向量最大值：result[i] = max(a[i], b[i])
 */
void max(const double* a, const double* b, double* result, size_t length);

/**
 * 向量最小值：result[i] = min(a[i], b[i])
 */
void min(const double* a, const double* b, double* result, size_t length);

/**
 * 向量比较：result[i] = (a[i] > b[i]) ? 1.0 : 0.0
 * 
 * 用于计算涨跌：gains = compare_gt(prices[1:], prices[:-1])
 */
void compare_gt(const double* a, const double* b, double* result, size_t length);

/**
 * 向量比较：result[i] = (a[i] < b[i]) ? 1.0 : 0.0
 */
void compare_lt(const double* a, const double* b, double* result, size_t length);

/**
 * 向量比较：result[i] = (a[i] >= b[i]) ? 1.0 : 0.0
 */
void compare_ge(const double* a, const double* b, double* result, size_t length);

/**
 * 向量比较：result[i] = (a[i] <= b[i]) ? 1.0 : 0.0
 */
void compare_le(const double* a, const double* b, double* result, size_t length);

/**
 * 向量比较：result[i] = (a[i] == b[i]) ? 1.0 : 0.0
 */
void compare_eq(const double* a, const double* b, double* result, size_t length);

/**
 * 向量条件选择：result[i] = (cond[i] != 0.0) ? a[i] : b[i]
 */
void select(const double* cond, const double* a, const double* b, double* result, size_t length);

/**
 * 三向量最大值：result[i] = max(a[i], b[i], c[i])
 * 
 * 用于计算True Range等
 */
void max3(const double* a, const double* b, const double* c, double* result, size_t length);

/**
 * 三向量最小值：result[i] = min(a[i], b[i], c[i])
 */
void min3(const double* a, const double* b, const double* c, double* result, size_t length);

/**
 * 滑动窗口最小值
 */
void rolling_min(const double* data, size_t length, int period, double* result);

/**
 * 滑动窗口最大值
 */
void rolling_max(const double* data, size_t length, int period, double* result);

/**
 * ============================================================================
 * 便捷接口（std::vector版本）
 * ============================================================================
 */

inline std::vector<double> add(const std::vector<double>& a, const std::vector<double>& b) {
    size_t len = std::min(a.size(), b.size());
    std::vector<double> result(len);
    add(a.data(), b.data(), result.data(), len);
    return result;
}

inline std::vector<double> sub(const std::vector<double>& a, const std::vector<double>& b) {
    size_t len = std::min(a.size(), b.size());
    std::vector<double> result(len);
    sub(a.data(), b.data(), result.data(), len);
    return result;
}

inline std::vector<double> mul(const std::vector<double>& a, const std::vector<double>& b) {
    size_t len = std::min(a.size(), b.size());
    std::vector<double> result(len);
    mul(a.data(), b.data(), result.data(), len);
    return result;
}

inline double sum(const std::vector<double>& data) {
    return sum(data.data(), data.size());
}

inline double mean(const std::vector<double>& data) {
    return mean(data.data(), data.size());
}

inline double stddev(const std::vector<double>& data) {
    return stddev(data.data(), data.size());
}

inline std::vector<double> rolling_mean(const std::vector<double>& data, int period) {
    std::vector<double> result(data.size());
    rolling_mean(data.data(), data.size(), period, result.data());
    return result;
}

} // namespace simd
} // namespace prophet

