cmake_minimum_required(VERSION 3.15)
project(ProphetCore VERSION 1.0.0 LANGUAGES CXX)

# CMake 策略设置
if(POLICY CMP0177)
    cmake_policy(SET CMP0177 NEW)
endif()

# 生成 compile_commands.json 供 clangd 使用
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# C++17 标准
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# 编译选项
if(MSVC)
    add_compile_options(/W4 /utf-8)
    # 禁用特定警告
    add_compile_options(/wd4251 /wd4275)
    # 将以下警告视为错误（提升代码质量）
    # C4100: 未引用的形参
    # C4101: 未引用的局部变量
    # C4189: 局部变量已初始化但未引用
    # C4267: size_t 到 int 的转换
    add_compile_options(/we4100 /we4101 /we4189)
    # 对C4267保持警告但不视为错误（因为有时确实需要转换）
    # 启用AVX2支持（性能优化）
    add_compile_options(/arch:AVX2)
else()
    add_compile_options(-Wall -Wextra -Wpedantic)
    # 未使用参数/变量视为错误
    add_compile_options(-Werror=unused-parameter -Werror=unused-variable)
    # 启用AVX2支持（性能优化）
    add_compile_options(-mavx2 -mfma)
endif()

# ============================================================================
# 可选功能
# ============================================================================

# TA-Lib 支持（可选）
option(USE_TALIB "Use TA-Lib for indicator calculations" ON)

# ============================================================================
# 查找依赖
# ============================================================================

# Python
find_package(Python COMPONENTS Interpreter Development REQUIRED)

# PyBind11
find_package(pybind11 QUIET)
if(NOT pybind11_FOUND)
    message(STATUS "PyBind11 not found via CMake, trying Python package...")
    execute_process(
        COMMAND ${Python_EXECUTABLE} -m pybind11 --cmakedir
        OUTPUT_VARIABLE pybind11_DIR
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
    )
    if(pybind11_DIR)
        message(STATUS "Found PyBind11 via Python: ${pybind11_DIR}")
        find_package(pybind11 REQUIRED PATHS ${pybind11_DIR})
    else()
        message(FATAL_ERROR "PyBind11 not found. Please install: pip install pybind11")
    endif()
endif()

# TA-Lib（可选）
if(USE_TALIB)
    # 方法 1: 尝试使用 vcpkg 安装的 TA-Lib
    find_package(ta-lib CONFIG QUIET)

    if(ta-lib_FOUND)
        message(STATUS "Found TA-Lib via vcpkg")
        set(TALIB_FOUND TRUE)
        set(TALIB_LIBRARIES ta-lib::ta-lib)
    else()
        # 方法 2: 手动查找（预编译二进制）
        # 尝试两种可能的头文件路径
        find_path(TALIB_INCLUDE_DIR
            NAMES ta-lib/ta_libc.h ta_libc.h
            PATHS
                "C:/ta-lib/include"
                "C:/ta-lib"
                "C:/Program Files/ta-lib/include"
                "$ENV{TALIB_ROOT}/include"
                "$ENV{TALIB_ROOT}"
        )

        find_library(TALIB_LIBRARY
            NAMES ta-lib-static ta-lib ta_lib ta_libc_cdr ta_libc
            PATHS
                "C:/ta-lib/lib"
                "C:/Program Files/ta-lib/lib"
                "$ENV{TALIB_ROOT}/lib"
        )

        if(TALIB_INCLUDE_DIR AND TALIB_LIBRARY)
            message(STATUS "Found TA-Lib manually: ${TALIB_LIBRARY}")
            set(TALIB_FOUND TRUE)
            set(TALIB_LIBRARIES ${TALIB_LIBRARY})
            set(TALIB_INCLUDE_DIRS ${TALIB_INCLUDE_DIR})
        else()
            message(FATAL_ERROR
                "TA-Lib is REQUIRED! Prophet.Core requires TA-Lib for accurate indicator calculations.\n"
                "TA-Lib is the industry-standard technical analysis library.\n\n"
                "Installation options:\n"
                "  Option 1 (Recommended): vcpkg install ta-lib:x64-windows-static\n"
                "  Option 2: Download from https://ta-lib.org/\n\n"
                "After installation, ensure TALIB_ROOT environment variable is set."
            )
        endif()
    endif()
else()
    message(FATAL_ERROR
        "TA-Lib is REQUIRED! Prophet.Core cannot be built without TA-Lib.\n"
        "Please enable TA-Lib support: cmake -DUSE_TALIB=ON ..\n\n"
        "Install TA-Lib: vcpkg install ta-lib:x64-windows-static"
    )
