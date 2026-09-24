/*
 * ============================================================================
 * 文件名：common.hpp
 * 功能：指标计算的常用辅助函数和宏定义
 * 
 * 说明：
 * - 提供未使用参数的标记宏
 * - 提供常用的类型转换辅助函数
 * - 减少代码重复
 * ============================================================================
 */

#pragma once

#include <vector>
#include <algorithm>

namespace prophet::indicators {

// 标记未使用的参数，避免编译警告
#define UNUSED(x) (void)(x)

// 安全的 size_t 到 int 转换
inline int safe_size_to_int(size_t size) {
    return static_cast<int>(size);
}

// 获取向量最后N个元素的最大值
inline double get_max_last_n(const std::vector<double>& vec, size_t n) {
    if (vec.empty() || n == 0) return 0.0;
    size_t start = vec.size() > n ? vec.size() - n : 0;
    return *std::max_element(vec.begin() + start, vec.end());
}

// 获取向量最后N个元素的最小值
inline double get_min_last_n(const std::vector<double>& vec, size_t n) {
    if (vec.empty() || n == 0) return 0.0;
    size_t start = vec.size() > n ? vec.size() - n : 0;
    return *std::min_element(vec.begin() + start, vec.end());
}

} // namespace prophet::indicators