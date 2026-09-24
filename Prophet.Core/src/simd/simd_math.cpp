/*
 * ============================================================================
 * 文件名：simd_math.cpp
 * 功能说明：SIMD数学运算实现
 * 
 * 实现策略：
 * 1. 如果AVX2可用，使用256位向量指令（4个double）
 * 2. 否则使用标量回退
 * 3. 主循环处理4的倍数，剩余元素用标量处理
 * ============================================================================
 */

#include "prophet/simd/simd_math.hpp"
#include "prophet/simd/simd_config.hpp"
#include <cmath>
#include <algorithm>
#include <limits>

// AVX2头文件
#if defined(__AVX2__) || defined(_MSC_VER)
#include <immintrin.h>
#define HAS_AVX2_SUPPORT
#endif

namespace prophet {
namespace simd {

/**
 * ============================================================================
 * 内部辅助函数
 * ============================================================================
 */

// 检查是否应该使用SIMD
static inline bool should_use_simd() {
    return Config::instance().isEnabled() && 
           Config::instance().features().has_avx2;
}

/**
 * ============================================================================
 * 基础向量运算实现
 * ============================================================================
 */

void add(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        // 向量化主循环：每次处理4个double
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vr = _mm256_add_pd(va, vb);
            _mm256_storeu_pd(result + i, vr);
        }
        
        // 处理剩余元素（标量）
        for (; i < length; i++) {
            result[i] = a[i] + b[i];
        }
        return;
    }
#endif
    
    // 标量回退
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = a[i] + b[i];
    }
}

void sub(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vr = _mm256_sub_pd(va, vb);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = a[i] - b[i];
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = a[i] - b[i];
    }
}

void mul(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vr = _mm256_mul_pd(va, vb);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = a[i] * b[i];
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = a[i] * b[i];
    }
}

void div(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vr = _mm256_div_pd(va, vb);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = a[i] / b[i];
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = a[i] / b[i];
    }
}

void add_scalar(const double* a, double scalar, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vscalar = _mm256_set1_pd(scalar);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vr = _mm256_add_pd(va, vscalar);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = a[i] + scalar;
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = a[i] + scalar;
    }
}

void mul_scalar(const double* a, double scalar, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vscalar = _mm256_set1_pd(scalar);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vr = _mm256_mul_pd(va, vscalar);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = a[i] * scalar;
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = a[i] * scalar;
    }
}

/**
 * ============================================================================
 * 统计运算实现
 * ============================================================================
 */

double sum(const double* data, size_t length) {
    if (length == 0) return 0.0;
    
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vsum = _mm256_setzero_pd();
        
        size_t i = 0;
        // 向量化累加
        for (; i + 3 < length; i += 4) {
            __m256d vdata = _mm256_loadu_pd(data + i);
            vsum = _mm256_add_pd(vsum, vdata);
        }
        
        // 水平求和：将4个double相加
        // vsum = [a, b, c, d]
        // 提取并相加
        double temp[4];
        _mm256_storeu_pd(temp, vsum);
        double result = temp[0] + temp[1] + temp[2] + temp[3];
        
        // 处理剩余元素
        for (; i < length; i++) {
            result += data[i];
        }
        
        return result;
    }
#endif
    
    // 标量回退
    Config::instance().recordScalarCall(length);
    double result = 0.0;
    for (size_t i = 0; i < length; i++) {
        result += data[i];
    }
    return result;
}

double mean(const double* data, size_t length) {
    if (length == 0) return 0.0;
    return sum(data, length) / static_cast<double>(length);
}

double variance(const double* data, size_t length, double mean_value) {
    if (length == 0) return 0.0;
    
    // 如果均值未提供，先计算
    if (mean_value == 0.0) {
        mean_value = mean(data, length);
    }
    
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vmean = _mm256_set1_pd(mean_value);
        __m256d vsum = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vdata = _mm256_loadu_pd(data + i);
            __m256d vdiff = _mm256_sub_pd(vdata, vmean);
            __m256d vsquared = _mm256_mul_pd(vdiff, vdiff);
            vsum = _mm256_add_pd(vsum, vsquared);
        }
        
        double temp[4];
        _mm256_storeu_pd(temp, vsum);
        double result = temp[0] + temp[1] + temp[2] + temp[3];
        
        for (; i < length; i++) {
            double diff = data[i] - mean_value;
            result += diff * diff;
        }
        
        return result / static_cast<double>(length);
    }