endif()

# ============================================================================
# 模块化配置
# ============================================================================

# 辅助函数
set(INDICATOR_UTILS_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/src/indicators/calculateMomentum.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/indicators/calculateSlope.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/indicators/detectCrossover.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/indicators/detectZeroCross.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/indicators/determineTrend.cpp
)

# 只编译已注册的指标（在 indicator_registrations.cpp 中）
set(REGISTERED_INDICATORS
    # Momentum indicators
    src/indicators/momentum/MACD.cpp
    src/indicators/momentum/RSI.cpp
    src/indicators/momentum/STOCHRSI.cpp
    src/indicators/momentum/CCI.cpp
    src/indicators/momentum/MFI.cpp
    src/indicators/momentum/WR.cpp
    src/indicators/momentum/ROC.cpp
    src/indicators/momentum/CMO.cpp
    src/indicators/momentum/STOCHF.cpp
    src/indicators/momentum/KDJ.cpp
    src/indicators/momentum/MTM.cpp
    src/indicators/momentum/ULTOSC.cpp
    src/indicators/momentum/PPO.cpp
    src/indicators/momentum/APO.cpp
    src/indicators/momentum/TSI.cpp
    src/indicators/momentum/ElderRay.cpp
    src/indicators/momentum/SchaffTrendCycle.cpp
    src/indicators/momentum/FisherTransform.cpp
    # Trend indicators
    src/indicators/trend/MA.cpp
    src/indicators/trend/EMA.cpp
    src/indicators/trend/WMA.cpp
    src/indicators/trend/DEMA.cpp
    src/indicators/trend/TEMA.cpp
    src/indicators/trend/KAMA.cpp
    src/indicators/trend/T3.cpp
    src/indicators/trend/MAMA.cpp
    src/indicators/trend/ADX.cpp
    src/indicators/trend/SAR.cpp
    src/indicators/trend/AROON.cpp
    src/indicators/trend/Ichimoku.cpp
    src/indicators/trend/DMI.cpp
    src/indicators/trend/Supertrend.cpp
    src/indicators/trend/DonchianChannel.cpp
    src/indicators/trend/VWMA.cpp
    src/indicators/trend/ChandelierExit.cpp
    src/indicators/trend/ChoppinessIndex.cpp
    src/indicators/trend/Alligator.cpp
    src/indicators/trend/McGinleyDynamic.cpp
    src/indicators/trend/ParabolicTP.cpp
    # Volatility indicators
    src/indicators/volatility/ATR.cpp
    src/indicators/volatility/BOLL.cpp
    src/indicators/volatility/Keltner.cpp
    src/indicators/volatility/NATR.cpp
    src/indicators/volatility/HV.cpp
    src/indicators/volatility/BollingerBW.cpp
    # Volume indicators
    src/indicators/volume/CMF.cpp
    src/indicators/volume/EMV.cpp
    src/indicators/volume/OBV.cpp
    src/indicators/volume/ADOSC.cpp
    src/indicators/volume/ADL.cpp
    src/indicators/volume/VWAPBands.cpp
    src/indicators/volume/ForceIndex.cpp
    src/indicators/volume/VolumePriceTrend.cpp
    # Price indicators
    src/indicators/price/VWAP.cpp
    src/indicators/price/TRIX.cpp
    src/indicators/price/PivotPoints.cpp
    # Pattern indicators
    src/indicators/pattern/SwingHL.cpp
    src/indicators/pattern/MSB.cpp
    src/indicators/pattern/HHLL.cpp
    # Statistics indicators
    src/indicators/statistics/StdDev.cpp
    src/indicators/statistics/LinearRegSlope.cpp
    src/indicators/statistics/KaufmanER.cpp
)

# ============================================================================
# 1. 公共模块 (prophet_core_common.dll)
# ============================================================================
set(COMMON_SOURCES
    src/common/performance_metrics.cpp
    src/parallel/thread_pool.cpp
    src/parallel/immutable_kline.cpp
    src/parallel/kline_converter.cpp
    src/simd/simd_config.cpp
    src/simd/simd_math.cpp
    src/simd/simd_indicators.cpp
    src/simd/kline_simd.cpp
)

set(COMMON_HEADERS
    include/prophet/common/types.hpp
    include/prophet/common/signal.hpp
    include/prophet/common/performance_metrics.hpp
    include/prophet/parallel/thread_pool.hpp
    include/prophet/parallel/immutable_kline.hpp
    include/prophet/parallel/kline_converter.hpp
    include/prophet/simd/simd_config.hpp
    include/prophet/simd/simd_math.hpp
    include/prophet/simd/simd_indicators.hpp
    include/prophet/simd/kline_simd.hpp
)

