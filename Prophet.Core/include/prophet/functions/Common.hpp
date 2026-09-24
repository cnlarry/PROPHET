#pragma once

namespace prophet::functions {

class FunctionRegistry;

void register_all_functions(FunctionRegistry& registry);
void register_frame_functions(FunctionRegistry& registry);
void register_weight_functions(FunctionRegistry& registry);
void register_logic_functions(FunctionRegistry& registry);
void register_data_functions(FunctionRegistry& registry);
void register_math_functions(FunctionRegistry& registry);
void register_timeseries_functions(FunctionRegistry& registry);

} // namespace prophet::functions

