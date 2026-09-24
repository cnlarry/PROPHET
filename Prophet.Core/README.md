# Prophet.Core

Prophet.Core是一个高性能、模块化的核心引擎，为客户端提供强大的计算支持。内置Prophet DSL的支持，用户编写加密货币量化交易系统，支持高达62个技术指标计算和30个功能强大的数据函数（截至2025年12月1日），并提供Python和C# API接口。

## 主要功能

### 核心引擎
- 高性能策略执行引擎，支持多时间框架
- 强大的DSL（领域特定语言），用于编写交易策略
- 支持实时和回测两种模式
- 内置30+种技术指标（基于TA-Lib）

### 模块化设计
- 采用7个独立DLL模块，实现高内聚低耦合
- 支持动态加载和扩展
- 清晰的依赖关系，便于维护和扩展

### 多语言支持
- Python API：用于策略开发和回测
- C# API：用于客户端应用集成
- C++ 核心：高性能计算

### 技术特性
- SIMD优化，支持AVX2指令集
- JIT编译，提高执行效率
- 多线程并行计算
- 内存池管理，减少内存分配开销

## 架构设计

### 模块结构

Prophet.Core采用模块化设计，包含7个核心DLL模块：

1. **prophet_core_common.dll** - 公共模块
   - 核心类型定义
   - 线程池
   - SIMD优化
   - 性能监控

2. **prophet_core_logging.dll** - 日志模块
   - 统一日志系统
   - 支持多级别日志
   - 同时输出到控制台和文件

3. **prophet_core_cache.dll** - 缓存模块
   - 指标缓存
   - 参数存储
   - K线数据管理
   - 转换缓存

4. **prophet_core_indicators.dll** - 指标模块
   - 为策略提供技术指标计算
   - 为客户端绘制指标线提供技术指标计算
   - 指标注册表
   - 并行计算支持
   - TA-Lib集成

5. **prophet_core_functions.dll** - 函数模块
   - 数据函数
   - 逻辑函数
   - 序列函数
   - 模式识别

6. **prophet_core_dsl.dll** - DSL模块
   - 词法分析器
   - 语法分析器
   - 表达式求值器
   - 字节码编译器和虚拟机
   - JIT编译

7. **prophet_core_engine.dll** - 引擎模块
   - 策略执行引擎
   - 上下文管理
   - 信号生成

### 依赖关系

```
prophet_core_engine
└── prophet_core_dsl
    ├── prophet_core_functions
    ├── prophet_core_indicators
    ├── prophet_core_cache
    └── prophet_core_logging
        └── prophet_core_common
```

## 安装和编译

### 环境要求
- Visual Studio 2022/2026
- CMake 3.15+
- Python 3.10+
- TA-Lib 0.4.0+

### 编译步骤

1. **克隆仓库**
   ```bash
   git clone <repository-url>
   cd Prophet.Core
   ```

2. **运行构建脚本**
   ```bash
   build.bat
   ```
   
   构建脚本支持以下选项：
   - `--debug` - 仅编译Debug版本
   - `--release` - 仅编译Release版本
   - `--both` - 编译Debug和Release版本（默认）
   - `--help` - 显示帮助信息

3. **手动编译**
   ```bash
   mkdir build
   cd build
   cmake .. -G "Ninja" -DCMAKE_BUILD_TYPE=Release
   cmake --build .
   ```

## 使用方法

### Python API

```python
import sys
sys.path.insert(0, 'Prophet.Core')
import Prophet

# 创建引擎
dsl_code = "ALL{1=1}=HOLD;"
engine = Prophet.Engine(dsl_code)

# 设置K线数据
# engine.set_klines(...)  # 设置OHLCV数据

# 获取信号
price = 100.0
time = 1620000000000
signal = engine.get_signal(price, time)
print(f"Signal: {signal}")
```

### C# API

```csharp
// 加载prophet_core.dll
// 创建引擎
IntPtr engine = ProphetCore.CreateEngine(dslCode);

// 设置K线数据
// ProphetCore.SetKlines(engine, ...);

// 获取信号
Signal signal = ProphetCore.GetSignal(engine, price, time);

// 释放资源
ProphetCore.DestroyEngine(engine);
```

### DSL语法示例

```
// 简单的MA交叉策略
MA5 = MA(CLOSE, 5);
MA20 = MA(CLOSE, 20);
BUY_CONDITION = CROSS(MA5, MA20);
SELL_CONDITION = CROSS(MA20, MA5);

ALL{BUY_CONDITION}=BUY;
ALL{SELL_CONDITION}=SELL;
ALL{1=1}=HOLD;
```

## 模块详细说明

### 1. 公共模块 (prophet_core_common)

**核心类型**
- `SignalAction`: 信号类型（BUY/SELL/HOLD）
- `Value`: 通用值类型
- `Kline`: K线数据结构
- `IndicatorResult`: 指标结果

**并行计算**
- 线程池实现
- 并行任务调度
- 原子操作支持

### 2. 日志模块 (prophet_core_logging)

**日志级别**
- TRACE: 最详细的追踪信息
- DEBUG: 调试信息
- INFO: 一般信息
- WARN: 警告
- ERROR: 错误
- FATAL: 致命错误
- OFF: 关闭日志

**使用示例**
```cpp
#include "prophet/logging/logger.hpp"

PROPHET_LOG_INFO("Module", "This is an info message");
PROPHET_LOG_ERROR("Module", "This is an error message");
```

