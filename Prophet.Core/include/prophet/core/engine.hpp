/*
 * ============================================================================
 * 文件名：engine.hpp
 * 功能说明：策略引擎头文件
 * 
 * 这是Prophet交易系统的核心引擎类定义
 * 
 * Engine类的职责：
 * 1. 加载和管理交易策略配置
 * 2. 解析DSL规则生成抽象语法树
 * 3. 管理策略运行时数据（指标、参数、K线等）
 * 4. 评估策略规则并生成交易信号
 * 
 * 新的简化使用流程：
 *   // 1. 构造时提供DSL和参数
 *   Engine engine(dsl_code, params);
 *   
 *   // 2. 循环调用
 *   while (trading) {
 *       engine.set_klines(...);  // 更新K线
 *       signal = engine.get_signal(price, time, env_data);  // 获取信号
 *   }
 * ============================================================================
 */

#pragma once

#include "../common/signal.hpp"
#include "../dsl/ast.hpp"
#include "../dsl/custom_function_ast.hpp"  // 🆕 v4.1: 支持全局变量节点
#include "../dsl/function_registry.hpp"     // 🆕 v4.0: 支持自定义函数注册表
#include "context.hpp"
#include "../functions/Pattern.hpp"
#include "../bytecode/compiler.hpp"
#include "../bytecode/vm.hpp"
#include "../bytecode/jit.hpp"
#include "../bytecode/compiler_cache.hpp"
#include <string>
#include <vector>
#include <unordered_map>
#include <set>

namespace prophet::core {

/**
 * 策略执行引擎
 *
 * 核心功能：
 * 1. 解析 DSL 规则
 * 2. 管理指标参数（全局状态）
 * 3. 接收K线数据
 * 4. 生成交易信号
 */
class Engine {
public:
    /**
     * 构造函数 - 接受DSL和参数配置
     * 
     * @param dsl_code DSL策略代码
     * @param params 指标参数配置（可选）
     *               格式：{"指标名": {"时间框架": {"参数名": 参数值}}}
     *               例如：{"MACD": {"5m": {"FAST_PERIOD": 12.0, "SLOW_PERIOD": 26.0}}}
     * 
     * 示例：
     *   Engine engine(
     *       "ALL{$(5m).MACD().trend = BULLISH} = BUY;",
     *       {{"MACD", {{"5m", {{"FAST_PERIOD", 12.0}, {"SLOW_PERIOD", 26.0}}}}}}
     *   );
     */
    Engine(const std::string& dsl_code,
           const std::unordered_map<std::string, 
                std::unordered_map<std::string, 
                    std::unordered_map<std::string, double>>>& params = {});
    
    // 显式允许移动语义
    Engine(Engine&&) = default;
    Engine& operator=(Engine&&) = default;
    
    // 禁止拷贝
    Engine(const Engine&) = delete;
    Engine& operator=(const Engine&) = delete;

    /**
     * v10.0 设置K线数据（必须指定时间框架）
     * 
     * 用于初始化时设置完整的300根K线窗口
     * 注意：如果传入超过300根，只保留最后300根
     * 
     * @param timeframe   时间框架（如"5m", "1h", "1d"）- 必需参数
     * @param open        开盘价数组
     * @param high        最高价数组
     * @param low         最低价数组
     * @param close       收盘价数组
     * @param volume      成交量数组
     * @param open_time   开盘时间数组（UTC毫秒）
     * @param close_time  收盘时间数组（UTC毫秒）
     * @param count       K线数量（建议300根）
     * 
     * 性能：
     *   - 支持NumPy零拷贝传递
     *   - 相比传统对象列表方式提升5-10倍
     * 
     * 示例（Python）：
     *   import pandas as pd
     *   # 加载 5m 数据
     *   df_5m = load_klines('BTCUSDT', '5m', start_date, end_date)
     *   engine.set_klines(
     *       "5m",  # 明确指定时间框架
     *       df_5m['open'].values,
     *       df_5m['high'].values,
     *       df_5m['low'].values,
     *       df_5m['close'].values,
     *       df_5m['volume'].values,
     *       df_5m['open_time'].values,
     *       df_5m['close_time'].values
     *   )
     *   
     *   # 加载 1h 数据
     *   df_1h = load_klines('BTCUSDT', '1h', start_date, end_date)
     *   engine.set_klines("1h", ...)
     */
    void set_klines(
        const std::string& timeframe,  // v10.0: 新增，必需参数
        const double* open,
        const double* high,
        const double* low,
        const double* close,
        const double* volume,
        const int64_t* open_time,
        const int64_t* close_time,
        size_t count
    );
    