add_library(prophet_core_common SHARED
    ${COMMON_SOURCES}
    ${COMMON_HEADERS}
)

target_include_directories(prophet_core_common PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# 链接依赖
target_link_libraries(prophet_core_common PUBLIC
    prophet_tools
)

# 定义导出宏
target_compile_definitions(prophet_core_common PRIVATE PROPHET_COMMON_EXPORTS)

# 设置DLL属性
set_target_properties(prophet_core_common PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}
)

# ============================================================================
# 2. 日志模块 (prophet_core_logging.dll)
# ============================================================================
set(LOGGING_SOURCES
    src/logging/logger.cpp
)

set(LOGGING_HEADERS
    include/prophet/logging/logger.hpp
)

add_library(prophet_core_logging SHARED
    ${LOGGING_SOURCES}
    ${LOGGING_HEADERS}
)

target_include_directories(prophet_core_logging PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core_logging PUBLIC
    prophet_core_common
)

# 定义导出宏
target_compile_definitions(prophet_core_logging PRIVATE PROPHET_LOGGING_EXPORTS)

# 设置DLL属性
set_target_properties(prophet_core_logging PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}
)

# ============================================================================
# 3. 缓存模块 (prophet_core_cache.dll)
# ============================================================================
set(CACHE_SOURCES
    src/cache/indicator_cache.cpp
    src/cache/parameter_store.cpp
    src/cache/kline_manager.cpp
    src/cache/kline_conversion_cache.cpp
)

set(CACHE_HEADERS
    include/prophet/cache/indicator_cache.hpp
    include/prophet/cache/parameter_store.hpp
    include/prophet/cache/kline_manager.hpp
    include/prophet/cache/kline_conversion_cache.hpp
)

add_library(prophet_core_cache SHARED
    ${CACHE_SOURCES}
    ${CACHE_HEADERS}
)

target_include_directories(prophet_core_cache PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core_cache PUBLIC
    prophet_core_common
    prophet_core_logging
)

# 定义导出宏
target_compile_definitions(prophet_core_cache PRIVATE PROPHET_CACHE_EXPORTS)

# 设置DLL属性
set_target_properties(prophet_core_cache PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}
)

# ============================================================================
# 4. 指标模块 (prophet_core_indicators.dll)
# ============================================================================
set(INDICATORS_SOURCES
    src/indicators/registry.cpp
    src/indicators/indicator_registrations.cpp
    src/indicators/parallel_calculator.cpp
    ${REGISTERED_INDICATORS}
    ${INDICATOR_UTILS_SOURCES}
)

set(INDICATORS_HEADERS
    include/prophet/indicators/calculator.hpp
    include/prophet/indicators/registry.hpp
)

add_library(prophet_core_indicators SHARED
    ${INDICATORS_SOURCES}
    ${INDICATORS_HEADERS}
)

target_include_directories(prophet_core_indicators PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core_indicators PUBLIC
    prophet_core_common
    prophet_core_logging
    prophet_core_cache
)

# 链接 TA-Lib（如果找到）
if(TALIB_FOUND)
    target_compile_definitions(prophet_core_indicators PUBLIC USE_TALIB)
    target_link_libraries(prophet_core_indicators PUBLIC ${TALIB_LIBRARIES})

    if(TALIB_INCLUDE_DIRS)
        target_include_directories(prophet_core_indicators PUBLIC ${TALIB_INCLUDE_DIRS})
    endif()

    message(STATUS "TA-Lib integration enabled")
else()
    message(STATUS "Using simplified indicator algorithms (no TA-Lib)")
endif()

# 定义导出宏
target_compile_definitions(prophet_core_indicators PRIVATE PROPHET_INDICATORS_EXPORTS)

# 设置DLL属性
set_target_properties(prophet_core_indicators PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}
)

# ============================================================================
# 5. 函数模块 (prophet_core_functions.dll)
# ============================================================================
add_subdirectory(src/functions)
add_subdirectory(src/tools)

# 注意：Base/Pattern/Sequence 三份源码同时存在于 prophet_functions 静态库中，
# prophet_core_functions(共享DLL) 直接编译它们，prophet_core_static 不再编译
# （符号通过链接 prophet_functions 解析），避免 pyd 用 /WHOLEARCHIVE 时重复。
set(FUNCTIONS_SOURCES
    src/functions/Base.cpp
    src/functions/Pattern.cpp
    src/functions/Sequence.cpp
)

