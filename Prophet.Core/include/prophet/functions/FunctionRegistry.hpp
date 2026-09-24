#pragma once

#include "prophet/functions/FunctionTypes.hpp"

#include <functional>
#include <memory>
#include <string>
#include <unordered_map>

namespace prophet::dsl {
class Context;
}

namespace prophet::functions {

using FunctionHandler = std::function<FunctionResult(const FunctionCall&, prophet::dsl::Context&)>;

class FunctionRegistry {
public:
    static FunctionRegistry& instance();

    void register_function(const std::string& name, FunctionHandler handler);

    FunctionHandler resolve(const std::string& name) const;

private:
    FunctionRegistry() = default;

    std::unordered_map<std::string, FunctionHandler> handlers_;
};

} // namespace prophet::functions

