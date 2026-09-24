# Prophet 策略状态与版本控制方案

## 📋 文档概述

本文档详细说明 Prophet 量化交易平台的策略状态管理和版本控制机制，包括状态流转规则、版本号管理规则、用户权限控制以及数据库架构设计。

**创建日期：** 2025-11-21  
**版本：** 1.0  
**作者：** Prophet Team

---

## 🎯 核心设计原则

### 三层状态管理

Prophet 采用三层状态管理架构，清晰分离不同层次的状态：

```
┌─────────────────────────────────────────────────────────┐
│ 1. 策略级别状态 (Strategy-Level Status)                │
│    位置: strategies 表                                   │
│    含义: 策略整体的生命周期状态                          │
│    状态: draft → active → deprecated → archived/anomaly  │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ 2. 版本级别状态 (Version-Level Status)                  │
│    位置: strategy_dsl_versions 表                        │
│    含义: 每个版本的状态（一个策略可以有多个版本）        │
│    状态: draft → active → inactive                       │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ 3. 实例级别状态 (Instance-Level Status)                 │
│    位置: strategy_instances 表                           │
│    含义: 用户运行实例的状态                              │
│    状态: stopped → running → paused → error              │
└─────────────────────────────────────────────────────────┘
```

### 版本管理原则

1. ✅ **语义化版本号**：采用 `MAJOR.MINOR.PATCH` 格式
2. ✅ **不可变版本**：版本一旦创建不可修改（INSERT only）
3. ✅ **用户版本锁定**：每个用户独立选择使用的版本
4. ✅ **渐进式升级**：支持推荐升级和强制升级
5. ✅ **完整性保证**：DSL代码和参数作为不可分割的版本快照

---

## 📊 策略级别状态 (Strategy-Level Status)

### 状态定义

策略的整体生命周期状态，定义在 `strategies.status` 字段：

| 状态 | 英文名 | 说明 | 可见性 | 颜色 |
|------|--------|------|--------|------|
| 📝 草稿 | draft | 策略开发中，尚未就绪 | 仅作者 | 灰色 #9E9E9E |
| ✅ 激活 | active | 策略已就绪，可正常使用 | 作者+订阅者 | 绿色 #4CAF50 |
| ⚠️ 废弃 | deprecated | 策略已过时，不推荐使用 | 作者+已订阅者 | 橙色 #FF9800 |
| 📦 归档 | archived | 策略已归档，完全不可用 | 仅作者 | 深灰 #616161 |
| 🔴 异常 | anomaly | 系统检测到技术异常 | 作者+已订阅者 | 红色 #F44336 |

### 1. 📝 草稿状态 (Draft)

**定义：** 策略处于开发阶段，作者正在编写、测试和优化策略代码。

**进入方式：**
- 用户新建策略时，初始状态为 draft
- 自动创建 v1.0.0 版本（version_status 也是 draft）

**允许的操作：**

| 操作 | 是否允许 | 说明 |
|------|---------|------|
| 编辑代码 | ✅ 允许 | 可自由修改 DSL 代码 |
| 修改参数 | ✅ 允许 | 可调整策略参数 |
| 重命名 | ✅ 允许 | 可修改策略名称 |
| 删除策略 | ✅ 允许 | 可直接删除（不可恢复） |
| 保存版本 | ✅ 允许 | 每次保存自动递增版本号 |
| **回测** | ✅ **允许** | **必须允许！用户需要验证策略** |
| 模拟盘 | ❌ 禁止 | 草稿不能用于交易 |
| 实盘 | ❌ 禁止 | 草稿不能用于交易 |
| 公开分享 | ❌ 禁止 | 不能设置 public=true |

**版本递增规则：**

草稿状态下，每次保存都会创建新版本：

```
首次保存：v1.0.0 (initial)

后续修改：
- 小改动（修复语法、调参数）：patch+1 → v1.0.1, v1.0.2, ...
- 功能增强（新增指标、逻辑）：minor+1, patch归零 → v1.1.0
- 重大变更（完全重写）：major+1, minor和patch归零 → v2.0.0

用户保存时选择变更类型（系统可提供智能建议）
```

**状态流转：**

```
draft → active    （用户点击"激活策略"）
draft → archived  （用户点击"归档"）
draft → deleted   （用户点击"删除"）
```

**UI示例：**

```
┌─────────────────────────────────────────────────────┐
│ 策略：RSI超卖买入策略                    [📝 草稿]  │
├─────────────────────────────────────────────────────┤
│ 版本：v1.0.5                                        │
│ 最后编辑：2025-11-21 14:30                          │
│                                                     │
│ ⚠️ 该策略为草稿状态，仅你可见                       │
│                                                     │
│ 下一步：                                            │
│ • 完成至少1次回测                                   │
│ • 验证策略逻辑正确                                  │
│ • 点击"激活策略"使其可用                            │
│                                                     │
│ [编辑代码] [回测] [重命名] [激活策略] [删除]        │
└─────────────────────────────────────────────────────┘
```

### 2. ✅ 激活状态 (Active)

**定义：** 策略已经过验证，可以正常使用和分享。

**进入方式：**
- 用户在 draft 状态点击"激活策略"
- 系统检查：
  1. ✅ 至少完成1次回测
  2. ✅ DSL 语法验证通过
  3. ✅ 填写了策略描述和类型

**激活流程：**

```
┌─────────────────────────────────────────────────────┐
│ 准备激活策略？                                      │
├─────────────────────────────────────────────────────┤
│ ✅ DSL 语法验证通过                                 │
│ ✅ 已完成 3 次回测                                  │
│ ⚠️ 建议：填写风险披露信息                           │
│                                                     │
│ 策略类型（必填）：                                  │
│ [趋势跟踪 ▼]                                        │
│                                                     │
│ 策略描述（选填）：                                  │
│ ┌─────────────────────────────────────────────────┐ │
│ │ 基于RSI超卖信号买入，RSI超买信号卖出...         │ │
│ └─────────────────────────────────────────────────┘ │
│                                                     │
│ 激活后：                                            │
│ • 可以用于实盘和模拟盘交易                          │
│ • 可以分享给其他用户                                │
│ • 编辑时会创建新版本                                │
│                                                     │
│ [ 取消 ]  [ 确认激活 ]                              │
└─────────────────────────────────────────────────────┘
```

