/*
 * ============================================================================
 * 文件名：function_registry.cpp
 * 功能说明：DSL函数注册表实现
 * 
 * 这个文件实现了DSL自定义函数的注册和管理机制
 * 
 * 主要功能：
 * - 注册自定义函数定义
 * - 查找和获取函数定义
 * - 函数调用和执行
 * - 参数绑定和作用域创建
 * 
 * 工作原理：
 * - 用户定义的函数存储在注册表中
 * - 调用函数时，创建新的作用域
 * - 将实参绑定到形参
 * - 执行函数体并返回结果
 * ============================================================================
 */

#include "prophet/dsl/function_registry.hpp"
#include "prophet/dsl/custom_function_ast.hpp"
#include "prophet/dsl/evaluation_context.hpp"
#include "prophet/dsl/type_checker.hpp"
#include <stdexcept>
#include <sstream>

namespace prophet::dsl {

// ========================================
// 函数注册和查询
// ========================================

void FunctionRegistry::register_function(
    const std::string& name,
    std::shared_ptr<FunctionDefinitionNode> func_def
) {
    if (has_function(name)) {
        throw std::runtime_error("Function '" + name + "' is already registered");
    }
    
    functions_[name] = func_def;
}

std::shared_ptr<FunctionDefinitionNode> FunctionRegistry::get_function(const std::string& name) const {
    auto it = functions_.find(name);
    if (it != functions_.end()) {
        return it->second;
    }
    return nullptr;
}

bool FunctionRegistry::has_function(const std::string& name) const {
    return functions_.find(name) != functions_.end();
}

bool FunctionRegistry::remove_function(const std::string& name) {
    auto it = functions_.find(name);
    if (it != functions_.end()) {
        functions_.erase(it);
        return true;
    }
    return false;
}

void FunctionRegistry::clear() {
    functions_.clear();
}

// ========================================
// 函数调用
// ========================================

Value FunctionRegistry::call_function(
    const std::string& name,
    const std::vector<Value>& arguments,
    EvaluationContext& ctx
) const {
    // 获取函数定义
    auto func_def = get_function(name);
    if (!func_def) {
        throw std::runtime_error("Function '" + name + "' is not defined");
    }
    
    // 检查参数数量
    if (arguments.size() != func_def->parameters.size()) {
        throw std::runtime_error(
            "Function '" + name + "' expects " + 
            std::to_string(func_def->parameters.size()) + 
            " arguments, but got " + 
            std::to_string(arguments.size())
        );
    }
    
    // 创建函数作用域
    auto function_scope = ctx.createScope(nullptr);
    
    // 绑定参数
    for (size_t i = 0; i < arguments.size(); ++i) {
        const auto& param = func_def->parameters[i];
        function_scope->declare_variable(param.name, arguments[i]);
    }
    
    // 保存当前作用域
    auto old_scope_ptr = ctx.getCurrentScopePtr();
    
    // 设置函数作用域为当前作用域
    ctx.setCurrentScope(function_scope);
    
    // 执行函数体
    for (const auto& stmt : func_def->body) {
        stmt->execute(ctx, *function_scope);
        
        // 检查是否有返回值
        if (function_scope->has_return_value()) {
            Value return_value = function_scope->get_return_value();
            
            // 恢复作用域
            ctx.setCurrentScope(old_scope_ptr);
            
            return return_value;
        }
    }
    
    // 恢复作用域
    ctx.setCurrentScope(old_scope_ptr);
    
    // 如果没有返回值，抛出错误
    throw std::runtime_error("Function '" + name + "' did not return a value");
}

// ========================================
// 类型检查 - Stub实现
// ========================================

void FunctionRegistry::type_check_all(TypeCheckContext& ctx) const {
    // Stub: 暂不实现类型检查
    (void)ctx;
    
    // 简单验证所有函数都有返回值
    for (const auto& [name, func_def] : functions_) {
        if (!func_def->all_paths_return()) {
            throw std::runtime_error("Function '" + name + "' does not return a value on all paths");
        }
    }
}

void FunctionRegistry::type_check_function(const std::string& name, TypeCheckContext& ctx) const {
    // Stub: 暂不实现类型检查
    (void)ctx;
    
    auto func_def = get_function(name);
    if (!func_def) {
        throw std::runtime_error("Function '" + name + "' is not defined");
    }
    
    // 简单验证函数有返回值
    if (!func_def->all_paths_return()) {
        throw std::runtime_error("Function '" + name + "' does not return a value on all paths");
    }
}

// ========================================
// 调试和工具函数
// ========================================

std::vector<std::string> FunctionRegistry::list_functions() const {
    std::vector<std::string> names;
    names.reserve(functions_.size());
    
    for (const auto& [name, _] : functions_) {
        names.push_back(name);
    }
    
    return names;
}

std::optional<std::string> FunctionRegistry::get_function_signature(const std::string& name) const {
    auto func_def = get_function(name);
    if (!func_def) {
        return std::nullopt;
    }
    
    return func_def->to_string();
}

std::string FunctionRegistry::to_string() const {
    std::ostringstream oss;
    oss << "FunctionRegistry with " << functions_.size() << " functions:\n";
    
    for (const auto& [name, func_def] : functions_) {
        oss << "  " << func_def->to_string() << "\n";
    }
    
    return oss.str();
}

} // namespace prophet::dsl