set(FUNCTIONS_HEADERS
    include/prophet/functions/Base.hpp
    include/prophet/functions/Pattern.hpp
    include/prophet/functions/Sequence.hpp
    include/prophet/functions/FunctionRegistry.hpp
    include/prophet/functions/FunctionTypes.hpp
)

add_library(prophet_core_functions SHARED
    ${FUNCTIONS_SOURCES}
    ${FUNCTIONS_HEADERS}
)

target_include_directories(prophet_core_functions PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core_functions PUBLIC
    prophet_core_common
    prophet_core_logging
    prophet_core_indicators
    prophet_core_cache
    prophet_core_dsl
    prophet_functions
    prophet_tools
)

# 定义导出宏
target_compile_definitions(prophet_core_functions PRIVATE PROPHET_FUNCTIONS_EXPORTS)

# 设置DLL属性
set_target_properties(prophet_core_functions PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}
)

# ============================================================================
# 6. DSL模块 (prophet_core_dsl.dll)
# ============================================================================
set(DSL_SOURCES
    src/dsl/lexer.cpp
    src/dsl/parser.cpp
    src/dsl/evaluator.cpp
    src/dsl/custom_function_ast.cpp
    src/dsl/scope.cpp
    src/dsl/function_registry.cpp
    src/dsl/type_checker.cpp
    src/bytecode/compiler.cpp
    src/bytecode/vm.cpp
    src/bytecode/jit.cpp
    src/bytecode/optimizer.cpp
    src/bytecode/compiler_cache.cpp
    src/core/context.cpp
)

set(DSL_HEADERS
    include/prophet/dsl/lexer.hpp
    include/prophet/dsl/parser.hpp
    include/prophet/dsl/ast.hpp
    include/prophet/dsl/custom_function_ast.hpp
    include/prophet/dsl/scope.hpp
    include/prophet/dsl/function_registry.hpp
    include/prophet/dsl/type_checker.hpp
    include/prophet/dsl/evaluation_context.hpp
    include/prophet/bytecode/compiler.hpp
    include/prophet/bytecode/vm.hpp
    include/prophet/bytecode/jit.hpp
    include/prophet/bytecode/optimizer.hpp
    include/prophet/bytecode/compiler_cache.hpp
)

add_library(prophet_core_dsl SHARED
    ${DSL_SOURCES}
    ${DSL_HEADERS}
)

target_include_directories(prophet_core_dsl PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core_dsl PUBLIC
    prophet_core_common
    prophet_core_logging
    prophet_core_indicators
    prophet_core_cache
    prophet_functions
    prophet_tools
)

# 定义导出宏
target_compile_definitions(prophet_core_dsl PRIVATE PROPHET_DSL_EXPORTS)

# 设置DLL属性
set_target_properties(prophet_core_dsl PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}
)

# ============================================================================
# 7. 引擎模块 (prophet_core_engine.dll)
# ============================================================================
set(ENGINE_SOURCES
    src/core/engine.cpp
)

set(ENGINE_HEADERS
    include/prophet/core/context.hpp
    include/prophet/core/engine.hpp
)

add_library(prophet_core_engine SHARED
    ${ENGINE_SOURCES}
    ${ENGINE_HEADERS}
)

target_include_directories(prophet_core_engine PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core_engine PUBLIC
    prophet_core_common
    prophet_core_logging
    prophet_core_dsl
    prophet_core_cache
)

# 定义导出宏
target_compile_definitions(prophet_core_engine PRIVATE PROPHET_ENGINE_EXPORTS)

# 设置DLL属性
set_target_properties(prophet_core_engine PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}
)

# ============================================================================
# 静态库（用于Python扩展和C API）
# ============================================================================
add_library(prophet_core_static STATIC
    ${COMMON_SOURCES}
    ${LOGGING_SOURCES}
    ${CACHE_SOURCES}
    ${INDICATORS_SOURCES}
    ${DSL_SOURCES}
    ${ENGINE_SOURCES}
    ${COMMON_HEADERS}
    ${LOGGING_HEADERS}
    ${CACHE_HEADERS}
    ${INDICATORS_HEADERS}
    ${FUNCTIONS_HEADERS}
    ${DSL_HEADERS}
    ${ENGINE_HEADERS}
)

target_include_directories(prophet_core_static PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core_static PUBLIC
    prophet_functions
    prophet_tools
)

