#pragma once

#include "prophet/common/types.hpp"
#include <unordered_map>
#include <string>
#include <memory>
#include <optional>

namespace prophet::dsl {

// ========================================
// 作用域类（块级作用域管理）
// ========================================
class Scope {
public:
    // 构造函数
    explicit Scope(Scope* parent = nullptr);
    
    // 析构函数
    ~Scope() = default;
    
    // 禁止拷贝
    Scope(const Scope&) = delete;
    Scope& operator=(const Scope&) = delete;
    
    // ========================================
    // 变量操作
    // ========================================
    
    /**
     * 在当前作用域声明新变量
     * @param name 变量名（包含 @ 前缀）
     * @param value 初始值
     * @throws std::runtime_error 如果变量已存在
     */
    void declare_variable(const std::string& name, const Value& value);
    
    /**
     * 给已存在的变量赋值
     * @param name 变量名（包含 @ 前缀）
     * @param value 新值
     * @throws std::runtime_error 如果变量不存在
     */
    void assign_variable(const std::string& name, const Value& value);
    
    /**
     * 获取变量的值
     * @param name 变量名（包含 @ 前缀）
     * @return 变量的值
     * @throws std::runtime_error 如果变量不存在
     */
    Value get_variable(const std::string& name) const;
    
    /**
     * 检查变量是否存在（在当前作用域或父作用域）
     * @param name 变量名（包含 @ 前缀）
     * @return 如果存在返回 true
     */
    bool has_variable(const std::string& name) const;
    
    /**
     * 检查变量是否在当前作用域（不检查父作用域）
     * @param name 变量名（包含 @ 前缀）
     * @return 如果存在返回 true
     */
    bool has_local_variable(const std::string& name) const;
    
    // ========================================
    // 返回值管理
    // ========================================
    
    /**
     * 设置函数返回值
     * @param value 返回值
     */
    void set_return_value(const Value& value);
    
    /**
     * 获取函数返回值
     * @return 返回值
     * @throws std::runtime_error 如果没有返回值
     */
    Value get_return_value() const;
    
    /**
     * 检查是否有返回值
     * @return 如果有返回值返回 true
     */
    bool has_return_value() const;
    
    // ========================================
    // 作用域层级
    // ========================================
    
    /**
     * 进入新的作用域（创建子作用域）
     * @return 新作用域的指针
     */
    std::unique_ptr<Scope> enter_scope();
    
    /**
     * 获取父作用域
     * @return 父作用域指针，如果没有则返回 nullptr
     */
    Scope* get_parent() const;
    
    /**
     * 检查是否是全局作用域（没有父作用域）
     * @return 如果是全局作用域返回 true
     */
    bool is_global() const;
    
    // ========================================
    // 调试和工具函数
    // ========================================
    
    /**
     * 获取当前作用域的深度（全局作用域深度为 0）
     * @return 作用域深度
     */
    size_t depth() const;
    
    /**
     * 列出当前作用域的所有变量
     * @return 变量名列表
     */
    std::vector<std::string> list_variables() const;
    
    /**
     * 转换为字符串（调试用）
     * @return 作用域的字符串表示
     */
    std::string to_string() const;
    
private:
    Scope* parent_;                                      // 父作用域
    std::unordered_map<std::string, Value> variables_;  // 当前作用域的变量
    std::optional<Value> return_value_;                 // 函数返回值
};

// ========================================
// RAII 作用域守卫（自动管理作用域进入和退出）
// ========================================
class ScopeGuard {
public:
    /**
     * 构造时创建新作用域
     * @param parent 父作用域
     */
    explicit ScopeGuard(Scope& parent);
    
    /**
     * 析构时自动退出作用域
     */
    ~ScopeGuard() = default;
    
    /**
     * 获取当前作用域
     * @return 作用域引用
     */
    Scope& get();
    
private:
    std::unique_ptr<Scope> scope_;
};

} // namespace prophet::dsl