**允许的操作：**

| 操作 | 是否允许 | 说明 |
|------|---------|------|
| 编辑代码 | ✅ 允许 | **但会创建新的 draft 版本** |
| 修改参数 | ✅ 允许 | 同样创建新版本 |
| 重命名 | ⚠️ 限制 | 可改名称，不能改 ID |
| 删除策略 | ❌ 禁止 | 只能归档，不能删除 |
| 回测 | ✅ 允许 | 所有订阅者都可以回测 |
| 模拟盘 | ✅ 允许 | 作者和订阅者都可以用 |
| 实盘 | ✅ 允许 | 作者和订阅者都可以用 |
| 公开分享 | ✅ 允许 | 可设置 public=true |

**编辑机制（重要）：**

激活后的策略采用**版本锁定模式**：

```
场景：用户点击"编辑"按钮

系统行为：
1. 当前 active 版本（如 v1.0.0）保持不变
2. 自动创建新的 draft 版本（如 v1.0.1 或 v1.1.0）
3. 用户在新版本上编辑
4. 编辑完成后，用户可以：
   a) 激活新版本 → 旧版本变为 inactive
   b) 放弃新版本 → 删除 draft 版本

示例：
当前: v1.0.0 (active)
编辑: 创建 v1.1.0 (draft)
激活v1.1.0后:
  - v1.0.0 → inactive
  - v1.1.0 → active
  - strategies.current_version = v1.1.0
  - strategies.status 保持 active
```

**公开分享：**

```
激活后可选择公开分享：

┌─────────────────────────────────────────────────────┐
│ 公开分享设置                                        │
├─────────────────────────────────────────────────────┤
│ ☑ 公开此策略（其他用户可以搜索和订阅）             │
│                                                     │
│ 风险披露（必填）：                                  │
│ ┌─────────────────────────────────────────────────┐ │
│ │ 主要风险：                                      │ │
│ │ - 震荡市场可能频繁止损                          │ │
│ │ - 需要较大资金规模（建议>$5000）                │ │
│ │                                                 │ │
│ │ 适用市场：单边趋势行情                          │ │
│ │                                                 │ │
│ │ 止损建议：单笔不超过本金2%                      │ │
│ └─────────────────────────────────────────────────┘ │
│                                                     │
│ [ 取消 ]  [ 公开分享 ]                              │
└─────────────────────────────────────────────────────┘
```

**状态流转：**

```
active → deprecated  （用户标记为废弃）
active → archived    （用户归档）
active → anomaly     （系统检测到异常）
```

### 3. ⚠️ 废弃状态 (Deprecated)

**定义：** 策略已过时或有更好的替代版本，不推荐使用但仍可运行。

**进入方式：**
- 用户手动标记策略为 deprecated
- 策略作者发布了更好的新版本策略（可选）

**允许的操作：**

| 操作 | 是否允许 | 说明 |
|------|---------|------|
| 编辑代码 | ❌ 禁止 | 废弃策略不应再编辑 |
| 查看代码 | ✅ 允许 | 所有人可查看 |
| 回测 | ✅ 允许 | 可继续回测 |
| 模拟盘 | ✅ 允许 | 已有实例可继续运行 |
| 实盘 | ✅ 允许 | 已有实例可继续运行 |
| 新订阅 | ❌ 禁止 | 新用户不能订阅 |
| 公开显示 | ⚠️ 限制 | 显示但带废弃警告 |

**UI显示：**

```
┌─────────────────────────────────────────────────────┐
│ ⚠️ 该策略已废弃                                     │
├─────────────────────────────────────────────────────┤
│ 策略：RSI超卖买入策略 v1.0               [⚠️ 废弃]  │
│                                                     │
│ 废弃原因：                                          │
│ 发现在震荡市场中频繁止损，建议使用改进版本          │
│                                                     │
│ 当前使用该策略的用户：12人                          │
│                                                     │
│ 建议操作：                                          │
│ • 已有实例可继续运行（风险自担）                    │
│ • 新用户不能订阅此策略                              │
│                                                     │
│ [查看代码] [归档]                                   │
└─────────────────────────────────────────────────────┘
```

**对现有用户的影响：**

```
已订阅用户：
✅ 实例继续运行（不会被强制停止）
⚠️ 显示警告提示："该策略已废弃，建议考虑升级"
✅ 可以手动停止
✅ 可以查看代码
✅ 可以继续回测

新用户：
❌ 不能创建新实例
❌ 搜索结果中不显示（或显示但不可订阅）
```

**状态流转：**

```
deprecated → archived  （用户归档）
deprecated → anomaly   （系统检测到异常）
```

### 4. 📦 归档状态 (Archived)

**定义：** 策略已归档，完全不可用，但保留历史记录。

**进入方式：**
- 用户主动归档（策略过时、不再需要）
- 从 anomaly 状态确认无法修复后归档

**归档确认流程：**

```
┌─────────────────────────────────────────────────────┐
│ ⚠️ 确认归档策略？                                   │
├─────────────────────────────────────────────────────┤
│ 策略：RSI超卖买入策略                               │
│                                                     │
│ 归档后：                                            │
│ • 所有运行中的实例将被停止                          │
│ • 其他用户无法再使用此策略                          │
│ • 策略代码仅可查看，不可运行                        │
│ • 归档操作不可撤销                                  │
│                                                     │
│ 当前有 3 个实例正在运行，将被强制停止                │
│                                                     │
│ 归档原因（必填）：                                  │
│ [策略已过时 ▼]                                      │
│   - 策略已过时                                      │
│   - 发现严重bug                                     │
│   - 不再需要                                        │
│   - 其他原因                                        │
│                                                     │
│ [ 取消 ]  [ 确认归档 ]                              │
└─────────────────────────────────────────────────────┘
```

**允许的操作：**

