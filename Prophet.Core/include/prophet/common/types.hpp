/*
 * ============================================================================
 * 文件名：types.hpp
 * 功能说明：核心类型定义
 * 
 * 这个文件定义了Prophet系统中使用的所有核心数据类型
 * 
 * 主要类型分类：
 * 
 * 1. 信号类型（SignalAction）：
 *    - BUY：买入信号
 *    - SELL：卖出信号
 *    - HOLD：持有信号
 * 
 * 2. 值类型（Value、ValueType）：
 *    - NUMBER：数字
 *    - STRING：字符串
 *    - BOOLEAN：布尔值
 *    - 用于DSL表达式求值
 * 
 * 3. K线数据（Kline）：
 *    - 蜡烛图数据：open、high、low、close、volume
 *    - 时间戳
 * 
 * 4. 指标结果（IndicatorResult）：
 *    - 存储技术指标的计算结果
 *    - 支持多字段（如MACD的value、signal、histogram）
 * 
 * 5. Token类型（TokenType）：
 *    - 词法分析器使用的Token类型枚举
 *    - 包括关键字、运算符、字面量等
 * 
 * 这些类型是整个系统的数据基础
 * ============================================================================
 */

#pragma once

#include <string>
#include <vector>
#include <unordered_map>
#include <memory>
#include <stdexcept>
#include <sstream>

namespace prophet {

// ============================================================================
// 信号类型
// ============================================================================

enum class SignalAction {
    BUY,    // 买入信号
    SELL,   // 卖出信号
    HOLD    // 持有信号（不交易）
};

// SignalAction 转字符串
inline std::string signalActionToString(SignalAction action) {
    switch (action) {
        case SignalAction::BUY:  return "BUY";
        case SignalAction::SELL: return "SELL";
        case SignalAction::HOLD: return "HOLD";
        default: return "UNKNOWN";
    }
}

// 字符串转 SignalAction
inline SignalAction stringToSignalAction(const std::string& str) {
    if (str == "BUY")  return SignalAction::BUY;
    if (str == "SELL") return SignalAction::SELL;
    if (str == "HOLD") return SignalAction::HOLD;
    throw std::invalid_argument("Invalid signal action: " + str);
}

// ============================================================================
// Token 类型（词法分析）
// ============================================================================

enum class TokenType {
    // 关键字 - 信号函数
    ALL,
    ANY,
    NONE,
    MIN,
    COUNT,
    MAX,
    VOTE,
    WEIGHTED,

    // CONSECUTIVE和CONSEC已重构为普通数据函数，不再是特殊Token

    // 关键字 - 二级/逻辑函数
    WEIGHT,
    NOT,
    AND,             // 逻辑与
    OR,              // 逻辑或

    // 关键字 - 自定义函数控制流
    IF,              // if
    ELSE,            // else
    RETURN,          // return

    // 关键字 - 信号类型
    BUY,
    SELL,
    HOLD,

    // 运算符
    EQ,              // =
    NE,              // !=
    GT,              // >
    LT,              // <
    GE,              // >=
    LE,              // <=
    PLUS,            // +
    MINUS,           // -
    MULTIPLY,        // *
    DIVIDE,          // /
    MODULO,          // %
    
    // 复合赋值运算符
    ADD_ASSIGN,      // +=
    SUB_ASSIGN,      // -=
    MUL_ASSIGN,      // *=
    DIV_ASSIGN,      // /=
    
    // 三元运算符
    QUESTION,        // ?
    COLON,           // :

    // 范围运算符
    BETWEEN,
    IN,
    NEAR,

    // 分隔符
    LPAREN,          // (
    RPAREN,          // )
    LBRACE,          // {
    RBRACE,          // }
    LBRACKET,        // [
    RBRACKET,        // ]
    COMMA,           // ,
    DOT,             // .
    SEMICOLON,       // ;

    // 字面量
    NUMBER,          // 123, 45.67
    STRING,          // "text"
    BOOLEAN,         // true, false
    IDENTIFIER,      // MACD, RSI, etc.

