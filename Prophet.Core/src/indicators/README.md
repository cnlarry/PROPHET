# Prophet 指标系统

## 1. 概述

本目录包含 Prophet 系统的所有技术指标实现，采用模块化设计，支持动态注册和并行计算。

## 2. 目录结构

```
indicators/
├── momentum/          # 动量指标
├── pattern/           # 形态指标
├── price/             # 价格指标
├── statistics/        # 统计指标
├── trend/             # 趋势指标
├── volatility/        # 波动率指标
├── volume/            # 成交量指标
├── registry.cpp       # 指标注册机制
├── indicator_registrations.cpp  # 指标注册列表
├── common.hpp         # 已移至 include/prophet/indicators/ 目录
├── calculateMomentum.cpp  # 工具函数
├── calculateSlope.cpp     # 工具函数
├── detectCrossover.cpp    # 工具函数
├── detectZeroCross.cpp    # 工具函数
├── determineTrend.cpp     # 工具函数
└── parallel_calculator.cpp  # 并行计算
```

## 3. 指标分类

| 分类       | 描述                     | 示例指标               |
|------------|--------------------------|------------------------|
| momentum   | 动量指标                 | MACD, RSI, KDJ          |
| pattern    | 形态指标                 | SwingHL, MSB, HHLL      |
| price      | 价格指标                 | VWAP, TRIX, PivotPoints |
| statistics | 统计指标                 | StdDev, LinearRegSlope  |
| trend      | 趋势指标                 | MA, EMA, ADX            |
| volatility | 波动率指标               | BOLL, ATR, Keltner      |
| volume     | 成交量指标               | OBV, CMF, ADOSC         |

## 4. 核心文件说明

### 4.1 注册机制

- **registry.cpp**: 实现了指标注册表，支持动态注册和查询指标
- **indicator_registrations.cpp**: 集中管理所有指标的注册代码

### 4.2 工具函数

- **common.hpp**: 公共辅助函数和宏定义（位于 include/prophet/indicators/ 目录）
- **calculateMomentum.cpp**: 计算动量
- **calculateSlope.cpp**: 计算斜率
- **detectCrossover.cpp**: 检测交叉信号
- **detectZeroCross.cpp**: 检测零轴穿越
- **determineTrend.cpp**: 判断趋势方向

### 4.3 并行计算

- **parallel_calculator.cpp**: 支持并行计算多个指标

## 5. 注册机制

### 5.1 注册方式

所有指标通过 `REGISTER_INDICATOR` 宏进行注册，示例：

```cpp
REGISTER_INDICATOR(MACD, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& /* high */,
    const std::vector<double>& /* low */,
    const std::vector<double>& /* volume */,
    const IndicatorParams& params
) {
    int fast = params.get_int("FAST_PERIOD", 12);
    int slow = params.get_int("SLOW_PERIOD", 26);
    int signal = params.get_int("SIGNAL_PERIOD", 9);
    return calc.MACD(close, fast, slow, signal);
});
```

### 5.2 注册优势

- 动态扩展：添加新指标无需修改核心代码
- 集中管理：所有注册代码集中在一个文件
- 编译时执行：自动注册，无需手动调用

## 6. 如何添加新指标

### 6.1 创建指标实现

1. 在对应分类目录下创建 `.cpp` 文件
2. 实现 `Calculator` 类的成员函数
3. 支持 SIMD 优化和 TA-Lib 回退

### 6.2 注册指标

在 `indicator_registrations.cpp` 文件中添加注册代码：

```cpp
REGISTER_INDICATOR(INDICATOR_NAME, [](
    Calculator& calc,
    const std::vector<double>& close,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    const IndicatorParams& params
) {
    // 参数解析和调用
    return calc.INDICATOR_NAME(params);
});
```

## 7. 编译说明

### 7.1 编译选项

- 支持 TA-Lib 集成（可选）
- 支持 SIMD 优化（AVX2）
- 支持并行计算

### 7.2 编译命令

```bash
# 编译所有版本
.\build.bat

# 仅编译 Debug 版本
.\build.bat --debug

# 仅编译 Release 版本
.\build.bat --release
```

## 8. 测试说明

### 8.1 单元测试

- 位于项目根目录 `tests/` 文件夹
- 运行方式：`python test_indicators_basic.py`

### 8.2 集成测试

- 与 DSL 系统集成测试
- 运行方式：`python test_prophet_core_comprehensive.py`

## 9. 维护指南

### 9.1 代码规范

- 遵循 C++17 标准
- 使用命名空间 `prophet::indicators`
- 函数命名采用驼峰命名法
- 注释清晰，包含功能说明和参数说明

### 9.2 性能优化

- 优先使用 SIMD 优化
- 支持 TA-Lib 回退机制
- 并行计算支持

### 9.3 调试建议

- 启用日志功能
- 使用性能监控工具
- 运行测试用例

## 10. 版本控制

- 遵循语义化版本控制
- 每次更新需同步更新 `DSLRules.yaml` 文件
- 保持指标 ID 唯一性

## 11. 参考文档

- Prophet DSL 指标.md
- TA-Lib 文档
- SIMD 优化指南

## 12. 联系方式

如有问题或建议，请联系开发团队。
