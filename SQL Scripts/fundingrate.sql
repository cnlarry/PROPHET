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

 Date: 21/11/2025 18:47:29
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for fundingrate
-- ----------------------------
DROP TABLE IF EXISTS `fundingrate`;
CREATE TABLE `fundingrate`  (
  `symbol` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '标的符号',
  `calc_time` bigint NOT NULL COMMENT '计算时间戳',
  `calc_time_str` datetime NULL DEFAULT NULL COMMENT '计算时间(格式化)',
  `funding_interval_hours` int NOT NULL COMMENT '资金费间隔(小时)',
  `last_funding_rate` decimal(20, 10) NOT NULL COMMENT '最新资金费率',
  PRIMARY KEY (`symbol`, `calc_time`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '资金费率快照' ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;
