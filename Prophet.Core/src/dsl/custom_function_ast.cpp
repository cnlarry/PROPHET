/*
 * ============================================================================
 * 文件名：custom_function_ast.cpp
 * 功能说明：自定义函数AST实现
 * 
 * 这个文件实现了DSL中的自定义函数功能
 * 允许用户在策略中定义和调用自己的函数
 * 
 * 支持的功能：
 * - 函数定义：function_name(param1, param2) { body }
 * - 参数传递和作用域管理
 * - 变量声明和赋值
 * - 返回语句
 * - if/else条件语句
 * - 类型系统（INTEGER、DOUBLE、BOOLEAN、STRING等）
 * 
 * 使用示例：
 *   myFunc(x, y) {
 *     let result = x + y;
 *     return result;
 *   }
 * ============================================================================
 */

#include "prophet/dsl/custom_function_ast.hpp"
#include "prophet/dsl/evaluation_context.hpp"
#include "prophet/core/context.hpp"
#include <stdexcept>
#include <sstream>

namespace prophet::dsl {

// ========================================
// 辅助函数：作用域变量导出/清理（用于表达式求值）
// ========================================

namespace {
    /**
     * 临时将作用域变量导出到环境变量
     * 这样表达式求值时可以通过 Context::getEnvVar() 访问函数参数和局部变量
     */
    std::vector<std::string> exportScopeToEnv(EvaluationContext& ctx, Scope& scope) {
        auto& base_ctx = ctx.getContext();
        std::vector<std::string> exported_vars;
        
        for (const auto& var_name : scope.list_variables()) {
            try {
                Value var_value = scope.get_variable(var_name);
                base_ctx.setEnvVar(var_name, var_value);
                exported_vars.push_back(var_name);
            } catch (...) {
                // 忽略无法导出的变量
            }
        }
        
        return exported_vars;
    }
    
