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

 Date: 27/11/2025 17:20:14
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for orders
-- ----------------------------
DROP TABLE IF EXISTS `orders`;
CREATE TABLE `orders`  (
  `id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '订单ID',
  `symbol` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '交易对符号',
  `side` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '方向(BUY/SELL)',
  `order_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '订单类型',
  `margin` decimal(20, 10) NOT NULL DEFAULT 0.0000000000 COMMENT '保证金',
  `leverage` int NOT NULL DEFAULT 50 COMMENT '杠杆倍数',
  `quantity` decimal(38, 18) NOT NULL COMMENT '下单数量',
  `open_price` decimal(20, 10) NOT NULL DEFAULT 0.0000000000 COMMENT '开仓价',
  `open_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '开仓时间',
  `close_price` decimal(20, 10) NOT NULL DEFAULT 0.0000000000 COMMENT '平仓价',
  `close_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '平仓时间',
  `handling_fee` decimal(20, 10) NOT NULL DEFAULT 0.0000000000 COMMENT '手续费',
  `funding_fee` decimal(20, 10) NOT NULL DEFAULT 0.0000000000 COMMENT '资金费',
  `surplus` decimal(20, 10) NOT NULL DEFAULT 0.0000000000 COMMENT '净盈亏',
  `take_profit` decimal(10, 6) NOT NULL DEFAULT 0.000000 COMMENT '止盈线',
  `stop_loss` decimal(10, 6) NOT NULL DEFAULT 0.000000 COMMENT '止损线',
  `liquidation_price` decimal(20, 10) NOT NULL DEFAULT 0.0000000000 COMMENT '强平价',
  `strategy` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略ID',
  `backtest_id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '回测批次ID',
  `closed` tinyint UNSIGNED NOT NULL DEFAULT 0 COMMENT '是否已平仓',
  `remarks` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '备注',
  `create_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `fk_orders_strategy`(`strategy` ASC) USING BTREE,
  INDEX `idx_orders_created_at`(`open_time` ASC) USING BTREE,
  INDEX `idx_orders_open`(`symbol` ASC, `closed` ASC, `open_time` ASC) USING BTREE,
  INDEX `idx_orders_side`(`symbol` ASC, `closed` ASC, `side` ASC) USING BTREE,
  INDEX `idx_orders_status`(`closed` ASC) USING BTREE,
  INDEX `idx_orders_symbol`(`symbol` ASC) USING BTREE,
  INDEX `idx_orders_sym_strat_bt_closed_time`(`symbol` ASC, `strategy` ASC, `backtest_id` ASC, `closed` ASC, `open_time` ASC) USING BTREE,
  INDEX `idx_orders_backtest`(`backtest_id` ASC, `strategy` ASC, `symbol` ASC, `closed` ASC) USING BTREE,
  INDEX `idx_orders_bt_strat_sym_closed_create_id`(`backtest_id` ASC, `strategy` ASC, `symbol` ASC, `closed` ASC, `create_at` ASC, `id` ASC) USING BTREE,
  CONSTRAINT `fk_orders_backtest` FOREIGN KEY (`backtest_id`) REFERENCES `backtest` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `orders_ibfk_1` FOREIGN KEY (`strategy`) REFERENCES `strategies` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '订单记录表' ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;
