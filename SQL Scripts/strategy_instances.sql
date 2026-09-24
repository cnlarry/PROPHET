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

 Date: 21/11/2025 15:05:13
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for strategy_instances
-- ----------------------------
DROP TABLE IF EXISTS `strategy_instances`;
CREATE TABLE `strategy_instances`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '实例ID',
  `strategy_id` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略ID',
  `user_id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '使用者ID',
  `instance_name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '实例名称（用户自定义，如\"BTC主力策略\"、\"测试A\"）',
  `locked_major_version` int NOT NULL COMMENT '锁定的主版本号',
  `locked_minor_version` int NOT NULL COMMENT '锁定的次版本号',
  `locked_patch_version` int NOT NULL COMMENT '锁定的修订版本号',
  `locked_version_string` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (concat(_utf8mb4'v',`locked_major_version`,_utf8mb4'.',`locked_minor_version`,_utf8mb4'.',`locked_patch_version`)) STORED COMMENT '锁定的版本号（自动生成）' NULL,
  `status` enum('running','stopped','paused','error') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'stopped' COMMENT '运行状态：running-运行中，stopped-已停止，paused-已暂停，error-错误/异常停止',
  `status_changed_at` datetime NULL DEFAULT NULL COMMENT '运行状态最后变更时间',
  `error_message` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '错误信息（当status=error时记录）',
  `environment` enum('backtest','paper','live') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '运行环境：backtest-回测，paper-模拟盘，live-实盘',
  `has_newer_version` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否有新版本可用（用于前端提示）',
  `force_upgrade_required` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否需要强制升级（1-必须升级才能继续运行）',
  `upgrade_notified_at` datetime NULL DEFAULT NULL COMMENT '最后一次升级提醒时间',
  `auto_upgrade` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否自动升级到最新版本（0-手动，1-自动升级patch版本）',
  `custom_parameters` json NULL COMMENT '用户自定义参数覆盖（在版本参数基础上的调整）',
  `risk_config` json NULL COMMENT '风控配置：{max_position_size: 0.1, stop_loss_pct: 0.05, daily_loss_limit: 0.1, ...}',
  `performance_data` json NULL COMMENT '实例绩效数据：{total_trades, win_rate, total_pnl, current_drawdown, last_7d_return, ...}',
  `last_performance_update` datetime NULL DEFAULT NULL COMMENT '绩效数据最后更新时间',
  `consecutive_errors` int NOT NULL DEFAULT 0 COMMENT '连续错误次数（用于异常检测）',
  `anomaly_detected_at` datetime NULL DEFAULT NULL COMMENT '异常检测时间（系统检测到异常的时间）',
  `notes` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '用户备注（用户可以记录自己的观察和想法）',
  `created_at` datetime NOT NULL COMMENT '创建时间（用户订阅策略的时间）',
  `updated_at` datetime NOT NULL COMMENT '更新时间',
  `last_executed_at` datetime NULL DEFAULT NULL COMMENT '最后执行时间',
  `started_at` datetime NULL DEFAULT NULL COMMENT '开始运行时间（首次或本次启动）',
  `stopped_at` datetime NULL DEFAULT NULL COMMENT '停止运行时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_user_id`(`user_id` ASC) USING BTREE COMMENT '按用户查询所有实例',
  INDEX `idx_status`(`status` ASC) USING BTREE COMMENT '按运行状态查询',
  INDEX `idx_environment`(`environment` ASC) USING BTREE COMMENT '按环境查询',
  INDEX `idx_force_upgrade`(`force_upgrade_required` ASC) USING BTREE COMMENT '查询需要强制升级的实例',
  INDEX `idx_last_executed`(`last_executed_at` DESC) USING BTREE COMMENT '按执行时间排序',
  UNIQUE INDEX `uk_strategy_user_env`(`strategy_id` ASC, `user_id` ASC, `environment` ASC) USING BTREE COMMENT '每个用户对同一策略在同一环境只能有一个实例',
  INDEX `idx_strategy_id`(`strategy_id` ASC) USING BTREE COMMENT '按策略查询所有实例',
  INDEX `idx_consecutive_errors`(`consecutive_errors` DESC) USING BTREE COMMENT '查询高错误率实例',
  CONSTRAINT `fk_instance_strategy` FOREIGN KEY (`strategy_id`) REFERENCES `strategies` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_instance_user` FOREIGN KEY (`user_id`) REFERENCES `members` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB AUTO_INCREMENT = 1 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '用户策略实例表（实例级别状态管理）' ROW_FORMAT = DYNAMIC;

SET FOREIGN_KEY_CHECKS = 1;
