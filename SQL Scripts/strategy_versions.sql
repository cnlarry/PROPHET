/*
 Navicat Premium Dump SQL

 Source Server         : localhost
 Source Server Type    : MySQL
 Source Server Version : 90400 (9.4.0)
 Source Host           : localhost:3306
 Source Schema         : prophet

 Target Server Type    : MySQL
 Target Server Version : 90400 (9.4.0)
 File Encoding         : 65001

 Date: 21/11/2025 23:59:28
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for strategy_versions
-- ----------------------------
DROP TABLE IF EXISTS `strategy_versions`;
CREATE TABLE `strategy_versions`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '版本记录ID',
  `strategy_id` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略ID',
  `major_version` int NOT NULL DEFAULT 1 COMMENT '主版本号（不兼容的重大变更）',
  `minor_version` int NOT NULL DEFAULT 0 COMMENT '次版本号（向下兼容的功能增强）',
  `patch_version` int NOT NULL DEFAULT 0 COMMENT '修订版本号（向下兼容的问题修复）',
  `version_string` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (concat(_utf8mb4'v',`major_version`,_utf8mb4'.',`minor_version`,_utf8mb4'.',`patch_version`)) STORED COMMENT '完整版本号字符串（自动生成）' NULL,
  `change_type` enum('major','minor','patch','initial') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '本次变更类型：initial-首次创建，major-主版本，minor-次版本，patch-修订版本',
  `dsl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Prophet DSL代码内容',
  `parameters` json NOT NULL COMMENT '策略参数（JSON格式）- 包含所有指标参数、交易参数、风控参数等',
  `dsl_hash` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'DSL代码的SHA-256哈希值（用于检测代码是否真的变更）',
  `version_status` enum('draft','active','inactive') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'draft' COMMENT '版本状态：draft-草稿/开发中，active-激活/当前使用，inactive-非活跃/历史版本',
  `status_changed_at` datetime NULL DEFAULT NULL COMMENT '版本状态最后变更时间',
  `validation_status` enum('pending','valid','invalid') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pending' COMMENT 'DSL验证状态：pending-待验证，valid-验证通过，invalid-验证失败',
  `validation_message` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '验证消息（错误信息或警告）',
  `validated_at` datetime NULL DEFAULT NULL COMMENT 'DSL验证时间',
  `backtest_data` json NULL COMMENT '回测结果快照：{total_return, win_rate, sharpe_ratio, max_drawdown, total_trades, ...}',
  `backtest_completed_at` datetime NULL DEFAULT NULL COMMENT '回测完成时间',
  `change_description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '版本变更说明（用户填写：本次更新了什么）',
  `breaking_changes` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '破坏性变更说明（Major版本升级时必填）',
  `risk_disclosure` json NULL COMMENT '风险披露信息：{main_risks: [...], suitable_market: [...], min_capital: 1000, stop_loss_advice: \"...\"}',
  `tags` json NULL COMMENT '版本标签：[\"stable\", \"beta\", \"experimental\", \"high-frequency\", \"beginner-friendly\"]',
  `deprecation_reason` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '废弃原因（当策略status=deprecated时，在此记录原因）',
  `force_upgrade` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否强制升级：1-所有用户必须升级到新版本，0-用户可选择升级',
  `upgrade_deadline` datetime NULL DEFAULT NULL COMMENT '强制升级截止日期（force_upgrade=1时有效）',
  `created_by` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '创建者ID',
  `created_at` datetime NOT NULL COMMENT '版本创建时间',
  `activated_at` datetime NULL DEFAULT NULL COMMENT '版本激活时间（version_status变为active的时间）',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_strategy_version`(`strategy_id` ASC, `major_version` ASC, `minor_version` ASC, `patch_version` ASC) USING BTREE COMMENT '确保同一策略的版本号唯一',
  INDEX `idx_strategy_id`(`strategy_id` ASC) USING BTREE COMMENT '按策略查询所有版本',
  INDEX `idx_version_status`(`version_status` ASC) USING BTREE COMMENT '按版本状态查询',
  INDEX `idx_validation_status`(`validation_status` ASC) USING BTREE COMMENT '查询验证状态',
  INDEX `idx_version_string`(`version_string` ASC) USING BTREE COMMENT '按版本号查询',
  INDEX `idx_created_at`(`created_at` DESC) USING BTREE COMMENT '按创建时间排序',
  INDEX `idx_created_by`(`created_by` ASC) USING BTREE COMMENT '按创建者查询',
  INDEX `idx_dsl_hash`(`dsl_hash` ASC) USING BTREE COMMENT '通过哈希检测重复代码',
  CONSTRAINT `fk_strategy_dsl_version` FOREIGN KEY (`strategy_id`) REFERENCES `strategies` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB AUTO_INCREMENT = 2 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '策略DSL版本管理表（版本级别状态管理）' ROW_FORMAT = DYNAMIC;

SET FOREIGN_KEY_CHECKS = 1;
