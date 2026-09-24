/*
 * ============================================================================
 * 文件名：ast.hpp
 * 功能说明：抽象语法树（AST）节点定义
 * 
 * 这个文件定义了DSL的抽象语法树的所有节点类型
 * 
 * 什么是AST？
 * - Abstract Syntax Tree（抽象语法树）
 * - 是源代码的树状结构表示
 * - 每个节点代表代码中的一个构造
 * - 可以被执行（evaluate）生成结果
 * 
 * 节点类型分类：
 * 1. 字面量节点：NumberNode、StringNode、BooleanNode
 * 2. 引用节点：IndicatorRefNode、ParamRefNode、EnvVarRefNode
 * 3. 运算符节点：BinaryOpNode、UnaryOpNode
 * 4. 信号函数节点：AllNode、AnyNode、NoneNode、MinNode等
 * 5. 加权节点：WeightedNode、WeightConditionNode
 * 6. 函数调用节点：FunctionCallNode、BuiltinFunctionNode
 * 7. 范围节点：BetweenNode、InNode
 * 8. 规则节点：RuleNode
 * 9. 赋值节点：AssignmentNode
 * 
 * 所有节点都继承自ASTNode基类，实现evaluate()和toString()方法
 * ============================================================================
 */

#pragma once

#include "../common/types.hpp"
#include "../common/signal.hpp"
#include <vector>
#include <memory>
#include <string>

namespace prophet {
namespace dsl {

// 前向声明
class Context;

/**
 * AST 节点基类
 * 所有AST节点都继承这个类
 */
class ASTNode {
public:
    virtual ~ASTNode() = default;

    // 求值（返回 Value）
    virtual Value evaluate(const Context& ctx) const = 0;

    // 转换为字符串（调试用）
    virtual std::string toString() const = 0;
};

using ASTNodePtr = std::shared_ptr<ASTNode>;

// ============================================================================
// 字面量节点
// ============================================================================

class NumberNode : public ASTNode {
public:
    explicit NumberNode(double value) : value_(value) {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;
    
    // 访问器（用于字节码编译器）
    double getValue() const { return value_; }

private:
    double value_;
};

class StringNode : public ASTNode {
public:
    explicit StringNode(const std::string& value) : value_(value) {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;
    
    // 访问器（用于字节码编译器）
    const std::string& getValue() const { return value_; }

private:
    std::string value_;
};

class BooleanNode : public ASTNode {
public:
    explicit BooleanNode(bool value) : value_(value) {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;
    
    // 访问器（用于字节码编译器）
    bool getValue() const { return value_; }

private:
    bool value_;
};

// ============================================================================
// 引用节点
// ============================================================================

// 指标引用：MACD(5m).histogram 或 KLINE(5m).close(0) 或 HT(5m).PHASOR.inphase
// 支持多时间框架：MACD[5m, 15m, 1h].histogram
// 新语法：$(timeframe).INDICATOR(params).field
class IndicatorRefNode : public ASTNode {
public:
    // 构造函数：支持多时间框架（旧语法）
    IndicatorRefNode(const std::string& indicator,
                     const std::vector<std::string>& timeframes,
                     const std::string& field,
                     const std::vector<ASTNodePtr>& field_params = {},
                     const std::string& subfield = "",
                     const std::vector<Value>& indicator_params = {},
                     const std::vector<ASTNodePtr>& submethod_params = {})
        : indicator_(indicator)
        , timeframes_(timeframes)
        , field_(field)
        , field_params_(field_params)
        , subfield_(subfield)
        , indicator_params_(indicator_params)
        , submethod_params_(submethod_params)
    {}
    
    // 便捷构造函数：单时间框架（兼容性）
    IndicatorRefNode(const std::string& indicator,
                     const std::string& timeframe,
                     const std::string& field,
                     const std::vector<ASTNodePtr>& field_params = {},
                     const std::string& subfield = "",
                     const std::vector<Value>& indicator_params = {},
                     const std::vector<ASTNodePtr>& submethod_params = {})
        : IndicatorRefNode(indicator, std::vector<std::string>{timeframe}, 
                          field, field_params, subfield, indicator_params, submethod_params)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;
    