| 操作 | 是否允许 | 说明 |
|------|---------|------|
| 查看代码 | ✅ 允许 | 仅作者可查看（只读） |
| 编辑代码 | ❌ 禁止 | 完全不可编辑 |
| 回测 | ❌ 禁止 | 不可运行 |
| 交易 | ❌ 禁止 | 所有交易环境都不可用 |
| 复制代码 | ✅ 允许 | 可复制到新策略 |
| 恢复 | ❌ 禁止 | 归档不可逆 |
| 删除 | ✅ 允许 | 作者可永久删除 |

**系统行为：**

```
归档策略时系统自动：
1. 停止所有运行中的实例（status → stopped）
2. 通知所有使用该策略的用户
3. 从公开列表中移除
4. 标记归档时间和原因
5. 更新订阅者的实例状态
```

**UI显示：**

```
策略列表视图：
├── ✅ 激活中 (3)
├── 📝 草稿 (5)
├── ⚠️ 废弃 (2)
├── 🔴 异常 (1)
└── 📦 已归档 (7) [点击展开/收起]
    ├── RSI策略 v1.0 (归档于 2025-10-15)
    ├── 网格策略 v2.3 (归档于 2025-09-20)
    └── ...
```

**状态流转：**

```
archived → [终态，不可转换]
```

### 5. 🔴 异常状态 (Anomaly)

**定义：** 系统自动检测到技术异常，策略无法正常运行。

**进入方式（系统自动检测）：**

```python
# 异常检测触发条件
触发条件：
1. DSL 解析失败（语法错误、不兼容）
2. 核心引擎报错（执行异常）
3. 指标计算崩溃（计算错误）
4. 连续执行失败 > 5次
5. 数据库连接失败（持续性）
6. 关键依赖缺失

# 系统行为
检测到异常时：
1. 立即暂停所有运行中的实例
2. 设置 strategies.status = 'anomaly'
3. 记录详细的异常原因到 anomaly_reason
4. 发送通知给策略作者
5. 发送通知给所有使用者
6. 记录异常检测时间
```

**重要说明：**

```
❌ 不会标记为 anomaly 的情况：
- 胜率低
- 回测亏损
- 回撤大
- 策略表现不佳
- 连续亏损

✅ 会标记为 anomaly 的情况：
- 技术故障
- 代码无法执行
- 系统兼容性问题
```

**允许的操作：**

| 操作 | 是否允许 | 说明 |
|------|---------|------|
| 查看代码 | ✅ 允许 | 作者和订阅者可查看 |
| 编辑代码 | ⚠️ 限制 | 仅作者可尝试修复 |
| 回测 | ⚠️ 限制 | 仅用于调试（可能失败） |
| 交易 | ❌ 禁止 | 禁止任何交易环境 |
| 复制代码 | ✅ 允许 | 可复制到新策略修复 |
| 归档 | ✅ 允许 | 如无法修复，可归档 |

**UI显示：**

```
┌─────────────────────────────────────────────────────┐
│ 🔴 策略异常                                         │
├─────────────────────────────────────────────────────┤
│ 策略：RSI超卖买入策略                    [🔴 异常]  │
│ 检测时间：2025-11-21 15:30                          │
│                                                     │
│ 异常原因：                                          │
│ DSL解析失败：函数 'rsi_custom' 不存在               │
│ 可能原因：DSL语言规范更新，该函数已被移除            │
│                                                     │
│ 系统已自动暂停所有运行中的实例(5个)                 │
│                                                     │
│ 建议操作：                                          │
│ • 查看最新的 DSL 函数文档                           │
│ • 修改代码使用新的函数                              │
│ • 或创建新策略替代                                  │
│                                                     │
│ [ 查看详情 ] [ 尝试修复 ] [ 归档策略 ]              │
└─────────────────────────────────────────────────────┘
```

**恢复流程：**

```
修复步骤：
1. 作者编辑代码，修复问题
2. 保存新版本（patch+1）
3. 新版本验证通过
4. 系统自动将 status 从 anomaly 改为 active
5. 通知所有用户：策略已修复
6. 用户可选择恢复实例运行

如果无法修复：
1. 作者选择归档策略
2. 通知用户寻找替代策略
3. 用户可复制代码自行修复
```

**状态流转：**

```
anomaly → active    （修复成功）
anomaly → archived  （无法修复，归档）
```

---

## 🔖 版本级别状态 (Version-Level Status)

### 状态定义

每个版本的状态，定义在 `strategy_dsl_versions.version_status` 字段：

| 状态 | 英文名 | 说明 |
|------|--------|------|
| 📝 草稿 | draft | 版本开发中 |
| ✅ 激活 | active | 当前使用的版本 |
| 💤 非活跃 | inactive | 历史版本（已被新版本替代） |

### 版本状态流转

```
draft → active    （版本激活）
active → inactive （发布新版本后，旧版本变为inactive）
draft → deleted   （放弃草稿版本）
```

**注意：** 版本状态 ≠ 策略状态

```
示例：
策略状态 = active（策略整体可用）
版本1: v1.0.0 (inactive) - 历史版本
版本2: v1.1.0 (active)   - 当前版本
版本3: v1.2.0 (draft)    - 开发中的新版本
```

---

## 🚀 运行实例状态 (Instance-Level Status)

### 状态定义

用户创建的策略实例的运行状态，定义在 `strategy_instances.status` 字段：

| 状态 | 英文名 | 说明 |
|------|--------|------|
| ⏹️ 停止 | stopped | 实例未运行 |
| ▶️ 运行中 | running | 实例正在执行交易 |
| ⏸️ 暂停 | paused | 实例暂停（用户手动） |
| ❌ 错误 | error | 实例因错误停止 |

### 实例状态流转

```
stopped → running  （用户启动）
running → paused   （用户暂停）
running → stopped  （用户停止）
running → error    （执行错误）
paused → running   （用户恢复）
error → stopped    （用户确认）
```

---

## 📐 版本号管理规则

### 语义化版本号格式

```
vMAJOR.MINOR.PATCH

示例：v2.1.3
      │ │ │
      │ │ └─ Patch: 修订版本号（向下兼容的bug修复）
      │ └─── Minor: 次版本号（向下兼容的功能增强）
      └───── Major: 主版本号（不兼容的重大变更）
```