#endif
    
    // 标量回退
    Config::instance().recordScalarCall(length);
    double sum_sq = 0.0;
    for (size_t i = 0; i < length; i++) {
        double diff = data[i] - mean_value;
        sum_sq += diff * diff;
    }
    return sum_sq / static_cast<double>(length);
}

double stddev(const double* data, size_t length, double mean_value) {
    return std::sqrt(variance(data, length, mean_value));
}

double min(const double* data, size_t length) {
    if (length == 0) return std::numeric_limits<double>::quiet_NaN();
    
    double result = data[0];
    for (size_t i = 1; i < length; i++) {
        if (data[i] < result) {
            result = data[i];
        }
    }
    return result;
}

double max(const double* data, size_t length) {
    if (length == 0) return std::numeric_limits<double>::quiet_NaN();
    
    double result = data[0];
    for (size_t i = 1; i < length; i++) {
        if (data[i] > result) {
            result = data[i];
        }
    }
    return result;
}

/**
 * ============================================================================
 * 高级运算实现
 * ============================================================================
 */

void rolling_sum(const double* data, size_t length, int period, double* result) {
    if (length == 0 || period <= 0) return;
    
    // 前period-1个元素：累加不足period个
    for (int i = 0; i < period - 1 && i < static_cast<int>(length); i++) {
        result[i] = sum(data, i + 1);
    }
    
    // 从period位置开始：使用滑动窗口优化
    if (static_cast<int>(length) >= period) {
        result[period - 1] = sum(data, period);
        
        for (size_t i = period; i < length; i++) {
            // 滑动窗口：result[i] = result[i-1] - data[i-period] + data[i]
            result[i] = result[i - 1] - data[i - period] + data[i];
        }
    }
}

void rolling_mean(const double* data, size_t length, int period, double* result) {
    if (length == 0 || period <= 0) return;
    
    // 先计算滑动和
    rolling_sum(data, length, period, result);
    
    // 除以窗口大小得到平均值
    for (size_t i = 0; i < length; i++) {
        int window_size = std::min(static_cast<int>(i + 1), period);
        result[i] /= static_cast<double>(window_size);
    }
}

void rolling_stddev(const double* data, size_t length, int period, double* result) {
    if (length == 0 || period <= 0) return;
    
    for (size_t i = 0; i < length; i++) {
        int start = std::max(0, static_cast<int>(i) - period + 1);
        int window_size = static_cast<int>(i) - start + 1;
        result[i] = stddev(data + start, window_size);
    }
}

void abs(const double* a, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        // 创建符号位掩码（清除符号位 = 绝对值）
        __m256d sign_mask = _mm256_set1_pd(-0.0);  // 0x8000000000000000
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vabs = _mm256_andnot_pd(sign_mask, va);
            _mm256_storeu_pd(result + i, vabs);
        }
        
        for (; i < length; i++) {
            result[i] = std::abs(a[i]);
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = std::abs(a[i]);
    }
}

void square(const double* a, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vsq = _mm256_mul_pd(va, va);
            _mm256_storeu_pd(result + i, vsq);
        }
        
        for (; i < length; i++) {
            result[i] = a[i] * a[i];
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = a[i] * a[i];
    }
}

void sqrt(const double* a, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vsqrt = _mm256_sqrt_pd(va);
            _mm256_storeu_pd(result + i, vsqrt);
        }
        
        for (; i < length; i++) {
            result[i] = std::sqrt(a[i]);
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = std::sqrt(a[i]);
    }
}

void max(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vmax = _mm256_max_pd(va, vb);
            _mm256_storeu_pd(result + i, vmax);
        }
        
        for (; i < length; i++) {
            result[i] = std::max(a[i], b[i]);
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = std::max(a[i], b[i]);
    }
}

void min(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vmin = _mm256_min_pd(va, vb);
            _mm256_storeu_pd(result + i, vmin);
        }
        
        for (; i < length; i++) {
            result[i] = std::min(a[i], b[i]);
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = std::min(a[i], b[i]);
    }
}

void compare_gt(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vone = _mm256_set1_pd(1.0);
        __m256d vzero = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vcmp = _mm256_cmp_pd(va, vb, _CMP_GT_OQ);  // a > b
            __m256d vr = _mm256_blendv_pd(vzero, vone, vcmp);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = (a[i] > b[i]) ? 1.0 : 0.0;
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = (a[i] > b[i]) ? 1.0 : 0.0;
    }
}

