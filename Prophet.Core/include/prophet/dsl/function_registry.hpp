#pragma once

#include "custom_function_ast.hpp"
#include <unordered_map>
#include <string>
#include <memory>
#include <optional>

namespace prophet::dsl {

// ========================================
// 前置声明
// ========================================
class TypeCheckContext;
class EvaluationContext;
class Scope;

// ========================================
// 函数注册表（管理所有自定义函数）
// ========================================
class FunctionRegistry {
public:
    FunctionRegistry() = default;
    ~FunctionRegistry() = default;
    
    // 禁止拷贝
    FunctionRegistry(const FunctionRegistry&) = delete;
    FunctionRegistry& operator=(const FunctionRegistry&) = delete;
    
    // 🆕 v4.0: 允许移动（用于从Parser移动到Engine）
    FunctionRegistry(FunctionRegistry&&) = default;
    FunctionRegistry& operator=(FunctionRegistry&&) = default;
    
    // ========================================
    // 函数注册和查询
    // ========================================
    
    /**
     * 注册自定义函数
     * @param name 函数名
     * @param func_def 函数定义节点
     * @throws std::runtime_error 如果函数已存在
     */
    void register_function(const std::string& name, std::shared_ptr<FunctionDefinitionNode> func_def);
    
    /**
     * 获取函数定义
     * @param name 函数名
     * @return 函数定义节点，如果不存在返回 nullptr
     */
    std::shared_ptr<FunctionDefinitionNode> get_function(const std::string& name) const;
    
    /**
     * 检查函数是否存在
     * @param name 函数名
     * @return 如果存在返回 true
     */
    bool has_function(const std::string& name) const;
    
    /**
     * 移除函数（用于测试或动态更新）
     * @param name 函数名
     * @return 如果成功移除返回 true
     */
    bool remove_function(const std::string& name);
    
    /**
     * 清空所有函数
     */
    void clear();
    
    // ========================================
    // 函数调用
    // ========================================
    
    /**
     * 调用自定义函数
     * @param name 函数名
     * @param arguments 实参值列表
     * @param ctx 求值上下文
     * @return 函数返回值
     * @throws std::runtime_error 如果函数不存在或参数不匹配
     */
    Value call_function(
        const std::string& name,
        const std::vector<Value>& arguments,
        EvaluationContext& ctx
    ) const;
    
    // ========================================
    // 类型检查
    // ========================================
    
    /**
     * 对所有已注册函数进行类型检查
     * @param ctx 类型检查上下文
     * @throws std::runtime_error 如果有类型错误
     */
    void type_check_all(TypeCheckContext& ctx) const;
    
    /**
     * 对单个函数进行类型检查
     * @param name 函数名
     * @param ctx 类型检查上下文
     * @throws std::runtime_error 如果有类型错误或函数不存在
     */
    void type_check_function(const std::string& name, TypeCheckContext& ctx) const;
    
    // ========================================
    // 调试和工具函数
    // ========================================
    
    /**
     * 获取所有已注册函数的名称
     * @return 函数名列表
     */
    std::vector<std::string> list_functions() const;
    
    /**
     * 获取函数的签名信息（用于文档生成和IDE提示）
     * @param name 函数名
     * @return 函数签名字符串，如果函数不存在返回空
     */
    std::optional<std::string> get_function_signature(const std::string& name) const;
    
    /**
     * 转换为字符串（调试用）
     * @return 注册表的字符串表示
     */
    std::string to_string() const;
    
private:
    std::unordered_map<std::string, std::shared_ptr<FunctionDefinitionNode>> functions_;
    
    /**
     * 创建函数调用作用域（将实参绑定到形参）
     * @param func_def 函数定义
     * @param arguments 实参值列表
     * @param parent_scope 父作用域（全局作用域）
     * @return 函数调用作用域
     * @throws std::runtime_error 如果参数数量不匹配或类型不兼容
     */
    std::unique_ptr<Scope> create_function_scope(
        const FunctionDefinitionNode& func_def,
        const std::vector<Value>& arguments,
        Scope& parent_scope
    ) const;
    
    /**
     * 执行函数体
     * @param body 函数体语句列表
     * @param scope 函数作用域
     * @param ctx 求值上下文
     * @return 函数返回值
     * @throws ReturnException 包含返回值
     */
    Value execute_function_body(
        const std::vector<StatementNodePtr>& body,
        Scope& scope,
        EvaluationContext& ctx
    ) const;
};

// ========================================
// Return 异常（用于实现 early return）
// ========================================
class ReturnException : public std::exception {
public:
    explicit ReturnException(Value val) : value(std::move(val)) {}
    
    Value value;
    
    const char* what() const noexcept override {
        return "Function return";
    }
};

// ========================================
// 全局函数注册表实例（单例）
// ========================================
FunctionRegistry& get_global_function_registry();

} // namespace prophet::dsl