### 版本号递增规则

#### 1. 首次创建 (Initial)

```
新建策略时：
版本号：v1.0.0
变更类型：initial
策略状态：draft
版本状态：draft
```

#### 2. Patch 版本递增（+0.0.1）

**触发条件：** 向下兼容的问题修复

```
典型场景：
✅ 修复语法错误
✅ 修复计算错误
✅ 优化代码性能（不改变逻辑）
✅ 调整参数默认值（小幅）
✅ 修正注释和文档

递增规则：
v1.2.3 → v1.2.4（patch+1）

适用状态：
- draft 状态：每次小修改保存
- active 状态：修复bug后发布
```

**示例：**

```
Before: v1.0.0
def calculate():
    return rsi(14)  # 语法错误

After: v1.0.1 (patch+1)
def calculate():
    return rsi(PERIOD=14)  # 修复语法
```

#### 3. Minor 版本递增（+0.1.0）

**触发条件：** 向下兼容的功能增强

```
典型场景：
✅ 新增交易条件（如增加MACD确认）
✅ 新增技术指标
✅ 新增可选参数
✅ 增强止损逻辑
✅ 优化入场/出场规则
✅ 增加通知功能

递增规则：
v1.2.3 → v1.3.0（minor+1, patch归零）

适用状态：
- 通常在 draft 状态完成开发
- 测试通过后激活
- 激活时从 draft 变为 active
```

**示例：**

```
Before: v1.0.0
when rsi < 30 then buy

After: v1.1.0 (minor+1, patch归零)
when rsi < 30 and macd_cross then buy  # 新增MACD确认
```

#### 4. Major 版本递增（+1.0.0）

**触发条件：** 不兼容的重大变更

```
典型场景：
✅ 完全重写策略逻辑
✅ 改变交易方向（做多↔做空）
✅ 更换核心算法
✅ 删除或重命名必需参数
✅ 改变参数含义
✅ 更换交易对（BTCUSDT → ETHUSDT）
✅ 不兼容的破坏性变更

递增规则：
v1.2.3 → v2.0.0（major+1, minor和patch归零）

必需字段：
⚠️ 必须填写 breaking_changes（破坏性变更说明）

适用状态：
- 在 draft 状态开发完成
- 激活时需明确告知用户不兼容
```

**示例：**

```
Before: v1.5.0
strategy "RSI Buy" on BTCUSDT
when rsi(14) < oversold then buy

After: v2.0.0 (major+1)
strategy "RSI Sell" on ETHUSDT
when rsi(PERIOD) > overbought then sell  # 完全不同的逻辑

Breaking Changes:
- 交易对从 BTC 改为 ETH
- 策略从做多改为做空
- 参数 oversold 改为 overbought
- RSI 参数从固定14改为可配置
```

### 版本递增时机

| 状态 | 保存行为 | 版本递增 |
|------|---------|---------|
| draft | 每次保存 | 根据变更类型递增 |
| active | 点击"编辑" | 创建新draft版本 |
| active | 激活新版本 | 旧版本 → inactive |

### 版本号自动 vs 手动

```
✅ 版本号数字：系统自动计算
   用户选择：变更类型（major/minor/patch）
   系统计算：具体版本号（v2.1.5）

示例流程：
1. 当前版本：v1.2.3
2. 用户修改代码并保存
3. 系统提示：
   ┌─────────────────────────────────┐
   │ 请选择变更类型：                │
   │ ○ Patch - 问题修复              │
   │ ● Minor - 功能增强（推荐）      │
   │ ○ Major - 重大变更              │
   │                                 │
   │ 新版本号将为：v1.3.0             │
   │                                 │
   │ 变更说明（选填）：              │
   │ ┌─────────────────────────────┐ │
   │ │ 新增MACD确认信号             │ │
   │ └─────────────────────────────┘ │
   │                                 │
   │ [ 取消 ]  [ 保存 ]              │
   └─────────────────────────────────┘
4. 系统自动创建 v1.3.0 版本
```

### 智能版本建议

系统可以根据代码变更自动建议版本类型：

```python
def suggest_version_type(old_dsl, new_dsl, old_params, new_params):
    """智能建议版本类型"""
    
    # 计算代码相似度
    similarity = calculate_similarity(old_dsl, new_dsl)
    
    # 检查参数变更
    params_added = set(new_params.keys()) - set(old_params.keys())
    params_removed = set(old_params.keys()) - set(new_params.keys())
    params_changed = [k for k in old_params if k in new_params and old_params[k] != new_params[k]]
    
    # 建议逻辑
    if similarity < 0.5:
        return "major"  # 代码变化超过50%
    elif params_removed:
        return "major"  # 删除了参数
    elif params_added:
        return "minor"  # 新增了参数
    elif params_changed:
        return "patch"  # 参数值调整
    elif similarity < 0.9:
        return "minor"  # 代码有明显变化
    else:
        return "patch"  # 小幅修改
```

---

## 🔐 权限控制矩阵

### 策略级别权限

完整的权限控制表：

| 操作 | Draft<br/>作者 | Active<br/>作者 | Active<br/>订阅者 | Deprecated<br/>作者 | Deprecated<br/>订阅者 | Archived<br/>作者 | Anomaly<br/>作者 | Anomaly<br/>订阅者 |
|------|-------|-------|-------|-------|-------|-------|-------|-------|
| 查看代码 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| 编辑代码 | ✅ | ✅<sup>1</sup> | ❌ | ❌ | ❌ | ❌ | ⚠️<sup>2</sup> | ❌ |
| 删除策略 | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| 重命名 | ✅ | ⚠️<sup>3</sup> | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| 回测 | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ⚠️<sup>4</sup> | ⚠️<sup>4</sup> |
| 模拟盘 | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 实盘 | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 公开分享 | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| 订阅 | ❌ | ✅ | - | ❌ | - | ❌ | ❌ | - |
| 归档 | ✅ | ✅ | ❌ | ✅ | ❌ | - | ✅ | ❌ |

**注释：**
1. 编辑会创建新的draft版本
2. 仅用于尝试修复异常
3. 可改名称，不能改ID
4. 仅用于调试，可能失败

---

