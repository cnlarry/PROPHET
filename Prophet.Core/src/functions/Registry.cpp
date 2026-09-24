/*
 * ============================================================================
 * 文件名：Registry.cpp
 * 功能说明：函数注册表实现
 * 
 * 这个文件实现了函数注册和查找机制
 * 
 * 工作原理：
 * - 使用单例模式，全局只有一个注册表实例
 * - 所有DSL函数（数学、数据、信号等）都注册到这个表中
 * - 求值时通过函数名查找对应的处理函数
 * 
 * 这是实现函数动态调用的关键组件
 * ============================================================================
 */

#include "prophet/functions/FunctionRegistry.hpp"

namespace prophet::functions {

FunctionRegistry& FunctionRegistry::instance() {
    static FunctionRegistry registry;
    return registry;
}

void FunctionRegistry::register_function(const std::string& name, FunctionHandler handler) {
    handlers_[name] = std::move(handler);
}

FunctionHandler FunctionRegistry::resolve(const std::string& name) const {
    auto it = handlers_.find(name);
    if (it == handlers_.end()) {
        return nullptr;
    }
    return it->second;
}

} // namespace prophet::functions

