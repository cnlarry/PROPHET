/*
 * ============================================================================
 * 文件名：TimeSeries.cpp
 * 功能说明：时间序列函数实现
 * 
 * 这个文件实现了不需要时间框架参数的时间序列数据访问函数
 * 
 * 支持的时间序列函数：
 * - FEARGREED：恐惧与贪婪指数
 *   - value(offset)：获取指定日期的指数值
 *   - classification(offset)：获取指定日期的分类标签
 *   - change(period)：计算N天变化量
 *   - avg(period)：计算N天平均值
 *   - min(period)：获取N天最小值
 *   - max(period)：获取N天最大值
 *   - trend(period)：判断N天趋势方向
 * - FUNDINGRATE：资金费率
 *   - value(offset)：获取指定偏移的资金费率
 *   - avg(period)：计算N次平均值
 *   - trend(period)：判断N次趋势方向
 * 
 * 使用示例：
 *   FEARGREED().value(0)  // 获取今天的指数值
 *   FEARGREED().classification(0)  // 获取今天的分类
 *   FEARGREED().change(7)  // 计算7天变化量
 *   FEARGREED().avg(30)  // 计算30天平均值
 *   FUNDINGRATE().value(0)  // 获取最新的资金费率
 *   FUNDINGRATE().avg(24)  // 计算24次平均值（约3天）
 *   FUNDINGRATE().trend(24)  // 判断24次趋势方向
 * ============================================================================
 */

#include "prophet/functions/FunctionRegistry.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include "prophet/core/context.hpp"
#include <algorithm>
#include <numeric>
#include <cmath>
#include <ctime>
#include <iostream>