## 📊 数据库架构设计

### 架构概述

三表结构，清晰分离三层状态：

```
strategies (策略主表)
    ├── 策略整体信息
    ├── 策略级别状态（draft/active/deprecated/archived/anomaly）
    └── 当前版本号指针
    
strategy_dsl_versions (版本管理表)
    ├── 每个版本的 DSL 代码
    ├── 版本级别状态（draft/active/inactive）
    └── 回测数据快照
    
strategy_instances (用户实例表)
    ├── 用户订阅记录
    ├── 版本锁定
    ├── 实例运行状态（stopped/running/paused/error）
    └── 性能数据
```

### 表关系图

```
┌─────────────────────┐
│    members          │
│  ┌──────────────┐   │
│  │ id (PK)      │◄──┼──────┐
│  │ username     │   │      │
│  │ email        │   │      │
│  └──────────────┘   │      │
└─────────────────────┘      │
                             │ owner_id (FK)
                             │
┌────────────────────────────┼─────────────────────────────┐
│    strategies              │                             │
│  ┌─────────────────────────┼──────────────┐              │
│  │ id (PK)                 │              │              │
│  │ owner_id (FK) ──────────┘              │              │
│  │ name                                   │              │
│  │ symbol                                 │              │
│  │ status ◄────┐                          │              │
│  │ current_version (v2.1.0)               │              │
│  │ quality_score                          │              │
│  │ public                                 │              │
│  └────────────────────────────────────────┘              │
└──────────────────┬───────────────────────────────────────┘
                   │ strategy_id (FK)
                   │
                   ├──────────────────────────────────┐
                   │                                  │
                   ▼                                  ▼
┌────────────────────────────────┐   ┌────────────────────────────────┐
│ strategy_dsl_versions          │   │ strategy_instances             │
│  ┌──────────────────────────┐  │   │  ┌──────────────────────────┐  │
│  │ id (PK)                  │  │   │  │ id (PK)                  │  │
│  │ strategy_id (FK) ────────┼──┘   │  │ strategy_id (FK) ────────┼──┘
│  │ version (v2.1.0)         │      │  │ user_id (FK) ────────────┼──┐
│  │ version_status           │      │  │ locked_version (v2.0.0)  │  │
│  │ dsl (code)               │      │  │ status (running)         │  │
│  │ parameters (json)        │      │  │ environment (live)       │  │
│  │ backtest_data (json)     │      │  │ performance_data (json)  │  │
│  │ validation_status        │      │  │ has_newer_version        │  │
│  └──────────────────────────┘      │  └──────────────────────────┘  │
└────────────────────────────────┘   └───────────────────┬────────────┘
                                                         │
                                                         │ user_id (FK)
                                                         ▼
                                              ┌─────────────────────┐
                                              │    members          │
                                              │  ┌──────────────┐   │
                                              │  │ id (PK)      │   │
                                              │  └──────────────┘   │
                                              └─────────────────────┘
```

### 核心查询示例

#### 1. 查询用户的所有策略（按状态分组）

```sql
-- 查询用户的策略，按状态分组
SELECT 
    status,
    COUNT(*) as count,
    JSON_ARRAYAGG(
        JSON_OBJECT(
            'id', id,
            'name', name,
            'version', current_version_string,
            'updated_at', updated_at
        )
    ) as strategies
FROM strategies
WHERE owner_id = ?
GROUP BY status
ORDER BY FIELD(status, 'active', 'draft', 'deprecated', 'anomaly', 'archived');
```

#### 2. 查询策略的所有版本

```sql
-- 查询某个策略的版本历史
SELECT 
    version_string,
    version_status,
    change_type,
    change_description,
    created_at,
    JSON_EXTRACT(backtest_data, '$.total_return') as total_return,
    JSON_EXTRACT(backtest_data, '$.win_rate') as win_rate
FROM strategy_dsl_versions
WHERE strategy_id = ?
ORDER BY major_version DESC, minor_version DESC, patch_version DESC;
```

#### 3. 查询用户的策略实例（含版本信息）

```sql
-- 查询用户的所有策略实例
SELECT 
    si.id,
    si.instance_name,
    s.name as strategy_name,
    si.locked_version_string,
    s.current_version_string,
    si.status as instance_status,
    si.environment,
    si.has_newer_version,
    si.last_executed_at,
    JSON_EXTRACT(si.performance_data, '$.total_pnl') as total_pnl
FROM strategy_instances si
JOIN strategies s ON si.strategy_id = s.id
WHERE si.user_id = ?
ORDER BY si.last_executed_at DESC;
```

#### 4. 检查是否有需要升级的实例

```sql
-- 查找所有需要提醒升级的实例
SELECT 
    si.id,
    si.user_id,
    s.name as strategy_name,
    si.locked_version_string as current_version,
    s.current_version_string as latest_version,
    sdv.force_upgrade
FROM strategy_instances si
JOIN strategies s ON si.strategy_id = s.id
JOIN strategy_dsl_versions sdv ON 
    sdv.strategy_id = s.id 
    AND sdv.major_version = s.current_major_version
    AND sdv.minor_version = s.current_minor_version
    AND sdv.patch_version = s.current_patch_version
WHERE 
    (si.locked_major_version < s.current_major_version
    OR (si.locked_major_version = s.current_major_version AND si.locked_minor_version < s.current_minor_version)
    OR (si.locked_major_version = s.current_major_version AND si.locked_minor_version = s.current_minor_version AND si.locked_patch_version < s.current_patch_version))
    AND si.status IN ('running', 'paused');
```

#### 5. 查询公开可用的策略（含评分）

```sql
-- 查询可供订阅的公开策略
SELECT 
    s.id,
    s.name,
    s.symbol,
    s.current_version_string,
    s.strategy_type,
    s.risk_level,
    s.quality_score,
    s.total_subscribers,
    m.username as author,
    sdv.backtest_data
FROM strategies s
JOIN members m ON s.owner_id = m.id
LEFT JOIN strategy_dsl_versions sdv ON 
    sdv.strategy_id = s.id 
    AND sdv.version_status = 'active'
WHERE 
    s.status = 'active'
    AND s.public = 1
    AND s.enabled = 1
ORDER BY s.quality_score DESC, s.total_subscribers DESC;
```

