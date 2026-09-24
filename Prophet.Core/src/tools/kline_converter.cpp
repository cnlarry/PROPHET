/*
 * ============================================================================
 * 文件名：kline_converter.cpp
 * 功能说明：K线时间框架转换器实现（优化版本）
 * 
 * Prophet系统统一的K线合成实现
 * 
 * 性能优化：
 * - SIMD向量化：使用AVX2/SSE4.2加速K线合并计算
 * - LRU缓存：避免重复转换相同数据
 * - 多线程：并行处理多个时间框架（见 parallel::ParallelKlineConverter）
 * ============================================================================
 */

#include "prophet/tools/kline_converter.hpp"
#include "prophet/simd/kline_simd.hpp"
#include <algorithm>
#include <stdexcept>
#include <unordered_map>
#include <cmath>
#include <cstring>
#include <iostream>
#define _CRT_SECURE_NO_WARNINGS

namespace prophet::tools::kline {

std::vector<Kline> Converter::convert(
    const std::vector<Kline>& records,
    int from_minutes,
    int to_minutes,
    int period
) {
    // 参数验证
    if (records.empty() || to_minutes <= 0 || from_minutes <= 0) {
        return {};
    }

    if (to_minutes <= from_minutes) {
        throw std::invalid_argument(
            "Target timeframe (" + std::to_string(to_minutes) + "m) must be greater than source timeframe (" + 
            std::to_string(from_minutes) + "m)"
        );
    }

    if (to_minutes % from_minutes != 0) {
        throw std::invalid_argument(
            "Target timeframe (" + std::to_string(to_minutes) + "m) must be a multiple of source timeframe (" + 
            std::to_string(from_minutes) + "m)"
        );
    }

    // 阶段2优化：如果已经排序，使用const引用避免拷贝
    const std::vector<Kline>* klines_ptr = &records;
    auto pooled_vec = getKlineVectorPool().acquire();
    
    if (!isSorted(records)) {
        // 仅在需要排序时才拷贝
        *pooled_vec = records;
        std::sort(pooled_vec->begin(), pooled_vec->end(), 
                  [](const Kline& a, const Kline& b) { return a.open_time < b.open_time; });
        klines_ptr = &(*pooled_vec);
    }
    
    const std::vector<Kline>& klines_list = *klines_ptr;

    // 计算时间窗口参数
    const int64_t target_window_ms = static_cast<int64_t>(to_minutes) * 60 * 1000;
    const int64_t source_step_ms = static_cast<int64_t>(from_minutes) * 60 * 1000;
    const int expected_per_window = to_minutes / from_minutes;

    // 按目标时间框架分组（严格按UTC纪元倍数对齐）
    // 阶段2优化：预分配空间
    std::unordered_map<int64_t, std::vector<Kline>> time_groups;
    time_groups.reserve(klines_list.size() / expected_per_window + 1);

    for (const auto& kline : klines_list) {
        // 以UTC纪元为锚点，按目标窗口大小取整，实现严格对齐
        int64_t aligned_start_ms = (kline.open_time / target_window_ms) * target_window_ms;
        
        // 阶段2优化：预留空间避免多次扩容
        auto& group = time_groups[aligned_start_ms];
        if (group.empty()) {
            group.reserve(expected_per_window);
        }
        group.push_back(kline);
    }

    // 计算最新源K线的结束时间（用于过滤不完整窗口）
    int64_t latest_close_ms = 0;
    for (const auto& k : klines_list) {
        if (k.close_time > latest_close_ms) {
            latest_close_ms = k.close_time;
        }
    }

    // 合并每个时间窗口的数据
    // 阶段2优化：预分配结果空间
    std::vector<Kline> result;
    result.reserve(time_groups.size());

    // 获取排序后的窗口开始时间
    std::vector<int64_t> sorted_windows;
    sorted_windows.reserve(time_groups.size());
    for (const auto& pair : time_groups) {
        sorted_windows.push_back(pair.first);
    }
    std::sort(sorted_windows.begin(), sorted_windows.end());
    
    for (int64_t window_start_ms : sorted_windows) {
        auto& group = time_groups[window_start_ms];

        if (group.empty()) {
            continue;
        }

        // 按时间排序确保正确顺序
        std::sort(group.begin(), group.end(),
                  [](const Kline& a, const Kline& b) { return a.open_time < b.open_time; });

        // 计算时间边界
        int64_t window_end_ms = window_start_ms + target_window_ms - 1;

        // 过滤不完整窗口：结束时间必须不大于最新源K线收盘时间
        if (window_end_ms > latest_close_ms) {
            continue;
        }

        // 过滤不完整窗口：数量必须等于期望值
        if (static_cast<int>(group.size()) != expected_per_window) {
            continue;
        }

        // 检查时间连续性：所有K线必须严格步进
        bool is_continuous = true;
        for (int i = 1; i < expected_per_window; ++i) {
            int64_t expected_next = group[0].open_time + static_cast<int64_t>(i) * source_step_ms;
            if (group[i].open_time != expected_next) {
                is_continuous = false;
                break;
            }
        }

        if (!is_continuous) {
            continue;
        }
        
        // 额外检查：确保整个group确实在这个窗口范围内
        if (group[0].open_time < window_start_ms || group[0].open_time >= window_start_ms + target_window_ms) {
            continue;
        }

        // 合并K线数据
        result.push_back(mergeKlines(group, window_start_ms, window_end_ms));
    }

    // 如果指定了period，返回最近的N根K线
    if (period > 0 && static_cast<int>(result.size()) > period) {
        result.erase(result.begin(), result.end() - period);
    }

    return result;
}

int Converter::timeframeToMinutes(const std::string& timeframe) {
    if (timeframe.empty()) {
        throw std::invalid_argument("Timeframe cannot be empty");
    }

    // 解析时间框架字符串
    std::string num_str;
    char unit = '\0';

    for (char c : timeframe) {
        if (std::isdigit(c)) {
            num_str += c;
        } else {
            unit = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
            break;
        }
    }

    if (num_str.empty()) {
        throw std::invalid_argument("Invalid timeframe format: " + timeframe);
    }

    int value = std::stoi(num_str);

    switch (unit) {
        case 'm':  // 分钟
            return value;
        case 'h':  // 小时
            return value * 60;
        case 'd':  // 天
            return value * 60 * 24;
        case 'w':  // 周
            return value * 60 * 24 * 7;
        default:
            throw std::invalid_argument("Invalid timeframe unit: " + std::string(1, unit));
    }
}

std::string Converter::minutesToTimeframe(int minutes) {
    if (minutes <= 0) {
        throw std::invalid_argument("Minutes must be positive");
    }

    // 优先使用更大的单位
    if (minutes % (60 * 24 * 7) == 0) {
        return std::to_string(minutes / (60 * 24 * 7)) + "w";
    }
    if (minutes % (60 * 24) == 0) {
        return std::to_string(minutes / (60 * 24)) + "d";
    }
    if (minutes % 60 == 0) {
        return std::to_string(minutes / 60) + "h";
    }
    return std::to_string(minutes) + "m";
}

bool Converter::isSorted(const std::vector<Kline>& klines) {
    for (size_t i = 1; i < klines.size(); ++i) {
        if (klines[i].open_time < klines[i - 1].open_time) {
            return false;
        }
    }
    return true;
}

Kline Converter::mergeKlines(
    const std::vector<Kline>& group,
    int64_t window_start_ms,
    int64_t window_end_ms
) {
    // 使用SIMD向量化加速K线合并
    // 当K线数量较多时（>= 4根），SIMD带来显著性能提升
    simd::MergeResult simd_result = simd::mergeKlinesSIMD(group);
    
    Kline merged;
    merged.open_time = window_start_ms;
    merged.close_time = window_end_ms;
    merged.open = simd_result.open;
    merged.high = simd_result.high;
    merged.low = simd_result.low;
    merged.close = simd_result.close;
    merged.volume = simd_result.volume;

    return merged;
}

} // namespace prophet::tools::kline

