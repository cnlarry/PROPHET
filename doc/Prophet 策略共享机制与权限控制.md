# Prophet 策略共享机制与权限控制

## 📋 目录
1. [策略共享机制](#策略共享机制)
2. [策略状态与共享权限](#策略状态与共享权限)
3. [所有者对已共享策略的操作](#所有者对已共享策略的操作)
4. [操作影响矩阵](#操作影响矩阵)
5. [实施细节](#实施细节)

---

## 策略共享机制

### 共享的前提条件

```
策略可被其他用户订阅的充要条件：
├─ enabled = true     (策略已激活)
└─ public = true      (所有者主动设置为公开)

任何不满足以上条件的策略都是"私有策略"，只有所有者可见。
```

### 共享流程

```
┌─────────────────────────────────────────────────────────────┐
│ 阶段1：创建与测试（私有阶段）                                │
│ ┌───────────────────────────────────────────────────────┐   │
│ │ 用户A创建策略                                         │   │
│ │ ├─ enabled: false (草稿)                             │   │
│ │ ├─ public: false                                     │   │
│ │ └─ 只有用户A可见                                      │   │
│ │                                                       │   │
│ │ 用户A激活策略（私有激活）                              │   │
│ │ ├─ enabled: true                                     │   │
│ │ ├─ public: false  ← 重点：不公开                      │   │
│ │ └─ 仍然只有用户A可见，可以自己测试                      │   │
│ └───────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ 阶段2：主动共享（公开阶段）                                  │
│ ┌───────────────────────────────────────────────────────┐   │
│ │ 用户A决定共享策略                                      │   │
│ │ ├─ 右键菜单 → "共享策略"                              │   │
│ │ ├─ 确认对话框：                                       │   │
│ │ │   "共享后其他用户可以订阅此策略"                      │   │
│ │ │   "您仍然拥有完全控制权"                             │   │
│ │ ├─ 点击确认                                           │   │
│ │ └─ public: true                                       │   │
│ │                                                       │   │
│ │ 策略进入"策略市场"                                     │   │
│ │ ├─ 所有用户可以看到                                    │   │
│ │ ├─ 其他用户可以订阅                                    │   │
│ │ └─ 用户A仍是所有者，拥有完全控制                        │   │
│ └───────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ 阶段3：取消共享（可选）                                      │
│ ┌───────────────────────────────────────────────────────┐   │
│ │ 用户A取消共享                                          │   │
│ │ ├─ 右键菜单 → "取消共享"                              │   │
│ │ ├─ 警告对话框：                                        │   │
│ │ │   "已有 X 个用户订阅此策略"                          │   │
│ │ │   "取消共享后：                                      │   │
│ │ │   - 新用户无法订阅                                   │   │
│ │ │   - 现有订阅者仍可使用当前版本                        │   │
│ │ │   - 现有订阅者无法升级到新版本"                       │   │
│ │ ├─ 点击确认                                           │   │
│ │ └─ public: false                                      │   │
│ │                                                       │   │
│ │ 策略从"策略市场"移除                                   │   │
│ │ ├─ 新用户看不到此策略                                  │   │
│ │ ├─ 现有订阅者的实例仍然存在                            │   │
│ │ └─ 现有订阅者看不到新版本更新                          │   │
│ └───────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

---

## 策略状态与共享权限

### 状态组合表

| enabled | public | 状态描述 | 所有者可见 | 他人可见 | 他人可订阅 |
|---------|--------|---------|-----------|---------|-----------|
| false | false | 草稿（未激活） | ✅ | ❌ | ❌ |
| false | true | 不存在（无效状态）| - | - | - |
| true | false | 私有激活 | ✅ | ❌ | ❌ |
| true | true | 公开共享 | ✅ | ✅ | ✅ |

**重要说明：**
- `enabled=false, public=true` 是无效状态，系统不允许出现
- 只有激活的策略才能设置为公开
- 公开的策略必须是激活状态

### 状态转换图

```
┌──────────────┐                    ┌──────────────┐
│   草稿状态    │  激活（私有）        │  私有激活     │
│ enabled=F    │─────────────────→  │ enabled=T    │
│ public=F     │                    │ public=F     │
└──────────────┘                    └──────┬───────┘
       ↑                                   │
       │ 返回草稿                            │ 共享
       │ (enabled=F)                       │ (public=T)
       │                                   ↓
       │                            ┌──────────────┐
       └────────────────────────────│  公开共享     │
              废弃/归档               │ enabled=T    │
              (enabled=F)            │ public=T     │
                                    └──────┬───────┘
                                           │
                                           │ 取消共享
                                           │ (public=F)
                                           ↓
                                    ┌──────────────┐
                                    │  私有激活     │
                                    │ enabled=T    │
                                    │ public=F     │
                                    └──────────────┘
```

---

## 所有者对已共享策略的操作

### 操作分类

当策略已被其他用户订阅后（strategy_instances 表有记录），所有者仍然可以进行以下操作，但需要考虑对订阅者的影响：

#### 类型A：无影响操作（随时可做）

1. **创建新版本**
   - 操作：修改DSL代码，保存创建新版本
   - 影响：❌ 无影响
   - 原因：订阅者锁定旧版本，不会自动升级
   - 订阅者看到：前端提示"有新版本可用"，可选择升级

2. **查看订阅者统计**
   - 操作：查看有多少用户订阅了哪些版本
   - 影响：❌ 无影响
   - 原因：只读操作

#### 类型B：有限影响操作（需谨慎）

3. **将旧版本标记为 deprecated**
   - 操作：右键版本 → "标记为已废弃"
   - 影响：⚠️ 轻微影响
   - 订阅者影响：
     - 使用该版本的用户看到"建议升级"提示
     - 策略仍可正常运行
     - 可以选择继续使用或升级

4. **取消共享 (public: true → false)**
   - 操作：右键策略 → "取消共享"
   - 影响：⚠️ 中等影响
   - 订阅者影响：
     - 现有订阅者：仍可使用当前锁定的版本
     - 现有订阅者：看不到新版本更新（因为策略不公开了）
     - 新用户：无法订阅此策略
   
5. **修改策略描述/备注**
   - 操作：编辑策略信息
   - 影响：⚠️ 轻微影响
   - 订阅者影响：
     - 看到更新后的策略描述
     - 不影响代码和运行

#### 类型C：强影响操作（需警告）

6. **将版本归档 (status: archived)**
   - 操作：右键版本 → "归档版本"
   - 影响：🔴 严重影响
   - 订阅者影响：
     - 使用该版本的实例立即停止运行
     - 设置 `force_upgrade_required=true`
     - 用户必须升级到其他版本才能继续使用
   - 前置警告：
     ```
     ⚠️ 警告：归档此版本
     
     当前有 X 个用户正在使用 v2.0.0
     
     归档后：
     • 所有使用此版本的实例将立即停止
     • 用户必须升级到其他版本
     • 此操作通常用于严重bug或安全漏洞
     
     确定要继续吗？
     ```

7. **废弃整个策略 (enabled: false, 但不删除)**
   - 操作：右键策略 → "废弃策略"
   - 影响：🔴 中等影响
   - 订阅者影响：
     - 策略从市场移除，新用户无法订阅
     - 现有订阅者可以继续使用当前版本
     - 现有订阅者看不到新版本更新
     - 前端显示"策略已废弃"徽章
   - 前置警告：
     ```
     ⚠️ 废弃策略
     
     当前有 X 个用户订阅此策略
     
     废弃后：
     • 策略将从市场移除
     • 现有用户可继续使用当前版本
     • 不会再有新用户订阅
     • 您无法发布新版本
     
     废弃原因（可选）：_____________
     ```

#### 类型D：禁止操作（有订阅者时不允许）

8. **删除策略**
   - 操作：右键策略 → "删除"
   - 限制：❌ 禁止
   - 原因：已有用户依赖此策略
   - 错误提示：
     ```
     ❌ 无法删除策略
     
     此策略有 X 个订阅者
     
     如需停用策略，请选择：
     • "废弃策略"：保留给现有用户
     • "归档所有版本"：强制所有人停用
     
     只有在以下情况才能删除：
     • 策略是草稿状态 (enabled=false)
     • 没有任何订阅者
     ```

9. **修改策略名称**
   - 操作：重命名策略
   - 限制：❌ 禁止（已激活的策略）
   - 原因：避免混淆订阅者
   - 允许条件：只有草稿状态 (enabled=false) 可以重命名

10. **修改交易对 (Symbol)**
    - 操作：编辑信息 → 修改Symbol
    - 限制：❌ 禁止（已激活的策略）
    - 原因：交易对是策略的核心属性，修改会导致完全不同的策略
    - 允许条件：只有草稿状态 (enabled=false) 可以修改

---

## 操作影响矩阵

### 完整矩阵表

| # | 操作 | 草稿 | 私有激活 | 已共享(无订阅) | 已共享(有订阅) | 对订阅者影响 |
|---|------|-----|---------|--------------|--------------|-------------|
| 1 | 创建新版本 | ✅ | ✅ | ✅ | ✅ | 无影响，版本锁定 |
| 2 | 查看统计 | ✅ | ✅ | ✅ | ✅ | 无影响，只读 |
| 3 | 标记版本为deprecated | ❌ | ✅ | ✅ | ⚠️ 需警告 | 提示升级，仍可用 |
| 4 | 取消共享 | ❌ | ❌ | ✅ | ⚠️ 需警告 | 无法看到新版本 |
| 5 | 修改备注 | ✅ | ✅ | ✅ | ✅ | 看到新描述 |
| 6 | 归档版本 | ❌ | ✅ | ✅ | 🔴 需强警告 | 强制停止，必须升级 |
| 7 | 废弃策略 | ❌ | ✅ | ✅ | 🔴 需警告 | 无新版本，现有可用 |
| 8 | 删除策略 | ✅ 无订阅 | ✅ 无订阅 | ✅ 无订阅 | ❌ 禁止 | N/A |
| 9 | 重命名 | ✅ | ❌ | ❌ | ❌ | N/A |
| 10 | 修改Symbol | ✅ | ❌ | ❌ | ❌ | N/A |
| 11 | 激活策略 | ✅ | ❌ | ❌ | ❌ | N/A |
| 12 | 共享策略 | ❌ | ✅ | ❌ | ❌ | N/A |

### 影响级别说明

- ✅ **允许**：无限制，随时可做
- ⚠️ **需警告**：允许但需要提示影响范围
- 🔴 **需强警告**：允许但有严重影响，需要二次确认
- ❌ **禁止**：不允许执行此操作

---

## 实施细节

### 1. 前端权限控制

#### 菜单项显示规则

```typescript
// 策略卡片右键菜单
const contextMenu = {
  // 基础操作
  "编辑": strategy.status === "draft" && isOwner,
  "重命名": strategy.status === "draft" && isOwner,
  "编辑信息": strategy.status === "draft" && isOwner,
  
  // 状态转换
  "激活（私有）": strategy.enabled === false && isOwner,
  "激活并共享": strategy.enabled === false && isOwner,
  "共享策略": strategy.enabled === true && strategy.public === false && isOwner,
  "取消共享": strategy.enabled === true && strategy.public === true && isOwner,
  
  // 版本管理
  "创建新版本": isOwner,
  "版本管理": isOwner || isSubscriber,
  
  // 危险操作
  "废弃策略": strategy.enabled === true && isOwner,
  "删除策略": strategy.enabled === false && subscriberCount === 0 && isOwner,
  
  // 订阅操作
  "订阅策略": strategy.public === true && !isOwner && !hasSubscribed,
  "取消订阅": isSubscriber && !isOwner,
  "升级版本": isSubscriber && hasNewerVersion,
};
```

### 2. 后端权限验证

#### API 端点权限

```csharp
// 策略操作权限检查
public class StrategyPermissionService
{
    // 检查是否可以修改策略信息
    public async Task<bool> CanEditStrategyInfoAsync(string strategyId, string userId)
    {
        var strategy = await GetStrategyAsync(strategyId);
        
        // 必须是所有者
        if (strategy.OwnerId != userId) return false;
        
        // 必须是草稿状态
        if (strategy.Enabled) return false;
        
        return true;
    }
    
    // 检查是否可以删除策略
    public async Task<bool> CanDeleteStrategyAsync(string strategyId, string userId)
    {
        var strategy = await GetStrategyAsync(strategyId);
        
        // 必须是所有者
        if (strategy.OwnerId != userId) return false;
        
        // 必须是草稿状态
        if (strategy.Enabled) return false;
        
        // 必须没有订阅者
        var subscriberCount = await GetSubscriberCountAsync(strategyId);
        if (subscriberCount > 0) return false;
        
        return true;
    }
    
    // 检查是否可以归档版本
    public async Task<bool> CanArchiveVersionAsync(string strategyId, string versionString, string userId)
    {
        var strategy = await GetStrategyAsync(strategyId);
        
        // 必须是所有者
        if (strategy.OwnerId != userId) return false;
        
        return true;
    }
    
    // 获取操作影响范围
    public async Task<OperationImpact> GetOperationImpactAsync(
        string strategyId, 
        string operation)
    {
        var subscriberCount = await GetSubscriberCountAsync(strategyId);
        var strategy = await GetStrategyAsync(strategyId);
        
        return operation switch
        {
            "archive_version" => new OperationImpact
            {
                Level = ImpactLevel.Critical,
                AffectedUsers = await GetVersionSubscribersAsync(strategyId, versionString),
                Message = $"将强制停止 {affectedCount} 个用户的实例"
            },
            
            "deprecate_strategy" => new OperationImpact
            {
                Level = ImpactLevel.Warning,
                AffectedUsers = subscriberCount,
                Message = $"将影响 {subscriberCount} 个订阅者"
            },
            
            "cancel_share" => new OperationImpact
            {
                Level = ImpactLevel.Warning,
                AffectedUsers = subscriberCount,
                Message = $"{subscriberCount} 个用户将无法看到新版本"
            },
            
            _ => new OperationImpact { Level = ImpactLevel.None }
        };
    }
}

public enum ImpactLevel
{
    None,       // 无影响
    Info,       // 信息提示
    Warning,    // 需要警告
    Critical    // 严重影响，需要强确认
}
```

### 3. 数据库约束

#### 添加检查约束

```sql
-- 确保 public=true 时必须 enabled=true
ALTER TABLE strategies
ADD CONSTRAINT chk_public_requires_enabled
CHECK (public = 0 OR (public = 1 AND enabled = 1));

-- 或者使用触发器实现更复杂的逻辑
DELIMITER $$
CREATE TRIGGER before_strategy_update
BEFORE UPDATE ON strategies
FOR EACH ROW
BEGIN
    -- 如果设置为公开，必须先激活
    IF NEW.public = 1 AND NEW.enabled = 0 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = '策略必须先激活才能设置为公开';
    END IF;
    
    -- 如果有订阅者，不能删除（通过enabled判断是否要删除）
    IF NEW.enabled = 0 AND OLD.enabled = 1 THEN
        DECLARE subscriber_count INT;
        SELECT COUNT(*) INTO subscriber_count
        FROM strategy_instances
        WHERE strategy_id = NEW.id;
        
        -- 这里不阻止，只是记录日志，实际删除在应用层控制
    END IF;
END$$
DELIMITER ;
```

### 4. 订阅者通知机制

#### 通知类型

```typescript
enum NotificationType {
  // 信息通知
  INFO_NEW_VERSION = "new_version_available",      // 有新版本可用
  INFO_DESCRIPTION_UPDATED = "description_updated", // 描述更新
  
  // 警告通知  
  WARNING_VERSION_DEPRECATED = "version_deprecated", // 版本被标记为废弃
  WARNING_STRATEGY_UNSHARED = "strategy_unshared",   // 策略取消共享
  WARNING_STRATEGY_DEPRECATED = "strategy_deprecated", // 策略废弃
  
  // 强制通知
  CRITICAL_VERSION_ARCHIVED = "version_archived",    // 版本归档，强制升级
  CRITICAL_FORCE_UPGRADE = "force_upgrade_required", // 需要强制升级
}

// 通知发送时机
const notificationTriggers = {
  // 所有者创建新版本 → 通知所有订阅者
  onNewVersionCreated: (strategyId, newVersion) => {
    notifySubscribers(strategyId, {
      type: NotificationType.INFO_NEW_VERSION,
      message: `策略有新版本 ${newVersion} 可用`,
      action: "查看更新内容",
    });
  },
  
  // 所有者归档版本 → 强制通知使用该版本的用户
  onVersionArchived: (strategyId, version) => {
    notifyVersionUsers(strategyId, version, {
      type: NotificationType.CRITICAL_VERSION_ARCHIVED,
      message: `版本 ${version} 已归档，您的实例已停止`,
      action: "立即升级",
      blocking: true, // 阻断式通知
    });
  },
};
```

### 5. 前端确认对话框模板

#### 归档版本确认

```typescript
const confirmArchiveVersion = async (strategy, version) => {
  const impact = await api.getOperationImpact(strategy.id, "archive_version", version);
  
  const dialog = ModernDialogPresets.Confirm(
    "⚠️ 归档版本",
    `
    您即将归档版本 ${version}
    
    当前有 ${impact.affectedUsers} 个用户正在使用此版本
    
    归档后：
    • 所有使用此版本的实例将立即停止
    • 用户必须升级到其他版本才能继续
    • 此操作通常用于严重bug或安全漏洞
    
    归档原因：
    `,
    async (confirmed, reason) => {
      if (confirmed) {
        await api.archiveVersion(strategy.id, version, reason);
      }
    }
  );
  
  await dialog.ShowDialog(window);
};
```

#### 取消共享确认

```typescript
const confirmUnshare = async (strategy) => {
  const impact = await api.getOperationImpact(strategy.id, "cancel_share");
  
  const dialog = ModernDialogPresets.Confirm(
    "取消共享策略",
    `
    当前有 ${impact.affectedUsers} 个用户订阅此策略
    
    取消共享后：
    • 策略将从市场移除
    • 新用户无法订阅
    • 现有订阅者仍可使用当前版本
    • 现有订阅者无法看到新版本更新
    
    您确定要取消共享吗？
    `,
    async (confirmed) => {
      if (confirmed) {
        await api.unshareStrategy(strategy.id);
      }
    }
  );
  
  await dialog.ShowDialog(window);
};
```

---

## 总结

### 核心原则

1. **共享必须主动**
   - 只有所有者明确设置 `public=true` 才能被订阅
   - 默认所有策略都是私有的

2. **所有者始终拥有控制权**
   - 可以创建新版本
   - 可以标记版本状态
   - 可以取消共享
   - 可以归档或废弃

3. **订阅者权益保护**
   - 版本锁定机制保护订阅者不受影响
   - 强影响操作需要明确警告
   - 提供明确的升级路径

4. **操作透明化**
   - 所有影响订阅者的操作都要显示影响范围
   - 订阅者及时收到通知
   - 操作日志可追溯

### 实施建议

1. **第一阶段**：实现基础共享机制
   - 激活并共享
   - 取消共享
   - 订阅/取消订阅

2. **第二阶段**：实现权限控制
   - 前端菜单权限
   - 后端API权限验证
   - 数据库约束

3. **第三阶段**：实现通知机制
   - 版本更新通知
   - 强制升级通知
   - 邮件/站内信

4. **第四阶段**：完善用户体验
   - 影响预览
   - 订阅者统计
   - 操作历史