#### 6. 检测异常策略

```sql
-- 查询需要异常检测的策略实例
SELECT 
    si.id,
    si.strategy_id,
    si.user_id,
    si.consecutive_errors,
    si.last_executed_at,
    s.status as strategy_status
FROM strategy_instances si
JOIN strategies s ON si.strategy_id = s.id
WHERE 
    si.consecutive_errors >= 5
    AND si.status = 'running'
    AND s.status NOT IN ('anomaly', 'archived');

-- 更新为异常状态
UPDATE strategies 
SET 
    status = 'anomaly',
    anomaly_reason = '连续执行失败超过5次',
    status_changed_at = NOW()
WHERE id = ?;

-- 暂停所有相关实例
UPDATE strategy_instances 
SET 
    status = 'error',
    error_message = '策略已被系统标记为异常',
    status_changed_at = NOW()
WHERE strategy_id = ? AND status = 'running';
```

---

## 🔄 典型业务流程

### 流程 1：创建并发布策略

```
1. 用户创建策略
   ↓
   strategies: {
     id: "uuid-1234",
     name: "RSI策略",
     owner_id: "user-001",
     status: "draft",
     current_version: v1.0.0
   }
   
   strategy_dsl_versions: {
     strategy_id: "uuid-1234",
     version: v1.0.0,
     version_status: "draft",
     change_type: "initial",
     dsl: "when rsi < 30 then buy",
     validation_status: "pending"
   }

2. 用户编辑并保存（3次小修改）
   ↓
   strategy_dsl_versions 新增记录:
     v1.0.1 (patch, draft)
     v1.0.2 (patch, draft)
     v1.0.3 (patch, draft)

3. 用户完成回测
   ↓
   strategy_dsl_versions 更新 v1.0.3:
     backtest_data: {
       total_return: 0.45,
       win_rate: 0.62,
       sharpe_ratio: 1.8,
       max_drawdown: 0.15
     }
     backtest_completed_at: "2025-11-21 10:00:00"

4. 用户激活策略
   ↓
   strategies 更新:
     status: "draft" → "active"
     status_changed_at: "2025-11-21 10:05:00"
   
   strategy_dsl_versions 更新 v1.0.3:
     version_status: "draft" → "active"
     activated_at: "2025-11-21 10:05:00"

5. 用户公开分享
   ↓
   strategies 更新:
     public: 0 → 1
   
   填写 strategy_dsl_versions.risk_disclosure:
     {
       "main_risks": ["震荡市频繁止损"],
       "suitable_market": ["单边趋势"],
       "min_capital": 5000,
       "stop_loss_advice": "单笔不超过2%"
     }

结果：
✅ 策略可以被其他用户搜索到
✅ 策略可以被订阅
✅ 作者可以继续编辑（会创建新版本）
```

### 流程 2：用户订阅并使用策略

```
1. 用户B搜索策略
   ↓
   查询: strategies where status='active' and public=1
   
   显示:
   - 策略名称: "RSI策略"
   - 当前版本: v1.0.3
   - 质量评分: 75/100
   - 回测收益: +45%
   - 胜率: 62%
   - 订阅人数: 0

2. 用户B订阅策略
   ↓
   strategy_instances 新增记录:
   {
     strategy_id: "uuid-1234",
     user_id: "user-002",
     locked_version: v1.0.3,  // 锁定当前版本
     status: "stopped",
     environment: "paper"  // 用户选择模拟盘
   }
   
   strategies 更新:
     total_subscribers: 0 → 1

3. 用户B启动策略
   ↓
   strategy_instances 更新:
     status: "stopped" → "running"
     started_at: "2025-11-21 11:00:00"

4. 策略作者发布新版本 v1.1.0
   ↓
   strategies 更新:
     current_version: v1.0.3 → v1.1.0
   
   strategy_dsl_versions:
     v1.0.3: version_status → "inactive"
     v1.1.0: version_status → "active" (新)
   
   strategy_instances (用户B的实例):
     has_newer_version: 0 → 1  // 标记有新版本

5. 用户B看到升级提示
   ↓
   前端显示:
   "⚠️ 策略有新版本 v1.1.0 可用
    当前版本: v1.0.3
    更新内容: 新增MACD确认信号
    [ 查看详情 ] [ 继续使用旧版本 ] [ 升级 ]"

6. 用户B选择升级
   ↓
   strategy_instances 更新:
     locked_version: v1.0.3 → v1.1.0
     has_newer_version: 1 → 0
     updated_at: NOW()

结果：
✅ 用户B使用最新版本
✅ 旧版本 v1.0.3 保持 inactive（其他用户可能还在用）
```

### 流程 3：系统检测异常并处理

```
1. 系统监控检测到策略 uuid-1234 的多个实例连续失败
   ↓
   查询发现:
   - 实例1 (用户B): consecutive_errors = 6
   - 实例2 (用户C): consecutive_errors = 5
   - 实例3 (用户D): consecutive_errors = 7
   
   错误日志: "DSL解析失败：函数 'rsi_custom' 不存在"

2. 系统自动标记策略为异常
   ↓
   strategies 更新:
     status: "active" → "anomaly"
     anomaly_reason: "DSL解析失败：函数 'rsi_custom' 不存在。可能原因：DSL规范更新"
     status_changed_at: NOW()

3. 系统暂停所有运行中的实例
   ↓
   strategy_instances 批量更新:
     status: "running" → "error"
     error_message: "策略已被系统标记为异常，自动暂停"
     status_changed_at: NOW()

4. 系统发送通知
   ↓
   通知作者: "您的策略 'RSI策略' 检测到异常，已自动暂停"
   通知所有用户: "您使用的策略 'RSI策略' 检测到异常，已暂停运行"

5. 作者查看并修复
   ↓
   作者编辑策略:
   - 将 'rsi_custom' 改为 'rsi'
   - 保存新版本 v1.1.1 (patch)
   
   strategy_dsl_versions 新增:
     v1.1.1 (patch, draft)
     change_description: "修复DSL兼容性问题"

6. 作者激活新版本
   ↓
   strategy_dsl_versions 更新 v1.1.1:
     version_status: "draft" → "active"
   
   strategies 更新:
     status: "anomaly" → "active"
     anomaly_reason: NULL
     current_version: v1.1.0 → v1.1.1
   
   strategy_instances 批量更新:
     has_newer_version: 1
     force_upgrade_required: 1  // 标记为强制升级

7. 用户收到修复通知
   ↓
   "✅ 策略 'RSI策略' 已修复
    新版本: v1.1.1
    修复内容: 修复DSL兼容性问题
    [ 立即升级并恢复运行 ]"

8. 用户升级并恢复
   ↓
   strategy_instances 更新:
     locked_version: v1.1.0 → v1.1.1
     status: "error" → "running"
     force_upgrade_required: 0
     error_message: NULL
     consecutive_errors: 0  // 重置错误计数

结果：
✅ 异常已修复
✅ 所有用户升级到修复版本
✅ 策略恢复正常运行
```

