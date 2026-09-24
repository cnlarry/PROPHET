/*
 * ============================================================================
 * 文件名：engine.cpp
 * 功能说明：策略引擎核心实现
 * 
 * 简化后的职责：
 * 1. 加载和解析DSL策略规则
 * 2. 管理指标参数（全局状态）
 * 3. 接收K线数据并自动合成时间框架
 * 4. 根据当前市场数据评估策略规则，生成交易信号
 * ============================================================================
 */

#include "prophet/core/engine.hpp"
#include "prophet/dsl/lexer.hpp"
#include "prophet/dsl/parser.hpp"
#include "prophet/tools/kline_converter.hpp"
#include "prophet/logging/logger.hpp"
#include <algorithm>
#include <sstream>
#include <stdexcept>
#include <iostream>

namespace prophet::core {

// 初始化编译缓存静态实例
bytecode::CompilationCache Engine::compilation_cache_;

// ============================================================================
// 🆕 v4.0: 辅助函数：填充信号中的K线数据快照
// ============================================================================
void Engine::fillKlineDataInSignal(Signal& signal) const {
    for (const auto& tf : timeframes_) {
        if (context_.hasKlines(tf)) {
            const auto& klines = context_.getKlines(tf);
            if (!klines.empty()) {
                // 获取最新的一根K线（当前K线）
                signal.klines[tf] = klines.back();
            }
        }
    }
}

// ============================================================================
// 构造函数 - 接受DSL和参数配置
// ============================================================================
Engine::Engine(const std::string& dsl_code,
               const std::unordered_map<std::string, 
                    std::unordered_map<std::string, 
                        std::unordered_map<std::string, double>>>& params)
    : strategy_name_("")
    , symbol_("")
    , uses_feargreed_(false)
    , uses_fundingrate_(false)
    , bytecode_enabled_(true) // 启用字节码执行
    , jit_enabled_(true)  // 启用JIT优化
{
    // 1. 加载DSL策略
    loadRulesFromDSL(dsl_code);
    
    // 2. 设置初始参数（如果提供）
    // 这些参数会作为"全局状态"存储在Context中
    for (const auto& [indicator_name, tf_map] : params) {
        for (const auto& [timeframe, param_map] : tf_map) {
            for (const auto& [param_name, param_value] : param_map) {
                context_.setParameter(indicator_name, timeframe, param_name, 
                                     Value::fromNumber(param_value));
            }
        }
    }
}

// ============================================================================
// 函数：loadRulesFromDSL
// 功能：从DSL语言字符串加载和解析交易规则
// ============================================================================
void Engine::loadRulesFromDSL(const std::string& dsl_str) {
    // 词法分析：将文本拆分成Token
    dsl::Lexer lexer(dsl_str);
    std::vector<dsl::Token> tokens = lexer.tokenize();

    // 语法分析：构建抽象语法树
    dsl::Parser parser(tokens, dsl_str);
    auto statements = parser.parseStatements();
    
    // 🆕 v4.0: 移动函数注册表到Engine（用于运行时调用自定义函数）
    // 注意：FunctionRegistry禁止拷贝，但Parser是局部变量，需要移动
    function_registry_ = parser.move_function_registry();
    
    // 清空之前的规则和赋值
    rules_.clear();
    assignments_.clear();
    global_var_decls_.clear();        // 🆕 v4.1: 清空全局变量声明
    global_var_assigns_.clear();      // 🆕 v4.1: 清空全局变量赋值
    execution_order_.clear();         // 🆕 v4.1: 清空执行顺序
    
    // 从DSL中提取时间框架
    std::set<std::string> extracted_timeframes;
    
    // 🆕 v4.1: 遍历所有解析出来的语句，根据类型分别处理并记录执行顺序
    for (const auto& stmt : statements) {
        if (stmt.type == dsl::Parser::Statement::RULE) {
            // 信号规则
            size_t rule_index = rules_.size();
            rules_.push_back(stmt.rule);
            execution_order_.emplace_back(TopLevelStatement::RULE, rule_index);
            
        } else if (stmt.type == dsl::Parser::Statement::ASSIGNMENT) {
            // 参数赋值
            assignments_.push_back(stmt.assignment);
            // 注意：参数赋值在策略加载时执行，不参与每次信号生成的执行顺序
            
        } else if (stmt.type == dsl::Parser::Statement::VAR_DECL) {
            // 🆕 全局变量声明
            size_t var_index = global_var_decls_.size();
            global_var_decls_.push_back(stmt.var_decl);
            execution_order_.emplace_back(TopLevelStatement::VAR_DECL, var_index);
            
        } else if (stmt.type == dsl::Parser::Statement::VAR_ASSIGN) {
            // 🆕 全局变量赋值
            size_t assign_index = global_var_assigns_.size();
            global_var_assigns_.push_back(stmt.var_assign);
            execution_order_.emplace_back(TopLevelStatement::VAR_ASSIGN, assign_index);
        }
        // FUNCTION_DEF 已在 parser 中注册到 function_registry_，不需要额外处理
    }
    
    // 从DSL中自动提取时间框架（扫描所有 (Xm)、(Xh) 等模式）
    extractTimeframesFromDSL(dsl_str, extracted_timeframes);
    
    // 检测DSL是否使用了FEARGREED和FUNDINGRATE
    uses_feargreed_ = (dsl_str.find("FEARGREED") != std::string::npos) ||
                      (dsl_str.find("CURRENT().feargreed") != std::string::npos);
    uses_fundingrate_ = (dsl_str.find("FUNDINGRATE") != std::string::npos) || 
                        (dsl_str.find("CURRENT().fundingrate") != std::string::npos);
    
    // 将提取的时间框架存储到成员变量
    timeframes_.clear();
    timeframes_.assign(extracted_timeframes.begin(), extracted_timeframes.end());
    
    // 排序时间框架（按分钟数从小到大）
    std::sort(timeframes_.begin(), timeframes_.end(), 
              [](const std::string& a, const std::string& b) {
                  try {
                      int min_a = tools::kline::Converter::timeframeToMinutes(a);
                      int min_b = tools::kline::Converter::timeframeToMinutes(b);
                      return min_a < min_b;
                  } catch (...) {
                      return a < b;
                  }
              });
    
    // 字节码编译（逐规则独立编译：某条规则编译失败只回退该规则，不影响其他规则）
    if (bytecode_enabled_) {
        bytecode_.clear();
        bytecode_.reserve(rules_.size());
        
        bytecode::BytecodeCompiler compiler;
        
        for (size_t i = 0; i < rules_.size(); ++i) {
            const auto& rule = rules_[i];
            // 字节码路径不生成止盈止损（含 tp/sl 的规则直接走 AST，保证字段正确）
            if (rule.getTakeProfitExpr() != nullptr || rule.getStopLossExpr() != nullptr) {
                bytecode_.push_back({});
                continue;
            }
            try {
                // 将规则转换为字符串，用于缓存键
                std::string rule_str = rule.toString();
                
                // 尝试从缓存获取
                auto cached_instructions = compilation_cache_.get(rule_str);
                
                std::vector<bytecode::Instruction> instructions;
                
                if (!cached_instructions.empty()) {
                    // 缓存命中
                    instructions = std::move(cached_instructions);
                } else {
                    // 缓存未命中，进行编译
                    instructions = compiler.compile(rule.getSignalFunc());
                    
                    // 存入缓存
                    compilation_cache_.put(rule_str, instructions);
                }
                
                bytecode_.push_back(std::move(instructions));
            } catch (const std::exception& e) {
                std::cerr << "Warning: Bytecode compilation failed for rule " 
                          << i + 1 << ": " << e.what() 
                          << ". Falling back to AST evaluation for this rule.\n";
                // 空指令向量 = 该规则使用 AST 评估
                bytecode_.push_back({});
            }
        }
    }
    
    // JIT编译（与字节码逐规则对齐：字节码为空的规则不生成JIT函数）
    if (jit_enabled_ && bytecode_enabled_ && !bytecode_.empty()) {
        jit_funcs_.clear();
        jit_funcs_.reserve(bytecode_.size());
        
        for (const auto& code : bytecode_) {
            if (code.empty()) {
                jit_funcs_.push_back(nullptr);
                continue;
            }
            auto jit_func = jit_compiler_.compile(code);
            jit_funcs_.push_back(jit_func);
        }
    }
}

// ============================================================================
// 函数：extractTimeframesFromDSL
// 功能：从DSL字符串中提取时间框架
// ============================================================================
void Engine::extractTimeframesFromDSL(const std::string& dsl_str, 
                                      std::set<std::string>& timeframes) const {
    // 匹配形如 (5m)、(1h)、(4h)、(1d) 等的时间框架
    for (size_t i = 0; i < dsl_str.length(); ++i) {
        if (dsl_str[i] == '(') {
            size_t j = i + 1;
            std::string tf;
            
            // 提取数字部分
            while (j < dsl_str.length() && std::isdigit(dsl_str[j])) {
                tf += dsl_str[j];
                ++j;
            }
            
            // 提取单位部分（m/h/d/w）
            if (j < dsl_str.length() && !tf.empty()) {
                char unit = dsl_str[j];
                if (unit == 'm' || unit == 'h' || unit == 'd' || unit == 'w') {
                    tf += unit;
                    if (j + 1 < dsl_str.length() && dsl_str[j + 1] == ')') {
                        timeframes.insert(tf);
                    }
                }
            }
        }
    }
}

// ============================================================================
// 函数：set_klines / append_kline / append_klines
// 功能：v10.0 重构 - 移除K线合成逻辑，支持增量更新
// 
// 核心变更：
// 1. set_klines 必须指定时间框架，直接设置指定时间框架的数据
// 2. 移除自动合成逻辑（客户端负责准备所有时间框架的数据）
// 3. 新增 append_kline 用于高性能增量更新（回测循环）
// 4. 新增 append_klines 用于批量追加（处理跳跃场景）
// ============================================================================

void Engine::set_klines(
    const std::string& timeframe,
    const double* open,
    const double* high,
    const double* low,
    const double* close,
    const double* volume,
    const int64_t* open_time,
    const int64_t* close_time,
    size_t count
) {
    if (count == 0) {
        throw DSLException("Klines count cannot be zero");
    }

    if (open == nullptr || high == nullptr || low == nullptr || close == nullptr ||
        volume == nullptr || open_time == nullptr || close_time == nullptr) {
        throw DSLException("Klines data pointers must not be null");
    }
    
    if (timeframe.empty()) {
        throw DSLException("Timeframe must be specified");
    }
    
    // 验证时间框架格式
    try {
        tools::kline::Converter::timeframeToMinutes(timeframe);
    } catch (...) {
        throw DSLException("Invalid timeframe format: " + timeframe);
    }
    
    // 批量构造Kline对象
    std::vector<functions::Kline> klines;
    klines.reserve(count);
    
    for (size_t i = 0; i < count; ++i) {
        klines.push_back({
            open[i],
            high[i],
            low[i],
            close[i],
            volume[i],
            open_time[i],
            close_time[i]
        });
    }
    
    // 直接设置指定时间框架的K线（无任何转换逻辑）
    context_.setKlines(timeframe, klines);
}

void Engine::append_kline(
    const std::string& timeframe,
    double open,
    double high,
    double low,
    double close,
    double volume,
    int64_t open_time,
    int64_t close_time
) {
    functions::Kline kline{
        open, high, low, close, volume,
        open_time, close_time
    };
    
    context_.appendKline(timeframe, kline);
}

void Engine::append_klines(
    const std::string& timeframe,
    const double* open,
    const double* high,
    const double* low,
    const double* close,
    const double* volume,
    const int64_t* open_time,
    const int64_t* close_time,
    size_t count
) {
    if (count == 0) return;
    
    std::vector<functions::Kline> klines;
    klines.reserve(count);
    
    for (size_t i = 0; i < count; ++i) {
        klines.push_back({
            open[i], high[i], low[i], close[i], volume[i],
            open_time[i], close_time[i]
        });
    }
    
    context_.appendKlines(timeframe, klines);
}

// ============================================================================
// 函数：set_fear_greed_series
// 功能：设置恐惧与贪婪指数序列数据
// ============================================================================
void Engine::set_fear_greed_series(const std::vector<FearGreedData>& series) {
    context_.setFearGreedSeries(series);
}

// ============================================================================
// 函数：set_funding_rate_series
// 功能：设置资金费率序列数据
// ============================================================================
void Engine::set_funding_rate_series(const std::vector<FundingRateData>& series) {
    context_.setFundingRateSeries(series);
}

// ============================================================================
// 函数：set_long_short_ratio_series
// 功能：设置多空比序列数据
// ============================================================================
void Engine::set_long_short_ratio_series(const std::vector<LongShortRatioData>& series) {
    context_.setLongShortRatioSeries(series);
}

// ============================================================================
// 函数：get_signal
// 功能：根据当前市场数据评估所有规则，生成交易信号
// ============================================================================
Signal Engine::get_signal(double current_price, int64_t current_time) {
    // 所有调试输出已移除，改为使用日志系统（可从客户端控制）
    // 如需启用调试，请在客户端设置 LogService.SetLevel(LogLevel.DEBUG)
    
    // ========================================================================
    // 边界验证：输入参数检查
    // ========================================================================
    if (current_price <= 0.0) {
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[VALIDATION] Invalid current_price: " + std::to_string(current_price) + " (must be > 0)";
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time > 0 ? current_time : static_cast<int64_t>(std::time(nullptr));
        error_signal.timestamp = error_signal.create_at;
        return error_signal;
    }
    
    if (current_time <= 0) {
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[VALIDATION] Invalid current_time: " + std::to_string(current_time) + " (must be > 0)";
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = static_cast<int64_t>(std::time(nullptr));
        error_signal.timestamp = error_signal.create_at;
        return error_signal;
    }
    
    // ========================================================================
    // 异常捕获包装：整个信号生成过程
    // ========================================================================
    try {
        // 开始新的evaluate会话
        context_.beginSession();
    
    // 🆕 v4.0: 设置函数注册表指针（用于自定义函数调用）
    context_.setFunctionRegistry(&function_registry_);
    
    // 设置当前市场环境
    context_.setCurrentPrice(current_price);
    context_.setCurrentTime(current_time);
    
    // ========================================================================
    // 数据完整性检查：确保所有必需的数据都已加载
    // ========================================================================
    
    // 1. 检查时间框架K线数据
    for (const auto& tf : timeframes_) {
        if (!context_.hasKlines(tf)) {
            Signal error_signal;
            error_signal.id = Signal::generateId();
            error_signal.strategy = strategy_name_;
            error_signal.action = "HOLD";
            error_signal.confidence = 0.0;
            error_signal.reason = "[DATA_CHECK] Missing K-line data for timeframe: " + tf;
            error_signal.trend = "NEUTRAL";
            error_signal.create_at = current_time;
            error_signal.timestamp = current_time;
            fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
            context_.endSession();
            return error_signal;
        }
        
        // 检查K线数量（至少需要300根）
        size_t kline_count = context_.getKlineCount(tf);
        if (kline_count < 300) {
            Signal error_signal;
            error_signal.id = Signal::generateId();
            error_signal.strategy = strategy_name_;
            error_signal.action = "HOLD";
            error_signal.confidence = 0.0;
            error_signal.reason = "[DATA_CHECK] Insufficient K-line data for timeframe " + tf + 
                                ": got " + std::to_string(kline_count) + ", need at least 300";
            error_signal.trend = "NEUTRAL";
            error_signal.create_at = current_time;
            error_signal.timestamp = current_time;
            fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
            context_.endSession();
            return error_signal;
        }
        
        // ========================================================================
        // K线数据质量验证：检查价格逻辑、价格有效性、成交量有效性、时间戳有效性
        // ========================================================================
        const auto& klines = context_.getKlines(tf);
        for (size_t i = 0; i < klines.size(); ++i) {
            const auto& k = klines[i];
            std::string kline_info = "timeframe " + tf + ", index " + std::to_string(i);
            
            // 价格有效性检查：所有价格必须 > 0
            if (k.open <= 0.0 || k.high <= 0.0 || k.low <= 0.0 || k.close <= 0.0) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid price in K-line (" + kline_info + 
                                    "): open=" + std::to_string(k.open) + 
                                    ", high=" + std::to_string(k.high) + 
                                    ", low=" + std::to_string(k.low) + 
                                    ", close=" + std::to_string(k.close) + " (all must be > 0)";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
            
            // 价格逻辑检查：high >= low, high >= open, high >= close, low <= open, low <= close
            if (k.high < k.low) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid price logic in K-line (" + kline_info + 
                                    "): high (" + std::to_string(k.high) + ") < low (" + std::to_string(k.low) + ")";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
            if (k.high < k.open || k.high < k.close) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid price logic in K-line (" + kline_info + 
                                    "): high (" + std::to_string(k.high) + 
                                    ") < open (" + std::to_string(k.open) + 
                                    ") or close (" + std::to_string(k.close) + ")";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
            if (k.low > k.open || k.low > k.close) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid price logic in K-line (" + kline_info + 
                                    "): low (" + std::to_string(k.low) + 
                                    ") > open (" + std::to_string(k.open) + 
                                    ") or close (" + std::to_string(k.close) + ")";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                context_.endSession();
                return error_signal;
            }
            
            // 成交量有效性检查：volume >= 0
            if (k.volume < 0.0) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid volume in K-line (" + kline_info + 
                                    "): volume=" + std::to_string(k.volume) + " (must be >= 0)";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
            
            // 时间戳有效性检查：open_time > 0, close_time > open_time
            if (k.open_time <= 0) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid timestamp in K-line (" + kline_info + 
                                    "): open_time=" + std::to_string(k.open_time) + " (must be > 0)";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
            if (k.close_time <= k.open_time) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid timestamp in K-line (" + kline_info + 
                                    "): close_time (" + std::to_string(k.close_time) + 
                                    ") <= open_time (" + std::to_string(k.open_time) + ")";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                context_.endSession();
                return error_signal;
            }
        }
        
        // ========================================================================
        // K线时间戳连续性验证：检查时间戳是否按顺序排列，时间间隔是否合理
        // ========================================================================
        if (klines.size() > 1) {
            try {
                int timeframe_minutes = tools::kline::Converter::timeframeToMinutes(tf);
                int64_t expected_interval_ms = static_cast<int64_t>(timeframe_minutes) * 60 * 1000;
                
                for (size_t i = 1; i < klines.size(); ++i) {
                    // 检查时间戳是否按升序排列
                    if (klines[i].open_time < klines[i-1].open_time) {
                        Signal error_signal;
                        error_signal.id = Signal::generateId();
                        error_signal.strategy = strategy_name_;
                        error_signal.action = "HOLD";
                        error_signal.confidence = 0.0;
                        error_signal.reason = "[DATA_CHECK] K-line timestamps not in ascending order for " + tf + 
                                            ": index " + std::to_string(i-1) + " (" + std::to_string(klines[i-1].open_time) + 
                                            ") > index " + std::to_string(i) + " (" + std::to_string(klines[i].open_time) + ")";
                        error_signal.trend = "NEUTRAL";
                        error_signal.create_at = current_time;
                        error_signal.timestamp = current_time;
                        fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                        context_.endSession();
                        return error_signal;
                    }
                    
                    // 检查时间间隔是否合理（允许 ±10% 的误差）
                    int64_t actual_interval = klines[i].open_time - klines[i-1].open_time;
                    double interval_ratio = static_cast<double>(actual_interval) / static_cast<double>(expected_interval_ms);
                    
                    // 允许时间间隔在 0.9 到 1.1 倍之间（考虑数据缺失或合成误差）
                    if (interval_ratio < 0.9 || interval_ratio > 1.1) {
                        // 如果间隔过大（可能是数据缺失），只记录警告，不阻止执行
                        // 如果间隔过小（可能是重复数据），需要阻止
                        if (interval_ratio < 0.9) {
                            Signal error_signal;
                            error_signal.id = Signal::generateId();
                            error_signal.strategy = strategy_name_;
                            error_signal.action = "HOLD";
                            error_signal.confidence = 0.0;
                            error_signal.reason = "[DATA_CHECK] Suspicious K-line time interval for " + tf + 
                                                ": expected ~" + std::to_string(expected_interval_ms) + 
                                                "ms, got " + std::to_string(actual_interval) + 
                                                "ms (possible duplicate data)";
                            error_signal.trend = "NEUTRAL";
                            error_signal.create_at = current_time;
                            error_signal.timestamp = current_time;
                            fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                            context_.endSession();
                            return error_signal;
                        }
                    }
                }
            } catch (...) {
                // 如果时间框架解析失败，跳过时间戳连续性检查
                // 这不应该发生，但如果发生也不会阻止执行
            }
        }
    }
    
    // ========================================================================
    // 当前价格与最新K线一致性验证：检查当前价格与最新K线收盘价的偏差
    // 在滑动窗口回测中，只检查最小时间框架（主时间框架）的价格
    // 因为不同时间框架的K线可能不同步，导致价格偏差
    // ========================================================================
    if (!timeframes_.empty()) {
        // 只检查最小时间框架（主时间框架），timeframes_ 已按分钟数从小到大排序
        const std::string& min_timeframe = timeframes_[0];
        const auto& klines = context_.getKlines(min_timeframe);
        if (!klines.empty()) {
            double latest_close = klines.back().close;
            double price_diff_pct = std::abs(current_price - latest_close) / latest_close;
            
            // 如果偏差超过5%，可能是数据不同步
            if (price_diff_pct > 0.05) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Price mismatch for timeframe " + min_timeframe + 
                                    ": current_price=" + std::to_string(current_price) + 
                                    ", latest_close=" + std::to_string(latest_close) + 
                                    ", diff=" + std::to_string(price_diff_pct * 100) + "% (threshold: 5%)";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
        }
    }
    
    // 2. 检查资金费率数据（如果DSL使用了）
    if (uses_fundingrate_ && !context_.hasFundingRateData()) {
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[DATA_CHECK] Missing funding rate data. Strategy uses FUNDINGRATE or CURRENT().fundingrate";
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time;
        error_signal.timestamp = current_time;
        context_.endSession();
        return error_signal;
    }
    
    // ========================================================================
    // 资金费率数据有效性验证：检查资金费率值是否在合理范围内
    // ========================================================================
    if (uses_fundingrate_ && context_.hasFundingRateData()) {
        const auto& fr_series = context_.getFundingRateSeries();
        if (!fr_series.empty()) {
            const auto& fr_data = fr_series.back();  // 获取最新的资金费率数据
            // 资金费率通常在 -0.1 到 0.1 之间（±10%），超出此范围可能是数据异常
            if (fr_data.value < -0.1 || fr_data.value > 0.1) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid funding rate value: " + std::to_string(fr_data.value) + 
                                    " (expected range: [-0.1, 0.1])";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
        }
    }
    
    // 3. 检查恐惧与贪婪指数数据（如果DSL使用了）
    if (uses_feargreed_ && !context_.hasFearGreedData()) {
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[DATA_CHECK] Missing fear & greed index data. Strategy uses FEARGREED or CURRENT().feargreed";
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time;
        error_signal.timestamp = current_time;
        fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
        context_.endSession();
        return error_signal;
    }
    
    // ========================================================================
    // 恐惧与贪婪指数数据有效性验证：检查指数值是否在合理范围内
    // ========================================================================
    if (uses_feargreed_ && context_.hasFearGreedData()) {
        const auto& fg_series = context_.getFearGreedSeries();
        if (!fg_series.empty()) {
            const auto& fg_data = fg_series.back();  // 获取最新的恐惧与贪婪指数数据
            // 恐惧与贪婪指数应该在 0-100 范围内
            if (fg_data.value < 0 || fg_data.value > 100) {
                Signal error_signal;
                error_signal.id = Signal::generateId();
                error_signal.strategy = strategy_name_;
                error_signal.action = "HOLD";
                error_signal.confidence = 0.0;
                error_signal.reason = "[DATA_CHECK] Invalid fear & greed index value: " + std::to_string(fg_data.value) + 
                                    " (expected range: [0, 100])";
                error_signal.trend = "NEUTRAL";
                error_signal.create_at = current_time;
                error_signal.timestamp = current_time;
                fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
                context_.endSession();
                return error_signal;
            }
        }
    }
    
    // 执行所有参数赋值（DSL中的动态参数调整）
    std::vector<std::string> assignment_errors;
    for (const auto& assignment : assignments_) {
        try {
            assignment.execute(context_);
        } catch (const EvaluatorException& e) {
            assignment_errors.push_back(std::string("[ASSIGNMENT] ") + e.what());
            continue;
        } catch (const std::exception& e) {
            assignment_errors.push_back(std::string("[ASSIGNMENT] ") + e.what());
            continue;
        }
    }
    
    // 如果参数赋值有严重错误，返回HOLD信号
    if (!assignment_errors.empty() && assignments_.size() == assignment_errors.size()) {
        // 所有赋值都失败了，这是严重问题
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[VALIDATION] All parameter assignments failed. First error: " + assignment_errors[0];
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time;
        error_signal.timestamp = current_time;
        fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
        context_.endSession();
        return error_signal;
    }
    
    // ========================================================================
    // 🆕 v4.1: 清空全局变量（每次GetSignal前清空，确保干净状态）
    // ========================================================================
    context_.clearGlobalVariables();
    
    // ========================================================================
    // 🆕 v4.1: 按顺序执行全局变量声明/赋值和规则（支持短路评估）
    // ========================================================================
    Signal result_signal;
    bool signal_triggered = false;
    
    for (const auto& stmt : execution_order_) {
        try {
            if (stmt.type == TopLevelStatement::VAR_DECL) {
                // 🆕 执行全局变量声明
                const auto& var_decl = global_var_decls_[stmt.index];
                
                // 声明变量
                context_.declareGlobalVariable(var_decl->name);
                
                // 如果有初始化表达式，执行初始化
                if (var_decl->initializer) {
                    Value init_value = var_decl->initializer->evaluate(context_);
                    context_.setGlobalVariable(var_decl->name, init_value);
                }
                
            } else if (stmt.type == TopLevelStatement::VAR_ASSIGN) {
                // 🆕 执行全局变量赋值
                const auto& var_assign = global_var_assigns_[stmt.index];
                
                // 计算右侧表达式
                Value rhs_value = var_assign->value->evaluate(context_);
                
                // 处理复合赋值运算符
                if (var_assign->op != dsl::AssignmentOperator::ASSIGN) {
                    // 获取当前值
                    Value current_val = context_.getGlobalVariable(var_assign->variable_name);
                    double current_num = current_val.toNumber();
                    double rhs_num = rhs_value.toNumber();
                    
                    switch (var_assign->op) {
                        case dsl::AssignmentOperator::ADD_ASSIGN:
                            rhs_value = Value::fromNumber(current_num + rhs_num);
                            break;
                        case dsl::AssignmentOperator::SUB_ASSIGN:
                            rhs_value = Value::fromNumber(current_num - rhs_num);
                            break;
                        case dsl::AssignmentOperator::MUL_ASSIGN:
                            rhs_value = Value::fromNumber(current_num * rhs_num);
                            break;
                        case dsl::AssignmentOperator::DIV_ASSIGN:
                            if (rhs_num == 0.0) {
                                throw EvaluatorException("Division by zero in compound assignment");
                            }
                            rhs_value = Value::fromNumber(current_num / rhs_num);
                            break;
                        default:
                            break;
                    }
                }
                
                // 赋值
                context_.setGlobalVariable(var_assign->variable_name, rhs_value);
                
            } else if (stmt.type == TopLevelStatement::RULE) {
                // 🆕 执行信号规则（短路评估：一旦触发立即返回）
                if (signal_triggered) {
                    continue;  // 已经有信号触发，跳过后续规则
                }
                
                const auto& rule = rules_[stmt.index];
                
                // 评估规则
                Signal sig;
                bool use_fast_path = false;
                bool fast_path_result = false;
                size_t i = stmt.index;  // 规则索引
                
                // 三级评估：JIT → 字节码VM → AST
                // 注意：JIT 命中也必须设置 sig.action，否则会返回 action 为空的非法信号
                if (jit_enabled_ && i < jit_funcs_.size() && jit_funcs_[i]) {
                    fast_path_result = jit_funcs_[i](context_);
                    use_fast_path = true;
                    if (fast_path_result) {
                        sig.action = rule.getAction();
                        sig.confidence = 1.0;
                        try {
                            sig.reason = rule.getSignalFunc()->toString();
                        } catch (...) {
                            sig.reason = rule.getAction() + " signal triggered";
                        }
                    }
                } else if (bytecode_enabled_ && i < bytecode_.size() && !bytecode_[i].empty()) {
                    std::vector<IndicatorSnapshot> vm_snapshots;
                    Value result = vm_.executeWithSnapshots(bytecode_[i], context_, vm_snapshots);
                    fast_path_result = result.toBool();
                    use_fast_path = true;
                    
                    if (fast_path_result) {
                        sig.action = rule.getAction();
                        sig.confidence = 1.0;
                        sig.indicator_snapshots = std::move(vm_snapshots);
                        try {
                            sig.reason = rule.getSignalFunc()->toString();
                        } catch (...) {
                            sig.reason = rule.getAction() + " signal triggered";
                        }
                    }
                }
                
                if (use_fast_path) {
                    if (!fast_path_result) {
                        continue;  // 规则未触发，继续下一个
                    }
                } else {
                    sig = rule.evaluateRule(context_);
                }
                
                // 信号标准化
                if (sig.id.empty()) sig.id = Signal::generateId();
                if (sig.strategy.empty()) sig.strategy = strategy_name_;
                if (sig.trend.empty()) {
                    if (sig.action == "BUY") {
                        sig.trend = "BULLISH";
                    } else if (sig.action == "SELL") {
                        sig.trend = "BEARISH";
                    } else {
                        sig.trend = "NEUTRAL";
                    }
                }
                sig.create_at = current_time;
                sig.timestamp = current_time;
                
                // 🆕 v4.0: 填充K线数据快照（用于校验K线对齐状态）
                fillKlineDataInSignal(sig);
                
                // 🆕 信号触发，短路评估
                if (sig.action != "HOLD") {
                    result_signal = sig;
                    signal_triggered = true;
                    break;  // 立即返回，不再评估后续语句
                }
            }
            
        } catch (const EvaluatorException& e) {
            // 🆕 v4.1: 运行时错误（如使用未赋值变量）
            Signal error_signal;
            error_signal.id = Signal::generateId();
            error_signal.strategy = strategy_name_;
            error_signal.action = "HOLD";
            error_signal.confidence = 0.0;
            error_signal.reason = std::string("[RUNTIME_ERROR] ") + e.what();
            error_signal.trend = "NEUTRAL";
            error_signal.create_at = current_time;
            error_signal.timestamp = current_time;
            fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
            context_.endSession();
            return error_signal;
        } catch (const std::exception& e) {
            // 其他异常
            Signal error_signal;
            error_signal.id = Signal::generateId();
            error_signal.strategy = strategy_name_;
            error_signal.action = "HOLD";
            error_signal.confidence = 0.0;
            error_signal.reason = std::string("[ERROR] ") + e.what();
            error_signal.trend = "NEUTRAL";
            error_signal.create_at = current_time;
            error_signal.timestamp = current_time;
            fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
            context_.endSession();
            return error_signal;
        }
    }
    
    // 🆕 如果没有信号触发，返回默认HOLD信号
    if (!signal_triggered) {
        result_signal.id = Signal::generateId();
        result_signal.strategy = strategy_name_;
        result_signal.action = "HOLD";
        result_signal.confidence = 0.0;
        result_signal.reason = "No rules triggered";
        result_signal.trend = "NEUTRAL";
        result_signal.create_at = current_time;
        result_signal.timestamp = current_time;
        
        // 🆕 v4.0: 填充K线数据快照（即使没有触发信号，也记录K线数据）
        fillKlineDataInSignal(result_signal);
    }
    
    // 结束会话
    context_.endSession();
    return result_signal;
    
    } catch (const EvaluatorException& e) {
        // DSL评估异常：返回HOLD信号，reason包含错误信息
        context_.endSession();
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[EVALUATOR] " + std::string(e.what());
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time;
        error_signal.timestamp = current_time;
        fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
        return error_signal;
    } catch (const DSLException& e) {
        // DSL相关异常：返回HOLD信号
        context_.endSession();
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[DSL] " + std::string(e.what());
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time;
        error_signal.timestamp = current_time;
        fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
        return error_signal;
    } catch (const std::exception& e) {
        // 通用异常：返回HOLD信号
        context_.endSession();
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[EXCEPTION] " + std::string(e.what());
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time;
        error_signal.timestamp = current_time;
        fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
        return error_signal;
    } catch (...) {
        // 未知异常：返回HOLD信号
        context_.endSession();
        Signal error_signal;
        error_signal.id = Signal::generateId();
        error_signal.strategy = strategy_name_;
        error_signal.action = "HOLD";
        error_signal.confidence = 0.0;
        error_signal.reason = "[UNKNOWN] Unexpected error occurred";
        error_signal.trend = "NEUTRAL";
        error_signal.create_at = current_time;
        error_signal.timestamp = current_time;
        fillKlineDataInSignal(error_signal);  // 🆕 v4.0: 填充K线数据
        return error_signal;
    }
}

// ============================================================================
// 函数：selectBestSignal
// 功能：从多个候选信号中选择最佳的一个（选择置信度最高的）
// ============================================================================
Signal Engine::selectBestSignal(const std::vector<Signal>& signals) {
    if (signals.empty()) {
        return Signal("HOLD", 0.0, "No signals");
    }

    auto best_it = std::max_element(signals.begin(), signals.end(),
        [](const Signal& a, const Signal& b) {
            return a.confidence < b.confidence;
        });

    return *best_it;
}

// ============================================================================
// 函数：get_rules_string
// 功能：获取所有规则的字符串表示
// ============================================================================
std::string Engine::get_rules_string() const {
    std::ostringstream oss;
    
    for (size_t i = 0; i < rules_.size(); i++) {
        if (i > 0) oss << "; ";
        oss << rules_[i].toString();
    }
    
    return oss.str();
}

// ============================================================================
// 函数：analyzeStrategyComplexity
// 功能：分析策略复杂度，决定是否启用会话缓存
// ============================================================================
bool Engine::analyzeStrategyComplexity() const {
    if (rules_.size() >= 3) {
        return true;
    }
    
    if (timeframes_.size() >= 3) {
        return true;
    }
    
    return false;
}

// ============================================================================
// 函数：clone
// 功能：克隆引擎实例（用于多线程并行回测）
// ============================================================================
std::unique_ptr<Engine> Engine::clone() const {
    // 创建空引擎（不调用构造函数）
    auto cloned = std::make_unique<Engine>("", std::unordered_map<std::string,
        std::unordered_map<std::string, std::unordered_map<std::string, double>>>());
    
    // 共享只读数据
    cloned->strategy_name_ = strategy_name_;
    cloned->symbol_ = symbol_;
    cloned->timeframes_ = timeframes_;
    cloned->rules_ = rules_;
    cloned->assignments_ = assignments_;
    // 全局变量/执行顺序/函数注册表也必须拷贝，否则含 @var 或自定义函数的
    // 策略在克隆引擎上恒返回 HOLD 或报函数未注册（AST 节点 parse 后不可变，浅拷贝安全）
    cloned->global_var_decls_ = global_var_decls_;
    cloned->global_var_assigns_ = global_var_assigns_;
    cloned->execution_order_ = execution_order_;
    // FunctionRegistry 禁止拷贝：逐个重注册（定义节点为 shared_ptr，浅拷贝安全）
    for (const auto& name : function_registry_.list_functions()) {
        auto def = function_registry_.get_function(name);
        if (def) {
            cloned->function_registry_.register_function(name, def);
        }
    }
    cloned->bytecode_enabled_ = bytecode_enabled_;
    cloned->bytecode_ = bytecode_;
    cloned->jit_enabled_ = jit_enabled_;
    cloned->jit_funcs_ = jit_funcs_;
    
    // Context和K线数据由调用方独立设置
    
    return cloned;
}

} // namespace prophet::core