    // 辅助方法
    bool hasMultipleTimeframes() const { return timeframes_.size() > 1; }
    const std::vector<std::string>& getTimeframes() const { return timeframes_; }
    const std::string& getSingleTimeframe() const { return timeframes_[0]; }
    const std::string& getIndicator() const { return indicator_; }
    const std::string& getField() const { return field_; }
    const std::vector<ASTNodePtr>& getFieldParams() const { return field_params_; }
    const std::string& getSubfield() const { return subfield_; }
    const std::vector<ASTNodePtr>& getSubmethodParams() const { return submethod_params_; }
    bool hasSubmethodParams() const { return !submethod_params_.empty(); }
    const std::vector<Value>& getIndicatorParams() const { return indicator_params_; }
    bool hasIndicatorParams() const { return !indicator_params_.empty(); }

private:
    std::string indicator_;
    std::vector<std::string> timeframes_;  // 支持多时间框架
    std::string field_;
    std::vector<ASTNodePtr> field_params_;  // 字段参数，例如 KLINE(5m).close(0) 中的 0
    std::string subfield_;  // 子字段/子方法名，例如 HT(5m).PHASOR.inphase 中的 "inphase"，或 SWING(5m).high().value 中的 "value"
    std::vector<ASTNodePtr> submethod_params_;  // 子方法参数，例如 SWING(5m).high().value(0) 中的 [0]
    std::vector<Value> indicator_params_;  // 指标参数（新语法），例如 MACD(12,26,9) 中的 [12,26,9]
};

// 参数引用：#.RSI(5m).OVERBOUGHT
class ParamRefNode : public ASTNode {
public:
    ParamRefNode(const std::string& indicator,
                 const std::string& timeframe,
                 const std::string& param_name)
        : indicator_(indicator)
        , timeframe_(timeframe)
        , param_name_(param_name)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;

private:
    std::string indicator_;
    std::string timeframe_;
    std::string param_name_;
};

// ============================================================================
// 运算符节点
// ============================================================================

// 二元运算符
class BinaryOpNode : public ASTNode {
public:
    enum class OpType {
        ADD, SUB, MUL, DIV, MOD,    // 算术运算符
        EQ, NE, GT, LT, GE, LE,     // 比较运算符
        AND, OR                      // 逻辑运算符
    };

    BinaryOpNode(ASTNodePtr left, OpType op, ASTNodePtr right)
        : left_(left), op_(op), right_(right)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;
    
    // 访问器（用于字节码编译器）
    const ASTNodePtr& getLeft() const { return left_; }
    const ASTNodePtr& getRight() const { return right_; }
    OpType getOp() const { return op_; }

private:
    ASTNodePtr left_;
    OpType op_;
    ASTNodePtr right_;
};

// 一元运算符（负号）
class UnaryOpNode : public ASTNode {
public:
    enum class OpType { NEG, POS };

    UnaryOpNode(OpType op, ASTNodePtr operand)
        : op_(op), operand_(operand)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;
    
    // 访问器（用于字节码编译器）
    OpType getOp() const { return op_; }
    const ASTNodePtr& getOperand() const { return operand_; }

private:
    OpType op_;
    ASTNodePtr operand_;
};

// ============================================================================
// 范围运算符节点
// ============================================================================

// BETWEEN(min, max)
class BetweenNode : public ASTNode {
public:
    BetweenNode(ASTNodePtr value, ASTNodePtr min, ASTNodePtr max, bool negate = false)
        : value_(value), min_(min), max_(max), negate_(negate)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;

private:
    ASTNodePtr value_;
    ASTNodePtr min_;
    ASTNodePtr max_;
    bool negate_;
};

// IN(val1, val2, ...)
class InNode : public ASTNode {
public:
    InNode(ASTNodePtr value, std::vector<ASTNodePtr> values, bool negate = false)
        : value_(value), values_(std::move(values)), negate_(negate)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;

private:
    ASTNodePtr value_;
    std::vector<ASTNodePtr> values_;
    bool negate_;
};

// ============================================================================
// 逻辑运算符节点
// ============================================================================

// NOT(condition)
class NotNode : public ASTNode {
public:
    explicit NotNode(ASTNodePtr condition)
        : condition_(condition)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;

private:
    ASTNodePtr condition_;
};

// ============================================================================
// 数学函数节点
// ============================================================================

// 数学函数
class MathFunctionNode : public ASTNode {
public:
    MathFunctionNode(std::string name, std::vector<ASTNodePtr> args)
        : name_(std::move(name))
        , args_(std::move(args))
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;