### 流程 4：策略归档

```
1. 作者决定归档策略（发现严重缺陷）
   ↓
   前端显示确认对话框:
   "⚠️ 确认归档策略？
    当前有 3 个实例正在运行，将被强制停止
    归档原因: [发现严重bug ▼]"

2. 作者确认归档
   ↓
   strategies 更新:
     status: "active" → "archived"
     status_changed_at: NOW()
   
   strategy_dsl_versions 更新所有版本:
     WHERE strategy_id = "uuid-1234"
     SET deprecation_reason = "发现严重bug，策略已归档"

3. 系统停止所有实例
   ↓
   strategy_instances 批量更新:
     status: "running"/"paused" → "stopped"
     stopped_at: NOW()
     notes: CONCAT(notes, "\n[系统] 策略已归档，实例已停止")

4. 系统发送通知
   ↓
   通知所有用户:
   "您使用的策略 'RSI策略' 已被作者归档
    归档原因: 发现严重bug
    您的实例已自动停止
    建议: 寻找其他替代策略或复制代码自行修复"

5. 用户查看归档策略
   ↓
   前端显示:
   "📦 该策略已归档（不可用）
    归档原因: 发现严重bug
    归档时间: 2025-11-21 15:00
    
    您可以:
    ✅ 查看代码（只读）
    ✅ 复制到新策略
    ❌ 不可运行或交易
    
    [ 查看代码 ] [ 复制到新策略 ]"

结果：
✅ 策略完全不可用
✅ 所有实例已停止
✅ 用户可以复制代码自行修复
❌ 归档不可逆
```

---

## 📈 策略质量评分算法

### 评分模型（0-100分）

```python
def calculate_quality_score(backtest_result):
    """
    计算策略质量评分
    
    评分维度：
    1. 收益表现 (30分)
    2. 风险控制 (25分)
    3. 稳定性 (20分)
    4. 数据充分性 (15分)
    5. 交易效率 (10分)
    """
    score = 0
    
    # 1. 收益表现 (30分)
    total_return = backtest_result['total_return']
    if total_return > 1.0:      # 收益>100%
        score += 30
    elif total_return > 0.5:    # 收益>50%
        score += 25
    elif total_return > 0.3:    # 收益>30%
        score += 20
    elif total_return > 0.1:    # 收益>10%
        score += 15
    elif total_return > 0:      # 收益>0
        score += 10
    else:
        score += 0              # 亏损
    
    # 2. 风险控制 (25分)
    # 2.1 夏普比率 (15分)
    sharpe_ratio = backtest_result['sharpe_ratio']
    if sharpe_ratio > 3.0:
        score += 15
    elif sharpe_ratio > 2.0:
        score += 12
    elif sharpe_ratio > 1.5:
        score += 10
    elif sharpe_ratio > 1.0:
        score += 7
    elif sharpe_ratio > 0.5:
        score += 4
    else:
        score += 0
    
    # 2.2 最大回撤 (10分)
    max_drawdown = backtest_result['max_drawdown']
    if max_drawdown < 0.05:     # 回撤<5%
        score += 10
    elif max_drawdown < 0.10:   # 回撤<10%
        score += 8
    elif max_drawdown < 0.15:   # 回撤<15%
        score += 6
    elif max_drawdown < 0.20:   # 回撤<20%
        score += 4
    elif max_drawdown < 0.30:   # 回撤<30%
        score += 2
    else:
        score += 0              # 回撤>=30%
    
    # 3. 稳定性 (20分)
    # 3.1 胜率 (10分)
    win_rate = backtest_result['win_rate']
    if win_rate > 0.70:
        score += 10
    elif win_rate > 0.60:
        score += 8
    elif win_rate > 0.50:
        score += 6
    elif win_rate > 0.40:
        score += 4
    elif win_rate > 0.30:
        score += 2
    else:
        score += 0
    
    # 3.2 盈亏比 (10分)
    profit_factor = backtest_result['profit_factor']
    if profit_factor > 3.0:
        score += 10
    elif profit_factor > 2.5:
        score += 8
    elif profit_factor > 2.0:
        score += 6
    elif profit_factor > 1.5:
        score += 4
    elif profit_factor > 1.0:
        score += 2
    else:
        score += 0
    
    # 4. 数据充分性 (15分)
    total_trades = backtest_result['total_trades']
    if total_trades >= 100:
        score += 15
    elif total_trades >= 50:
        score += 12
    elif total_trades >= 30:
        score += 9
    elif total_trades >= 20:
        score += 6
    elif total_trades >= 10:
        score += 3
    else:
        score += 0              # 样本量不足
    
    # 5. 交易效率 (10分)
    avg_trade_duration = backtest_result['avg_trade_duration']  # 小时
    if 24 <= avg_trade_duration <= 168:  # 1-7天
        score += 10  # 适中的持仓时间
    elif 12 <= avg_trade_duration < 24 or 168 < avg_trade_duration <= 336:
        score += 7   # 略短或略长
    elif avg_trade_duration < 12:
        score += 4   # 过度频繁
    else:
        score += 4   # 过长
    
    return min(score, 100)  # 确保不超过100


def get_risk_level(score):
    """根据评分确定风险等级"""
    if score >= 80:
        return "low"          # 低风险
    elif score >= 60:
        return "medium-low"   # 中低风险
    elif score >= 40:
        return "medium"       # 中等风险
    elif score >= 20:
        return "medium-high"  # 中高风险
    else:
        return "high"         # 高风险
```