    /**
     * v10.0 增量追加单根K线（高性能，回测循环使用）
     * 
     * 自动维护300根窗口：追加新K线，丢弃最旧的
     * 注意：必须先调用 set_klines() 初始化窗口
     * 
     * @param timeframe   时间框架
     * @param open        开盘价
     * @param high        最高价
     * @param low         最低价
     * @param close       收盘价
     * @param volume      成交量
     * @param open_time   开盘时间（UTC毫秒）
     * @param close_time  收盘时间（UTC毫秒）
     * 
     * 性能优势：
     *   - 相比全量设置快300倍
     *   - 内存分配减少99.7%
     * 
     * 示例（回测循环）：
     *   for (int i = 300; i < total_candles; i++) {
     *       // 增量追加新K线
     *       engine.append_kline(
     *           "5m",
     *           candles[i].open,
     *           candles[i].high,
     *           candles[i].low,
     *           candles[i].close,
     *           candles[i].volume,
     *           candles[i].open_time,
     *           candles[i].close_time
     *       );
     *       
     *       // 获取信号
     *       auto signal = engine.get_signal(candles[i].close, candles[i].time);
     *   }
     */
    void append_kline(
        const std::string& timeframe,
        double open,
        double high,
        double low,
        double close,
        double volume,
        int64_t open_time,
        int64_t close_time
    );
    
    /**
     * v10.0 增量追加多根K线（批量追加，处理跳跃场景）
     * 
     * @param timeframe   时间框架
     * @param open        开盘价数组
     * @param high        最高价数组
     * @param low         最低价数组
     * @param close       收盘价数组
     * @param volume      成交量数组
     * @param open_time   开盘时间数组（UTC毫秒）
     * @param close_time  收盘时间数组（UTC毫秒）
     * @param count       K线数量
     * 
     * 使用场景：
     *   - 处理时间跳跃（例如跳过5根，批量追加）
     *   - 其他时间框架的批量更新
     */
    void append_klines(
        const std::string& timeframe,
        const double* open,
        const double* high,
        const double* low,
        const double* close,
        const double* volume,
        const int64_t* open_time,
        const int64_t* close_time,
        size_t count
    );

    /**
     * 设置恐惧与贪婪指数序列数据
     * 
     * 这个方法用于将恐惧与贪婪指数数据传递给核心引擎，
     * 数据将存储在Context中，供FEARGREED()函数使用。
     * 
     * @param series 恐惧与贪婪指数数据序列（按日期降序排序，最新的在前）
     * 
     * 示例（Python）：
     *   import pandas as pd
     *   df = pd.read_sql(
     *       "SELECT date, value, classification FROM feargreed ORDER BY date DESC LIMIT 365",
     *       con
     *   )
     *   date_ts = (df['date'].astype('int64') // 10**9).values
     *   engine.set_fear_greed_series(
     *       date_ts,
     *       df['value'].values,
     *       df['classification'].tolist()
     *   )
     */
    void set_fear_greed_series(const std::vector<FearGreedData>& series);

    /**
     * 设置资金费率序列数据
     * 
     * 这个方法用于将资金费率数据传递给核心引擎，
     * 数据将存储在Context中，供FUNDINGRATE()函数使用。
     * 
     * @param series 资金费率数据序列（按时间戳降序排序，最新的在前）
     * 
     * 示例（Python）：
     *   import pandas as pd
     *   df = pd.read_sql(
     *       "SELECT timestamp, value FROM fundingrate ORDER BY timestamp DESC LIMIT 720",
     *       con
     *   )
     *   timestamps = df['timestamp'].astype('int64').values
     *   values = df['value'].astype('float64').values
     *   engine.set_funding_rate_series(timestamps, values)
     */
    void set_funding_rate_series(const std::vector<FundingRateData>& series);

    /**
     * 设置多空比序列数据
     * 
     * 这个方法用于将多空比数据传递给核心引擎，
     * 数据将存储在Context中，供CURRENT().longshort函数组使用。
     * 
     * @param series 多空比数据序列（按时间戳降序排序，最新的在前）
     * 
     * 示例（Python）：
     *   import pandas as pd
     *   df = pd.read_sql(
     *       "SELECT timestamp, long_ratio, short_ratio, ratio FROM longshortratio ORDER BY timestamp DESC LIMIT 100",
     *       con
     *   )
     *   timestamps = (df['timestamp'].astype('int64') // 1000).values  # 转换为秒
     *   long_ratios = df['long_ratio'].astype('float64').values
     *   short_ratios = df['short_ratio'].astype('float64').values
     *   ratios = df['ratio'].astype('float64').values
     *   engine.set_long_short_ratio_series(timestamps, long_ratios, short_ratios, ratios)
     */
    void set_long_short_ratio_series(const std::vector<LongShortRatioData>& series);