# 链接 TA-Lib（如果找到）
if(TALIB_FOUND)
    target_compile_definitions(prophet_core_static PUBLIC USE_TALIB)
    target_link_libraries(prophet_core_static PUBLIC ${TALIB_LIBRARIES})

    if(TALIB_INCLUDE_DIRS)
        target_include_directories(prophet_core_static PUBLIC ${TALIB_INCLUDE_DIRS})
    endif()
endif()

# ============================================================================
# Python 扩展模块 - 统一模块 Prophet.Core
# ============================================================================

# Prophet.Core - 统一的 Python 模块（包含引擎、指标、数据函数、K线工具）
pybind11_add_module(Prophet
    bindings/python_bindings.cpp
)

target_link_libraries(Prophet PRIVATE
    prophet_core_static
    prophet_tools
)

# MSVC 下默认会裁剪静态库中未被直接引用的符号（包括仅靠静态初始化注册的
# 指标 REGISTER_INDICATOR / 函数 register_all_functions 等），导致 Python
# 绑定运行时出现 "Unsupported indicator for auto-calculation"。
# 使用 /WHOLEARCHIVE 强制保留静态库所有目标文件中的初始化逻辑。
# 注意：prophet_core_static 不再包含 Base/Pattern/Sequence（已移交
# prophet_functions），二者无重复符号，故都可安全加 /WHOLEARCHIVE。
if(MSVC)
    target_link_options(Prophet PRIVATE "/WHOLEARCHIVE:prophet_core_static.lib")
    target_link_options(Prophet PRIVATE "/WHOLEARCHIVE:prophet_functions.lib")
    target_link_options(Prophet PRIVATE "/WHOLEARCHIVE:prophet_tools.lib")
endif()

target_include_directories(Prophet PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

set_target_properties(Prophet PROPERTIES
    OUTPUT_NAME "Prophet"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/$<CONFIG>"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/$<CONFIG>"
    # 禁用MSVC自动添加的子目录
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/Release"
    LIBRARY_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/Debug"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/Release"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/Debug"
)

# ============================================================================
# C API 动态库（供C#调用）
# ============================================================================

# C API 源文件
set(C_API_SOURCES
    src/c_api/c_api.cpp
    src/c_api/strategy_api.cpp
    src/c_api/logger_api.cpp
)

set(C_API_HEADERS
    include/prophet/c_api.h
)

add_library(prophet_core SHARED
    src/tools/kline_converter.cpp
    ${PROPHET_CORE_SOURCES}
    ${C_API_SOURCES}
    ${C_API_HEADERS}
)

target_include_directories(prophet_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(prophet_core PUBLIC
    prophet_functions
    prophet_tools
    prophet_core_common
    prophet_core_logging
    prophet_core_engine
    prophet_core_dsl
    prophet_core_indicators
    prophet_core_cache
    ${TALIB_LIBRARIES}
)

if(TALIB_FOUND)
    target_compile_definitions(prophet_core PUBLIC USE_TALIB)
    if(TALIB_INCLUDE_DIRS)
        target_include_directories(prophet_core PUBLIC ${TALIB_INCLUDE_DIRS})
    endif()
endif()

# 定义导出宏（用于C API）
target_compile_definitions(prophet_core PRIVATE PROPHET_CORE_EXPORTS)

# 设置DLL导出
set_target_properties(prophet_core PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON  # 改为ON，自动导出所有符号
    # 根据构建类型设置输出目录（适配 Prophet.Client 的期望路径）
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin/Debug
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin/Release
)

message(STATUS "C API for C# support enabled (prophet_core.dll)")

# ============================================================================
# 安装配置
# ============================================================================

# 将编译好的 Python 模块安装到 site-packages
install(TARGETS Prophet
    LIBRARY DESTINATION ${Python_SITELIB}
)

# 安装C API动态库
install(TARGETS prophet_core
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

# 注意：Python 包文件已移除，直接使用 C++ 扩展模块
# 测试文件已迁移到项目根目录的 tests/ 文件夹

# ============================================================================
# 输出信息
# ============================================================================

message(STATUS "========================================")
message(STATUS "Prophet Core Configuration")
message(STATUS "========================================")
message(STATUS "Version:        ${PROJECT_VERSION}")
message(STATUS "C++ Compiler:   ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}")
message(STATUS "C++ Standard:   C++${CMAKE_CXX_STANDARD}")
message(STATUS "Python:         ${Python_VERSION}")
message(STATUS "Python Include: ${Python_INCLUDE_DIRS}")
message(STATUS "Install Prefix: ${CMAKE_INSTALL_PREFIX}")
message(STATUS "========================================")
