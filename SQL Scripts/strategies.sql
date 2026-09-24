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

 Date: 21/11/2025 15:04:58
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for strategies
-- ----------------------------
DROP TABLE IF EXISTS `strategies`;
CREATE TABLE `strategies`  (
  `id` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略ID',
  `name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略名称',
  `owner_id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '拥有者ID（策略作者）',
  `symbol` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '交易对（如 BTCUSDT）',
  `status` enum('draft','active','deprecated','archived','anomaly') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'draft' COMMENT '策略状态：draft-草稿/开发中，active-激活/可用，deprecated-已废弃/不推荐，archived-已归档/不可用，anomaly-异常/系统检测到问题',
  `status_changed_at` datetime NULL DEFAULT NULL COMMENT '状态最后变更时间',
  `anomaly_reason` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '异常原因（当status=anomaly时记录详细原因）',
  `current_major_version` int NOT NULL DEFAULT 1 COMMENT '当前主版本号',
  `current_minor_version` int NOT NULL DEFAULT 0 COMMENT '当前次版本号',
  `current_patch_version` int NOT NULL DEFAULT 0 COMMENT '当前修订版本号',
  `current_version_string` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (concat(_utf8mb4'v',`current_major_version`,_utf8mb4'.',`current_minor_version`,_utf8mb4'.',`current_patch_version`)) STORED COMMENT '当前版本号字符串（自动生成）' NULL,
  `description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '策略描述（用户填写）',
  `strategy_type` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '策略类型：trend_following-趋势跟踪，mean_reversion-均值回归，grid-网格，breakout-突破等',
  `risk_level` enum('low','medium-low','medium','medium-high','high') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '风险等级（系统自动评估或用户标注）',
  `quality_score` int NULL DEFAULT NULL COMMENT '策略质量评分（0-100，系统自动计算）',
  `public` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否公开共享：0-仅自己可见，1-所有人可见可订阅',
  `total_subscribers` int NOT NULL DEFAULT 0 COMMENT '总订阅用户数（不含作者自己）',
  `total_backtest_count` int NOT NULL DEFAULT 0 COMMENT '总回测次数（所有用户）',
  `enabled` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否启用：0-临时禁用（不影响status），1-启用',
  `remarks` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '备注（内部使用）',
  `created_at` datetime NOT NULL COMMENT '创建时间',
  `updated_at` datetime NOT NULL COMMENT '最后修改时间',
  `last_backtest_at` datetime NULL DEFAULT NULL COMMENT '最后回测时间（任何用户）',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_owner_id`(`owner_id` ASC) USING BTREE COMMENT '按作者查询',
  INDEX `idx_symbol`(`symbol` ASC) USING BTREE COMMENT '按交易对查询',
  INDEX `idx_public`(`public` ASC) USING BTREE COMMENT '查询公开策略',
  INDEX `idx_enabled`(`enabled` ASC) USING BTREE COMMENT '查询启用的策略',
  INDEX `idx_created_at`(`created_at` DESC) USING BTREE COMMENT '按创建时间排序',
  INDEX `idx_status`(`status` ASC) USING BTREE COMMENT '按状态查询',
  INDEX `idx_quality_score`(`quality_score` DESC) USING BTREE COMMENT '按质量评分排序',
  INDEX `idx_status_public`(`status` ASC, `public` ASC) USING BTREE COMMENT '联合索引：查询可用的公开策略',
  CONSTRAINT `strategies_ibfk_1` FOREIGN KEY (`owner_id`) REFERENCES `members` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '策略主表（策略级别状态管理）' ROW_FORMAT = DYNAMIC;

SET FOREIGN_KEY_CHECKS = 1;