### 3. 缓存模块 (prophet_core_cache)

**主要组件**
- `IndicatorCache`: 指标结果缓存
- `ParameterStore`: 参数存储
- `KlineManager`: K线数据管理
- `KlineConversionCache`: K线转换缓存

### 4. 指标模块 (prophet_core_indicators)

**支持的指标类型**
```
有两种需要指标计算的情形：

情形1：DSL模块通过解析用dsl编写的策略，根据需要计算指标；
情形2：客户端基于绘制指标线的需要，通过c_api计算指标；

指标名称、指标参数、指标字段全部统一；
```

- 趋势指标：MA, EMA, WMA, ADX, SAR
- 动量指标：MACD, RSI, STOCH, CCI
- 波动率指标：ATR, BOLL, Keltner
- 成交量指标：OBV, CMF, ADL

### 5. 函数模块 (prophet_core_functions)

**函数分类**
- 数据函数：PRICE, VOLUME, HIGH, LOW
- 数学函数：ADD, SUB, MUL, DIV
- 逻辑函数：AND, OR, NOT, IF
- 序列函数：MA, EMA, CROSS
- 模式识别：FVG, OrderBlock, PremiumDiscount

### 6. DSL模块 (prophet_core_dsl)

**DSL特性**
- 支持变量定义
- 支持函数调用
- 支持条件语句
- 支持自定义函数
- 支持多时间框架

### 7. 引擎模块 (prophet_core_engine)

**核心功能**
- 策略解析和编译
- K线数据管理
- 指标计算
- 信号生成
- 性能监控

## 性能优化

1. **SIMD优化**
   - 使用AVX2指令集加速计算
   - 支持SIMD和标量自动切换
   - 优化的K线处理算法

2. **JIT编译**
   - 将DSL编译为机器码
   - 减少解释执行开销
   - 支持动态优化

3. **缓存机制**
   - 指标结果缓存
   - K线转换缓存
   - 参数缓存

4. **并行计算**
   - 多线程指标计算
   - 并行K线处理
   - 任务调度优化

## 开发指南

### 代码风格
- 遵循C++17标准
- 使用4空格缩进
- 类名采用PascalCase
- 函数名和变量名采用camelCase
- 常量名采用全大写，下划线分隔

### 编译选项
- Debug模式：包含调试信息，禁用优化
- Release模式：启用O2优化，禁用调试信息
- 支持AVX2指令集
- 启用多线程编译

### 测试
- 单元测试：使用Google Test
- 集成测试：测试模块间交互
- 性能测试：测试执行效率

## 贡献指南

1. **提交代码**
   - 遵循代码风格
   - 编写单元测试
   - 提交前运行测试

2. **报告问题**
   - 提供详细的错误信息
   - 包含复现步骤
   - 提供相关日志

3. **功能请求**
   - 描述功能需求
   - 说明使用场景
   - 提供设计建议

## 许可证

Prophet.Core采用MIT许可证，详见LICENSE文件。

## 联系方式

- 项目主页：<https://github.com/cnlarry/Prophet>
- 问题反馈：<https://github.com/cnlarry/Prophet/issues>（安全问题请使用私密报告渠道）

## 版本历史

### v1.0.0 (当前版本)
- 初始版本
- 7个模块化DLL架构
- Python和C# API支持
- TA-Lib集成
- SIMD和JIT优化

## 致谢

- TA-Lib：提供技术指标计算
- pybind11：Python绑定
- CMake：构建系统
- Ninja：快速构建工具

## 附录

### 目录结构

```
Prophet.Core/
├── include/          # 头文件
│   └── prophet/      # 命名空间目录
├── src/              # 源代码
│   ├── common/       # 公共模块
│   ├── logging/      # 日志模块
│   ├── cache/        # 缓存模块
│   ├── indicators/   # 指标模块
│   ├── functions/    # 函数模块
│   ├── dsl/          # DSL模块
│   ├── core/         # 引擎模块
│   ├── strategy/     # 策略模块
│   ├── bytecode/     # 字节码模块
│   ├── simd/         # SIMD优化
│   ├── parallel/     # 并行计算
│   ├── tools/        # 工具模块
│   └── c_api/        # C API
├── bindings/         # 语言绑定
├── build/            # 构建目录
├── CMakeLists.txt    # CMake配置
├── build.bat         # 构建脚本
└── README.md         # 项目文档
```

### 依赖关系

| 模块 | 依赖模块 |
|------|----------|
| prophet_core_common | - |
| prophet_core_logging | prophet_core_common |
| prophet_core_cache | prophet_core_common, prophet_core_logging |
| prophet_core_indicators | prophet_core_common, prophet_core_logging, prophet_core_cache |
| prophet_core_functions | prophet_core_common, prophet_core_logging, prophet_core_cache, prophet_core_indicators |
| prophet_core_dsl | prophet_core_common, prophet_core_logging, prophet_core_cache, prophet_core_indicators, prophet_functions, prophet_tools |
| prophet_core_engine | prophet_core_common, prophet_core_logging, prophet_core_dsl, prophet_core_cache |
| prophet_core | prophet_functions, prophet_tools, prophet_core_common, prophet_core_logging, prophet_core_engine, prophet_core_dsl, prophet_core_indicators, prophet_core_cache |

---

**Prophet.Core** - 高性能策略引擎核心