    /**
     * 生成交易信号
     *
     * @param current_price 当前价格
     * @param current_time 当前时间戳
     * @return Signal 对象（包含 action 和 confidence）
     */
    Signal get_signal(double current_price, int64_t current_time);

    /**
     * 获取规则数量（调试用）
     */
    size_t get_rule_count() const { return rules_.size(); }

    /**
     * 获取规则字符串（调试用）
     */
    std::string get_rules_string() const;
    
    /**
     * 克隆引擎实例（用于多线程并行回测）
     * 
     * 共享部分（只读）：规则AST/字节码/JIT
     * 独立部分：Context、K线数据
     * 
     * @return 新的Engine实例
     */
    std::unique_ptr<Engine> clone() const;
    
    /**
     * 启用或禁用字节码执行
     * 
     * @param enabled 是否启用字节码
     */
    void setBytecodeEnabled(bool enabled) {
        bytecode_enabled_ = enabled;
    }
    
    /**
     * 检查字节码是否启用
     * 
     * @return 字节码启用状态
     */
    bool isBytecodeEnabled() const {
        return bytecode_enabled_;
    }
    
    /**
     * 启用或禁用JIT优化
     * 
     * @param enabled 是否启用JIT
     */
    void setJITEnabled(bool enabled) {
        jit_enabled_ = enabled;
    }
    
    /**
     * 检查JIT是否启用
     * 
     * @return JIT启用状态
     */
    bool isJITEnabled() const {
        return jit_enabled_;
    }

private:
    /**
     * 加载并解析 DSL 规则字符串
     */
    void loadRulesFromDSL(const std::string& dsl_str);

    /**
     * 从DSL字符串中提取时间框架（模式匹配）
     */
    void extractTimeframesFromDSL(const std::string& dsl_str,
                                  std::set<std::string>& timeframes) const;

    /**
     * 从多个候选信号中选择最佳信号
     */
    Signal selectBestSignal(const std::vector<Signal>& signals);
    
    /**
     * 分析策略复杂度，决定是否启用会话缓存
     */
    bool analyzeStrategyComplexity() const;

private:
    // DSL 规则
    std::vector<dsl::RuleNode> rules_;
    
    // 参数赋值语句（DSL中的动态参数设置）
    std::vector<dsl::AssignmentStatement> assignments_;
    
    // 🆕 v4.1: 全局变量声明和赋值
    std::vector<std::shared_ptr<dsl::VariableDeclarationNode>> global_var_decls_;
    std::vector<std::shared_ptr<dsl::AssignmentStatementNode>> global_var_assigns_;
    
    // 🆕 v4.1: 顶层语句执行顺序（用于短路评估）
    struct TopLevelStatement {
        enum Type { VAR_DECL, VAR_ASSIGN, RULE };
        Type type;
        size_t index;  // 在对应数组中的索引
        
        TopLevelStatement(Type t, size_t idx) : type(t), index(idx) {}
    };
    std::vector<TopLevelStatement> execution_order_;

    // 执行上下文（包含指标缓存、参数状态、K线数据等）
    dsl::Context context_;

    // 🆕 v4.0: 自定义函数注册表（用于运行时调用自定义函数）
    dsl::FunctionRegistry function_registry_;
    
    // 🆕 v4.0: 辅助函数：填充信号中的K线数据快照
    void fillKlineDataInSignal(Signal& signal) const;

    // 策略元数据
    std::string strategy_name_;
    std::string symbol_;
    std::vector<std::string> timeframes_;
    
    // DSL功能使用标志（用于数据检查）
    bool uses_feargreed_;
    bool uses_fundingrate_;
    
    // 字节码编译支持
    bool bytecode_enabled_;
    std::vector<std::vector<bytecode::Instruction>> bytecode_;
    bytecode::BytecodeVM vm_;
    
    // JIT编译支持
    bool jit_enabled_;
    std::vector<bytecode::JITFunc> jit_funcs_;
    bytecode::JITCompiler jit_compiler_;
    
    // 编译缓存（静态实例，共享使用）
    static bytecode::CompilationCache compilation_cache_;
};

} // namespace prophet::core
