#pragma once

#include "prophet/common/types.hpp"
#include "prophet/dsl/ast.hpp"

#include <optional>
#include <string>
#include <vector>

namespace prophet::dsl {
class Context;
}

namespace prophet::functions {

struct WeightedConditionCall {
    std::vector<dsl::ASTNodePtr> conditions;
    double weight{0.0};
};

enum class FunctionCategory {
    Frame,
    Weight,
    Logic,
    Data,
    Math
};

struct FunctionCall {
    FunctionCategory category;
    std::string name;
    std::vector<dsl::ASTNodePtr> arguments;
    std::vector<dsl::ASTNodePtr> conditions;
    std::vector<WeightedConditionCall> weight_conditions;
    std::string timeframe;
    std::string field;
    std::string subfield;  // 子字段/子方法名
    std::vector<dsl::ASTNodePtr> submethod_params;  // 子方法参数（如果subfield是submethod）
    double threshold{0.0};
    double numeric_arg{0.0};
    bool has_numeric_arg{false};
};

struct FunctionResult {
    bool has_value{false};
    Value value{};

    static FunctionResult none() {
        return FunctionResult{};
    }

    static FunctionResult fromValue(const Value& v) {
        FunctionResult result;
        result.has_value = true;
        result.value = v;
        return result;
    }

    static FunctionResult fromNumber(double number) {
        return fromValue(Value::fromNumber(number));
    }

    static FunctionResult fromBoolean(bool boolean) {
        return fromValue(Value::fromBoolean(boolean));
    }

    static FunctionResult fromString(const std::string& str) {
        return fromValue(Value::fromString(str));
    }
};

} // namespace prophet::functions

