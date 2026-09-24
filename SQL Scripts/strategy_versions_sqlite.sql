-- ============================================
-- Prophet 策略版本管理表 (SQLite版本)
-- 基于 MySQL 版本转换
-- ============================================

-- 删除旧表（如果存在）
DROP TABLE IF EXISTS strategy_versions;

-- 创建策略版本表
CREATE TABLE strategy_versions (
    -- 基础字段
    id INTEGER PRIMARY KEY AUTOINCREMENT,                               -- 版本记录ID
    strategy_id TEXT NOT NULL,                                          -- 策略ID
    
    -- 版本号字段
    major_version INTEGER NOT NULL DEFAULT 1,                           -- 主版本号（不兼容的重大变更）
    minor_version INTEGER NOT NULL DEFAULT 0,                           -- 次版本号（向下兼容的功能增强）
    patch_version INTEGER NOT NULL DEFAULT 0,                           -- 修订版本号（向下兼容的问题修复）
    version_string TEXT NOT NULL,                                       -- 完整版本号字符串（如 v1.0.0）
    
    -- 变更信息
    change_type TEXT NOT NULL CHECK(change_type IN ('major', 'minor', 'patch', 'initial')), -- 变更类型
    dsl TEXT NOT NULL,                                                  -- Prophet DSL代码内容
    parameters TEXT NOT NULL DEFAULT '{}',                              -- 策略参数（JSON格式）
    dsl_hash TEXT,                                                      -- DSL代码的SHA-256哈希值
    
    -- 版本状态
    version_status TEXT NOT NULL DEFAULT 'draft' CHECK(version_status IN ('draft', 'active', 'inactive')), -- 版本状态
    status_changed_at TEXT,                                             -- 版本状态最后变更时间
    
    -- 验证信息
    validation_status TEXT NOT NULL DEFAULT 'pending' CHECK(validation_status IN ('pending', 'valid', 'invalid')), -- DSL验证状态
    validation_message TEXT,                                            -- 验证消息（错误信息或警告）
    validated_at TEXT,                                                  -- DSL验证时间
    
    -- 回测信息
    backtest_data TEXT,                                                 -- 回测结果快照（JSON格式）
    backtest_completed_at TEXT,                                         -- 回测完成时间
    
    -- 变更说明
    change_description TEXT,                                            -- 版本变更说明
    breaking_changes TEXT,                                              -- 破坏性变更说明（Major版本升级时必填）
    
    -- 风险和标签
    risk_disclosure TEXT,                                               -- 风险披露信息（JSON格式）
    tags TEXT,                                                          -- 版本标签（JSON数组格式）
    
    -- 废弃和升级
    deprecation_reason TEXT,                                            -- 废弃原因
    force_upgrade INTEGER NOT NULL DEFAULT 0,                           -- 是否强制升级（0=否，1=是）
    upgrade_deadline TEXT,                                              -- 强制升级截止日期
    
    -- 审计字段
    created_by TEXT NOT NULL DEFAULT 'system',                          -- 创建者ID
    created_at TEXT NOT NULL,                                           -- 版本创建时间
    activated_at TEXT,                                                  -- 版本激活时间
    
    -- 外键约束
    FOREIGN KEY (strategy_id) REFERENCES strategies(id) ON DELETE CASCADE,
    
    -- 唯一约束：同一策略的版本号必须唯一
    UNIQUE(strategy_id, major_version, minor_version, patch_version)
);

-- 创建索引
CREATE INDEX idx_versions_strategy_id ON strategy_versions(strategy_id);
CREATE INDEX idx_versions_version_status ON strategy_versions(version_status);
CREATE INDEX idx_versions_validation_status ON strategy_versions(validation_status);
CREATE INDEX idx_versions_version_string ON strategy_versions(version_string);
CREATE INDEX idx_versions_created_at ON strategy_versions(created_at DESC);
CREATE INDEX idx_versions_created_by ON strategy_versions(created_by);
CREATE INDEX idx_versions_dsl_hash ON strategy_versions(dsl_hash);

-- 创建复合索引：策略ID + 版本号（用于快速查询特定版本）
CREATE INDEX idx_versions_strategy_version ON strategy_versions(strategy_id, major_version DESC, minor_version DESC, patch_version DESC);

-- 说明：
-- 1. SQLite 不支持 GENERATED ALWAYS AS，所以 version_string 需要在插入时手动构造
-- 2. SQLite 使用 TEXT 类型存储日期时间（格式：yyyy-MM-dd HH:mm:ss）
-- 3. SQLite 的 JSON 字段使用 TEXT 类型存储
-- 4. SQLite 的 ENUM 类型使用 CHECK 约束实现
-- 5. SQLite 的 AUTOINCREMENT 相当于 MySQL 的 AUTO_INCREMENT

