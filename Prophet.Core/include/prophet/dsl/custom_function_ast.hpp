#pragma once

#include "ast.hpp"
#include "prophet/common/types.hpp"
#include <vector>
#include <memory>
#include <string>
#include <optional>

namespace prophet::dsl {

// ========================================
// 前置声明
// ========================================
class EvaluationContext;
class TypeCheckContext;
class Scope;

// ========================================
// 声明类型枚举（用于类型声明和检查）
// ========================================
enum class DeclaredType {
    INTEGER,     // 整数类型
    DOUBLE,      // 浮点数类型
    BOOLEAN,     // 布尔类型
    STRING,      // 字符串类型
    ENUM,        // 枚举类型
    VOID,        // 无返回值
    ANY          // 任意类型（用于类型推断）
};

// ========================================
// 函数参数
// ========================================
struct FunctionParameter {
    std::string name;                       // 参数名称
    std::optional<DeclaredType> type;       // 可选的类型声明
    
    FunctionParameter(const std::string& n, std::optional<DeclaredType> t = std::nullopt)
        : name(n), type(t) {}
};

// ========================================
// 语句基类
// ========================================
class StatementNode {
public:
    virtual ~StatementNode() = default;
    
    // 执行语句
    virtual void execute(EvaluationContext& ctx, Scope& scope) = 0;
    
    // 类型检查
    virtual void type_check(TypeCheckContext& ctx) = 0;
    
    // 调试信息
    virtual std::string to_string() const = 0;
};

using StatementNodePtr = std::shared_ptr<StatementNode>;

// ========================================
// 变量声明节点
// ========================================
class VariableDeclarationNode : public StatementNode {
public:
    std::string name;                       // 变量名（包含 @ 前缀）
    std::optional<DeclaredType> declared_type;  // 显式声明的类型
    ASTNodePtr initializer;                 // 初始化表达式
    
    VariableDeclarationNode(
        const std::string& n,
        ASTNodePtr init,
        std::optional<DeclaredType> type = std::nullopt
    ) : name(n), initializer(std::move(init)), declared_type(type) {}
    
    void execute(EvaluationContext& ctx, Scope& scope) override;
    void type_check(TypeCheckContext& ctx) override;
    std::string to_string() const override;
};

// ========================================
// 赋值语句节点（包括复合赋值）
// ========================================
enum class AssignmentOperator {
    ASSIGN,     // =
    ADD_ASSIGN, // +=
    SUB_ASSIGN, // -=
    MUL_ASSIGN, // *=
    DIV_ASSIGN  // /=
};

class AssignmentStatementNode : public StatementNode {
public:
    std::string variable_name;              // 变量名（包含 @ 前缀）
    AssignmentOperator op;                  // 赋值运算符
    ASTNodePtr value;                       // 赋值表达式
    
    AssignmentStatementNode(
        const std::string& name,
        ASTNodePtr val,
        AssignmentOperator assign_op = AssignmentOperator::ASSIGN
    ) : variable_name(name), value(std::move(val)), op(assign_op) {}
    
    void execute(EvaluationContext& ctx, Scope& scope) override;
    void type_check(TypeCheckContext& ctx) override;
    std::string to_string() const override;
};

// ========================================
// if-else 语句节点
// ========================================
class IfStatementNode : public StatementNode {
public:
    ASTNodePtr condition;                           // 条件表达式
    std::vector<StatementNodePtr> then_branch;       // if 分支
    std::vector<StatementNodePtr> else_branch;       // else 分支（可选）
    
    IfStatementNode(
        ASTNodePtr cond,
        std::vector<StatementNodePtr> then_stmts,
        std::vector<StatementNodePtr> else_stmts = {}
    ) : condition(std::move(cond)),
        then_branch(std::move(then_stmts)),
        else_branch(std::move(else_stmts)) {}
    
    void execute(EvaluationContext& ctx, Scope& scope) override;
    void type_check(TypeCheckContext& ctx) override;
    std::string to_string() const override;
};

// ========================================
// return 语句节点
// ========================================
class ReturnStatementNode : public StatementNode {
public:
    ASTNodePtr return_value;                // 返回值表达式
    
    explicit ReturnStatementNode(ASTNodePtr val)
        : return_value(std::move(val)) {}
    
