# Prophet DSL 文档中心

**版本**: 1.0  
**更新日期**: 2025-10-31  
**项目**: Prophet Trading Strategy DSL

---

## 📖 快速导航

### 🎯 新手入门

如果你是第一次使用 Prophet DSL，建议按以下顺序阅读：

1. **[Prophet DSL 规范](Prophet%20DSL%20规范.md)** - 语言核心规范和基础概念
2. **[运算符和数据类型](Prophet%20DSL%20运算符和数据类型文档.md)** - 基础语法
3. **[信号函数](Prophet%20DSL%20信号函数.md)** - 策略条件组合
4. **[指标引用](Prophet%20DSL%20指标.md)** - 如何使用技术指标

### 📚 核心参考文档

#### 语言规范
- **[Prophet DSL 规范](Prophet%20DSL%20规范.md)** - 完整语言规范
- **[运算符和数据类型](Prophet%20DSL%20运算符和数据类型文档.md)** - 运算符、比较、逻辑运算
- **[Prophet策略JSON规范](Prophet策略JSON规范.md)** - 策略配置文件格式

#### 函数系统
- **[函数系统完整文档](Prophet%20DSL%20函数系统完整文档.md)** - 所有函数总览 ⭐
- **[数学函数](Prophet%20DSL%20数学函数.md)** - ABS, SQRT, SIN, COS, EXP, LN 等
- **[数据函数](Prophet%20DSL%20数据函数.md)** - KLINE, PRICE 等基础数据访问
- **[统计函数](Prophet%20DSL%20统计函数.md)** - HIGHEST, LOWEST, AVERAGE, STD, CHANGE, RANK 等
- **[特殊数据函数](Prophet%20DSL%20特殊数据函数.md)** - HT希尔伯特变换, POWER力量平衡
- **[PATTERN函数](Prophet%20DSL%20PATTERN函数.md)** - 61种K线形态识别

#### 信号与指标
- **[信号函数](Prophet%20DSL%20信号函数.md)** - ALL, ANY, WEIGHTED, VOTE 等
- **[指标引用](Prophet%20DSL%20指标.md)** - 52个技术指标的完整文档

#### 高级功能
- **[参数赋值功能](Prophet%20DSL%20参数赋值功能文档（v3.0.2）.md)** - 动态参数配置
- **[参数赋值快速参考](Prophet%20DSL%20参数赋值快速参考.md)** - 快速查阅
- **[自定义函数用户指南](Prophet%20DSL%20自定义函数用户指南.md)** - 扩展DSL功能
- **[CONSECUTIVE函数](CONSECUTIVE%20函数完整实现文档.md)** - 连续条件判断
- **[COUNT函数](COUNT%20函数完整文档.md)** - 条件计数

---

## 🛠️ 开发者文档

### 系统设计
- **[自定义函数设计文档](Prophet%20DSL%20自定义函数设计文档.md)** - 自定义函数架构
- **[K线数据架构设计](K线数据架构设计方案.md)** - 数据层设计
- **[K线数据API接口](K线数据API接口文档.md)** - API规范
- **[信号对象规范](SIGNAL_OBJECT_SPEC.md)** - 信号系统设计
- **[止损止盈设计](stop_loss_take_profit_design.md)** - TPSL功能设计

### 开发指南
- **[新指标开发流程](新指标开发流程.md)** - 如何添加新指标
- **[环境变量参考手册](环境变量参考手册.md)** - 可用的环境变量
- **[缓存机制说明](缓存机制说明.md)** - 数据缓存机制

### 功能实现总结
- **[数据策略实施总结](数据策略实施总结.md)** - 数据层实施
- **[环境数据包功能总结](环境数据包功能实现总结.md)** - 环境数据功能
- **[指标注册表实施总结](指标注册表实施总结.md)** - 指标系统

---

## 📊 参考资料

### 速查表
- **[完整61个K线形态列表](完整61个K线形态列表.md)** - K线形态速查
- **[参数赋值作用域规则](参数赋值作用域规则.md)** - 作用域规则
- **[功能状态矩阵](FEATURE_STATUS.md)** - 功能实现状态

### 历史变更
- **[v3.0.1统一语法更新说明](v3.0.1_统一语法更新说明.md)** - 重要语法变更

---

## 💡 常见使用场景

### 场景1：创建简单均线策略
```javascript
// 查看：信号函数 + 指标引用
ALL{
  $(5m).EMA().value > $(15m).EMA().value;
  $(5m).MACD().trend = "BULLISH";
} = BUY;
```

### 场景2：使用数据函数计算动态条件
```javascript
// 查看：数据函数 + 统计函数
ALL{
  KLINE(5m).close(0) > HIGHEST(5m).high(20);
  CHANGE(5m).close(1).pct > 2.0;
} = BUY;
```

### 场景3：K线形态识别
```javascript
// 查看：PATTERN函数
PATTERN(5m).HAMMER();
PATTERN(5m).ENGULFING_BEARISH();
```

### 场景4：自定义参数
```javascript
// 查看：参数赋值功能
@PERIOD = 21;
@OVERBOUGHT = 75;

$(5m).RSI().value > @OVERBOUGHT;
```

---

## 🔑 核心概念

### 函数系统层次

```
Prophet DSL 函数系统
├── 信号函数（Signal）- 决策逻辑：ALL, ANY, WEIGHTED, VOTE
├── 数学函数（Math）- 数值计算：ABS, SQRT, SIN, LN, POW
├── 数据函数（Data）- 数据访问：KLINE, PRICE
├── 统计函数（Stat）- 统计分析：HIGHEST, AVERAGE, STD, RANK
├── 特殊函数（Special）- 高级分析：HT, POWER
└── PATTERN函数 - K线形态：HAMMER, DOJI, ENGULFING
```

