/*
 * ============================================================================
 * 文件名：Common.cpp
 * 功能说明：函数注册汇总
 * 
 * 这个文件负责注册所有类别的函数到函数注册表
 * 
 * 注册的函数类别：
 * - Frame functions：信号框架函数（ALL、ANY等）
 * - Weight functions：加权函数（WEIGHTED）
 * - Logic functions：逻辑函数（NOT）
 * - Data functions：数据函数（KLINE、PRICE等）
 * - Math functions：数学函数（ABS、POW等）
 * 
 * 这是系统初始化时的入口函数
 * ============================================================================
 */

#include "prophet/functions/Common.hpp"
#include "prophet/functions/FunctionRegistry.hpp"

namespace prophet::functions {

void register_all_functions(FunctionRegistry& registry) {
    static bool initialized = false;
    if (initialized) {
        return;
    }
    initialized = true;
    register_frame_functions(registry);
    register_weight_functions(registry);
    register_logic_functions(registry);
    register_data_functions(registry);
    register_math_functions(registry);
    register_timeseries_functions(registry);
}

} // namespace prophet::functions