namespace prophet::functions {

namespace {

// 辅助函数：根据当前时间查找FEARGREED数据索引
// 数据按时间戳降序排列（最新的在前），需要找到最接近current_time的数据点
int findFearGreedIndexByTime(const std::vector<FearGreedData>& series, int64_t current_time) {
    if (series.empty()) {
        return -1;
    }
    
    // 将current_time转换为日期时间戳（UTC 00:00:00）
    // FEARGREED数据是每日更新的，所以需要对齐到日期
    const int64_t seconds_per_day = 86400;
    int64_t current_date_ts = (current_time / 1000) / seconds_per_day * seconds_per_day;
    
    // 二分查找最接近的数据点（series是降序排列）
    int left = 0;
    int right = static_cast<int>(series.size()) - 1;
    int best_index = 0;
    int64_t min_diff = std::abs(series[0].date_timestamp - current_date_ts);
    
    while (left <= right) {
        int mid = (left + right) / 2;
        int64_t diff = series[mid].date_timestamp - current_date_ts;
        
        if (std::abs(diff) < min_diff) {
            min_diff = std::abs(diff);
            best_index = mid;
        }
        
        if (diff > 0) {
            // series[mid]的时间戳大于current_date_ts，说明在更前面（更新的数据）
            left = mid + 1;
        } else if (diff < 0) {
            // series[mid]的时间戳小于current_date_ts，说明在后面（更旧的数据）
            right = mid - 1;
        } else {
            // 找到精确匹配
            return mid;
        }
    }
    
    return best_index;
}

// 辅助函数：获取offset对应的FEARGREED数据（带边界检查和时间对齐）
const FearGreedData* getFearGreedByOffset(const std::vector<FearGreedData>& series, int offset, int64_t current_time) {
    if (series.empty()) {
        return nullptr;
    }
    
    // 根据current_time找到当前数据点的索引
    int current_index = findFearGreedIndexByTime(series, current_time);
    if (current_index < 0) {
        return nullptr;
    }
    
    // 边界检查：偏移量范围 [-100, 0]
    if (offset < -100 || offset > 0) {
        // 超出范围，返回最新的数据
        return &series[0];
    }
    
    // 根据offset进行偏移（offset=0表示今天，offset=-1表示昨天）
    int target_index = current_index - offset;  // offset为负数
    
    // 边界检查
    if (target_index < 0) {
        // 超出范围，返回最新的数据
        return &series[0];
    }
    if (target_index >= static_cast<int>(series.size())) {
        // 超出范围，返回最旧的数据
        return &series.back();
    }
    
    return &series[target_index];
}

// 辅助函数：根据当前时间查找FUNDINGRATE数据索引
// 数据按时间戳降序排列（最新的在前），需要找到最接近current_time的数据点
int findFundingRateIndexByTime(const std::vector<FundingRateData>& series, int64_t current_time) {
    if (series.empty()) {
        return -1;
    }
    
    // FUNDINGRATE数据是每8小时更新一次，需要找到最接近current_time的数据点
    // 使用毫秒时间戳直接比较
    
    // 二分查找最接近的数据点（series是降序排列）
    int left = 0;
    int right = static_cast<int>(series.size()) - 1;
    int best_index = 0;
    int64_t min_diff = std::abs(series[0].timestamp - current_time);
    
    while (left <= right) {
        int mid = (left + right) / 2;
        int64_t diff = series[mid].timestamp - current_time;
        
        if (std::abs(diff) < min_diff) {
            min_diff = std::abs(diff);
            best_index = mid;
        }
        
        if (diff > 0) {
            // series[mid]的时间戳大于current_time，说明在更前面（更新的数据）
            left = mid + 1;
        } else if (diff < 0) {
            // series[mid]的时间戳小于current_time，说明在后面（更旧的数据）
            right = mid - 1;
        } else {
            // 找到精确匹配
            return mid;
        }
    }
    
    return best_index;
}

// 辅助函数：获取offset对应的资金费率数据（带边界检查和时间对齐）
const FundingRateData* getFundingRateByOffset(const std::vector<FundingRateData>& series, int offset, int64_t current_time) {
    if (series.empty()) {
        return nullptr;
    }
    
    // 根据current_time找到当前数据点的索引
    int current_index = findFundingRateIndexByTime(series, current_time);
    if (current_index < 0) {
        return nullptr;
    }
    
    // 边界检查：偏移量范围 [-100, 0]
    if (offset < -100 || offset > 0) {
        // 超出范围，返回最新的数据
        return &series[0];
    }
    
    // 根据offset进行偏移（offset=0表示当前，offset=-1表示上一次）
    int target_index = current_index - offset;  // offset为负数
    
    // 边界检查
    if (target_index < 0) {
        // 超出范围，返回最新的数据
        return &series[0];
    }
    if (target_index >= static_cast<int>(series.size())) {
        // 超出范围，返回最旧的数据
        return &series.back();
    }
    
    return &series[target_index];
}

// 辅助函数：根据当前时间查找LONGSHORT数据索引
// 数据按时间戳降序排列（最新的在前），需要找到最接近current_time的数据点
int findLongShortRatioIndexByTime(const std::vector<LongShortRatioData>& series, int64_t current_time) {
    if (series.empty()) {
        return -1;
    }
    
    // LONGSHORT数据的时间戳是秒，current_time可能是毫秒，需要转换
    int64_t current_time_seconds = current_time / 1000;
    
    // 二分查找最接近的数据点（series是降序排列）
    int left = 0;
    int right = static_cast<int>(series.size()) - 1;
    int best_index = 0;
    int64_t min_diff = std::abs(series[0].timestamp - current_time_seconds);
    
    while (left <= right) {
        int mid = (left + right) / 2;
        int64_t diff = series[mid].timestamp - current_time_seconds;
        
        if (std::abs(diff) < min_diff) {
            min_diff = std::abs(diff);
            best_index = mid;
        }
        
        if (diff > 0) {
            // series[mid]的时间戳大于current_time_seconds，说明在更前面（更新的数据）
            left = mid + 1;
        } else if (diff < 0) {
            // series[mid]的时间戳小于current_time_seconds，说明在后面（更旧的数据）
            right = mid - 1;
        } else {
            // 找到精确匹配
            return mid;
        }
    }
    
    return best_index;
}

// 辅助函数：获取offset对应的多空比数据（带边界检查和时间对齐）
const LongShortRatioData* getLongShortRatioByOffset(const std::vector<LongShortRatioData>& series, int offset, int64_t current_time) {
    if (series.empty()) {
        return nullptr;
    }
    
    // 根据current_time找到当前数据点的索引
    int current_index = findLongShortRatioIndexByTime(series, current_time);
    if (current_index < 0) {
        return nullptr;
    }
    
    // 边界检查：偏移量范围 [-100, 0]
    if (offset < -100 || offset > 0) {
        // 超出范围，返回最新的数据
        return &series[0];
    }
    
    // 根据offset进行偏移（offset=0表示当前，offset=-1表示上一次）
    int target_index = current_index - offset;  // offset为负数
    
    // 边界检查
    if (target_index < 0) {
        // 超出范围，返回最新的数据
        return &series[0];
    }
    if (target_index >= static_cast<int>(series.size())) {
        // 超出范围，返回最旧的数据
        return &series.back();
    }
    
    return &series[target_index];
}

// 计算线性回归斜率（用于trend判断）- 整数版本
double calculateSlope(const std::vector<int>& values) {
    if (values.size() < 2) {
        return 0.0;
    }
    
    int n = static_cast<int>(values.size());
    double sum_x = 0.0, sum_y = 0.0, sum_xy = 0.0, sum_x2 = 0.0;
    
    for (int i = 0; i < n; ++i) {
        double x = static_cast<double>(i);
        double y = static_cast<double>(values[i]);
        sum_x += x;
        sum_y += y;
        sum_xy += x * y;
        sum_x2 += x * x;
    }
    
    // 斜率 = (n*Σxy - Σx*Σy) / (n*Σx² - (Σx)²)
    double denominator = n * sum_x2 - sum_x * sum_x;
    if (std::abs(denominator) < 1e-10) {
        return 0.0;
    }
    
    return (n * sum_xy - sum_x * sum_y) / denominator;
}

// 计算线性回归斜率（用于trend判断）- 浮点数版本
double calculateSlopeDouble(const std::vector<double>& values) {
    if (values.size() < 2) {
        return 0.0;
    }
    
    int n = static_cast<int>(values.size());
    double sum_x = 0.0, sum_y = 0.0, sum_xy = 0.0, sum_x2 = 0.0;
    
    for (int i = 0; i < n; ++i) {
        double x = static_cast<double>(i);
        double y = values[i];
        sum_x += x;
        sum_y += y;
        sum_xy += x * y;
        sum_x2 += x * x;
    }
    
    // 斜率 = (n*Σxy - Σx*Σy) / (n*Σx² - (Σx)²)
    double denominator = n * sum_x2 - sum_x * sum_x;
    if (std::abs(denominator) < 1e-10) {
        return 0.0;
    }
    
    return (n * sum_xy - sum_x * sum_y) / denominator;
}

// 判断趋势方向
std::string judgeTrend(double slope, double threshold = 0.5) {
    if (slope > threshold) {
        return "RISING";
    } else if (slope < -threshold) {
        return "FALLING";
    } else {
        return "STABLE";
    }
}

} // namespace

void register_timeseries_functions(FunctionRegistry& registry) {
    
    // ========================================================================
    // FEARGREED - 恐惧与贪婪指数
    // ========================================================================
    
    registry.register_function("FEARGREED", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        // 检查是否有数据
        if (!ctx.hasFearGreedData()) {
            throw EvaluatorException("FEARGREED data not available. Please set fear greed series data.");
        }
        
        const auto& series = ctx.getFearGreedSeries();
        
        // 根据method字段决定调用哪个功能
        const std::string& method = call.field;
        
        if (method.empty()) {
            throw EvaluatorException("FEARGREED requires a method call: value(), classification(), change(), etc.");
        }
        
        // ====================================================================
        // value(offset) - 获取指定日期的指数值
        // ====================================================================
        if (method == "value") {
            int offset = 0;
            if (!call.arguments.empty()) {
                offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            
            const FearGreedData* data = getFearGreedByOffset(series, offset, current_time);
            if (!data) {
                throw EvaluatorException("FEARGREED: No data available for offset " + std::to_string(offset));
            }
            
            IndicatorResult result;
            result.set("value", Value::fromNumber(data->value));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // classification(offset) - 获取指定日期的分类标签
        // ====================================================================
        if (method == "classification") {
            int offset = 0;
            if (!call.arguments.empty()) {
                offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            
            const FearGreedData* data = getFearGreedByOffset(series, offset, current_time);
            if (!data) {
                throw EvaluatorException("FEARGREED: No data available for offset " + std::to_string(offset));
            }
            
            IndicatorResult result;
            result.set("value", Value::fromString(data->classification));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // change(period) - 计算N天变化量
        // ====================================================================
        if (method == "change") {
            int period = 7;  // 默认7天
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            // period 参数改为负数偏移（与 offset 统一）
            // 例如：period = 7 表示7天前，需要转换为 offset = -7
            int offset = -period;
            if (offset < -100 || offset > 0) {
                throw EvaluatorException("FEARGREED.change: period must be in range [1, 100]");
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            
            // 获取当前值和period天前的值（使用负数偏移）
            const FearGreedData* current = getFearGreedByOffset(series, 0, current_time);
            const FearGreedData* past = getFearGreedByOffset(series, offset, current_time);
            
            if (!current || !past) {
                throw EvaluatorException("FEARGREED.change: Insufficient data");
            }
            
            int change = current->value - past->value;
            
            IndicatorResult result;
            result.set("value", Value::fromNumber(change));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // avg(period) - 计算N天平均值
        // ====================================================================
        if (method == "avg") {
            int period = 7;  // 默认7天
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            if (period < 1) {
                throw EvaluatorException("FEARGREED.avg: period must be >= 1");
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            int current_index = findFearGreedIndexByTime(series, current_time);
            if (current_index < 0) {
                throw EvaluatorException("FEARGREED.avg: No data available");
            }
            
            // 从当前索引开始，向前取period个数据点计算平均值
            int count = std::min(period, static_cast<int>(series.size()) - current_index);
            if (count == 0) {
                throw EvaluatorException("FEARGREED.avg: Insufficient data");
            }
            
            double sum = 0.0;
            for (int i = 0; i < count; ++i) {
                int idx = current_index + i;
                if (idx >= static_cast<int>(series.size())) {
                    break;
                }
                sum += series[idx].value;
            }
            
            double avg = sum / count;
            
            IndicatorResult result;
            result.set("value", Value::fromNumber(avg));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // min(period) - 获取N天最小值
        // ====================================================================
        if (method == "min") {
            int period = 7;  // 默认7天
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            if (period < 1) {
                throw EvaluatorException("FEARGREED.min: period must be >= 1");
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            int current_index = findFearGreedIndexByTime(series, current_time);
            if (current_index < 0) {
                throw EvaluatorException("FEARGREED.min: No data available");
            }
            
            // 从当前索引开始，向前取period个数据点找最小值
            int count = std::min(period, static_cast<int>(series.size()) - current_index);
            if (count == 0) {
                throw EvaluatorException("FEARGREED.min: Insufficient data");
            }
            
            int min_value = series[current_index].value;
            for (int i = 1; i < count; ++i) {
                int idx = current_index + i;
                if (idx >= static_cast<int>(series.size())) {
                    break;
                }
                min_value = std::min(min_value, series[idx].value);
            }
            
            IndicatorResult result;
            result.set("value", Value::fromNumber(min_value));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // max(period) - 获取N天最大值
        // ====================================================================
        if (method == "max") {
            int period = 7;  // 默认7天
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            if (period < 1) {
                throw EvaluatorException("FEARGREED.max: period must be >= 1");
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            int current_index = findFearGreedIndexByTime(series, current_time);
            if (current_index < 0) {
                throw EvaluatorException("FEARGREED.max: No data available");
            }
            
            // 从当前索引开始，向前取period个数据点找最大值
            int count = std::min(period, static_cast<int>(series.size()) - current_index);
            if (count == 0) {
                throw EvaluatorException("FEARGREED.max: Insufficient data");
            }
            
            int max_value = series[current_index].value;
            for (int i = 1; i < count; ++i) {
                int idx = current_index + i;
                if (idx >= static_cast<int>(series.size())) {
                    break;
                }
                max_value = std::max(max_value, series[idx].value);
            }
            
            IndicatorResult result;
            result.set("value", Value::fromNumber(max_value));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // trend(period) - 判断N天趋势方向
        // ====================================================================
        if (method == "trend") {
            int period = 7;  // 默认7天
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            if (period < 2) {
                throw EvaluatorException("FEARGREED.trend: period must be >= 2");
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            int current_index = findFearGreedIndexByTime(series, current_time);
            if (current_index < 0) {
                throw EvaluatorException("FEARGREED.trend: No data available");
            }
            
            // 从当前索引开始，向前取period个数据点
            int count = std::min(period, static_cast<int>(series.size()) - current_index);
            if (count < 2) {
                throw EvaluatorException("FEARGREED.trend: Insufficient data for trend calculation");
            }
            
            std::vector<int> values;
            values.reserve(count);
            
            // 注意：series是降序（最新在前），需要从旧到新收集数据来计算趋势
            // 从current_index开始，向前取count个数据点（从旧到新）
            for (int i = count - 1; i >= 0; --i) {
                int idx = current_index + i;
                if (idx >= static_cast<int>(series.size())) {
                    break;
                }
                values.push_back(series[idx].value);
            }
            
            double slope = calculateSlope(values);
            std::string trend_direction = judgeTrend(slope);
            
            IndicatorResult result;
            result.set("value", Value::fromString(trend_direction));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // 未知方法
        throw EvaluatorException("FEARGREED: Unknown method '" + method + "'. Available methods: value, classification, change, avg, min, max, trend");
    });
    
    // ========================================================================
    // FUNDINGRATE - 资金费率
    // ========================================================================
    
    registry.register_function("FUNDINGRATE", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        // 检查是否有数据
        if (!ctx.hasFundingRateData()) {
            throw EvaluatorException("FUNDINGRATE data not available. Please set funding rate series data.");
        }
        
        const auto& series = ctx.getFundingRateSeries();
        
        // 根据method字段决定调用哪个功能
        const std::string& method = call.field;
        
        if (method.empty()) {
            throw EvaluatorException("FUNDINGRATE requires a method call: value(), avg(), trend()");
        }
        
        // ====================================================================
        // value(offset) - 获取指定偏移的资金费率
        // ====================================================================
        if (method == "value") {
            int offset = 0;
            if (!call.arguments.empty()) {
                offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            
            const FundingRateData* data = getFundingRateByOffset(series, offset, current_time);
            if (!data) {
                throw EvaluatorException("FUNDINGRATE: No data available for offset " + std::to_string(offset));
            }
            
            IndicatorResult result;
            result.set("value", Value::fromNumber(data->value));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // avg(period) - 计算N次平均值
        // ====================================================================
        if (method == "avg") {
            int period = 24;  // 默认24次（约3天，每天3次）
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            if (period < 1) {
                throw EvaluatorException("FUNDINGRATE.avg: period must be >= 1");
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            int current_index = findFundingRateIndexByTime(series, current_time);
            if (current_index < 0) {
                throw EvaluatorException("FUNDINGRATE.avg: No data available");
            }
            
            // 从当前索引开始，向前取period个数据点计算平均值
            int count = std::min(period, static_cast<int>(series.size()) - current_index);
            if (count == 0) {
                throw EvaluatorException("FUNDINGRATE.avg: Insufficient data");
            }
            
            double sum = 0.0;
            for (int i = 0; i < count; ++i) {
                int idx = current_index + i;
                if (idx >= static_cast<int>(series.size())) {
                    break;
                }
                sum += series[idx].value;
            }
            
            double avg = sum / count;
            
            IndicatorResult result;
            result.set("value", Value::fromNumber(avg));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // ====================================================================
        // trend(period) - 判断N次趋势方向
        // ====================================================================
        if (method == "trend") {
            int period = 24;  // 默认24次
            if (!call.arguments.empty()) {
                period = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
            }
            
            if (period < 2) {
                throw EvaluatorException("FUNDINGRATE.trend: period must be >= 2");
            }
            
            // 获取当前时间，用于数据对齐
            int64_t current_time = ctx.getCurrentTime();
            int current_index = findFundingRateIndexByTime(series, current_time);
            if (current_index < 0) {
                throw EvaluatorException("FUNDINGRATE.trend: No data available");
            }
            
            // 从当前索引开始，向前取period个数据点
            int count = std::min(period, static_cast<int>(series.size()) - current_index);
            if (count < 2) {
                throw EvaluatorException("FUNDINGRATE.trend: Insufficient data for trend calculation");
            }
            
            std::vector<double> values;
            values.reserve(count);
            
            // 注意：series是降序（最新在前），需要从旧到新收集数据来计算趋势
            // 从current_index开始，向前取count个数据点（从旧到新）
            for (int i = count - 1; i >= 0; --i) {
                int idx = current_index + i;
                if (idx >= static_cast<int>(series.size())) {
                    break;
                }
                values.push_back(series[idx].value);
            }
            
            double slope = calculateSlopeDouble(values);
            // 资金费率趋势判断阈值较小（0.0001），因为资金费率本身数值较小
            std::string trend_direction = judgeTrend(slope, 0.0001);
            
            IndicatorResult result;
            result.set("value", Value::fromString(trend_direction));
            return FunctionResult::fromValue(result.get("value"));
        }
        
        // 未知方法
        throw EvaluatorException("FUNDINGRATE: Unknown method '" + method + "'. Available methods: value, avg, trend");
    });
    
    // ========================================================================
    // CURRENT - 当前状态访问函数
    // ========================================================================
    
    registry.register_function("CURRENT", [](const FunctionCall& call, prophet::dsl::Context& ctx) -> FunctionResult {
        const std::string& field = call.field;
        
        if (field.empty()) {
            throw EvaluatorException("CURRENT requires a field: price, timestamp, time, fundingrate, feargreed");
        }
        
        // ====================================================================
        // price - 返回当前价格
        // ====================================================================
        if (field == "price") {
            double price = ctx.getCurrentPrice();
            return FunctionResult::fromValue(Value::fromNumber(price));
        }
        
        // ====================================================================
        // timestamp - 返回Unix时间戳（秒）
        // ====================================================================
        if (field == "timestamp") {
            int64_t timestamp = ctx.getCurrentTime();
            return FunctionResult::fromValue(Value::fromNumber(static_cast<double>(timestamp)));
        }
        
        // ====================================================================
        // fundingrate - 返回当前资金费率
        // ====================================================================
        if (field == "fundingrate") {
            if (!ctx.hasFundingRateData()) {
                throw EvaluatorException("CURRENT.fundingrate: No funding rate data available. Please set funding rate series.");
            }
            
            const auto& series = ctx.getFundingRateSeries();
            int64_t current_time = ctx.getCurrentTime();
            
            const FundingRateData* data = getFundingRateByOffset(series, 0, current_time);
            if (!data) {
                throw EvaluatorException("CURRENT.fundingrate: No funding rate data found for current time");
            }
            
            return FunctionResult::fromValue(Value::fromNumber(data->value));
        }
        
        // ====================================================================
        // time - 时间字段访问（支持时区）
        // 语法：
        //   CURRENT().time.hour          // UTC时间的小时
        //   CURRENT().time(8).hour       // 北京时间的小时（UTC+8）
        //   CURRENT().time(-4).minute    // UTC-4时区的分钟
        // ====================================================================
        if (field == "time") {
            const std::string& subfield = call.subfield;
            
            if (subfield.empty()) {
                throw EvaluatorException("CURRENT.time requires a subfield: hour, minute, second, week, day, month");
            }
            
            // 获取当前时间戳（秒）
            int64_t timestamp = ctx.getCurrentTime();
            
            // 检查是否有时区参数
            int timezone_offset = 0;  // 默认UTC（无偏移）
            if (!call.arguments.empty()) {
                timezone_offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
                
                // 验证时区范围：-12 到 +12
                if (timezone_offset < -12 || timezone_offset > 12) {
                    throw EvaluatorException("CURRENT.time: timezone offset must be between -12 and 12, got " + std::to_string(timezone_offset));
                }
            }
            
            // 应用时区偏移（小时转秒）
            int64_t adjusted_timestamp = timestamp + (timezone_offset * 3600);
            
            // 将时间戳转换为时间字段
            // Unix时间戳是从1970-01-01 00:00:00 UTC开始的秒数
            time_t time_val = static_cast<time_t>(adjusted_timestamp);
            struct tm time_info;
            
            #ifdef _WIN32
                gmtime_s(&time_info, &time_val);
            #else
                gmtime_r(&time_val, &time_info);
            #endif
            
            // 根据子字段返回相应的值
            if (subfield == "hour") {
                // 小时：0-23
                return FunctionResult::fromValue(Value::fromNumber(time_info.tm_hour));
            }
            else if (subfield == "minute") {
                // 分钟：0-59
                return FunctionResult::fromValue(Value::fromNumber(time_info.tm_min));
            }
            else if (subfield == "second") {
                // 秒：0-59
                return FunctionResult::fromValue(Value::fromNumber(time_info.tm_sec));
            }
            else if (subfield == "week") {
                // 星期几：0-6（0=周日，1=周一，...，6=周六）
                return FunctionResult::fromValue(Value::fromNumber(time_info.tm_wday));
            }
            else if (subfield == "day") {
                // 日期：1-31
                return FunctionResult::fromValue(Value::fromNumber(time_info.tm_mday));
            }
            else if (subfield == "month") {
                // 月份：1-12（注意：tm_mon是0-11，需要+1）
                return FunctionResult::fromValue(Value::fromNumber(time_info.tm_mon + 1));
            }
            else {
                throw EvaluatorException("CURRENT.time: Unknown subfield '" + subfield + "'. Available: hour, minute, second, week, day, month");
            }
        }
        
        // ====================================================================
        // feargreed - 恐惧与贪婪指数访问
        // 语法：
        //   CURRENT().feargreed.value           // 当天的指数值
        //   CURRENT().feargreed.classification  // 当天的分类
        //   CURRENT().feargreed(-1).value       // 昨天的指数值
        // ====================================================================
        if (field == "feargreed") {
            if (!ctx.hasFearGreedData()) {
                throw EvaluatorException("CURRENT.feargreed: No fear greed data available. Please set fear greed series.");
            }
            
            const std::string& subfield = call.subfield;
            
            if (subfield.empty()) {
                throw EvaluatorException("CURRENT.feargreed requires a subfield: value, classification");
            }
            
            // 获取偏移量（默认为0，表示当天）
            int offset = 0;
            if (!call.arguments.empty()) {
                offset = static_cast<int>(call.arguments[0]->evaluate(ctx).toNumber());
                
                // 验证偏移量范围：-100 到 0
                if (offset > 0 || offset < -100) {
                    throw EvaluatorException("CURRENT.feargreed: offset must be between -100 and 0, got " + std::to_string(offset));
                }
            }
            
            const auto& series = ctx.getFearGreedSeries();
            int64_t current_time = ctx.getCurrentTime();
            
            // offset是负数，表示过去的天数
            // offset=0表示今天，offset=-1表示昨天
            const FearGreedData* data = getFearGreedByOffset(series, offset, current_time);
            if (!data) {
                throw EvaluatorException("CURRENT.feargreed: No data available for offset " + std::to_string(offset));
            }
            
            // 根据子字段返回相应的值
            if (subfield == "value") {
                return FunctionResult::fromValue(Value::fromNumber(data->value));
            }
            else if (subfield == "classification") {
                return FunctionResult::fromValue(Value::fromString(data->classification));
            }
            else {
                throw EvaluatorException("CURRENT.feargreed: Unknown subfield '" + subfield + "'. Available: value, classification");
            }
        }
        
        // ====================================================================
        // longshort - 多空比数据访问
        // 语法：
        //   CURRENT().longshort.ratio(offset)          // 多空比率
        //   CURRENT().longshort.long(offset)           // 多头比例
        //   CURRENT().longshort.short(offset)          // 空头比例
        //   CURRENT().longshort.net(offset)            // 净多头比例
        //   CURRENT().longshort.dominance(offset)      // 多头主导率
        //   CURRENT().longshort.trend(period)          // 趋势判断
        //   CURRENT().longshort.extreme(threshold)     // 极端检测
        // ====================================================================
        if (field == "longshort") {
            if (!ctx.hasLongShortRatioData()) {
                throw EvaluatorException("CURRENT.longshort: No long short ratio data available. Please set long short ratio series.");
            }
            
            const std::string& subfield = call.subfield;
            
            if (subfield.empty()) {
                throw EvaluatorException("CURRENT.longshort requires a subfield: ratio, long, short, net, dominance, trend, extreme");
            }
            
            const auto& series = ctx.getLongShortRatioSeries();
            int64_t current_time = ctx.getCurrentTime();
            
            // ================================================================
            // ratio(offset) - 多空比率（long/short）
            // ================================================================
            if (subfield == "ratio") {
                int offset = 0;
                if (!call.submethod_params.empty()) {
                    offset = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                }
                
                if (offset < -100 || offset > 0) {
                    throw EvaluatorException("CURRENT.longshort.ratio: offset must be between -100 and 0, got " + std::to_string(offset));
                }
                
                const LongShortRatioData* data = getLongShortRatioByOffset(series, offset, current_time);
                if (!data) {
                    throw EvaluatorException("CURRENT.longshort.ratio: No data available for offset " + std::to_string(offset));
                }
                
                return FunctionResult::fromValue(Value::fromNumber(data->ratio));
            }
            
            // ================================================================
            // long(offset) - 多头比例（0~1）
            // ================================================================
            if (subfield == "long") {
                int offset = 0;
                if (!call.submethod_params.empty()) {
                    offset = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                }
                
                if (offset < -100 || offset > 0) {
                    throw EvaluatorException("CURRENT.longshort.long: offset must be between -100 and 0, got " + std::to_string(offset));
                }
                
                const LongShortRatioData* data = getLongShortRatioByOffset(series, offset, current_time);
                if (!data) {
                    throw EvaluatorException("CURRENT.longshort.long: No data available for offset " + std::to_string(offset));
                }
                
                return FunctionResult::fromValue(Value::fromNumber(data->long_ratio));
            }
            
            // ================================================================
            // short(offset) - 空头比例（0~1）
            // ================================================================
            if (subfield == "short") {
                int offset = 0;
                if (!call.submethod_params.empty()) {
                    offset = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                }
                
                if (offset < -100 || offset > 0) {
                    throw EvaluatorException("CURRENT.longshort.short: offset must be between -100 and 0, got " + std::to_string(offset));
                }
                
                const LongShortRatioData* data = getLongShortRatioByOffset(series, offset, current_time);
                if (!data) {
                    throw EvaluatorException("CURRENT.longshort.short: No data available for offset " + std::to_string(offset));
                }
                
                return FunctionResult::fromValue(Value::fromNumber(data->short_ratio));
            }
            
            // ================================================================
            // net(offset) - 净多头比例 (long_ratio - short_ratio) / (long_ratio + short_ratio)
            // ================================================================
            if (subfield == "net") {
                int offset = 0;
                if (!call.submethod_params.empty()) {
                    offset = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                }
                
                if (offset < -100 || offset > 0) {
                    throw EvaluatorException("CURRENT.longshort.net: offset must be between -100 and 0, got " + std::to_string(offset));
                }
                
                const LongShortRatioData* data = getLongShortRatioByOffset(series, offset, current_time);
                if (!data) {
                    throw EvaluatorException("CURRENT.longshort.net: No data available for offset " + std::to_string(offset));
                }
                
                double total = data->long_ratio + data->short_ratio;
                if (std::abs(total) < 1e-10) {
                    return FunctionResult::fromValue(Value::fromNumber(0.0));
                }
                
                double net = (data->long_ratio - data->short_ratio) / total;
                return FunctionResult::fromValue(Value::fromNumber(net));
            }
            
            // ================================================================
            // dominance(offset) - 多头主导率 long_ratio / (long_ratio + short_ratio) * 100
            // ================================================================
            if (subfield == "dominance") {
                int offset = 0;
                if (!call.submethod_params.empty()) {
                    offset = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                }
                
                if (offset < -100 || offset > 0) {
                    throw EvaluatorException("CURRENT.longshort.dominance: offset must be between -100 and 0, got " + std::to_string(offset));
                }
                
                const LongShortRatioData* data = getLongShortRatioByOffset(series, offset, current_time);
                if (!data) {
                    throw EvaluatorException("CURRENT.longshort.dominance: No data available for offset " + std::to_string(offset));
                }
                
                double total = data->long_ratio + data->short_ratio;
                if (std::abs(total) < 1e-10) {
                    return FunctionResult::fromValue(Value::fromNumber(50.0));  // 默认50%
                }
                
                double dominance = (data->long_ratio / total) * 100.0;
                return FunctionResult::fromValue(Value::fromNumber(dominance));
            }
            
            // ================================================================
            // trend(period) - 趋势判断（基于移动平均或斜率）
            // ================================================================
            if (subfield == "trend") {
                int period = 7;  // 默认7个数据点
                if (!call.submethod_params.empty()) {
                    period = static_cast<int>(call.submethod_params[0]->evaluate(ctx).toNumber());
                }
                
                if (period < 2) {
                    throw EvaluatorException("CURRENT.longshort.trend: period must be >= 2");
                }
                
                int current_index = findLongShortRatioIndexByTime(series, current_time);
                if (current_index < 0) {
                    throw EvaluatorException("CURRENT.longshort.trend: No data available");
                }
                
                // 从当前索引开始，向前取period个数据点
                int count = std::min(period, static_cast<int>(series.size()) - current_index);
                if (count < 2) {
                    throw EvaluatorException("CURRENT.longshort.trend: Insufficient data for trend calculation");
                }
                
                std::vector<double> ratios;
                ratios.reserve(count);
                
                // 注意：series是降序（最新在前），需要从旧到新收集数据来计算趋势
                for (int i = count - 1; i >= 0; --i) {
                    int idx = current_index + i;
                    if (idx >= static_cast<int>(series.size())) {
                        break;
                    }
                    ratios.push_back(series[idx].ratio);
                }
                
                double slope = calculateSlopeDouble(ratios);
                std::string trend_direction = judgeTrend(slope, 0.1);  // 阈值0.1用于多空比趋势判断
                
                return FunctionResult::fromValue(Value::fromString(trend_direction));
            }
            
            // ================================================================
            // extreme(threshold) - 极端检测（ratio > threshold 或 < 1/threshold）
            // ================================================================
            if (subfield == "extreme") {
                double threshold = 2.0;  // 默认阈值2.0
                if (!call.submethod_params.empty()) {
                    threshold = call.submethod_params[0]->evaluate(ctx).toNumber();
                }
                
                if (threshold <= 0) {
                    throw EvaluatorException("CURRENT.longshort.extreme: threshold must be > 0");
                }
                
                const LongShortRatioData* data = getLongShortRatioByOffset(series, 0, current_time);
                if (!data) {
                    throw EvaluatorException("CURRENT.longshort.extreme: No data available");
                }
                
                bool is_extreme = (data->ratio > threshold) || (data->ratio < (1.0 / threshold));
                return FunctionResult::fromValue(Value::fromBoolean(is_extreme));
            }
            
            // 未知子字段
            throw EvaluatorException("CURRENT.longshort: Unknown subfield '" + subfield + "'. Available: ratio, long, short, net, dominance, trend, extreme");
        }
        
        // 未知字段
        throw EvaluatorException("CURRENT: Unknown field '" + field + "'. Available: price, timestamp, time, fundingrate, feargreed, longshort");
    });
}

} // namespace prophet::functions