void compare_lt(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vone = _mm256_set1_pd(1.0);
        __m256d vzero = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vcmp = _mm256_cmp_pd(va, vb, _CMP_LT_OQ);  // a < b
            __m256d vr = _mm256_blendv_pd(vzero, vone, vcmp);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = (a[i] < b[i]) ? 1.0 : 0.0;
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = (a[i] < b[i]) ? 1.0 : 0.0;
    }
}

void compare_ge(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vone = _mm256_set1_pd(1.0);
        __m256d vzero = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vcmp = _mm256_cmp_pd(va, vb, _CMP_GE_OQ);  // a >= b
            __m256d vr = _mm256_blendv_pd(vzero, vone, vcmp);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = (a[i] >= b[i]) ? 1.0 : 0.0;
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = (a[i] >= b[i]) ? 1.0 : 0.0;
    }
}

void compare_le(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vone = _mm256_set1_pd(1.0);
        __m256d vzero = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vcmp = _mm256_cmp_pd(va, vb, _CMP_LE_OQ);  // a <= b
            __m256d vr = _mm256_blendv_pd(vzero, vone, vcmp);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = (a[i] <= b[i]) ? 1.0 : 0.0;
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = (a[i] <= b[i]) ? 1.0 : 0.0;
    }
}

void compare_eq(const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vone = _mm256_set1_pd(1.0);
        __m256d vzero = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vcmp = _mm256_cmp_pd(va, vb, _CMP_EQ_OQ);  // a == b
            __m256d vr = _mm256_blendv_pd(vzero, vone, vcmp);
            _mm256_storeu_pd(result + i, vr);
        }
        
        for (; i < length; i++) {
            result[i] = (a[i] == b[i]) ? 1.0 : 0.0;
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = (a[i] == b[i]) ? 1.0 : 0.0;
    }
}

void select(const double* cond, const double* a, const double* b, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vcond = _mm256_loadu_pd(cond + i);
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            
            // 使用blendv_pd指令根据条件选择a或b
            // cond[i] != 0.0时选择a[i]，否则选择b[i]
            __m256d vresult = _mm256_blendv_pd(vb, va, vcond);
            _mm256_storeu_pd(result + i, vresult);
        }
        
        for (; i < length; i++) {
            result[i] = (cond[i] != 0.0) ? a[i] : b[i];
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = (cond[i] != 0.0) ? a[i] : b[i];
    }
}

void max3(const double* a, const double* b, const double* c, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vc = _mm256_loadu_pd(c + i);
            
            __m256d vmax = _mm256_max_pd(va, vb);
            vmax = _mm256_max_pd(vmax, vc);
            
            _mm256_storeu_pd(result + i, vmax);
        }
        
        for (; i < length; i++) {
            result[i] = std::max({a[i], b[i], c[i]});
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = std::max({a[i], b[i], c[i]});
    }
}

void min3(const double* a, const double* b, const double* c, double* result, size_t length) {
#ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d va = _mm256_loadu_pd(a + i);
            __m256d vb = _mm256_loadu_pd(b + i);
            __m256d vc = _mm256_loadu_pd(c + i);
            
            __m256d vmin = _mm256_min_pd(va, vb);
            vmin = _mm256_min_pd(vmin, vc);
            
            _mm256_storeu_pd(result + i, vmin);
        }
        
        for (; i < length; i++) {
            result[i] = std::min({a[i], b[i], c[i]});
        }
        return;
    }
#endif
    
    Config::instance().recordScalarCall(length);
    for (size_t i = 0; i < length; i++) {
        result[i] = std::min({a[i], b[i], c[i]});
    }
}

void rolling_min(const double* data, size_t length, int period, double* result) {
    if (length == 0 || period <= 0) return;
    
    for (size_t i = 0; i < length; i++) {
        int start = std::max(0, static_cast<int>(i) - period + 1);
        int window_size = static_cast<int>(i) - start + 1;
        result[i] = min(data + start, window_size);
    }
}

void rolling_max(const double* data, size_t length, int period, double* result) {
    if (length == 0 || period <= 0) return;
    
    for (size_t i = 0; i < length; i++) {
        int start = std::max(0, static_cast<int>(i) - period + 1);
        int window_size = static_cast<int>(i) - start + 1;
        result[i] = max(data + start, window_size);
    }
}

} // namespace simd
} // namespace prophet