### 评分更新时机

```sql
-- 策略首次回测完成后计算评分
TRIGGER after_backtest_insert
UPDATE strategies s
JOIN strategy_dsl_versions sdv ON s.id = sdv.strategy_id
SET 
    s.quality_score = calculate_quality_score(sdv.backtest_data),
    s.risk_level = get_risk_level(s.quality_score)
WHERE 
    sdv.version_status = 'active'
    AND sdv.backtest_completed_at IS NOT NULL;

-- 定期重新计算（基于所有用户的实盘数据）
SCHEDULE daily_score_recalculation
UPDATE strategies s
SET s.quality_score = (
    SELECT AVG(calculate_quality_score(si.performance_data))
    FROM strategy_instances si
    WHERE si.strategy_id = s.id
    AND si.environment = 'live'
    AND si.performance_data IS NOT NULL
)
WHERE s.status = 'active' AND s.public = 1;
```

---

## 🎨 前端UI设计建议

### 策略列表视图

```
┌─────────────────────────────────────────────────────────┐
│ 我的策略                                    [+ 新建策略] │
├─────────────────────────────────────────────────────────┤
│                                                         │
│ 🔍 [搜索策略...]                    [全部 ▼] [排序 ▼]  │
│                                                         │
│ ✅ 激活中 (3)                                           │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ RSI超卖买入策略                          v1.2.0 ✅  │ │
│ │ BTCUSDT · 趋势跟踪 · 评分: 75/100                   │ │
│ │ 订阅: 12人 · 最后更新: 2025-11-20                   │ │
│ │ [编辑] [回测] [查看实例] [设置]                     │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ 📝 草稿 (5)                                             │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ MACD金叉策略                            v1.0.3 📝   │ │
│ │ ETHUSDT · 开发中                                    │ │
│ │ ⚠️ 未完成回测 · 最后编辑: 1小时前                   │ │
│ │ [继续编辑] [回测] [激活] [删除]                     │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ 🔴 异常 (1)                                             │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ 网格策略                                v2.1.0 🔴   │ │
│ │ BTCUSDT · DSL解析失败                               │ │
│ │ ⚠️ 检测时间: 2025-11-21 15:30                       │ │
│ │ [查看详情] [尝试修复] [归档]                        │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ 📦 已归档 (7) [展开 ▼]                                 │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### 策略详情页

```
┌─────────────────────────────────────────────────────────┐
│ ◄ 返回                                                  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│ RSI超卖买入策略                              v1.2.0 ✅  │
│ 作者: @YourName · BTCUSDT · 趋势跟踪                    │
│                                                         │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ 📊 策略评分: 75/100 (中低风险)                      │ │
│ │                                                     │ │
│ │ 🟢 总收益: +45%        🟢 夏普比率: 1.8             │ │
│ │ 🟡 最大回撤: -15%      🟢 胜率: 62%                 │ │
│ │ 🟢 盈亏比: 2.1         🟢 交易次数: 85              │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ [回测] [编辑] [公开分享] [归档]                         │
│                                                         │
│ ┌─ 策略描述 ─────────────────────────────────────────┐ │
│ │ 基于RSI超卖信号买入，RSI超买信号卖出。              │ │
│ │ 适合单边趋势行情，不适合震荡市。                    │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ ┌─ 风险提示 ─────────────────────────────────────────┐ ���
│ │ ⚠️ 主要风险:                                        │ │
│ │ • 震荡市场可能频繁止损                              │ │
│ │ • 需要较大资金规模（建议>$5000）                    │ │
│ │                                                     │ │
│ │ 💡 建议:                                            │ │
│ │ • 适用市场: 单边趋势行情                            │ │
│ │ • 止损建议: 单笔不超过本金2%                        │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ ┌─ 版本历史 ─────────────────────────────────────────┐ │
│ │ v1.2.0 (active) · 2025-11-20                        │ │
│ │ └─ 新增止损保护逻辑                                 │ │
│ │                                                     │ │
│ │ v1.1.0 (inactive) · 2025-11-15                      │ │
│ │ └─ 新增MACD确认信号                                 │ │
│ │                                                     │ │
│ │ v1.0.0 (inactive) · 2025-11-01                      │ │
│ │ └─ 初始版本                                         │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ ┌─ 订阅用户 (12) ────────────────────────────────────┐ │
│ │ 实盘: 5人 · 模拟盘: 7人                             │ │
│ │ 平均收益: +12.3%                                    │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 📝 总结

### 关键要点

1. **三层状态管理**：策略状态、版本状态、实例状态独立管理
2. **草稿可回测**：用户必须能在草稿状态验证策略
3. **版本锁定模式**：激活后编辑会创建新版本，保护已有版本
4. **智能异常检测**：系统自动检测技术异常，不管策略表现
5. **不设置胜率门槛**：提供信息披露，用户自行判断
6. **归档不可逆**：归档是最终状态，需谨慎操作
7. **语义化版本号**：系统自动计算，用户选择变更类型

### 状态流转总览

```
策略生命周期：
draft → active → deprecated → archived
                     ↓
                  anomaly → archived (或修复后 → active)

版本生命周期：
draft → active → inactive

实例生命周期：
stopped ⇄ running ⇄ paused
           ↓
         error → stopped
```

### 数据库表总结

| 表名 | 用途 | 核心字段 |
|------|------|----------|
| strategies | 策略主表 | status (策略状态)<br/>current_version (当前版本)<br/>quality_score (质量评分) |
| strategy_dsl_versions | 版本管理表 | version_status (版本状态)<br/>dsl (代码)<br/>backtest_data (回测数据) |
| strategy_instances | 用户实例表 | status (运行状态)<br/>locked_version (锁定版本)<br/>performance_data (绩效) |

---

**文档结束**

如有疑问或需要补充，请联系 Prophet 开发团队。

