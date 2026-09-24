/*
 * ============================================================================
 * 文件名：kline_simd.cpp
 * 功能说明：K线合并的SIMD向量化实现
 * ============================================================================
 */

#include "prophet/simd/kline_simd.hpp"
#include <algorithm>
#include <limits>
#include <cstring>

// SIMD 头文件
#if defined(_MSC_VER)
    #include <intrin.h>
#elif defined(__GNUC__) || defined(__clang__)
    #include <cpuid.h>
    #include <x86intrin.h>
#endif

namespace prophet::simd {

// ============================================================================
// CPU 特性检测
// ============================================================================

CPUFeatures CPUFeatures::detect() {
    CPUFeatures features;
    features.sse42_supported = false;
    features.avx2_supported = false;
    
#if defined(_MSC_VER)
    // Windows: 使用 __cpuid
    int cpu_info[4];
    
    // EAX=1: 获取特性标志
    __cpuid(cpu_info, 1);
    features.sse42_supported = (cpu_info[2] & (1 << 20)) != 0;  // ECX bit 20
    
    // EAX=7, ECX=0: 获取扩展特性
    __cpuidex(cpu_info, 7, 0);
    features.avx2_supported = (cpu_info[1] & (1 << 5)) != 0;    // EBX bit 5
    
#elif defined(__GNUC__) || defined(__clang__)
    // Linux/Mac: 使用 __get_cpuid
    unsigned int eax, ebx, ecx, edx;
    
    // EAX=1
    if (__get_cpuid(1, &eax, &ebx, &ecx, &edx)) {
        features.sse42_supported = (ecx & bit_SSE4_2) != 0;
    }
    
    // EAX=7, ECX=0
    if (__get_cpuid_count(7, 0, &eax, &ebx, &ecx, &edx)) {
        features.avx2_supported = (ebx & bit_AVX2) != 0;
    }
#endif
    
    return features;
}

const CPUFeatures& getCPUFeatures() {
    static CPUFeatures features = CPUFeatures::detect();
    return features;
}

// ============================================================================
// SIMD 自动分发
// ============================================================================

MergeResult mergeKlinesSIMD(const std::vector<Kline>& klines) {
    if (klines.empty()) {
        return {0, 0, 0, 0, 0};
    }
    
    if (klines.size() == 1) {
        const auto& k = klines[0];
        return {k.open, k.high, k.low, k.close, k.volume};
    }
    
    const auto& features = getCPUFeatures();
    
    // AVX2 要求至少8个元素才值得使用
    if (features.avx2_supported && klines.size() >= 8) {
        return mergeKlinesAVX2(klines);
    }
    
    // SSE4.2 要求至少4个元素才值得使用
    if (features.sse42_supported && klines.size() >= 4) {
        return mergeKlinesSSE42(klines);
    }
    
    // 降级到标量计算
    return mergeKlinesScalar(klines);
}

// ============================================================================
// AVX2 实现（8个double并行）
// ============================================================================

MergeResult mergeKlinesAVX2(const std::vector<Kline>& klines) {
#if defined(__AVX2__) || (defined(_MSC_VER) && defined(__AVX2__))
    const size_t count = klines.size();
    
    MergeResult result;
    result.open = klines[0].open;
    result.close = klines[count - 1].close;
    
    // 提取数据到对齐数组（AVX2要求32字节对齐）
    alignas(32) double high_array[256];
    alignas(32) double low_array[256];
    alignas(32) double volume_array[256];
    
    // 批量提取（分批次处理，避免栈溢出）
    const size_t batch_size = 256;
    size_t processed = 0;
    
    double final_high = klines[0].high;
    double final_low = klines[0].low;
    double final_volume = 0.0;
    
    while (processed < count) {
        size_t current_batch = std::min(batch_size, count - processed);
        
        // 提取当前批次
        for (size_t i = 0; i < current_batch; ++i) {
            const auto& k = klines[processed + i];
            high_array[i] = k.high;
            low_array[i] = k.low;
            volume_array[i] = k.volume;
        }
        
        // AVX2 向量化计算（8个double一组）
        __m256d vec_high = _mm256_set1_pd(high_array[0]);
        __m256d vec_low = _mm256_set1_pd(low_array[0]);
        __m256d vec_volume = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 8 <= current_batch; i += 8) {
            // 加载8个high
            __m256d h1 = _mm256_load_pd(&high_array[i]);
            __m256d h2 = _mm256_load_pd(&high_array[i + 4]);
            vec_high = _mm256_max_pd(vec_high, h1);
            vec_high = _mm256_max_pd(vec_high, h2);
            
            // 加载8个low
            __m256d l1 = _mm256_load_pd(&low_array[i]);
            __m256d l2 = _mm256_load_pd(&low_array[i + 4]);
            vec_low = _mm256_min_pd(vec_low, l1);
            vec_low = _mm256_min_pd(vec_low, l2);
            
            // 加载8个volume
            __m256d v1 = _mm256_load_pd(&volume_array[i]);
            __m256d v2 = _mm256_load_pd(&volume_array[i + 4]);
            vec_volume = _mm256_add_pd(vec_volume, v1);
            vec_volume = _mm256_add_pd(vec_volume, v2);
        }
        
        // 水平归约（将向量中的4个元素合并）
        alignas(32) double high_results[4];
        alignas(32) double low_results[4];
        alignas(32) double volume_results[4];
        
        _mm256_store_pd(high_results, vec_high);
        _mm256_store_pd(low_results, vec_low);
        _mm256_store_pd(volume_results, vec_volume);
        
        double batch_high = high_results[0];
        double batch_low = low_results[0];
        double batch_volume = volume_results[0];
        
        for (int j = 1; j < 4; ++j) {
            batch_high = std::max(batch_high, high_results[j]);
            batch_low = std::min(batch_low, low_results[j]);
            batch_volume += volume_results[j];
        }
        
        // 处理剩余元素（标量）
        for (; i < current_batch; ++i) {
            batch_high = std::max(batch_high, high_array[i]);
            batch_low = std::min(batch_low, low_array[i]);
            batch_volume += volume_array[i];
        }
        
        // 合并批次结果
        final_high = std::max(final_high, batch_high);
        final_low = std::min(final_low, batch_low);
        final_volume += batch_volume;
        
        processed += current_batch;
    }
    
