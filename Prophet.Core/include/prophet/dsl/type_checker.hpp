#pragma once

#include "custom_function_ast.hpp"
#include "function_registry.hpp"
#include <unordered_map>
#include <string>
#include <memory>
#include <vector>

namespace prophet::dsl {

// ========================================
// 类型检查上下文
// ========================================
class TypeCheckContext {
public:
    TypeCheckContext() = default;
    ~TypeCheckContext() = default;
    
    // ========================================
    // 变量类型管理
    // ========================================
    
    /**
     * 声明变量类型
     * @param name 变量名
     * @param type 变量类型
     * @throws std::runtime_error 如果变量已声明
     */
    void declare_variable_type(const std::string& name, DeclaredType type);
    
    /**
     * 获取变量类型
     * @param name 变量名
     * @return 变量类型
     * @throws std::runtime_error 如果变量未声明
     */
    DeclaredType get_variable_type(const std::string& name) const;
    
    /**
     * 检查变量是否已声明
     * @param name 变量名
     * @return 如果已声明返回 true
     */
    bool has_variable(const std::string& name) const;
    
    /**
     * 进入新作用域
     */
    void enter_scope();
    
    /**
     * 退出当前作用域
     */
    void exit_scope();
    
    // ========================================
    // 函数类型管理
    // ========================================
    
    /**
     * 设置当前检查的函数的返回类型
     * @param type 返回类型
     */
    void set_current_function_return_type(DeclaredType type);
    
    /**
     * 获取当前函数的返回类型
     * @return 返回类型
     * @throws std::runtime_error 如果不在函数内部
     */
    DeclaredType get_current_function_return_type() const;
    
    /**
     * 检查是否在函数内部
     * @return 如果在函数内部返回 true
     */
    bool is_in_function() const;
    
    // ========================================
    // 错误收集
    // ========================================
    
    /**
     * 添加类型错误
     * @param message 错误信息
     */
    void add_error(const std::string& message);
    
    /**
     * 添加警告
     * @param message 警告信息
     */
    void add_warning(const std::string& message);
    
    /**
     * 检查是否有错误
     * @return 如果有错误返回 true
     */
    bool has_errors() const;
    
    /**
     * 获取所有错误信息
     * @return 错误信息列表
     */
    const std::vector<std::string>& get_errors() const;
    
    /**
     * 获取所有警告信息
     * @return 警告信息列表
     */
    const std::vector<std::string>& get_warnings() const;
    
    /**
     * 抛出所有错误（如果有）
     * @throws std::runtime_error 如果有错误
     */
    void throw_if_errors() const;
    
private:
    // 作用域栈（每个作用域是一个变量类型映射）
    std::vector<std::unordered_map<std::string, DeclaredType>> scope_stack_;
    
    // 当前函数的返回类型（如果不在函数内部则为空）
    std::optional<DeclaredType> current_function_return_type_;
    
    // 错误和警告
    std::vector<std::string> errors_;
    std::vector<std::string> warnings_;
};

// ========================================
// 类型检查器（主要逻辑）
// ========================================
class TypeChecker {
public:
    explicit TypeChecker(TypeCheckContext& ctx);
    
    // ========================================
    // 检查函数定义
    // ========================================
    
    /**
     * 检查函数定义
     * @param func 函数定义节点
     * @throws std::runtime_error 如果有类型错误
     */
    void check_function(const FunctionDefinitionNode& func);
    
    // ========================================
    // 检查语句
    // ========================================
    
    /**
     * 检查语句
     * @param stmt 语句节点
     */
    void check_statement(const StatementNode& stmt);
    
    /**
     * 检查变量声明
     * @param decl 变量声明节点
     */
    void check_variable_declaration(const VariableDeclarationNode& decl);
    
    /**
     * 检查赋值语句
     * @param assign 赋值语句节点
     */
    void check_assignment(const AssignmentStatementNode& assign);
    
    /**
     * 检查 if-else 语句
     * @param if_stmt if-else 语句节点
     */
    void check_if_statement(const IfStatementNode& if_stmt);
    
    /**
     * 检查 return 语句
     * @param return_stmt return 语句节点
     */
    void check_return_statement(const ReturnStatementNode& return_stmt);
    
    // ========================================
    // 类型推断
    // ========================================
    
    /**
     * 推断表达式的类型
     * @param expr 表达式节点
     * @return 表达式类型
     * @throws std::runtime_error 如果无法推断类型
     */
    DeclaredType infer_type(const ASTNode& expr);
    
    /**
     * 检查类型兼容性
     * @param expected 期望类型
     * @param actual 实际类型
     * @return 如果兼容返回 true
     */
    bool is_compatible(DeclaredType expected, DeclaredType actual) const;
    
    /**
     * 检查是否可以自动转换
     * @param from 源类型
     * @param to 目标类型
     * @return 如果可以转换返回 true
     */
    bool can_convert(DeclaredType from, DeclaredType to) const;
    
    // ========================================
    // 控制流分析
    // ========================================
    
    /**
     * 检查函数体的所有代码路径是否都有返回值
     * @param body 函数体语句列表
     * @return 如果所有路径都有返回值返回 true
     */
    bool all_paths_return(const std::vector<StatementNodePtr>& body) const;
    
    /**
     * 检查语句块是否一定会返回
     * @param statements 语句列表
     * @return 如果一定会返回返回 true
     */
    bool definitely_returns(const std::vector<StatementNodePtr>& statements) const;
    
private:
    TypeCheckContext& ctx_;
};

// ========================================
// 辅助函数
// ========================================

/**
 * 检查两个类型是否相同
 * @param type1 类型1
 * @param type2 类型2
 * @return 如果相同返回 true
 */
bool types_equal(DeclaredType type1, DeclaredType type2);

/**
 * 获取二元运算的结果类型
 * @param left_type 左操作数类型
 * @param right_type 右操作数类型
 * @param op 运算符
 * @return 结果类型
 * @throws std::runtime_error 如果类型不兼容
 */
DeclaredType get_binary_result_type(
    DeclaredType left_type,
    DeclaredType right_type,
    const std::string& op
);

/**
 * 获取一元运算的结果类型
 * @param operand_type 操作数类型
 * @param op 运算符
 * @return 结果类型
 * @throws std::runtime_error 如果类型不兼容
 */
DeclaredType get_unary_result_type(
    DeclaredType operand_type,
    const std::string& op
);

} // namespace prophet::dsl

