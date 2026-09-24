# Prophet - 加密货币量化交易平台

<div align="center">

![Prophet Logo](https://img.shields.io/badge/Prophet-量化交易平台-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Avalonia](https://img.shields.io/badge/Avalonia-11.3.0-green)
![C++](https://img.shields.io/badge/C++-17-blue)
![License](https://img.shields.io/badge/License-MIT-green)

**高性能、模块化的加密货币量化交易平台**

[功能特性](#-核心功能) • [快速开始](#-快速开始) • [文档](#-文档) • [架构](#-项目架构)

</div>

---

## 📋 项目简介

**Prophet** 是一个专为加密货币市场设计的高性能量化交易平台，采用 C++ 核心引擎和 Avalonia 跨平台桌面应用，提供完整的策略开发、回测验证和实盘交易解决方案。

### 🎯 核心优势

- ⚡ **极致性能** - C++ 核心引擎，比 Python 快 40-60 倍，指标计算仅需 5-25 微秒
- 🎨 **简洁 DSL** - 声明式策略语言，语法直观，易于上手
- 🏠 **本地执行** - 完全本地化，数据隐私安全，无需云端依赖
- 📊 **完整生态** - 策略开发、回测、优化、实盘交易一体化
- 🔧 **模块化设计** - 7 个独立 DLL 模块，高内聚低耦合
- 🚀 **高性能优化** - SIMD 指令集、JIT 编译、多线程并行计算

---

## ✨ 核心功能

### 1. 策略开发系统

- **Prophet DSL 语言**
  - 声明式语法，专为交易策略设计
  - 62 个技术指标支持（MA、EMA、MACD、RSI、BOLL 等）
  - 30 个数据函数（KLINE、PRICE、HIGHEST、CROSS 等）
  - 61 种 K 线形态识别（Hammer、Doji、Engulfing 等）
  - 8 种信号函数（ALL、ANY、WEIGHTED、VOTE 等）
  - 支持自定义函数和变量系统

- **Monaco 代码编辑器**
  - VS Code 核心编辑器
  - 语法高亮和智能提示
  - 代码补全和参数提示
  - 实时语法验证
  - 代码折叠和格式化

- **AI 辅助功能**
  - 策略生成助手
  - 参数优化建议
  - 智能风控分析
  - 支持 OpenAI、DeepSeek、ChatGLM、Qwen 等模型

### 2. 回测系统

- **高性能回测引擎**
  - 本地 C++ 引擎执行
  - 支持多时间框架回测
  - 单次回测 < 3 秒（1 年 1 分钟数据）
  - 支持 4 线程并行回测

- **完整绩效分析**
  - 基础指标：总收益率、年化收益率、最大回撤
  - 风险指标：夏普比率、索提诺比率、卡玛比率
  - 交易统计：胜率、盈亏比、最大连续盈亏
  - 回撤周期分析和信号事件追踪

- **可视化展示**
  - K 线图 + 买卖信号标注
  - 权益曲线图表
  - 详细统计报告面板

### 3. 实盘交易系统

- **多交易所支持**
  - 币安（Binance）U 本位合约（已实现）
  - 支持 OKX、Bybit 等（规划中）

- **多实盘管理**
  - 支持同时运行多个独立实盘实例
  - 每个实例独立配置和风控
  - 跨实例统计和监控

- **完整风险控制**
  - 8 项风险检查机制
  - 紧急停止功能
  - 实时监控和告警
  - 止损/止盈自动触发

- **实时数据流**
  - WebSocket 实时行情
  - 账户和仓位监控
  - 订单状态实时更新

### 4. 数据管理系统

- **数据收集**
  - Binance K 线数据自动下载
  - 恐惧贪婪指数（Fear & Greed Index）
  - 资金费率（Funding Rate）
  - 增量更新模式，按需准备数据

- **数据存储**
  - SQLite 本地数据库（客户端）
  - MySQL 后端数据库（可选）
  - 数据完整性检查
  - 时间序列数据管理

### 5. 策略管理系统

- **版本控制**
  - 策略版本管理
  - 版本对比和回滚
  - 策略归档和废弃

- **策略模板**
  - 内置 10+ 策略模板
  - 模板浏览器
  - 快速创建策略

---

## 🏗️ 项目架构

### 整体结构

```
Prophet/
├── Prophet.Client/          # Avalonia UI 桌面客户端
│   ├── Views/               # 视图层（XAML 界面）
│   ├── ViewModels/          # 视图模型（MVVM）
│   ├── Services/            # 业务服务层
│   │   ├── AI/              # AI 辅助服务
│   │   ├── Data/            # 数据收集和管理
│   │   ├── Editor/          # 代码编辑器服务
│   │   ├── Market/          # 市场数据服务
│   │   └── Strategy/        # 策略管理服务
│   ├── Trading/             # 实盘交易模块
│   ├── Backtest/            # 回测模块
│   ├── Database/            # 数据库访问层
│   └── Models/              # 数据模型
│
├── Prophet.Core/            # C++ 高性能核心引擎
│   ├── prophet_core_common.dll      # 公共模块
│   ├── prophet_core_logging.dll     # 日志模块
│   ├── prophet_core_cache.dll       # 缓存模块
│   ├── prophet_core_indicators.dll  # 指标模块（62个指标）
│   ├── prophet_core_functions.dll   # 函数模块（30个函数）
│   ├── prophet_core_dsl.dll         # DSL 解析和执行
│   └── prophet_core_engine.dll      # 策略执行引擎
│
├── doc/                     # 项目文档
│   ├── Prophet DSL 规范/    # DSL 语言规范文档
│   └── Prophet Backtest Engine/  # 回测引擎文档
│
└── SQL Scripts/             # 数据库脚本
```

### 技术栈

#### 前端技术
- **UI 框架**: Avalonia UI 11.3.0（跨平台 .NET UI 框架）
- **MVVM 框架**: CommunityToolkit.Mvvm
- **代码编辑器**: AvaloniaEdit（Monaco 核心）
- **图表组件**: 自定义 K 线图和权益曲线图

#### 后端技术
- **核心引擎**: C++ 17（高性能计算）
- **客户端**: C# .NET 8.0
- **数据库**: SQLite（客户端）、MySQL（后端）
- **ORM**: Dapper
- **序列化**: System.Text.Json、Newtonsoft.Json

#### 性能优化
- **SIMD**: AVX2 指令集优化
- **JIT 编译**: 字节码编译和虚拟机
- **并行计算**: 多线程并行指标计算
- **内存管理**: 对象池和零拷贝优化

---

## 🚀 快速开始

### 环境要求

- **.NET 8.0 SDK**（仓库根 `global.json` 已锁定 `8.0.425`，装 .NET 10 SDK 也可向下构建）
- **Visual Studio 2022+**（已验证 VS 2026）或 **JetBrains Rider**，需 C++ 桌面开发工作负载（含 MSVC + Ninja）
- **CMake 3.15+**（已验证 4.2）
- **Python 3.14**（可选，仅 Python API；`pyd` 按 `cp314` 构建，`cp313` 残留已忽略）
- **TA-Lib**（C++ 指标计算必需，二选一）：
  - 方式 A（推荐）：`vcpkg install ta-lib:x64-windows-static`，CMake 会自动探测；
  - 方式 B：官网下载解压后设环境变量 `TALIB_ROOT`（如 `setx TALIB_ROOT C:\ta-lib`），`CMakeLists` 回退查找 `C:/ta-lib/include` 与 `C:/ta-lib/lib`。

> 构建顺序必须先 Core 后 Client：`Prophet.Client.csproj` 按配置从 `Prophet.Core/build/bin/{Debug|Release}/` 复制 `prophet_core*.dll`。

### 编译 Prophet.Core

1. **进入 Prophet.Core 目录**
   ```bash
   cd Prophet.Core
   ```

2. **运行构建脚本**
   ```bash
   build.bat --release
   ```
   
   构建选项：
   - `--debug` - 仅编译 Debug 版本
   - `--release` - 仅编译 Release 版本
   - `--both` - 编译 Debug 和 Release 版本（默认）

   注意：`build.bat` 尾部有 `pause`，CI/无人值守请直接调用：
   ```powershell
   cmd /c "call `"C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat`" >nul && cmake --build Prophet.Core\build --config Release"
   ```
   Ninja 为单配置生成器，Debug/Release 切换需重新 `cmake -S Prophet.Core -B Prophet.Core/build` 配置。

3. **手动编译（可选）**
   ```bash
   mkdir build
   cd build
   cmake .. -G "Ninja" -DCMAKE_BUILD_TYPE=Release
   cmake --build .
   ```

### 运行 Prophet.Client

1. **还原 NuGet 包**
   ```bash
   cd Prophet.Client
   dotnet restore
   ```

2. **运行项目**
   ```bash
   dotnet run
   ```

   或在 Visual Studio 中按 `F5` 运行。

3. **开发调试**
   - Debug 模式下按 `F12` 打开 Avalonia DevTools
   - 日志文件保存在 `logs/` 目录

### 数据库初始化

Prophet 使用 SQLite 作为本地数据库，首次运行时会自动创建数据库文件 `PROPHET.db`。
客户端权威 schema 以 `Prophet.Client/Database/Migrations/ClientMigrations.cs` 为准；
`SQL Scripts/` 下 19 个 `.sql` 为历史导出与调试脚本（`prophet.sql` 为 Navicat 导出），仅供参考，新环境不要逐个执行。

---

## 📚 文档

### 用户文档

- **[Prophet DSL 规范](doc/Prophet%20DSL%20规范/)** - 完整的 DSL 语言规范文档
- **[回测引擎文档](doc/Prophet%20Backtest%20Engine/)** - 回测系统架构和使用指南
- **[实盘交易文档](Prophet.Client/docs/Prophet%20Client%20实盘交易模块文档.md)** - 实盘交易功能说明

### 开发者文档

- **[Prophet.Core README](Prophet.Core/README.md)** - 核心引擎详细文档
- **[Prophet.Client README](Prophet.Client/README.md)** - 客户端应用文档
- **[性能优化文档](doc/Prophet%20Core%20性能优化/)** - 性能优化历程和总结

### 快速参考

- **62 个技术指标清单**: [doc/Prophet DSL 规范/7. 附录/7.4 62个指标清单.md](doc/Prophet%20DSL%20规范/7.%20附录/7.4%2062个指标清单.md)
- **30 个数据函数清单**: [doc/Prophet DSL 规范/7. 附录/7.5 30个数据函数清单.md](doc/Prophet%20DSL%20规范/7.%20附录/7.5%2030个数据函数清单.md)
- **61 种 K 线形态清单**: [doc/Prophet DSL 规范/7. 附录/7.6 61个K线形态清单.md](doc/Prophet%20DSL%20规范/7.%20附录/7.6%2061个K线形态清单.md)

---

## 💡 使用示例

### DSL 策略示例

```javascript
// 简单的均线交叉策略
// 买入：5日均线金叉20日均线
ALL {
  $(5m).MA(5).value > $(5m).MA(20).value;
  $(5m).MA(5).value(-1) <= $(5m).MA(20).value(-1);
} = BUY;

// 卖出：5日均线死叉20日均线
ALL {
  $(5m).MA(5).value < $(5m).MA(20).value;
  $(5m).MA(5).value(-1) >= $(5m).MA(20).value(-1);
} = SELL;

// 默认持有
ALL { 1 = 1; } = HOLD;
```

```javascript
// RSI 超买超卖策略
// 买入：RSI超卖
ALL {
  $(5m).RSI(14).value < 30;
  $(5m).RSI(14).oversold = true;
} = BUY;

// 卖出：RSI超买
ALL {
  $(5m).RSI(14).value > 70;
  $(5m).RSI(14).overbought = true;
} = SELL;

// 默认持有
ALL { 1 = 1; } = HOLD;
```

```javascript
// 多时间框架趋势策略
// 买入：多时间框架共振看涨
ALL {
  $(5m).EMA(20).value > $(5m).EMA(50).value;
  $(15m).EMA(20).value > $(15m).EMA(50).value;
  $(5m).MACD().trend == BULLISH;
  $(5m).MACD().crossover_type == GOLDEN_CROSS;
} = BUY;

// 卖出：多时间框架共振看跌
ALL {
  $(5m).EMA(20).value < $(5m).EMA(50).value;
  $(15m).EMA(20).value < $(15m).EMA(50).value;
  $(5m).MACD().trend == BEARISH;
  $(5m).MACD().crossover_type == DEATH_CROSS;
} = SELL;

// 默认持有
ALL { 1 = 1; } = HOLD;
```

```javascript
// MACD + RSI 动量策略（带止盈止损）
// 买入：MACD金叉且RSI在合理区间
ALL {
  $(5m).MACD().crossover_type == GOLDEN_CROSS;
  $(5m).MACD().histogram > 0;
  $(5m).RSI(14).value > 50;
  $(5m).RSI(14).value < 75;
  KLINE(5m).close(0) > $(5m).EMA(20).value;
} = BUY(
  KLINE(5m).close() + $(5m).ATR(14).value * 2.0,  // 止盈：当前价 + 2倍ATR
  KLINE(5m).close() - $(5m).ATR(14).value * 1.5  // 止损：当前价 - 1.5倍ATR
);

// 卖出：MACD死叉或RSI超买
ANY {
  $(5m).MACD().crossover_type == DEATH_CROSS;
  $(5m).RSI(14).value > 80;
  KLINE(5m).close(0) < $(5m).EMA(20).value;
} = SELL;

// 默认持有
ALL { 1 = 1; } = HOLD;
```

### Python API 示例

```python
import sys
sys.path.insert(0, 'Prophet.Core')
import Prophet

# 创建引擎
dsl_code = """
ALL {
  $(5m).RSI(14).value < 30;
  $(5m).RSI(14).oversold = true;
} = BUY;

ALL {
  $(5m).RSI(14).value > 70;
  $(5m).RSI(14).overbought = true;
} = SELL;

ALL { 1 = 1; } = HOLD;
"""
engine = Prophet.Engine(dsl_code)

# 设置 K 线数据
# engine.set_klines(...)

# 获取信号
price = 100.0
time = 1620000000000
signal = engine.get_signal(price, time)
print(f"Signal: {signal}")
```

### C# API 示例

```csharp
// 加载 prophet_core.dll
// 创建引擎
IntPtr engine = ProphetCore.CreateEngine(dslCode);

// 设置 K 线数据
// ProphetCore.SetKlines(engine, ...);

// 获取信号
Signal signal = ProphetCore.GetSignal(engine, price, time);

// 释放资源
ProphetCore.DestroyEngine(engine);
```

---

## 🎨 界面预览

### 主界面
- 策略列表和版本管理
- 代码编辑器（Monaco）
- K 线图表和指标显示
- 实时监控面板

### 回测界面
- 回测配置对话框
- K 线图 + 买卖信号标注
- 权益曲线图表
- 详细统计报告

### 实盘交易界面
- 多实盘实例管理
- 实时账户和仓位监控
- 订单列表和状态
- 风险控制面板

---

## 🔧 开发指南

### 代码规范

- **C# 代码**: 遵循 C# 编码规范，使用 PascalCase（类/方法）和 camelCase（变量）
- **C++ 代码**: 遵循 C++17 标准，使用 4 空格缩进
- **日志输出**: 统一使用 `Console.WriteLine()`，禁止使用 `Debug.WriteLine()`
- **数据库访问**: 必须通过 `DBHelper` 工具类，禁止直接操作数据库

### 项目规则

- 数据库访问必须通过 `DBHelper` 工具类，禁止绕过直接拼 SQL / ADO.NET
- 交易信号只依据指标，禁止时间过滤、禁止基于历史亏损限制订单量
- 欢迎提交 Issue 和 PR，安全问题请走私密报告渠道

### 添加新功能

1. **添加新指标**: 参考 [doc/新指标开发流程.md](doc/新指标开发流程.md)
2. **添加新函数**: 参考 [doc/Prophet DSL 规范/5. 扩展功能/5.1 自定义函数.md](doc/Prophet%20DSL%20规范/5.%20扩展功能/5.1%20自定义函数.md)
3. **修改 DSL 语法**: 需要同步更新 `DSLRules.yaml` 和相关文档

---

## 📊 性能指标

### 核心引擎性能

- **指标计算**: 5-25 微秒/指标
- **策略执行**: 比 Python 快 40-60 倍
- **回测速度**: < 3 秒（1 年 1 分钟数据）
- **内存占用**: 单次回测 < 500MB

### 优化技术

- ✅ SIMD 优化（AVX2 指令集）
- ✅ JIT 字节码编译
- ✅ 多线程并行计算
- ✅ 指标结果缓存
- ✅ 内存池管理
- ✅ 零拷贝优化

详细性能优化历程请参考 [doc/Prophet Core 性能优化/](doc/Prophet%20Core%20性能优化/)。

---

## 🗺️ 路线图

### 已完成 ✅

- [x] 核心引擎架构（7 个模块化 DLL）
- [x] DSL 语言系统（62 指标 + 30 函数）
- [x] 策略编辑器（Monaco + 智能提示）
- [x] 回测引擎（高性能本地回测）
- [x] 实盘交易系统（Binance 支持）
- [x] AI 辅助功能（策略生成和优化建议）
- [x] 数据管理系统（K 线、恐惧贪婪指数、资金费率）

### 进行中 🚧

- [ ] 更多交易所支持（OKX、Bybit）
- [ ] 策略参数优化（Optuna 集成）
- [ ] 机器学习模型集成
- [ ] 策略市场功能

### 规划中 📋

- [ ] 多币种组合策略
- [ ] 网格交易策略
- [ ] 套利策略支持
- [ ] 移动端应用

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request。安全问题请通过 GitHub 私密报告渠道提交，不要在公开 Issue 粘贴密钥：

- **GitHub Issues**: [提交问题](https://github.com/cnlarry/Prophet/issues)

---

## 📄 许可证

本项目采用 MIT 许可证，详见 [LICENSE](LICENSE) 文件。

---

## 🙏 致谢

- **TA-Lib**: 提供技术指标计算库
- **Avalonia UI**: 跨平台 UI 框架
- **AvaloniaEdit**: 代码编辑器组件
- **CMake & Ninja**: 构建系统工具

---

## 📞 联系方式

- **项目主页**: [GitHub](https://github.com/cnlarry/Prophet)
- **问题反馈**: [GitHub Issues](https://github.com/cnlarry/Prophet/issues)

---

<div align="center">

**Prophet** - 让量化交易更简单、更高效、更强大

Made with ❤️ by Prophet Development Team

</div>