    result.high = final_high;
    result.low = final_low;
    result.volume = final_volume;
    
    return result;
#else
    // 如果编译时未启用AVX2，降级到SSE或标量
    return mergeKlinesScalar(klines);
#endif
}

// ============================================================================
// SSE4.2 实现（2个double并行）
// ============================================================================

MergeResult mergeKlinesSSE42(const std::vector<Kline>& klines) {
#if defined(__SSE4_2__) || (defined(_MSC_VER) && _M_IX86_FP >= 2)
    const size_t count = klines.size();
    
    MergeResult result;
    result.open = klines[0].open;
    result.close = klines[count - 1].close;
    
    // SSE 向量化（2个double一组）
    __m128d vec_high = _mm_set1_pd(klines[0].high);
    __m128d vec_low = _mm_set1_pd(klines[0].low);
    __m128d vec_volume = _mm_setzero_pd();
    
    size_t i = 1;
    for (; i + 2 <= count; i += 2) {
        // 加载2个high
        __m128d h = _mm_set_pd(klines[i + 1].high, klines[i].high);
        vec_high = _mm_max_pd(vec_high, h);
        
        // 加载2个low
        __m128d l = _mm_set_pd(klines[i + 1].low, klines[i].low);
        vec_low = _mm_min_pd(vec_low, l);
        
        // 加载2个volume
        __m128d v = _mm_set_pd(klines[i + 1].volume, klines[i].volume);
        vec_volume = _mm_add_pd(vec_volume, v);
    }
    
    // 水平归约
    alignas(16) double high_results[2];
    alignas(16) double low_results[2];
    alignas(16) double volume_results[2];
    
    _mm_store_pd(high_results, vec_high);
    _mm_store_pd(low_results, vec_low);
    _mm_store_pd(volume_results, vec_volume);
    
    result.high = std::max(high_results[0], high_results[1]);
    result.low = std::min(low_results[0], low_results[1]);
    result.volume = volume_results[0] + volume_results[1];
    
    // 处理剩余元素（标量）
    for (; i < count; ++i) {
        result.high = std::max(result.high, klines[i].high);
        result.low = std::min(result.low, klines[i].low);
        result.volume += klines[i].volume;
    }
    
    return result;
#else
    return mergeKlinesScalar(klines);
#endif
}

// ============================================================================
// 标量实现（无SIMD）
// ============================================================================

MergeResult mergeKlinesScalar(const std::vector<Kline>& klines) {
    if (klines.empty()) {
        return {0, 0, 0, 0, 0};
    }
    
    MergeResult result;
    result.open = klines[0].open;
    result.close = klines[klines.size() - 1].close;
    result.high = klines[0].high;
    result.low = klines[0].low;
    result.volume = 0.0;
    
    for (const auto& k : klines) {
        result.high = std::max(result.high, k.high);
        result.low = std::min(result.low, k.low);
        result.volume += k.volume;
    }
    
    return result;
}

// ============================================================================
// 字段提取（SIMD优化版本留作扩展）
// ============================================================================

void extractField(
    const std::vector<Kline>& klines,
    double* out_array,
    size_t count,
    const char* field_name
) {
    const size_t actual_count = std::min(count, klines.size());
    
    if (strcmp(field_name, "open") == 0) {
        for (size_t i = 0; i < actual_count; ++i) {
            out_array[i] = klines[i].open;
        }
    } else if (strcmp(field_name, "high") == 0) {
        for (size_t i = 0; i < actual_count; ++i) {
            out_array[i] = klines[i].high;
        }
    } else if (strcmp(field_name, "low") == 0) {
        for (size_t i = 0; i < actual_count; ++i) {
            out_array[i] = klines[i].low;
        }
    } else if (strcmp(field_name, "close") == 0) {
        for (size_t i = 0; i < actual_count; ++i) {
            out_array[i] = klines[i].close;
        }
    } else if (strcmp(field_name, "volume") == 0) {
        for (size_t i = 0; i < actual_count; ++i) {
            out_array[i] = klines[i].volume;
        }
    }
}

// ============================================================================
// 向量运算
// ============================================================================

double vectorMax(const double* data, size_t count) {
    if (count == 0) {
        return std::numeric_limits<double>::lowest();
    }
    
    double result = data[0];
    for (size_t i = 1; i < count; ++i) {
        result = std::max(result, data[i]);
    }
    return result;
}

double vectorMin(const double* data, size_t count) {
    if (count == 0) {
        return std::numeric_limits<double>::max();
    }
    
    double result = data[0];
    for (size_t i = 1; i < count; ++i) {
        result = std::min(result, data[i]);
    }
    return result;
}

double vectorSum(const double* data, size_t count) {
    double result = 0.0;
    for (size_t i = 0; i < count; ++i) {
        result += data[i];
    }
    return result;
}

} // namespace prophet::simd

