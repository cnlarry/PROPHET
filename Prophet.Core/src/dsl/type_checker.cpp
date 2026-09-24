/*
 * ============================================================================
 * 文件名：type_checker.cpp
 * 功能说明：类型检查器实现
 * 
 * 这个文件实现了DSL的类型检查系统
 * 在执行前验证代码的类型正确性，防止类型错误
 * 
 * 主要功能：
 * - 变量类型声明和推断
 * - 表达式类型检查
 * - 函数参数和返回值类型检查
 * - 作用域管理
 * 
 * 支持的类型：
 * - INTEGER：整数
 * - DOUBLE：浮点数
 * - BOOLEAN：布尔值
 * - STRING：字符串
 * - ENUM：枚举类型
 * - VOID：无返回值
 * 
 * 为什么需要类型检查？
 * - 提前发现类型错误，避免运行时错误
 * - 提供更好的错误提示
 * - 保证代码的类型安全
 * ============================================================================
 */

#include "prophet/dsl/type_checker.hpp"
#include "prophet/dsl/custom_function_ast.hpp"
#include <stdexcept>
#include <sstream>

namespace prophet::dsl {

// ========================================
// TypeCheckContext 实现
// ========================================

void TypeCheckContext::declare_variable_type(const std::string& name, DeclaredType type) {
    if (scope_stack_.empty()) {
        scope_stack_.push_back({});
    }
    
    auto& current_scope = scope_stack_.back();
    if (current_scope.find(name) != current_scope.end()) {
        throw std::runtime_error("Variable '" + name + "' is already declared in this scope");
    }
    
    current_scope[name] = type;
}

DeclaredType TypeCheckContext::get_variable_type(const std::string& name) const {
    // 从最内层作用域向外查找
    for (auto it = scope_stack_.rbegin(); it != scope_stack_.rend(); ++it) {
        auto var_it = it->find(name);
        if (var_it != it->end()) {
            return var_it->second;
        }
    }
    
    throw std::runtime_error("Variable '" + name + "' is not declared");
}

bool TypeCheckContext::has_variable(const std::string& name) const {
    for (auto it = scope_stack_.rbegin(); it != scope_stack_.rend(); ++it) {
        if (it->find(name) != it->end()) {
            return true;
        }
    }
    return false;
}

void TypeCheckContext::enter_scope() {
    scope_stack_.push_back({});
}

void TypeCheckContext::exit_scope() {
    if (!scope_stack_.empty()) {
        scope_stack_.pop_back();
    }
}

void TypeCheckContext::set_current_function_return_type(DeclaredType type) {
    current_function_return_type_ = type;
}

DeclaredType TypeCheckContext::get_current_function_return_type() const {
    if (!current_function_return_type_.has_value()) {
        throw std::runtime_error("Not in function context");
    }
    return current_function_return_type_.value();
}

bool TypeCheckContext::is_in_function() const {
    return current_function_return_type_.has_value();
}

void TypeCheckContext::add_error(const std::string& message) {
    errors_.push_back(message);
}

void TypeCheckContext::add_warning(const std::string& message) {
    warnings_.push_back(message);
}

bool TypeCheckContext::has_errors() const {
    return !errors_.empty();
}

const std::vector<std::string>& TypeCheckContext::get_errors() const {
    return errors_;
}

const std::vector<std::string>& TypeCheckContext::get_warnings() const {
    return warnings_;
}

void TypeCheckContext::throw_if_errors() const {
    if (has_errors()) {
        std::ostringstream oss;
        oss << "Type checking failed with " << errors_.size() << " error(s):\n";
        for (const auto& error : errors_) {
            oss << "  - " << error << "\n";
        }
        throw std::runtime_error(oss.str());
    }
}

// ========================================
// TypeChecker 实现 - Stub版本
// ========================================

TypeChecker::TypeChecker(TypeCheckContext& ctx) : ctx_(ctx) {}

void TypeChecker::check_function(const FunctionDefinitionNode& func) {
    // Stub: 最小实现
    (void)func;
    // TODO: 实现完整的函数类型检查
}

void TypeChecker::check_statement(const StatementNode& stmt) {
    // Stub: 最小实现
    (void)stmt;
    // TODO: 实现完整的语句类型检查
}

void TypeChecker::check_variable_declaration(const VariableDeclarationNode& decl) {
    // Stub: 最小实现
    (void)decl;
    // TODO: 实现完整的变量声明类型检查
}

void TypeChecker::check_assignment(const AssignmentStatementNode& assign) {
    // Stub: 最小实现
    (void)assign;
    // TODO: 实现完整的赋值语句类型检查
}

void TypeChecker::check_if_statement(const IfStatementNode& if_stmt) {
    // Stub: 最小实现
    (void)if_stmt;
    // TODO: 实现完整的if语句类型检查
}

void TypeChecker::check_return_statement(const ReturnStatementNode& return_stmt) {
    // Stub: 最小实现
    (void)return_stmt;
    // TODO: 实现完整的return语句类型检查
}

DeclaredType TypeChecker::infer_type(const ASTNode& expr) {
    // Stub: 返回ANY类型
    (void)expr;
    // TODO: 实现完整的类型推断
    return DeclaredType::ANY;
}

bool TypeChecker::is_compatible(DeclaredType expected, DeclaredType actual) const {
    // Stub: 简单实现
    if (expected == DeclaredType::ANY || actual == DeclaredType::ANY) {
        return true;
    }
    
    if (expected == actual) {
        return true;
    }
    
    // Integer 可以转换为 Double
    if (expected == DeclaredType::DOUBLE && actual == DeclaredType::INTEGER) {
        return true;
    }
    
    return false;
}

bool TypeChecker::can_convert(DeclaredType from, DeclaredType to) const {
    return is_compatible(to, from);
}

bool TypeChecker::all_paths_return(const std::vector<StatementNodePtr>& body) const {
    // Stub: 简单实现，检查是否有return语句
    (void)body;
    // TODO: 实现完整的控制流分析
    return true;
}

bool TypeChecker::definitely_returns(const std::vector<StatementNodePtr>& statements) const {
    // Stub: 简单实现
    (void)statements;
    // TODO: 实现完整的控制流分析
    return false;
}

// ========================================
// 辅助函数
// ========================================

bool types_equal(DeclaredType type1, DeclaredType type2) {
    return type1 == type2;
}

DeclaredType get_binary_result_type(
    DeclaredType left_type,
    DeclaredType right_type,
    const std::string& op
) {
    // Stub: 简单实现
    (void)op;
    
    // 如果有ANY类型，返回ANY
    if (left_type == DeclaredType::ANY || right_type == DeclaredType::ANY) {
        return DeclaredType::ANY;
    }
    
    // 如果有Double，返回Double
    if (left_type == DeclaredType::DOUBLE || right_type == DeclaredType::DOUBLE) {
        return DeclaredType::DOUBLE;
    }
    
    // 否则返回左操作数类型
    return left_type;
}

DeclaredType get_unary_result_type(
    DeclaredType operand_type,
    const std::string& op
) {
    // Stub: 简单实现
    (void)op;
    return operand_type;
}

} // namespace prophet::dsl
