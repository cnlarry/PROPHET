#pragma once

#include "prophet/core/context.hpp"
#include "function_registry.hpp"
#include "scope.hpp"
#include <memory>

namespace prophet::dsl {

/**
 * 求值上下文
 * 
 * 包装原有的Context，并添加自定义函数支持
 */
class EvaluationContext {
public:
    /**
     * 构造函数
     * @param ctx 策略上下文
     * @param func_reg 函数注册表
     */
    EvaluationContext(Context& ctx, FunctionRegistry& func_reg)
        : context_(ctx)
        , function_registry_(func_reg)
        , current_scope_(nullptr)
    {}
    
    // 获取底层Context
    Context& getContext() { return context_; }
    const Context& getContext() const { return context_; }
    
    // 获取函数注册表
    FunctionRegistry& getFunctionRegistry() { return function_registry_; }
    const FunctionRegistry& getFunctionRegistry() const { return function_registry_; }
    
    // 获取当前作用域
    Scope* getCurrentScope() { return current_scope_.get(); }
    
    // 获取当前作用域的shared_ptr
    std::shared_ptr<Scope> getCurrentScopePtr() { return current_scope_; }
    
    // 设置当前作用域
    void setCurrentScope(std::shared_ptr<Scope> scope) {
        current_scope_ = scope;
    }
    
    // 创建新作用域
    std::shared_ptr<Scope> createScope(Scope* parent = nullptr) {
        return std::make_shared<Scope>(parent);
    }

private:
    Context& context_;                          // 策略上下文
    FunctionRegistry& function_registry_;       // 函数注册表
    std::shared_ptr<Scope> current_scope_;      // 当前作用域
};

} // namespace prophet::dsl

