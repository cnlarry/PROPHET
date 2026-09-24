/*
 * ============================================================================
 * 文件名：scope.cpp
 * 功能说明：作用域管理实现
 * 
 * 这个文件实现了变量作用域的管理机制
 * 
 * 什么是作用域？
 *   作用域定义了变量的可见范围
 *   就像不同房间的私有空间，内层房间可以看到外层，但外层看不到内层
 * 
 * 主要功能：
 * - 变量声明和查找
 * - 变量赋值和更新
 * - 父子作用域链（作用域嵌套）
 * - 局部变量和外层变量的区分
 * 
 * 使用场景：
 * - 函数调用时创建新作用域
 * - if/else语句块创建新作用域
 * - 变量的生命周期管理
 * 
 * 作用域规则：
 * - 内层作用域可以访问外层变量
 * - 外层作用域不能访问内层变量
 * - 内层可以覆盖（shadow）外层同名变量
 * ============================================================================
 */

#include "prophet/dsl/scope.hpp"
#include <stdexcept>
#include <sstream>

namespace prophet::dsl {

// ========================================
// Scope 实现
// ========================================

Scope::Scope(Scope* parent)
    : parent_(parent)
{}

void Scope::declare_variable(const std::string& name, const Value& value) {
    // 检查变量是否已在当前作用域声明
    if (has_local_variable(name)) {
        throw std::runtime_error("Variable '" + name + "' already declared in current scope");
    }
    
    // 在当前作用域声明变量
    variables_[name] = value;
}

void Scope::assign_variable(const std::string& name, const Value& value) {
    // 在当前作用域查找
    if (variables_.count(name)) {
        variables_[name] = value;
        return;
    }
    
    // 在父作用域查找
    if (parent_) {
        parent_->assign_variable(name, value);
        return;
    }
    
    // 变量不存在
    throw std::runtime_error("Variable '" + name + "' not declared");
}

Value Scope::get_variable(const std::string& name) const {
    // 在当前作用域查找
    auto it = variables_.find(name);
    if (it != variables_.end()) {
        return it->second;
    }
    
    // 在父作用域查找
    if (parent_) {
        return parent_->get_variable(name);
    }
    
    // 变量不存在
    throw std::runtime_error("Variable '" + name + "' not declared");
}

bool Scope::has_variable(const std::string& name) const {
    // 在当前作用域查找
    if (variables_.count(name)) {
        return true;
    }
    
    // 在父作用域查找
    if (parent_) {
        return parent_->has_variable(name);
    }
    
    return false;
}

bool Scope::has_local_variable(const std::string& name) const {
    return variables_.count(name) > 0;
}

// ========================================
// 返回值管理
// ========================================

void Scope::set_return_value(const Value& value) {
    return_value_ = value;
}

Value Scope::get_return_value() const {
    if (!return_value_.has_value()) {
        throw std::runtime_error("No return value set");
    }
    return return_value_.value();
}

bool Scope::has_return_value() const {
    return return_value_.has_value();
}

// ========================================
// 作用域层级
// ========================================

std::unique_ptr<Scope> Scope::enter_scope() {
    return std::make_unique<Scope>(this);
}

Scope* Scope::get_parent() const {
    return parent_;
}

bool Scope::is_global() const {
    return parent_ == nullptr;
}

size_t Scope::depth() const {
    size_t d = 0;
    const Scope* current = this;
    while (current->parent_) {
        d++;
        current = current->parent_;
    }
    return d;
}

std::vector<std::string> Scope::list_variables() const {
    std::vector<std::string> vars;
    vars.reserve(variables_.size());
    
    for (const auto& [name, _] : variables_) {
        vars.push_back(name);
    }
    
    return vars;
}

std::string Scope::to_string() const {
    std::ostringstream oss;
    oss << "Scope(depth=" << depth() << ", variables=[";
    
    bool first = true;
    for (const auto& [name, value] : variables_) {
        if (!first) oss << ", ";
        oss << name;
        first = false;
    }
    
    oss << "])";
    return oss.str();
}

// ========================================
// ScopeGuard 实现
// ========================================

ScopeGuard::ScopeGuard(Scope& parent)
    : scope_(parent.enter_scope())
{}

Scope& ScopeGuard::get() {
    return *scope_;
}

} // namespace prophet::dsl