    /**
     * 清理临时导出的环境变量
     */
    void cleanupExportedVars(EvaluationContext& ctx, const std::vector<std::string>& exported_vars) {
        auto& base_ctx = ctx.getContext();
        for (const auto& var_name : exported_vars) {
            base_ctx.clearEnvVar(var_name);
        }
    }
}

// ========================================
// 辅助函数：类型转换
// ========================================

std::string declared_type_to_string(DeclaredType type) {
    switch (type) {
        case DeclaredType::INTEGER:  return "Integer";
        case DeclaredType::DOUBLE:   return "Double";
        case DeclaredType::BOOLEAN:  return "Boolean";
        case DeclaredType::STRING:   return "String";
        case DeclaredType::ENUM:     return "Enum";
        case DeclaredType::VOID:     return "Void";
        case DeclaredType::ANY:      return "Any";
        default: return "Unknown";
    }
}

DeclaredType string_to_declared_type(const std::string& type_str) {
    if (type_str == "Integer" || type_str == "INTEGER") return DeclaredType::INTEGER;
    if (type_str == "Double" || type_str == "DOUBLE")   return DeclaredType::DOUBLE;
    if (type_str == "Boolean" || type_str == "BOOLEAN") return DeclaredType::BOOLEAN;
    if (type_str == "String" || type_str == "STRING")   return DeclaredType::STRING;
    if (type_str == "Enum" || type_str == "ENUM")       return DeclaredType::ENUM;
    if (type_str == "Void" || type_str == "VOID")       return DeclaredType::VOID;
    if (type_str == "Any" || type_str == "ANY")         return DeclaredType::ANY;
    
    throw std::runtime_error("Unknown type: " + type_str);
}

DeclaredType value_type_to_declared_type(ValueType type) {
    switch (type) {
        case ValueType::NUMBER:  return DeclaredType::DOUBLE;
        case ValueType::STRING:  return DeclaredType::STRING;
        case ValueType::BOOLEAN: return DeclaredType::BOOLEAN;
        case ValueType::ENUM:    return DeclaredType::ENUM;
        default: return DeclaredType::ANY;
    }
}

ValueType declared_type_to_value_type(DeclaredType type) {
    switch (type) {
        case DeclaredType::INTEGER:  return ValueType::NUMBER;
        case DeclaredType::DOUBLE:   return ValueType::NUMBER;
        case DeclaredType::BOOLEAN:  return ValueType::BOOLEAN;
        case DeclaredType::STRING:   return ValueType::STRING;
        case DeclaredType::ENUM:     return ValueType::ENUM;
        default: return ValueType::NUMBER;
    }
}

std::string assignment_operator_to_string(AssignmentOperator op) {
    switch (op) {
        case AssignmentOperator::ASSIGN:     return "=";
        case AssignmentOperator::ADD_ASSIGN: return "+=";
        case AssignmentOperator::SUB_ASSIGN: return "-=";
        case AssignmentOperator::MUL_ASSIGN: return "*=";
        case AssignmentOperator::DIV_ASSIGN: return "/=";
        default: return "unknown";
    }
}

// ========================================
// VariableDeclarationNode - Stub实现
// ========================================

void VariableDeclarationNode::execute(EvaluationContext& ctx, Scope& scope) {
    // v4.0: 导出作用域变量以便表达式求值
    auto exported_vars = exportScopeToEnv(ctx, scope);
    
    // 求值初始化表达式
    Value init_value = initializer->evaluate(ctx.getContext());
    
    // 清理临时变量
    cleanupExportedVars(ctx, exported_vars);
    
    // 声明变量
    scope.declare_variable(name, init_value);
}

void VariableDeclarationNode::type_check(TypeCheckContext& ctx) {
    // Stub: 暂不实现类型检查
    (void)ctx; // 避免未使用警告
}

std::string VariableDeclarationNode::to_string() const {
    std::ostringstream oss;
    oss << name;
    if (declared_type.has_value()) {
        oss << ": " << declared_type_to_string(declared_type.value());
    }
    oss << " = " << initializer->toString();
    return oss.str();
}

// ========================================
// AssignmentStatementNode - Stub实现
// ========================================

void AssignmentStatementNode::execute(EvaluationContext& ctx, Scope& scope) {
    // v4.0: 导出作用域变量以便表达式求值
    auto exported_vars = exportScopeToEnv(ctx, scope);
    
    // 求值右侧表达式
    Value new_value = value->evaluate(ctx.getContext());
    
    // 清理临时变量
    cleanupExportedVars(ctx, exported_vars);
    
    // 如果是复合赋值，先计算
    if (op != AssignmentOperator::ASSIGN) {
        Value current_value = scope.get_variable(variable_name);
        
        switch (op) {
            case AssignmentOperator::ADD_ASSIGN:
                new_value = Value::fromNumber(current_value.toNumber() + new_value.toNumber());
                break;
            case AssignmentOperator::SUB_ASSIGN:
                new_value = Value::fromNumber(current_value.toNumber() - new_value.toNumber());
                break;
            case AssignmentOperator::MUL_ASSIGN:
                new_value = Value::fromNumber(current_value.toNumber() * new_value.toNumber());
                break;
            case AssignmentOperator::DIV_ASSIGN:
                if (new_value.toNumber() == 0.0) {
                    throw std::runtime_error("Division by zero in assignment");
                }
                new_value = Value::fromNumber(current_value.toNumber() / new_value.toNumber());
                break;
            default:
                throw std::runtime_error("Unknown assignment operator");
        }
    }
    
    // 赋值
    scope.assign_variable(variable_name, new_value);
}

void AssignmentStatementNode::type_check(TypeCheckContext& ctx) {
    // Stub: 暂不实现类型检查
    (void)ctx;
}

std::string AssignmentStatementNode::to_string() const {
    std::ostringstream oss;
    oss << variable_name << " " << assignment_operator_to_string(op) << " " << value->toString();
    return oss.str();
}

// ========================================
// IfStatementNode - Stub实现
// ========================================

void IfStatementNode::execute(EvaluationContext& ctx, Scope& scope) {
    // v4.0: 导出作用域变量以便表达式求值
    auto exported_vars = exportScopeToEnv(ctx, scope);
    
    // 求值条件
    Value condition_value = condition->evaluate(ctx.getContext());
    
    // 清理临时变量
    cleanupExportedVars(ctx, exported_vars);
    
    if (condition_value.toBool()) {
        // 创建新的作用域（用于 then 分支）
        auto then_scope = ctx.createScope(&scope);
        
        // 执行 then 分支
        for (const auto& stmt : then_branch) {
            stmt->execute(ctx, *then_scope);
            
            // 检查是否有返回值
            if (then_scope->has_return_value()) {
                scope.set_return_value(then_scope->get_return_value());
                return;
            }
        }
    } else if (!else_branch.empty()) {
        // 创建新的作用域（用于 else 分支）
        auto else_scope = ctx.createScope(&scope);
        
        // 执行 else 分支
        for (const auto& stmt : else_branch) {
            stmt->execute(ctx, *else_scope);
            
            // 检查是否有返回值
            if (else_scope->has_return_value()) {
                scope.set_return_value(else_scope->get_return_value());
                return;
            }
        }
    }
}

void IfStatementNode::type_check(TypeCheckContext& ctx) {
    // Stub: 暂不实现类型检查
    (void)ctx;
}

std::string IfStatementNode::to_string() const {
    std::ostringstream oss;
    oss << "if (" << condition->toString() << ") { ... }";
    if (!else_branch.empty()) {
        oss << " else { ... }";
    }
    return oss.str();
}

// ========================================
// ReturnStatementNode - Stub实现
// ========================================

void ReturnStatementNode::execute(EvaluationContext& ctx, Scope& scope) {
    // v4.0: 导出作用域变量以便表达式求值
    auto exported_vars = exportScopeToEnv(ctx, scope);
    
    // 求值返回表达式
    Value ret_value = return_value->evaluate(ctx.getContext());
    
    // 清理临时变量
    cleanupExportedVars(ctx, exported_vars);
    
    // 设置返回值
    scope.set_return_value(ret_value);
}

void ReturnStatementNode::type_check(TypeCheckContext& ctx) {
    // Stub: 暂不实现类型检查
    (void)ctx;
}

std::string ReturnStatementNode::to_string() const {
    return "return " + return_value->toString();
}

// ========================================
// ExpressionStatementNode - Stub实现
// ========================================

void ExpressionStatementNode::execute(EvaluationContext& ctx, Scope& scope) {
    // 执行表达式（忽略返回值）
    expression->evaluate(ctx.getContext());
    (void)scope;
}

void ExpressionStatementNode::type_check(TypeCheckContext& ctx) {
    // Stub: 暂不实现类型检查
    (void)ctx;
}

std::string ExpressionStatementNode::to_string() const {
    return expression->toString() + ";";
}

// ========================================
// TernaryExpressionNode 实现
// ========================================

Value TernaryExpressionNode::evaluate(const Context& ctx) const {
    // 求值条件
    Value condition_value = condition->evaluate(ctx);
    
    // 根据条件返回相应的值
    if (condition_value.toBool()) {
        return true_value->evaluate(ctx);
    } else {
        return false_value->evaluate(ctx);
    }
}

std::string TernaryExpressionNode::to_string() const {
    std::ostringstream oss;
    oss << condition->toString() << " ? " 
        << true_value->toString() << " : " 
        << false_value->toString();
    return oss.str();
}

// ========================================
// VariableReferenceNode 实现
// ========================================

Value VariableReferenceNode::evaluate(const Context& ctx) const {
    // v4.1: 优先级：全局变量 > 局部变量（函数参数）
    // 1. 先尝试从全局变量中读取
    if (ctx.hasGlobalVariable(name)) {
        return ctx.getGlobalVariable(name);  // 如果未赋值会抛出异常
    }
    
    // 2. 再尝试从局部变量中读取（函数参数或函数内局部变量）
    try {
        return ctx.getEnvVar(name);
    } catch (...) {
        throw std::runtime_error("Variable '" + name + "' not found in current scope");
    }
}

std::string VariableReferenceNode::to_string() const {
    return name;
}

// ========================================
// FunctionDefinitionNode - Stub实现
// ========================================

void FunctionDefinitionNode::type_check(TypeCheckContext& ctx) {
    // Stub: 暂不实现类型检查
    (void)ctx;
}

bool FunctionDefinitionNode::all_paths_return() const {
    // Stub: 简化实现，假设所有路径都有返回值
    return !body.empty();
}

std::string FunctionDefinitionNode::to_string() const {
    std::ostringstream oss;
    oss << name << "(";
    for (size_t i = 0; i < parameters.size(); ++i) {
        if (i > 0) oss << ", ";
        oss << parameters[i].name;
        if (parameters[i].type.has_value()) {
            oss << ": " << declared_type_to_string(parameters[i].type.value());
        }
    }
    oss << ")";
    if (return_type.has_value()) {
        oss << ": " << declared_type_to_string(return_type.value());
    }
    oss << " { ... }";
    return oss.str();
}

// ========================================
// FunctionCallNode 实现
// ========================================

Value FunctionCallNode::evaluate(const Context& ctx) const {
    // 自定义函数调用需要EvaluationContext
    (void)ctx;
    throw std::runtime_error("Custom function '" + function_name + "' can only be called with EvaluationContext");
}

Value FunctionCallNode::evaluateWithContext(EvaluationContext& ctx) const {
    // 获取函数注册表
    auto& func_registry = ctx.getFunctionRegistry();
    
    // 获取函数定义
    auto func_def = func_registry.get_function(function_name);
    if (!func_def) {
        throw std::runtime_error("Function '" + function_name + "' is not defined");
    }
    
    // 检查参数数量
    if (arguments.size() != func_def->parameters.size()) {
        throw std::runtime_error(
            "Function '" + function_name + "' expects " + 
            std::to_string(func_def->parameters.size()) + 
            " arguments, but got " + std::to_string(arguments.size())
        );
    }
    
    // 求值所有参数
    std::vector<Value> arg_values;
    arg_values.reserve(arguments.size());
    for (const auto& arg : arguments) {
        arg_values.push_back(arg->evaluate(ctx.getContext()));
    }
    
    // 创建新的函数作用域
    auto function_scope = ctx.createScope(nullptr);
    
    // 绑定参数
    for (size_t i = 0; i < arg_values.size(); ++i) {
        const auto& param = func_def->parameters[i];
        const auto& arg_value = arg_values[i];
        function_scope->declare_variable(param.name, arg_value);
    }
    
    // 保存旧的作用域
    auto old_scope_ptr = ctx.getCurrentScopePtr();
    
    // 设置函数作用域为当前作用域
    ctx.setCurrentScope(function_scope);
    
    // 执行函数体
    for (const auto& stmt : func_def->body) {
        stmt->execute(ctx, *function_scope);
        
        // 检查是否有返回值
        if (function_scope->has_return_value()) {
            Value return_value = function_scope->get_return_value();
            
            // 恢复旧的作用域
            ctx.setCurrentScope(old_scope_ptr);
            
            return return_value;
        }
    }
    
    // 恢复旧的作用域
    ctx.setCurrentScope(old_scope_ptr);
    
    // 如果没有 return 语句，抛出错误
    throw std::runtime_error("Function '" + function_name + "' did not return a value");
}

std::string FunctionCallNode::to_string() const {
    std::ostringstream oss;
    oss << function_name << "(";
    for (size_t i = 0; i < arguments.size(); ++i) {
        if (i > 0) oss << ", ";
        oss << arguments[i]->toString();
    }
    oss << ")";
    return oss.str();
}

} // namespace prophet::dsl