    void execute(EvaluationContext& ctx, Scope& scope) override;
    void type_check(TypeCheckContext& ctx) override;
    std::string to_string() const override;
};

// ========================================
// 表达式语句节点（表达式作为语句）
// ========================================
class ExpressionStatementNode : public StatementNode {
public:
    ASTNodePtr expression;                  // 表达式
    
    explicit ExpressionStatementNode(ASTNodePtr expr)
        : expression(std::move(expr)) {}
    
    void execute(EvaluationContext& ctx, Scope& scope) override;
    void type_check(TypeCheckContext& ctx) override;
    std::string to_string() const override;
};

// ========================================
// 三元表达式节点（扩展现有 ASTNode）
// ========================================
class TernaryExpressionNode : public ASTNode {
public:
    ASTNodePtr condition;                   // 条件表达式
    ASTNodePtr true_value;                  // 条件为真时的值
    ASTNodePtr false_value;                 // 条件为假时的值
    
    TernaryExpressionNode(
        ASTNodePtr cond,
        ASTNodePtr true_val,
        ASTNodePtr false_val
    ) : condition(std::move(cond)),
        true_value(std::move(true_val)),
        false_value(std::move(false_val)) {}
    
    // 实现基类接口
    Value evaluate(const Context& ctx) const override;
    std::string toString() const override { return to_string(); }
    
    // 内部实现
    std::string to_string() const;
};

// ========================================
// 变量引用节点
// ========================================
class VariableReferenceNode : public ASTNode {
public:
    std::string name;                       // 变量名（包含 @ 前缀）
    
    explicit VariableReferenceNode(const std::string& var_name)
        : name(var_name) {}
    
    // 实现基类接口（当没有EvaluationContext时抛出异常）
    Value evaluate(const Context& ctx) const override;
    std::string toString() const override { return to_string(); }
    
    // 内部实现
    std::string to_string() const;
};

// ========================================
// 自定义函数定义节点
// ========================================
class FunctionDefinitionNode : public ASTNode {
public:
    std::string name;                               // 函数名
    std::vector<FunctionParameter> parameters;       // 参数列表
    std::optional<DeclaredType> return_type;         // 返回类型（可选）
    std::vector<StatementNodePtr> body;              // 函数体
    
    FunctionDefinitionNode(
        const std::string& func_name,
        std::vector<FunctionParameter> params,
        std::vector<StatementNodePtr> func_body,
        std::optional<DeclaredType> ret_type = std::nullopt
    ) : name(func_name),
        parameters(std::move(params)),
        body(std::move(func_body)),
        return_type(ret_type) {}
    
    // 函数定义不直接求值，而是注册到函数表
    Value evaluate(const Context& /* ctx */) const override {
        throw std::runtime_error("Function definition cannot be evaluated directly");
    }
    std::string toString() const override { return to_string(); }
    
    // 类型检查
    void type_check(TypeCheckContext& ctx);
    
    // 检查所有代码路径是否都有返回值
    bool all_paths_return() const;
    
    std::string to_string() const;
};

// ========================================
// 自定义函数调用节点
// ========================================
class FunctionCallNode : public ASTNode {
public:
    std::string function_name;              // 函数名
    std::vector<ASTNodePtr> arguments;      // 实参列表
    
    FunctionCallNode(
        const std::string& name,
        std::vector<ASTNodePtr> args
    ) : function_name(name), arguments(std::move(args)) {}
    
    // 实现基类接口（当没有EvaluationContext时抛出异常）
    Value evaluate(const Context& ctx) const override;
    std::string toString() const override { return to_string(); }
    
    // 使用EvaluationContext求值（在自定义函数内部调用）
    Value evaluateWithContext(EvaluationContext& ctx) const;
    
    // 内部实现
    std::string to_string() const;
};

// ========================================
// 辅助函数：声明类型转换
// ========================================
std::string declared_type_to_string(DeclaredType type);
DeclaredType string_to_declared_type(const std::string& type_str);

// ========================================
// 辅助函数：DeclaredType 和 ValueType 之间的转换
// ========================================
DeclaredType value_type_to_declared_type(ValueType type);
ValueType declared_type_to_value_type(DeclaredType type);

// ========================================
// 辅助函数：赋值运算符转换
// ========================================
std::string assignment_operator_to_string(AssignmentOperator op);

} // namespace prophet::dsl

