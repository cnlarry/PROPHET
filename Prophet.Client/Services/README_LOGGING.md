# Prophet Core 日志系统使用指南

## 概述

Prophet Core 提供了一个统一的日志系统，支持多级别日志、控制台和文件输出、线程安全等特性。

## 日志级别

- **TRACE**: 最详细的追踪信息（仅用于深度调试）
- **DEBUG**: 调试信息
- **INFO**: 一般信息（默认级别）
- **WARN**: 警告
- **ERROR**: 错误
- **FATAL**: 致命错误
- **OFF**: 关闭所有日志

## C# 使用方法

### 1. 初始化日志系统

```csharp
using Prophet.Client.Services;

// 默认配置（INFO级别，控制台+文件输出）
LogService.Initialize();

// 自定义配置
LogService.Initialize(
    level: LogService.LogLevel.DEBUG,
    enableConsole: true,
    enableFile: true,
    logFilePath: "logs/prophet_core.log"
);
```

### 2. 写入日志

```csharp
// 不同级别的日志
LogService.Trace("Module", "详细追踪信息");
LogService.Debug("Module", "调试信息");
LogService.Info("Module", "一般信息");
LogService.Warn("Module", "警告信息");
LogService.Error("Module", "错误信息");
LogService.Fatal("Module", "致命错误");
```

### 3. 配置日志

```csharp
// 设置日志级别
LogService.SetLevel(LogService.LogLevel.DEBUG);

// 启用/禁用控制台输出
LogService.SetConsoleOutput(true);

// 启用/禁用文件输出
LogService.SetFileOutput(true);

// 设置日志文件
LogService.SetLogFile("logs/my_log.log");

// 刷新日志缓冲区
LogService.Flush();

// 关闭日志文件
LogService.CloseLogFile();
```

## C++ 使用方法

### 1. 引入头文件

```cpp
#include "prophet/logger.hpp"
```

### 2. 使用宏写入日志

```cpp
using namespace prophet;

// 使用宏（推荐）
PROPHET_LOG_TRACE("Module", "追踪信息");
PROPHET_LOG_DEBUG("Module", "调试信息");
PROPHET_LOG_INFO("Module", "一般信息");
PROPHET_LOG_WARN("Module", "警告信息");
PROPHET_LOG_ERROR("Module", "错误信息");
PROPHET_LOG_FATAL("Module", "致命错误");

// 带格式化的宏
PROPHET_LOG_INFO_FMT("Engine", "引擎创建成功，地址: " << engine_ptr);
PROPHET_LOG_DEBUG_FMT("Kline", "设置K线数据: " << timeframe << ", 数量: " << count);
```

### 3. 使用Logger类

```cpp
auto& logger = Logger::instance();

// 配置日志
logger.set_level(LogLevel::DEBUG);
logger.set_log_file("prophet_core.log");
logger.set_console_output(true);
logger.set_file_output(true);

// 写入日志
logger.info("Module", "一般信息");
logger.warn("Module", "警告信息");
logger.error("Module", "错误信息");

// 刷新缓冲区
logger.flush();

// 关闭日志文件
logger.close_log_file();
```

## 日志输出格式

```
[2025-11-26 10:30:15.123] [INFO ] [Engine  ] 引擎初始化成功: 3 条规则
[2025-11-26 10:30:15.234] [DEBUG] [Kline   ] 设置5m时间框架K线: 300根
[2025-11-26 10:30:15.345] [TRACE] [Signal  ] 计算信号: price=50000.00
[2025-11-26 10:30:15.456] [WARN ] [Engine  ] 指标计算超时: RSI(14)
[2025-11-26 10:30:15.567] [ERROR] [Parser  ] DSL语法错误: 第10行
```

格式说明：
- `[时间戳]`: 精确到毫秒
- `[级别]`: 5个字符固定宽度
- `[模块]`: 8个字符固定宽度
- 消息内容

## 最佳实践

### 1. 选择合适的日志级别

- **TRACE**: 用于追踪每次K线数据注入、每次信号生成等频繁操作
- **DEBUG**: 用于调试信息，如引擎创建、参数设置等
- **INFO**: 用于重要的状态变化，如引擎初始化成功、回测开始/结束
- **WARN**: 用于警告，如性能问题、不推荐的配置等
- **ERROR**: 用于错误，如参数验证失败、指标计算失败
- **FATAL**: 用于致命错误，如引擎崩溃、内存分配失败

### 2. 模块命名规范

使用简短、清晰的模块名（最多8个字符）：
- `Engine`: 引擎核心
- `Kline`: K线数据
- `Signal`: 信号生成
- `Parser`: DSL解析
- `Indicat`: 指标计算（Indicator缩写）
- `Strategy`: 策略相关
- `Order`: 订单管理
- `Backtest`: 回测引擎

### 3. 开发阶段配置

```csharp
#if DEBUG
    LogService.Initialize(
        level: LogService.LogLevel.DEBUG,  // Debug模式：详细日志
        enableConsole: true,
        enableFile: true
    );
#else
    LogService.Initialize(
        level: LogService.LogLevel.INFO,   // Release模式：仅重要信息
        enableConsole: true,
        enableFile: true
    );
#endif
```

### 4. 性能考虑

- TRACE级别日志量很大，仅在深度调试时启用
- 日志写入是线程安全的，但频繁写入会影响性能
- 回测时建议使用INFO或WARN级别

## 日志文件管理

默认日志文件路径：`应用程序目录/logs/prophet_core_yyyyMMdd.log`

示例：
```
logs/
├── prophet_core_20251126.log
├── prophet_core_20251125.log
└── prophet_core_20251124.log
```

建议定期清理旧日志文件，保留最近7-30天的日志。

## 故障排查

### 问题1：日志没有输出

检查：
1. 日志级别是否设置正确（是否设置为OFF）
2. 控制台/文件输出是否启用
3. 日志消息级别是否低于当前设置

### 问题2：无法创建日志文件

检查：
1. 日志文件路径是否正确
2. 目录是否存在（LogService会自动创建）
3. 是否有写入权限

### 问题3：日志文件内容不完整

解决：
- 在程序退出前调用 `LogService.Flush()` 或 `LogService.CloseLogFile()`

## 示例

完整示例请参考：
- C++: `Prophet.Core/examples/test_logger.cpp`
- C#: `Prophet.Client/App.axaml.cs`