    // 特殊
    INDICATOR_REF_START, // $( 指标引用前缀（新语法）
    ENV_VAR,         // @CURRENT_PRICE（环境变量，全大写）
    USER_VAR,        // @myvar（用户变量，全小写）
    TYPE_NAME,       // Integer, Double, Boolean, String, Enum（类型名称）

    // 结束符
    END_OF_FILE
};

// ============================================================================
// 指标结果值类型
// ============================================================================

enum class ValueType {
    NUMBER,
    STRING,
    BOOLEAN,
    ENUM
};

// 通用值类型（variant-like）
struct Value {
    ValueType type;
    double number_value;
    std::string string_value;
    bool boolean_value;

    Value() : type(ValueType::NUMBER), number_value(0.0), boolean_value(false) {}

    static Value fromNumber(double val) {
        Value v;
        v.type = ValueType::NUMBER;
        v.number_value = val;
        return v;
    }

    static Value fromString(const std::string& val) {
        Value v;
        v.type = ValueType::STRING;
        v.string_value = val;
        return v;
    }

    static Value fromBoolean(bool val) {
        Value v;
        v.type = ValueType::BOOLEAN;
        v.boolean_value = val;
        return v;
    }

    // 类型检查方法（P2字节码优化）
    bool isNumber() const { return type == ValueType::NUMBER; }
    bool isBoolean() const { return type == ValueType::BOOLEAN; }
    bool isString() const { return type == ValueType::STRING; }
    
    // 转换为 bool（用于条件判断）
    bool toBool() const {
        switch (type) {
            case ValueType::BOOLEAN: return boolean_value;
            case ValueType::NUMBER:  return number_value != 0.0;
            case ValueType::STRING:  return !string_value.empty();
            default: return false;
        }
    }
    
    // 转换为 double
    double toNumber() const {
        switch (type) {
            case ValueType::NUMBER:  return number_value;
            case ValueType::BOOLEAN: return boolean_value ? 1.0 : 0.0;
            case ValueType::STRING:  return std::stod(string_value);
            default: return 0.0;
        }
    }
    
    // 转换为字符串
    std::string toString() const {
        switch (type) {
            case ValueType::STRING:  return string_value;
            case ValueType::NUMBER:  return std::to_string(number_value);
            case ValueType::BOOLEAN: return boolean_value ? "true" : "false";
            default: return "";
        }
    }
};

// ============================================================================
// 异常类型
// ============================================================================

class DSLException : public std::runtime_error {
public:
    int line;      // 行号（从1开始，0表示未知）
    int column;    // 列号（从1开始，0表示未知）
    
    explicit DSLException(const std::string& message, int line_num = 0, int col_num = 0)
        : std::runtime_error(message), line(line_num), column(col_num) {}
    
    // 格式化错误信息，包含位置信息
    std::string formatMessage() const {
        std::ostringstream oss;
        if (line > 0 && column > 0) {
            oss << "[第" << line << "行, 第" << column << "列] " << what();
        } else if (line > 0) {
            oss << "[第" << line << "行] " << what();
        } else {
            oss << what();
        }
        return oss.str();
    }
};

class LexerException : public DSLException {
public:
    explicit LexerException(const std::string& message, int line_num = 0, int col_num = 0)
        : DSLException("Lexer error: " + message, line_num, col_num) {}
};

class ParserException : public DSLException {
public:
    explicit ParserException(const std::string& message, int line_num = 0, int col_num = 0)
        : DSLException("Parser error: " + message, line_num, col_num) {}
};

class EvaluatorException : public DSLException {
public:
    explicit EvaluatorException(const std::string& message, int line_num = 0, int col_num = 0)
        : DSLException("Evaluator error: " + message, line_num, col_num) {}
};

// ============================================================================
// 指标数据结构
// ============================================================================

// 指标参数（用于传递指标计算参数）
class IndicatorParams {
private:
    std::unordered_map<std::string, int> int_params;
    std::unordered_map<std::string, double> double_params;
    std::unordered_map<std::string, std::string> string_params;

public:
    // 设置整数参数
    void set_int(const std::string& key, int value) {
        int_params[key] = value;
    }

