/*
 * ============================================================================
 * 文件名：simd_config.cpp
 * 功能说明：SIMD配置实现
 * ============================================================================
 */

#include "prophet/simd/simd_config.hpp"
#include <sstream>
#include <vector>

// 平台相关的CPUID指令
#ifdef _MSC_VER
#include <intrin.h>
#else
#include <cpuid.h>
#endif

namespace prophet {
namespace simd {

/**
 * CPUID包装函数
 * 
 * 跨平台调用CPUID指令
 */
static void cpuid(int info[4], int function_id) {
#ifdef _MSC_VER
    __cpuid(info, function_id);
#else
    __cpuid(function_id, info[0], info[1], info[2], info[3]);
#endif
}

static void cpuidex(int info[4], int function_id, int subfunction_id) {
#ifdef _MSC_VER
    __cpuidex(info, function_id, subfunction_id);
#else
    __cpuid_count(function_id, subfunction_id, info[0], info[1], info[2], info[3]);
#endif
}

/**
 * 检测CPU特性
 */
CPUFeatures detectCPUFeatures() {
    CPUFeatures features;
    
    int info[4];
    
    // 检查CPUID是否可用
    cpuid(info, 0);
    int max_id = info[0];
    
    if (max_id >= 1) {
        cpuid(info, 1);
        
        // ECX寄存器的特性位
        int ecx = info[2];
        int edx = info[3];
        
        // SSE2: bit 26 of EDX
        features.has_sse2 = (edx & (1 << 26)) != 0;
        
        // AVX: bit 28 of ECX
        features.has_avx = (ecx & (1 << 28)) != 0;
    }
    
    if (max_id >= 7) {
        cpuidex(info, 7, 0);
        
        // EBX寄存器的特性位
        int ebx = info[1];
        
        // AVX2: bit 5 of EBX
        features.has_avx2 = (ebx & (1 << 5)) != 0;
        
        // AVX-512F: bit 16 of EBX
        features.has_avx512f = (ebx & (1 << 16)) != 0;
    }
    
    return features;
}

/**
 * CPUFeatures::toString
 */
std::string CPUFeatures::toString() const {
    std::ostringstream oss;
    oss << "CPU SIMD Features: ";
    
    std::vector<std::string> features;
    if (has_sse2) features.push_back("SSE2");
    if (has_avx) features.push_back("AVX");
    if (has_avx2) features.push_back("AVX2");
    if (has_avx512f) features.push_back("AVX-512");
    
    if (features.empty()) {
        oss << "None";
    } else {
        for (size_t i = 0; i < features.size(); i++) {
            if (i > 0) oss << ", ";
            oss << features[i];
        }
    }
    
    oss << " (Vector Width: " << getVectorWidth() << " doubles)";
    
    return oss.str();
}

/**
 * Config单例实现
 */
Config::Config()
    : enabled_(true)  // 默认启用
    , features_(detectCPUFeatures())
    , stats_() {
}

Config& Config::instance() {
    static Config instance;
    return instance;
}

std::string Config::getSummary() const {
    std::ostringstream oss;
    oss << "SIMD Configuration:\n";
    oss << "  Enabled: " << (enabled_ ? "Yes" : "No") << "\n";
    oss << "  " << features_.toString() << "\n";
    oss << "  Best Instruction Set: " << features_.getBestInstructionSet() << "\n";
    
    if (stats_.simd_calls > 0 || stats_.scalar_calls > 0) {
        oss << "  Statistics:\n";
        oss << "    SIMD calls: " << stats_.simd_calls << "\n";
        oss << "    Scalar calls: " << stats_.scalar_calls << "\n";
        oss << "    SIMD ratio: " << (stats_.simd_ratio() * 100) << "%\n";
        oss << "    Elements processed: " << stats_.elements_processed;
    }
    
    return oss.str();
}

void Config::recordSIMDCall(uint64_t elements) {
    stats_.simd_calls++;
    stats_.elements_processed += elements;
}

void Config::recordScalarCall(uint64_t elements) {
    stats_.scalar_calls++;
    stats_.elements_processed += elements;
}

/**
 * SIMDInitializer实现
 */
SIMDInitializer::SIMDInitializer(bool force_enable)
    : available_(false)
    , features_() {
    
    features_ = detectCPUFeatures();
    
    // 至少需要SSE2才认为SIMD可用
    available_ = features_.has_sse2;
    
    if (force_enable && available_) {
        Config::instance().setEnabled(true);
    }
}

SIMDInitializer::~SIMDInitializer() {
    // 清理工作（如果需要）
}

} // namespace simd
} // namespace prophet

