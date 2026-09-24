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

 Date: 21/11/2025 18:17:12
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for klines
-- ----------------------------
DROP TABLE IF EXISTS `klines`;
CREATE TABLE `klines`  (
  `symbol` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '交易对符号',
  `interval` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'K线周期',
  `open_time` bigint NOT NULL COMMENT '开始时间戳(ms)',
  `open_time_str` datetime NULL DEFAULT NULL COMMENT '开始时间',
  `open` decimal(28, 12) NOT NULL COMMENT '开盘价',
  `high` decimal(28, 12) NOT NULL COMMENT '最高价',
  `low` decimal(28, 12) NOT NULL COMMENT '最低价',
  `close` decimal(28, 12) NOT NULL COMMENT '收盘价',
  `volume` decimal(38, 18) NOT NULL COMMENT '成交量',
  `close_time` bigint NOT NULL COMMENT '结束时间戳(ms)',
  `close_time_str` datetime NULL DEFAULT NULL COMMENT '结束时间',
  `quote_volume` decimal(38, 18) NOT NULL COMMENT '成交额',
  `count` bigint NOT NULL COMMENT '成交笔数',
  `taker_buy_volume` decimal(38, 18) NOT NULL COMMENT '主动买入量',
  `taker_buy_quote_volume` decimal(38, 18) NOT NULL COMMENT '主动买入额',
  `ignore` tinyint(1) NOT NULL COMMENT '保留字段',
  PRIMARY KEY (`symbol`, `interval`, `open_time`) USING BTREE,
  INDEX `idx_symbol_interval_open_time_str`(`symbol` ASC, `interval` ASC, `open_time_str` ASC) USING BTREE,
  INDEX `idx_klines_symbol_interval_time`(`symbol` ASC, `interval` ASC, `open_time` ASC) USING BTREE,
  INDEX `idx_kline_covering`(`symbol` ASC, `interval` ASC, `open_time` ASC, `open` ASC, `high` ASC, `low` ASC, `close` ASC, `volume` ASC) USING BTREE,
  INDEX `idx_time_range`(`open_time` ASC, `symbol` ASC, `interval` ASC) USING BTREE,
  INDEX `idx_latest_kline`(`symbol` ASC, `interval` ASC, `open_time` DESC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'K线数据' ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;