    // 获取整数参数（带默认值）
    int get_int(const std::string& key, int default_value = 0) const {
        auto it = int_params.find(key);
        if (it == int_params.end()) {
            return default_value;
        }
        return it->second;
    }

    // 设置浮点数参数
    void set_double(const std::string& key, double value) {
        double_params[key] = value;
    }

    // 获取浮点数参数（带默认值）
    double get_double(const std::string& key, double default_value = 0.0) const {
        auto it = double_params.find(key);
        if (it == double_params.end()) {
            return default_value;
        }
        return it->second;
    }

    // 设置字符串参数
    void set_string(const std::string& key, const std::string& value) {
        string_params[key] = value;
    }

    // 获取字符串参数（带默认值）
    std::string get_string(const std::string& key, const std::string& default_value = "") const {
        auto it = string_params.find(key);
        if (it == string_params.end()) {
            return default_value;
        }
        return it->second;
    }

    // 检查参数是否存在
    bool has(const std::string& key) const {
        return int_params.find(key) != int_params.end() ||
               double_params.find(key) != double_params.end() ||
               string_params.find(key) != string_params.end();
    }

    // 清空所有参数
    void clear() {
        int_params.clear();
        double_params.clear();
        string_params.clear();
    }
};

// 指标计算结果（统一序列存储，支持历史值访问）
struct IndicatorResult {
    // 统一使用序列存储：字段名 -> 值序列（从旧到新，索引0为最旧，最后一个为最新）
    std::unordered_map<std::string, std::vector<Value>> field_series;

    /**
     * 获取字段值（支持偏移量）
     * @param field_name 字段名
     * @param offset 偏移量，0表示最新值，-1表示前一根K线，范围：-100到0
     * @return 字段值，如果索引越界返回NaN
     */
    Value get(const std::string& field_name, int offset = 0) const {
        auto it = field_series.find(field_name);
        if (it == field_series.end() || it->second.empty()) {
            throw EvaluatorException("Field not found: " + field_name);
        }
        
        const auto& series = it->second;
        // offset: 0=最新(最后一个), -1=前一根, -2=前两根...
        int index = static_cast<int>(series.size()) - 1 + offset;
        
        // 边界检查：offset范围 -100 到 0
        if (offset < -100 || offset > 0) {
            throw EvaluatorException("Offset out of range [-100, 0]: " + std::to_string(offset));
        }
        
        if (index < 0 || index >= static_cast<int>(series.size())) {
            // 索引越界，返回NaN
            return Value::fromNumber(std::nan(""));
        }
        
        return series[index];
    }

    /**
     * 检查字段是否存在
     */
    bool has(const std::string& field_name) const {
        auto it = field_series.find(field_name);
        return it != field_series.end() && !it->second.empty();
    }

    /**
     * 设置单个值（向后兼容，内部存储为单元素序列）
     * 注意：此方法主要用于向后兼容，新代码应使用 setSeries()
     */
    void set(const std::string& field_name, const Value& value) {
        field_series[field_name] = std::vector<Value>{value};
    }

    /**
     * 设置序列值（推荐使用）
     * @param field_name 字段名
     * @param series 值序列（从旧到新）
     */
    void setSeries(const std::string& field_name, const std::vector<Value>& series) {
        field_series[field_name] = series;
    }

    /**
     * 设置序列值（从double数组）
     * @param field_name 字段名
     * @param values double数组
     * @param size 数组大小
     */
    void setSeries(const std::string& field_name, const double* values, size_t size) {
        std::vector<Value> series;
        series.reserve(size);
        for (size_t i = 0; i < size; ++i) {
            series.push_back(Value::fromNumber(values[i]));
        }
        field_series[field_name] = std::move(series);
    }