    const std::string& name() const { return name_; }
    const std::vector<ASTNodePtr>& arguments() const { return args_; }

private:
    std::string name_;
    std::vector<ASTNodePtr> args_;
};

// ============================================================================
// 数据函数节点
// ============================================================================

// PATTERN(timeframe).pattern_name(penetration)
// K线形态识别函数
class PatternNode : public ASTNode {
public:
    PatternNode(const std::string& timeframe,
                const std::string& pattern_name,
                ASTNodePtr penetration_param = nullptr)
        : timeframe_(timeframe)
        , pattern_name_(pattern_name)
        , penetration_param_(penetration_param)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;

private:
    std::string timeframe_;
    std::string pattern_name_;
    ASTNodePtr penetration_param_;  // penetration参数（可选，默认0.3）
};

// ============================================================================
// CONSECUTIVE和COUNT已重构为functions/系统实现
// 不再需要专门的AST节点，使用IndicatorRefNode代替
// ============================================================================

// ============================================================================
// 信号函数节点（信号函数）
// ============================================================================

// 所有信号函数的基类
class SignalFunctionNode : public ASTNode {
public:
    explicit SignalFunctionNode(std::vector<ASTNodePtr> conditions)
        : conditions_(std::move(conditions))
    {}

    virtual ~SignalFunctionNode() = default;

    Value evaluate(const Context&) const override {
        throw EvaluatorException("Signal functions should use evaluateSignal()");
    }

    virtual double evaluateSignal(const Context& ctx) const = 0;

    const std::vector<ASTNodePtr>& getConditions() const { return conditions_; }

    // 收集指标快照（用于详细分析）
    std::vector<IndicatorSnapshot> collectIndicatorSnapshots(const Context& ctx) const;

protected:
    std::vector<ASTNodePtr> conditions_;

    int countSatisfiedConditions(const Context& ctx) const;
};

// ALL{}
class AllNode : public SignalFunctionNode {
public:
    explicit AllNode(std::vector<ASTNodePtr> conditions)
        : SignalFunctionNode(std::move(conditions))
    {}

    double evaluateSignal(const Context& ctx) const override;
    std::string toString() const override;
};

// ANY{}
class AnyNode : public SignalFunctionNode {
public:
    explicit AnyNode(std::vector<ASTNodePtr> conditions)
        : SignalFunctionNode(std::move(conditions))
    {}

    double evaluateSignal(const Context& ctx) const override;
    std::string toString() const override;
};

// NONE{}
class NoneNode : public SignalFunctionNode {
public:
    explicit NoneNode(std::vector<ASTNodePtr> conditions)
        : SignalFunctionNode(std::move(conditions))
    {}

    double evaluateSignal(const Context& ctx) const override;
    std::string toString() const override;
};

// MIN(n){}
class MinNode : public SignalFunctionNode {
public:
    MinNode(int min_count, std::vector<ASTNodePtr> conditions)
        : SignalFunctionNode(std::move(conditions))
        , min_count_(min_count)
    {}

    double evaluateSignal(const Context& ctx) const override;
    std::string toString() const override;

private:
    int min_count_;
};

// COUNT(n){}
class CountNode : public SignalFunctionNode {
public:
    CountNode(int target_count, std::vector<ASTNodePtr> conditions)
        : SignalFunctionNode(std::move(conditions))
        , target_count_(target_count)
    {}

    double evaluateSignal(const Context& ctx) const override;
    std::string toString() const override;

private:
    int target_count_;
};

// MAX(n){}
class MaxNode : public SignalFunctionNode {
public:
    MaxNode(int max_count, std::vector<ASTNodePtr> conditions)
        : SignalFunctionNode(std::move(conditions))
        , max_count_(max_count)
    {}

