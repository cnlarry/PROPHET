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

 Date: 21/11/2025 16:41:14
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for backtest
-- ----------------------------
DROP TABLE IF EXISTS `backtest`;
CREATE TABLE `backtest`  (
  `id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '回测结果ID',
  `strategy` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略标识',
  `symbol` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '交易对符号',
  `timeframe` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '回测时间周期',
  `start_date` datetime NOT NULL COMMENT '回测起始日期',
  `end_date` datetime NOT NULL COMMENT '回测结束日期',
  `backtest_start_time` datetime NOT NULL COMMENT '回测执行开始时间',
  `backtest_end_time` datetime NULL DEFAULT NULL COMMENT '回测执行结束时间',
  `cycle_index` int NOT NULL DEFAULT 1 COMMENT '当前循环序号',
  `cycle_count` int NOT NULL DEFAULT 1 COMMENT '需要循环的数量',
  `execution_duration` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '执行耗时（文本）',
  `execution_duration_seconds` int NULL DEFAULT NULL COMMENT '执行耗时（秒）',
  `trading_days` int NOT NULL DEFAULT 0 COMMENT '交易天数',
  `study` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '优化study名称',
  `notes` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '备注',
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '策略回测结果' ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;