// ============================================================================
// C API 实现
// ============================================================================

extern "C" {

int prophet_kline_convert(
    const ProphetKline* input,
    int input_count,
    int from_minutes,
    int to_minutes,
    ProphetKline* output,
    int* output_count,
    int period
) {
    if (!input || !output || !output_count || input_count <= 0) {
        return -1; // 参数错误
    }

    try {
        // 将C结构体转换为C++对象
        std::vector<prophet::Kline> klines_cpp;
        klines_cpp.reserve(input_count);
        
        for (int i = 0; i < input_count; ++i) {
            prophet::Kline k;
            k.open_time = input[i].open_time;
            k.close_time = input[i].close_time;
            k.open = input[i].open;
            k.high = input[i].high;
            k.low = input[i].low;
            k.close = input[i].close;
            k.volume = input[i].volume;
            klines_cpp.push_back(k);
        }

        // 调用C++实现
        auto result = prophet::tools::kline::Converter::convert(
            klines_cpp, from_minutes, to_minutes, period);

        // 转换回C结构体
        *output_count = static_cast<int>(result.size());
        for (size_t i = 0; i < result.size() && i < static_cast<size_t>(input_count); ++i) {
            output[i].open_time = result[i].open_time;
            output[i].close_time = result[i].close_time;
            output[i].open = result[i].open;
            output[i].high = result[i].high;
            output[i].low = result[i].low;
            output[i].close = result[i].close;
            output[i].volume = result[i].volume;
        }

        return 0; // 成功
    } catch (...) {
        return -2; // 内部错误
    }
}

int prophet_kline_timeframe_to_minutes(const char* timeframe) {
    if (!timeframe) {
        return -1;
    }

    try {
        return prophet::tools::kline::Converter::timeframeToMinutes(timeframe);
    } catch (...) {
        return -1;
    }
}

int prophet_kline_minutes_to_timeframe(int minutes, char* output, int output_size) {
    if (!output || output_size < 1) {
        return -1;
    }

    try {
        std::string result = prophet::tools::kline::Converter::minutesToTimeframe(minutes);
        if (static_cast<int>(result.length()) + 1 > output_size) {
            return -1; // 缓冲区太小
        }
        strncpy_s(output, output_size, result.c_str(), _TRUNCATE);
        return 0;
    } catch (...) {
        return -1;
    }
}

} // extern "C"