    double evaluateSignal(const Context& ctx) const override;
    std::string toString() const override;

private:
    int max_count_;
};

// VOTE(n){}
// 支持两种模式：
// 1. 整数模式：VOTE(2) - 至少2个条件满足
// 2. 百分比模式：VOTE(0.6) - 至少60%的条件满足
class VoteNode : public SignalFunctionNode {
public:
    VoteNode(double threshold, std::vector<ASTNodePtr> conditions)
        : SignalFunctionNode(std::move(conditions))
        , threshold_(threshold)
    {}

    double evaluateSignal(const Context& ctx) const override;
    std::string toString() const override;

    // 判断是否为百分比模式
    bool isPercentageMode() const { return threshold_ > 0.0 && threshold_ <= 1.0; }
    
    // 获取阈值
    double getThreshold() const { return threshold_; }

private:
    double threshold_;  // 阈值：>1 表示数量，0-1 表示百分比
};

// WEIGHTED(threshold){}
// 支持多条件：WEIGHT(cond1, cond2, ...) = weight
// 语义：全部条件满足才得分（AND 逻辑）
struct WeightedCondition {
    std::vector<ASTNodePtr> conditions;  // 多个条件（AND 逻辑）
    double weight;
};

class WeightedNode : public ASTNode {
public:
    WeightedNode(double threshold, std::vector<WeightedCondition> conditions)
        : threshold_(threshold)
        , conditions_(std::move(conditions))
    {}

    Value evaluate(const Context&) const override {
        throw EvaluatorException("WEIGHTED should use evaluateSignal()");
    }

    double evaluateSignal(const Context& ctx) const;
    std::string toString() const override;

private:
    double threshold_;
    std::vector<WeightedCondition> conditions_;
};

// ============================================================================
// 赋值节点（v3.0.2 新增）
// ============================================================================

// 参数赋值：$(timeframe).INDICATOR.PARAM_NAME = value
class AssignmentNode : public ASTNode {
public:
    AssignmentNode(const std::string& indicator,
                   const std::string& timeframe,
                   const std::string& param_name,
                   ASTNodePtr value_expr)
        : indicator_(indicator)
        , timeframe_(timeframe)
        , param_name_(param_name)
        , value_expr_(value_expr)
    {}

    Value evaluate(const Context& ctx) const override;
    std::string toString() const override;

    // 执行赋值操作（修改 Context）
    void executeAssignment(Context& ctx) const;

private:
    std::string indicator_;
    std::string timeframe_;
    std::string param_name_;
    ASTNodePtr value_expr_;
};

// ============================================================================
// 规则节点（顶层）
// ============================================================================

class RuleNode {
public:
    RuleNode(std::shared_ptr<ASTNode> signal_func, const std::string& action,
             std::shared_ptr<ASTNode> tp_expr = nullptr,
             std::shared_ptr<ASTNode> sl_expr = nullptr)
        : signal_func_(signal_func)
        , action_(action)
        , tp_expr_(tp_expr)
        , sl_expr_(sl_expr)
    {}

    // 评估规则，返回 Signal
    Signal evaluateRule(const Context& ctx) const;

    std::string toString() const;
    
    // P2优化：字节码编译需要的访问器
    std::shared_ptr<ASTNode> getSignalFunc() const { return signal_func_; }
    std::string getAction() const { return action_; }
    std::shared_ptr<ASTNode> getTakeProfitExpr() const { return tp_expr_; }
    std::shared_ptr<ASTNode> getStopLossExpr() const { return sl_expr_; }

private:
    std::shared_ptr<ASTNode> signal_func_;  // 信号函数（ALL/ANY/NONE等）
    std::string action_;                     // 交易动作（"BUY"/"SELL"/"HOLD"）
    std::shared_ptr<ASTNode> tp_expr_;      // 止盈表达式（可选，nullptr表示使用默认值0）
    std::shared_ptr<ASTNode> sl_expr_;      // 止损表达式（可选，nullptr表示使用默认值0）
};

// 赋值语句节点（v3.0.2 新增）
class AssignmentStatement {
public:
    explicit AssignmentStatement(std::shared_ptr<AssignmentNode> assignment)
        : assignment_(assignment)
    {}

    // 执行赋值
    void execute(Context& ctx) const;

    std::string toString() const;

private:
    std::shared_ptr<AssignmentNode> assignment_;
};

} // namespace dsl
} // namespace prophet