### 数据来源区分

| 语法 | 类型 | 示例 | 说明 |
|------|------|------|------|
| `$.` | 指标引用 | `$(5m).RSI().value` | 预计算的技术指标 |
| 函数调用 | 数据函数 | `KLINE(5m).close(0)` | 实时计算的数据 |
| `@` | 环境变量 | `@CURRENT_PRICE` | 系统提供的变量 |

---

## 📝 文档约定

### 语法高亮
文档中的代码示例统一使用 `javascript` 语法高亮：

```javascript
WHEN $(5m).RSI().value < 30 = BUY;
```

### 命名规范
- **指标字段**: 小写或混合（如 `value`, `crossover_type`）
- **指标参数**: 全大写+下划线（如 `PERIOD`、`FAST_PERIOD`）
- **函数名**: 全大写（如 `HIGHEST`, `AVERAGE`）
- **信号函数**: 全大写（如 `ALL`, `WEIGHTED`）

### 符号说明
- ⭐ 推荐优先阅读
- ✅ 已实现功能
- 🔥 最新功能
- ⚠️ 重要提示

---

## 🤝 贡献指南

### 文档更新原则
1. 保持文档与代码实现同步
2. 每个新功能必须有完整文档
3. 提供可运行的示例代码
4. 标注版本和更新日期

### 文档结构
- **用户文档** (`Prophet DSL *.md`) - 面向策略开发者
- **开发者文档** (其他 `*.md`) - 面向系统开发者
- **参考资料** - 速查表和工具文档

---

## 📞 获取帮助

### 查找信息
1. **快速查找函数** → [函数系统完整文档](Prophet%20DSL%20函数系统完整文档.md)
2. **查看指标用法** → [指标引用](Prophet%20DSL%20指标.md)
3. **学习语法规则** → [Prophet DSL 规范](Prophet%20DSL%20规范.md)
4. **了解数据类型** → [运算符和数据类型](Prophet%20DSL%20运算符和数据类型文档.md)

### 常见问题
- **如何引用指标？** - 使用 `$(timeframe).INDICATOR().field` 格式
- **如何访问历史数据？** - 使用 `KLINE(5m).close(1)` 等数据函数
- **如何组合条件？** - 使用 `ALL{}`, `ANY{}`, `WEIGHTED{}` 等信号函数
- **如何识别K线形态？** - 使用 `PATTERN(5m).pattern_name()` 函数

---

## 📅 版本信息

**当前版本**: DSL v3.2  
**最后更新**: 2025-10-31  
**主要变更**:
- ✅ 删除技术指标函数（改用指标系统）
- ✅ 新增完整的函数分类文档
- ✅ 重组文档结构，提升可读性

---

## 📂 文档结构

```
doc/
├── README.md                                    # 本文件 - 文档中心
│
├── 核心用户文档/
│   ├── Prophet DSL 规范.md                      # 语言核心规范
│   ├── Prophet DSL 运算符和数据类型文档.md       # 基础语法
│   ├── Prophet DSL 函数系统完整文档.md           # 函数总览 ⭐
│   ├── Prophet DSL 数学函数.md                  # 数学函数详解
│   ├── Prophet DSL 数据函数.md                  # 数据访问函数
│   ├── Prophet DSL 统计函数.md                  # 统计分析函数
│   ├── Prophet DSL 特殊数据函数.md              # HT, POWER等
│   ├── Prophet DSL PATTERN函数.md              # K线形态识别
│   ├── Prophet DSL 信号函数.md                  # 决策逻辑函数
│   ├── Prophet DSL 指标.md                      # 技术指标引用
│   ├── Prophet DSL 参数赋值功能文档.md           # 参数系统
│   ├── Prophet DSL 参数赋值快速参考.md           # 参数速查
│   ├── Prophet DSL 自定义函数用户指南.md         # 自定义扩展
│   └── Prophet策略JSON规范.md                   # 配置文件格式
│
├── 开发者文档/
│   ├── Prophet DSL 自定义函数设计文档.md         # 架构设计
│   ├── K线数据架构设计方案.md                   # 数据层设计
│   ├── K线数据API接口文档.md                    # API规范
│   ├── SIGNAL_OBJECT_SPEC.md                   # 信号对象
│   ├── stop_loss_take_profit_design.md         # TPSL设计
│   ├── 新指标开发流程.md                        # 开发指南
│   ├── 环境变量参考手册.md                      # 环境变量
│   └── 缓存机制说明.md                          # 缓存机制
│
├── 功能文档/
│   ├── CONSECUTIVE 函数完整实现文档.md          # 连续条件
│   ├── COUNT 函数完整文档.md                    # 计数功能
│   ├── 数据策略实施总结.md                      # 数据层实施
│   ├── 环境数据包功能实现总结.md                # 环境数据
│   └── 指标注册表实施总结.md                    # 指标系统
│
└── 参考资料/
    ├── FEATURE_STATUS.md                        # 功能状态
    ├── 完整61个K线形态列表.md                   # K线形态表
    ├── 参数赋值作用域规则.md                    # 作用域规则
    └── v3.0.1_统一语法更新说明.md               # 历史变更
```

---

**维护者**: Prophet Development Team  
**最后更新**: 2025-10-31
