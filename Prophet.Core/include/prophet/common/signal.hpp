/*
 * ============================================================================
 * 文件名：signal.hpp
 * 功能说明：交易信号定义
 * 
 * 定义了交易信号Signal和K线形态PatternInfo
 * 
 * Signal结构：
 * - 策略引擎评估规则后生成的交易信号
 * - 包含交易动作、置信度、止损止盈等信息
 * - 返回给Python层进行交易执行
 * 
 * PatternInfo结构：
 * - K线形态识别结果
 * - 包含形态名称、方向、强度等信息
 * 
 * Signal字段说明：
 * - action：交易动作（BUY/SELL/HOLD）
 * - confidence：置信度（0.0-1.0）
 * - reason：触发原因（哪个规则触发的）
 * - stop_loss：建议止损价格
 * - take_profit：建议止盈价格
 * - entry_price：建议入场价格
 * - timestamp：信号生成时间
 * - rule_name：触发的规则名称
 * 
 * 这是策略引擎的最终输出
 * ============================================================================
 */

#pragma once

#include "types.hpp"
#include <string>
#include <cstdint>
#include <vector>
#include <unordered_map>
#include <sstream>
#include <random>

namespace prophet {

/**
 * K线形态信息
 * 由形态识别函数返回
 */
struct PatternInfo {
    std::string name;           // 形态名称（如"HAMMER"、"DOJI"）
    std::string side;           // 方向（BULLISH看涨/BEARISH看跌/NEUTRAL中性）
    int raw = 0;                // TA-Lib 原始值（-100到100）
    int score = 0;              // 自定义打分
    int rank = 0;               // 排名
    int64_t kline_open_time = 0;
    int64_t kline_close_time = 0;
    std::string description;    // 形态说明
};

/**
 * 指标快照信息
 * 用于记录条件触发时刻的指标上下文
 */
struct IndicatorSnapshot {
    std::string condition_id;                              // 条件ID（由条件节点toString生成）
    std::string label;                                      // 可读标签（基于节点类型自动生成）
    std::string timeframe;                                  // 时间框架
    std::string indicator;                                  // 指标名称
    std::unordered_map<std::string, Value> parameters;     // 参数快照
    int offset = 0;                                         // 偏移量（0=当前，-1=前一根）
    std::unordered_map<std::string, Value> ohlcv;          // OHLCV快照
    std::unordered_map<std::string, Value> results;        // 指标计算结果
    std::string comparison;                                 // 比较表达式（如"macd > signal"）
    std::string status;                                     // 状态："hit"（命中）或"miss"（未命中）
};

/**
 * 交易信号结构
 *
 * 由策略引擎生成，返回给 Python 层
 */
struct Signal {
    // 对齐 Python 实体字段
    std::string id;                         // 信号ID（默认生成）
    std::string strategy;                   // 策略名称
    std::string action;                     // "BUY" / "SELL" / "HOLD" (字符串，约定全字母大写)
    double confidence;                      // 置信度 (0.0 ~ 1.0)
    std::string reason;                     // 信号原因
    std::string trend;                      // NEUTRAL/BULLISH/BEARISH（默认 NEUTRAL）
    double sl;                              // 止损
    double tp;                              // 止盈
    
    // 原始数据结构（语言无关，各客户端自行序列化）
    std::unordered_map<std::string, Value> configs;     // 配置参数
    std::unordered_map<std::string, Value> indicators;  // 指标快照（已废弃，使用indicator_snapshots）
    std::vector<std::unordered_map<std::string, Value>> debug; // 调试信息
    std::vector<IndicatorSnapshot> indicator_snapshots; // 结构化指标快照（新增）
    
    // 🆕 v4.0: K线数据快照（用于校验K线对齐状态）
    // key: timeframe (如 "5m", "1h"), value: 当前K线数据
    std::unordered_map<std::string, Kline> klines;  // 每个时间框架的当前K线
    
    int64_t create_at;                      // 创建时间（epoch秒）

    // 兼容旧字段名（保留）：
    double stop_loss;                       // 兼容旧字段
    double take_profit;                     // 兼容旧字段
    int64_t timestamp;                      // 兼容旧字段

    // 构造函数
    Signal()
        : id("")
        , strategy("")
        , action("HOLD")  // 默认为HOLD字符串
        , confidence(0.0)
        , reason("")
        , trend("NEUTRAL")
        , sl(0.0)
        , tp(0.0)
        , create_at(0)
        , stop_loss(0.0)
        , take_profit(0.0)
        , timestamp(0)
    {}

    Signal(const std::string& act, double conf, const std::string& rsn = "")
        : id("")
        , strategy("")
        , action(act)  // 直接使用字符串
        , confidence(conf)
        , reason(rsn)
        , trend("NEUTRAL")
        , sl(0.0)
        , tp(0.0)
        , create_at(0)
        , stop_loss(0.0)
        , take_profit(0.0)
        , timestamp(0)
    {}

    // 转换为字符串（调试用）
    std::string toString() const {
        std::ostringstream oss;
        oss << action  // action已经是字符串了
            << " (confidence: " << confidence
            << ", reason: " << reason
            << ", trend: " << trend
            << ")";
        return oss.str();
    }

    // 判断是否为有效信号
    bool isValid() const {
        return action != "HOLD" && confidence > 0.0;
    }

    static std::string generateId() {
        // 简单ID生成：时间戳 + 随机数
        auto now = static_cast<uint64_t>(std::time(nullptr));
        static thread_local std::mt19937_64 rng{std::random_device{}()};
        uint64_t r = rng();
        std::ostringstream oss;
        oss << std::hex << now << r;
        return oss.str();
    }
};

} // namespace prophet