    /**
     * 获取序列长度
     */
    size_t getSeriesSize(const std::string& field_name) const {
        auto it = field_series.find(field_name);
        if (it == field_series.end()) {
            return 0;
        }
        return it->second.size();
    }
};

// 时间框架 -> 指标结果
using TimeframeIndicators = std::unordered_map<std::string, IndicatorResult>;

// 指标名称 -> 时间框架指标
using IndicatorMap = std::unordered_map<std::string, TimeframeIndicators>;

// ============================================================================
// K线数据结构
// ============================================================================

/**
 * K线结构（蜡烛图数据）- 7字段标准格式
 * 
 * 字段说明：
 *   - OHLCV：开盘价、最高价、最低价、收盘价、成交量
 *   - open_time：K线开盘时间（UTC毫秒时间戳）
 *   - close_time：K线收盘时间（UTC毫秒时间戳）
 * 
 * 这是整个系统中K线数据的标准表示，设计遵循交易所标准
 */
struct Kline {
    double open;           // 开盘价
    double high;           // 最高价
    double low;            // 最低价
    double close;          // 收盘价
    double volume;         // 成交量
    int64_t open_time;     // 开盘时间（UTC毫秒）
    int64_t close_time;    // 收盘时间（UTC毫秒）
};

/**
 * 恐惧与贪婪指数数据结构
 * 
 * 存储单日的恐惧与贪婪指数数据
 * 数据来源：Alternative.me Crypto Fear & Greed Index
 * 
 * 字段说明：
 * - date_timestamp：日期时间戳（Unix时间戳，秒，UTC 00:00:00）
 * - value：指数值（0-100）
 *   - 0-24: Extreme Fear（极度恐慌）
 *   - 25-44: Fear（恐慌）
 *   - 45-55: Neutral（中性）
 *   - 56-75: Greed（贪婪）
 *   - 76-100: Extreme Greed（极度贪婪）
 * - classification：分类标签字符串
 */
struct FearGreedData {
    int64_t date_timestamp;
    int value;
    std::string classification;

    FearGreedData() : date_timestamp(0), value(0), classification("") {}

    FearGreedData(int64_t ts, int val, const std::string& cls)
        : date_timestamp(ts), value(val), classification(cls) {}
};

/**
 * 资金费率数据
 * 
 * 存储单次资金费率数据
 * 数据来源：Binance API + fundingrate 数据表
 * 
 * 字段说明：
 * - timestamp：时间戳（Unix时间戳，毫秒，UTC）
 * - value：资金费率值（浮点数，通常在 -0.01 到 0.01 之间）
 *   正值表示多头支付空头，负值表示空头支付多头
 * 
 * 更新频率：每8小时更新一次（00:00, 08:00, 16:00 UTC）
 */
struct FundingRateData {
    int64_t timestamp;
    double value;

    FundingRateData() : timestamp(0), value(0.0) {}

    FundingRateData(int64_t ts, double val)
        : timestamp(ts), value(val) {}
};

/**
 * 多空比数据
 * 
 * 存储单次多空比数据
 * 数据来源：Binance API + 多空比数据表
 * 
 * 字段说明：
 * - timestamp：时间戳（Unix时间戳，秒，UTC）
 * - long_ratio：多头比例（0~1）
 * - short_ratio：空头比例（0~1）
 * - ratio：多空比率（long/short）
 * 
 * 更新频率：根据周期（5m, 15m, 30m, 1h等）定期更新
 */
struct LongShortRatioData {
    int64_t timestamp;      // 时间戳（秒）
    double long_ratio;       // 多头比例（0~1）
    double short_ratio;      // 空头比例（0~1）
    double ratio;            // 多空比率（long/short）

    LongShortRatioData() : timestamp(0), long_ratio(0.0), short_ratio(0.0), ratio(0.0) {}

    LongShortRatioData(int64_t ts, double lr, double sr, double r)
        : timestamp(ts), long_ratio(lr), short_ratio(sr), ratio(r) {}
};

} // namespace prophet
