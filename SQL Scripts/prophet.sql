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

 Date: 21/11/2025 16:38:46
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

-- ----------------------------
-- Table structure for bvol_btc
-- ----------------------------
DROP TABLE IF EXISTS `bvol_btc`;
CREATE TABLE `bvol_btc`  (
  `calc_time` bigint UNSIGNED NOT NULL COMMENT '计算时刻(UTC)毫秒级时间戳',
  `index_value` decimal(7, 4) NOT NULL COMMENT '波动率指数值',
  `open_time` bigint NOT NULL COMMENT '对齐5分钟k线的开盘时间时间戳',
  PRIMARY KEY (`calc_time`) USING BTREE,
  INDEX `idx_bvol_calc_time`(`calc_time` ASC) USING BTREE,
  INDEX `idx_bvol_open_time`(`open_time` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '比特币波动率' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Table structure for bvol_eth
-- ----------------------------
DROP TABLE IF EXISTS `bvol_eth`;
CREATE TABLE `bvol_eth`  (
  `calc_time` bigint NOT NULL COMMENT '计算时刻(UTC)毫秒级时间戳',
  `index_value` decimal(7, 4) NOT NULL COMMENT '波动率指数值',
  `open_time` bigint NOT NULL COMMENT '对齐5分钟k线的开盘时间时间戳',
  PRIMARY KEY (`calc_time`) USING BTREE,
  INDEX `idx_bvol_calc_time`(`calc_time` ASC) USING BTREE,
  INDEX `idx_bvol_open_time`(`open_time` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '以太币波动率' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for feargreed
-- ----------------------------
DROP TABLE IF EXISTS `feargreed`;
CREATE TABLE `feargreed`  (
  `date` date NOT NULL,
  `value` int NOT NULL,
  `classification` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`date` DESC) USING BTREE,
  INDEX `idx_date`(`date` ASC) USING BTREE,
  CONSTRAINT `feargreed_chk_1` CHECK ((`value` >= 0) and (`value` <= 100))
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '加密货币恐惧与贪婪指数' ROW_FORMAT = Dynamic;

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

-- ----------------------------
-- Table structure for login_logs
-- ----------------------------
DROP TABLE IF EXISTS `login_logs`;
CREATE TABLE `login_logs`  (
  `id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '日志ID (UUID)',
  `member_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '会员ID',
  `login_method` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '登录方式: email, wallet, google, github, wechat等',
  `ip_address` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'IP地址 (支持IPv4和IPv6)',
  `user_agent` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '用户代理字符串',
  `login_at` datetime NOT NULL COMMENT '登录时间 (UTC)',
  `login_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'success' COMMENT '登录状态: success, failed, locked',
  `failure_reason` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '失败原因 (如果登录失败)',
  `country` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '国家 (可选，通过IP解析)',
  `city` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '城市 (可选，通过IP解析)',
  `device_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '设备类型: desktop, mobile, tablet',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '记录创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_member_id`(`member_id` ASC) USING BTREE,
  INDEX `idx_login_at`(`login_at` ASC) USING BTREE,
  INDEX `idx_login_method`(`login_method` ASC) USING BTREE,
  INDEX `idx_login_status`(`login_status` ASC) USING BTREE,
  INDEX `idx_ip_address`(`ip_address` ASC) USING BTREE,
  CONSTRAINT `fk_login_logs_member` FOREIGN KEY (`member_id`) REFERENCES `members` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '用户登录日志表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for member_wallets
-- ----------------------------
DROP TABLE IF EXISTS `member_wallets`;
CREATE TABLE `member_wallets`  (
  `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '自增ID',
  `member_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '会员ID',
  `wallet_address` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '钱包地址',
  `chain` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ETH' COMMENT '区块链类型(ETH/BSC/MATIC等)',
  `chain_id` int NULL DEFAULT NULL COMMENT '链ID(1=以太坊主网,56=BSC等)',
  `is_primary` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否主钱包(登录用)',
  `is_verified` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否已验证',
  `verified_at` datetime NULL DEFAULT NULL COMMENT '验证时间',
  `last_signature` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '最后一次签名',
  `nonce` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '当前nonce(防重放攻击)',
  `wallet_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '钱包类型(MetaMask/WalletConnect等)',
  `ens_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'ENS域名(如有)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '绑定时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `last_used_at` datetime NULL DEFAULT NULL COMMENT '最后使用时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_wallet_chain`(`wallet_address` ASC, `chain` ASC) USING BTREE,
  INDEX `idx_member_id`(`member_id` ASC) USING BTREE,
  INDEX `idx_wallet_address`(`wallet_address` ASC) USING BTREE,
  INDEX `idx_is_primary`(`is_primary` ASC) USING BTREE,
  CONSTRAINT `fk_wallets_member` FOREIGN KEY (`member_id`) REFERENCES `members` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 3 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '会员钱包地址表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for members
-- ----------------------------
DROP TABLE IF EXISTS `members`;
CREATE TABLE `members`  (
  `id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '会员唯一标识(UUID)',
  `nickname` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '昵称(用户自定义,可选)',
  `avatar_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '头像URL(可选)',
  `email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '邮箱地址(可选,用于密码登录)',
  `password_hash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '密码哈希值(BCrypt加密)',
  `phone` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '手机号码(可选)',
  `real_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '真实姓名(可选,自愿填写)',
  `country` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '国家(可选)',
  `timezone` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT 'UTC' COMMENT '时区设置',
  `language` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT 'zh-CN' COMMENT '语言偏好',
  `api_key` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'API密钥(客户端认证用)',
  `is_active` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否激活(0=禁用,1=正常)',
  `is_verified` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否已验证(邮箱或KYC)',
  `member_level` enum('free','basic','pro','enterprise') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'free' COMMENT '会员等级',
  `expire_at` datetime NULL DEFAULT NULL COMMENT '会员到期时间(NULL=永久)',
  `api_quota_daily` int NOT NULL DEFAULT 10000 COMMENT '每日API调用配额',
  `api_calls_today` int NOT NULL DEFAULT 0 COMMENT '今日已调用次数',
  `api_calls_total` bigint NOT NULL DEFAULT 0 COMMENT '总调用次数',
  `rate_limit_per_minute` int NOT NULL DEFAULT 60 COMMENT '每分钟请求速率限制',
  `ip_whitelist` json NULL COMMENT 'IP白名单(JSON数组,NULL=不限制)',
  `two_factor_enabled` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否启用2FA',
  `two_factor_secret` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '2FA密钥',
  `balance` decimal(15, 2) NOT NULL DEFAULT 0.00 COMMENT '账户余额',
  `total_recharged` decimal(15, 2) NOT NULL DEFAULT 0.00 COMMENT '累计充值金额',
  `company` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '公司名称(可选)',
  `bio` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '个人简介(可选)',
  `social_links` json NULL COMMENT '社交媒体链接(Twitter,Telegram等)',
  `preferences` json NULL COMMENT '用户偏好设置',
  `metadata` json NULL COMMENT '扩展元数据',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '注册时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `last_login_at` datetime NULL DEFAULT NULL COMMENT '最后登录时间',
  `last_login_ip` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '最后登录IP',
  `failed_login_attempts` int NOT NULL DEFAULT 0 COMMENT '连续登录失败次数',
  `account_locked_until` datetime NULL DEFAULT NULL COMMENT '账户锁定截止时间',
  `password_reset_token` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '密码重置令牌',
  `password_reset_expires_at` datetime NULL DEFAULT NULL COMMENT '重置令牌过期时间',
  `email_verified` tinyint(1) NOT NULL DEFAULT 0 COMMENT '邮箱是否已验证',
  `email_verification_token` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '邮箱验证令牌',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_api_key`(`api_key` ASC) USING BTREE,
  UNIQUE INDEX `uk_email`(`email` ASC) USING BTREE,
  INDEX `idx_is_active`(`is_active` ASC) USING BTREE,
  INDEX `idx_member_level`(`member_level` ASC) USING BTREE,
  INDEX `idx_created_at`(`created_at` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '会员主表(Web3钱包登录)' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for order_signals
-- ----------------------------
DROP TABLE IF EXISTS `order_signals`;
CREATE TABLE `order_signals`  (
  `order_id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '订单ID',
  `signal_id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '信号ID',
  `action` enum('OPEN','CLOSE','HOLDING') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '动作：开仓/平仓/持仓',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '关联创建时间',
  INDEX `idx_order_signals_order`(`order_id` ASC) USING BTREE,
  INDEX `idx_order_signals_signal`(`signal_id` ASC) USING BTREE,
  INDEX `idx_order_signals_action`(`action` ASC) USING BTREE,
  CONSTRAINT `order_signals_ibfk_1` FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `order_signals_ibfk_2` FOREIGN KEY (`signal_id`) REFERENCES `signals` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '订单信号关联表' ROW_FORMAT = Dynamic;

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

-- ----------------------------
-- Table structure for params
-- ----------------------------
DROP TABLE IF EXISTS `params`;
CREATE TABLE `params`  (
  `strategy` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略ID',
  `name` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '参数名',
  `value` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '参数值',
  `default` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '默认值',
  `min` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '最小值',
  `max` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '最大值',
  `step` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '步长',
  `type` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '参数类型',
  `description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '参数描述',
  `opt` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否用于优化',
  PRIMARY KEY (`strategy`, `name`) USING BTREE,
  INDEX `idx_params_strategy_name`(`strategy` ASC, `name` ASC) USING BTREE,
  CONSTRAINT `fk_params_strategy` FOREIGN KEY (`strategy`) REFERENCES `strategies` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '策略参数表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for patterns
-- ----------------------------
DROP TABLE IF EXISTS `patterns`;
CREATE TABLE `patterns`  (
  `id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '自增主键',
  `signal_id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '关联 signals.id',
  `name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '形态名称（如 ENGULFING）',
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '中文形态说明',
  `side` enum('BULLISH','BEARISH','NEUTRAL') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '方向（BUY/SELL/HOLD）',
  `raw` smallint NOT NULL COMMENT 'TA-Lib 原值（±100/±200）',
  `score` smallint UNSIGNED NOT NULL COMMENT '绝对分值 0-200',
  `rank` tinyint UNSIGNED NOT NULL COMMENT '按强度排序名次，从1开始',
  `kline_open_time` bigint NOT NULL COMMENT '对应最后一根k线开盘时间',
  `kline_close_time` bigint NOT NULL COMMENT '对应最后一根k线收盘时间',
  `create_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_patterns_signal`(`signal_id` ASC) USING BTREE,
  INDEX `ix_patterns_name`(`name` ASC) USING BTREE,
  INDEX `ix_patterns_name_side`(`name` ASC, `side` ASC) USING BTREE,
  INDEX `ix_patterns_signal_rank`(`signal_id` ASC, `rank` ASC) USING BTREE,
  CONSTRAINT `fk_patterns_signal` FOREIGN KEY (`signal_id`) REFERENCES `signals` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '策略信号形态明细表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for signals
-- ----------------------------
DROP TABLE IF EXISTS `signals`;
CREATE TABLE `signals`  (
  `id` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '信号ID',
  `strategy` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '策略ID',
  `action` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '交易动作',
  `confidence` decimal(5, 2) NOT NULL DEFAULT 0.00 COMMENT '置信度，范围0.00-1.00',
  `reason` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '原因',
  `trend` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '趋势(BULLISH\\BEARISH\\NEUTRAL)',
  `sl` decimal(10, 6) NOT NULL DEFAULT 0.000000 COMMENT '止损线',
  `tp` decimal(10, 6) NOT NULL DEFAULT 0.000000 COMMENT '止盈线',
  `configs` json NOT NULL COMMENT '配置参数JSON',
  `indicators` json NOT NULL COMMENT '技术指标JSON',
  `debug` json NOT NULL COMMENT '调试信息和决策过程详情JSON',
  `create_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '时间戳',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `fk_signals_strategy`(`strategy` ASC) USING BTREE,
  INDEX `ix_signals_orderid_createat`(`create_at` ASC) USING BTREE,
  INDEX `ix_signals_orderid_action_ct`(`action` ASC, `create_at` ASC) USING BTREE,
  INDEX `ix_signals_debug_action`(`action` ASC, `create_at` ASC) USING BTREE,
  INDEX `fk_signals_id`(`id` ASC) USING BTREE,
  CONSTRAINT `fk_signals_strategy` FOREIGN KEY (`strategy`) REFERENCES `strategies` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '策略信号表' ROW_FORMAT = Dynamic;

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

-- ----------------------------
-- Table structure for strategy_dsl_versions
-- ----------------------------
DROP TABLE IF EXISTS `strategy_dsl_versions`;
CREATE TABLE `strategy_dsl_versions`  (
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
  INDEX `idx_version_string`(`version_string` ASC) USING BTREE COMMENT '按版本号查询',
  INDEX `idx_created_at`(`created_at` DESC) USING BTREE COMMENT '按创建时间排序',
  INDEX `idx_created_by`(`created_by` ASC) USING BTREE COMMENT '按创建者查询',
  INDEX `idx_version_status`(`version_status` ASC) USING BTREE COMMENT '按版本状态查询',
  INDEX `idx_validation_status`(`validation_status` ASC) USING BTREE COMMENT '查询验证状态',
  INDEX `idx_dsl_hash`(`dsl_hash` ASC) USING BTREE COMMENT '通过哈希检测重复代码',
  CONSTRAINT `fk_strategy_dsl_version` FOREIGN KEY (`strategy_id`) REFERENCES `strategies` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE = InnoDB AUTO_INCREMENT = 2 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '策略DSL版本管理表（版本级别状态管理）' ROW_FORMAT = DYNAMIC;

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

-- ----------------------------
-- Table structure for wallet_login_sessions
-- ----------------------------
DROP TABLE IF EXISTS `wallet_login_sessions`;
CREATE TABLE `wallet_login_sessions`  (
  `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '会话ID',
  `wallet_address` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '钱包地址',
  `nonce` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '登录nonce(一次性)',
  `challenge_message` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '签名挑战消息',
  `signature` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '钱包签名',
  `jwt_token` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT 'JWT Token',
  `status` enum('pending','signed','verified','expired') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pending' COMMENT '会话状态',
  `ip_address` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '客户端IP',
  `user_agent` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '用户代理',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `expires_at` datetime NOT NULL COMMENT '过期时间(5分钟有效期)',
  `verified_at` datetime NULL DEFAULT NULL COMMENT '验证完成时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_nonce`(`nonce` ASC) USING BTREE,
  INDEX `idx_wallet_address`(`wallet_address` ASC) USING BTREE,
  INDEX `idx_status`(`status` ASC) USING BTREE,
  INDEX `idx_expires_at`(`expires_at` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '钱包登录会话表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- View structure for backtest_with_stats
-- ----------------------------
DROP VIEW IF EXISTS `backtest_with_stats`;
CREATE ALGORITHM = UNDEFINED SQL SECURITY DEFINER VIEW `backtest_with_stats` AS select `br`.`id` AS `id`,`br`.`strategy` AS `strategy`,`br`.`symbol` AS `symbol`,`br`.`timeframe` AS `timeframe`,`br`.`start_date` AS `start_date`,`br`.`end_date` AS `end_date`,`br`.`backtest_start_time` AS `backtest_start_time`,`br`.`backtest_end_time` AS `backtest_end_time`,`br`.`cycle_index` AS `cycle_index`,`br`.`cycle_count` AS `cycle_count`,`br`.`execution_duration` AS `execution_duration`,`br`.`execution_duration_seconds` AS `execution_duration_seconds`,`br`.`trading_days` AS `trading_days`,`br`.`study` AS `study`,`br`.`notes` AS `notes`,`br`.`created_at` AS `created_at`,coalesce(`stats`.`total_orders`,0) AS `total_orders`,coalesce(`stats`.`long_orders`,0) AS `long_orders`,coalesce(`stats`.`short_orders`,0) AS `short_orders`,coalesce(`stats`.`winning_orders`,0) AS `winning_orders`,coalesce(`stats`.`losing_orders`,0) AS `losing_orders`,coalesce(`stats`.`long_wins`,0) AS `long_wins`,coalesce(`stats`.`long_losses`,0) AS `long_losses`,coalesce(`stats`.`short_wins`,0) AS `short_wins`,coalesce(`stats`.`short_losses`,0) AS `short_losses`,coalesce(`stats`.`total_win_rate`,0.0) AS `total_win_rate`,coalesce(`stats`.`long_win_rate`,0.0) AS `long_win_rate`,coalesce(`stats`.`short_win_rate`,0.0) AS `short_win_rate`,coalesce(`stats`.`total_profit`,0.0) AS `total_profit`,coalesce(`stats`.`long_profit_sum`,0.0) AS `long_profit_sum`,coalesce(`stats`.`long_loss_sum`,0.0) AS `long_loss_sum`,coalesce(`stats`.`short_profit_sum`,0.0) AS `short_profit_sum`,coalesce(`stats`.`short_loss_sum`,0.0) AS `short_loss_sum`,coalesce(`stats`.`max_profit`,0.0) AS `max_profit`,coalesce(`stats`.`max_loss`,0.0) AS `max_loss`,coalesce(`stats`.`total_fees`,0.0) AS `total_fees`,coalesce(`stats`.`total_funding_fees`,0.0) AS `total_funding_fees`,coalesce(`stats`.`long_funding_fees`,0.0) AS `long_funding_fees`,coalesce(`stats`.`short_funding_fees`,0.0) AS `short_funding_fees`,coalesce(`dd`.`max_drawdown`,0.0) AS `max_drawdown`,coalesce(`dd`.`max_drawdown_ratio`,0.0) AS `max_drawdown_ratio`,(case when (coalesce(`stats`.`avg_loss_abs`,0.0) > 0) then round((coalesce(`stats`.`avg_win`,0.0) / nullif(coalesce(`stats`.`avg_loss_abs`,0.0),0.0)),4) else 0.0 end) AS `avg_pl_ratio`,(case when (coalesce(`stats`.`gross_pnl_abs`,0.0) > 0) then round((coalesce(`stats`.`total_fees`,0.0) / nullif(coalesce(`stats`.`gross_pnl_abs`,0.0),0.0)),4) else 0.0 end) AS `fee_ratio`,(case when (coalesce(`stats`.`gross_pnl_abs`,0.0) > 0) then round((coalesce(`stats`.`total_funding_fees`,0.0) / nullif(coalesce(`stats`.`gross_pnl_abs`,0.0),0.0)),4) else 0.0 end) AS `funding_fee_ratio`,(case when (coalesce(`stats`.`total_orders`,0) > 0) then round((coalesce(`stats`.`total_funding_fees`,0.0) / nullif(coalesce(`stats`.`total_orders`,0),0.0)),4) else 0.0 end) AS `avg_funding_fee_per_order`,(10000 + coalesce(`stats`.`total_profit`,0.0)) AS `final_balance`,(case when (`br`.`trading_days` > 0) then (coalesce(`stats`.`total_orders`,0) / `br`.`trading_days`) else 0.0 end) AS `avg_orders_per_day` from ((`backtest` `br` left join (select `o`.`backtest_id` AS `backtest_id`,`o`.`strategy` AS `strategy`,`o`.`symbol` AS `symbol`,count(0) AS `total_orders`,sum((case when (`o`.`side` = 'BUY') then 1 else 0 end)) AS `long_orders`,sum((case when (`o`.`side` = 'SELL') then 1 else 0 end)) AS `short_orders`,sum((case when (`o`.`surplus` > 0) then 1 else 0 end)) AS `winning_orders`,sum((case when (`o`.`surplus` <= 0) then 1 else 0 end)) AS `losing_orders`,sum((case when ((`o`.`side` = 'BUY') and (`o`.`surplus` > 0)) then 1 else 0 end)) AS `long_wins`,sum((case when ((`o`.`side` = 'BUY') and (`o`.`surplus` <= 0)) then 1 else 0 end)) AS `long_losses`,sum((case when ((`o`.`side` = 'SELL') and (`o`.`surplus` > 0)) then 1 else 0 end)) AS `short_wins`,sum((case when ((`o`.`side` = 'SELL') and (`o`.`surplus` <= 0)) then 1 else 0 end)) AS `short_losses`,(case when (count(0) > 0) then round((sum((case when (`o`.`surplus` > 0) then 1 else 0 end)) / count(0)),4) else 0.0 end) AS `total_win_rate`,(case when (sum((case when (`o`.`side` = 'BUY') then 1 else 0 end)) > 0) then round((sum((case when ((`o`.`side` = 'BUY') and (`o`.`surplus` > 0)) then 1 else 0 end)) / sum((case when (`o`.`side` = 'BUY') then 1 else 0 end))),4) else 0.0 end) AS `long_win_rate`,(case when (sum((case when (`o`.`side` = 'SELL') then 1 else 0 end)) > 0) then round((sum((case when ((`o`.`side` = 'SELL') and (`o`.`surplus` > 0)) then 1 else 0 end)) / sum((case when (`o`.`side` = 'SELL') then 1 else 0 end))),4) else 0.0 end) AS `short_win_rate`,sum(`o`.`surplus`) AS `total_profit`,sum((case when ((`o`.`side` = 'BUY') and (`o`.`surplus` > 0)) then `o`.`surplus` else 0 end)) AS `long_profit_sum`,sum((case when ((`o`.`side` = 'BUY') and (`o`.`surplus` <= 0)) then `o`.`surplus` else 0 end)) AS `long_loss_sum`,sum((case when ((`o`.`side` = 'SELL') and (`o`.`surplus` > 0)) then `o`.`surplus` else 0 end)) AS `short_profit_sum`,sum((case when ((`o`.`side` = 'SELL') and (`o`.`surplus` <= 0)) then `o`.`surplus` else 0 end)) AS `short_loss_sum`,max(`o`.`surplus`) AS `max_profit`,min(`o`.`surplus`) AS `max_loss`,sum(`o`.`handling_fee`) AS `total_fees`,sum(`o`.`funding_fee`) AS `total_funding_fees`,sum((case when (`o`.`side` = 'BUY') then `o`.`funding_fee` else 0 end)) AS `long_funding_fees`,sum((case when (`o`.`side` = 'SELL') then `o`.`funding_fee` else 0 end)) AS `short_funding_fees`,avg((case when (`o`.`surplus` > 0) then `o`.`surplus` end)) AS `avg_win`,abs(avg((case when (`o`.`surplus` <= 0) then `o`.`surplus` end))) AS `avg_loss_abs`,sum(abs(`o`.`surplus`)) AS `gross_pnl_abs` from `orders` `o` where (`o`.`closed` = 1) group by `o`.`backtest_id`,`o`.`strategy`,`o`.`symbol`) `stats` on(((`br`.`id` = `stats`.`backtest_id`) and (`br`.`strategy` = `stats`.`strategy`) and (`br`.`symbol` = `stats`.`symbol`)))) left join (select `e`.`backtest_id` AS `backtest_id`,`e`.`strategy` AS `strategy`,`e`.`symbol` AS `symbol`,max((`e`.`running_max_cum_pnl` - `e`.`cum_pnl`)) AS `max_drawdown`,max((case when ((10000 + `e`.`running_max_cum_pnl`) > 0) then ((`e`.`running_max_cum_pnl` - `e`.`cum_pnl`) / (10000 + `e`.`running_max_cum_pnl`)) else 0.0 end)) AS `max_drawdown_ratio` from `orders_equity` `e` group by `e`.`backtest_id`,`e`.`strategy`,`e`.`symbol`) `dd` on(((`br`.`id` = `dd`.`backtest_id`) and (`br`.`strategy` = `dd`.`strategy`) and (`br`.`symbol` = `dd`.`symbol`))));

-- ----------------------------
-- View structure for orders_equity
-- ----------------------------
DROP VIEW IF EXISTS `orders_equity`;
CREATE ALGORITHM = UNDEFINED SQL SECURITY DEFINER VIEW `orders_equity` AS select `t`.`backtest_id` AS `backtest_id`,`t`.`strategy` AS `strategy`,`t`.`symbol` AS `symbol`,`t`.`create_at` AS `create_at`,`t`.`order_id` AS `order_id`,`t`.`cum_pnl` AS `cum_pnl`,max(`t`.`cum_pnl`) OVER (PARTITION BY `t`.`backtest_id`,`t`.`strategy`,`t`.`symbol` ORDER BY `t`.`create_at`,`t`.`order_id` ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)  AS `running_max_cum_pnl` from (select `o`.`backtest_id` AS `backtest_id`,`o`.`strategy` AS `strategy`,`o`.`symbol` AS `symbol`,`o`.`create_at` AS `create_at`,`o`.`id` AS `order_id`,sum(`o`.`surplus`) OVER (PARTITION BY `o`.`backtest_id`,`o`.`strategy`,`o`.`symbol` ORDER BY `o`.`create_at`,`o`.`id` ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)  AS `cum_pnl` from `orders` `o` where (`o`.`closed` = 1)) `t`;

-- ----------------------------
-- View structure for orders_with_signals
-- ----------------------------
DROP VIEW IF EXISTS `orders_with_signals`;
CREATE ALGORITHM = UNDEFINED SQL SECURITY DEFINER VIEW `orders_with_signals` AS select `o`.`id` AS `id`,`o`.`symbol` AS `symbol`,`b`.`timeframe` AS `timeframe`,`o`.`side` AS `side`,`o`.`order_type` AS `order_type`,`o`.`margin` AS `margin`,`o`.`leverage` AS `leverage`,`o`.`quantity` AS `quantity`,`o`.`open_price` AS `open_price`,`o`.`open_time` AS `open_time`,`open_signal`.`reason` AS `open_reason`,coalesce(`open_signal`.`confidence`,0) AS `open_confidence`,`o`.`close_price` AS `close_price`,`o`.`close_time` AS `close_time`,`close_signal`.`reason` AS `close_reason`,coalesce(`close_signal`.`confidence`,0) AS `close_confidence`,`o`.`handling_fee` AS `handling_fee`,`o`.`funding_fee` AS `funding_fee`,`o`.`surplus` AS `surplus`,`o`.`take_profit` AS `take_profit`,`o`.`stop_loss` AS `stop_loss`,`o`.`liquidation_price` AS `liquidation_price`,`o`.`strategy` AS `strategy`,`o`.`backtest_id` AS `backtest_id`,`o`.`closed` AS `closed`,`o`.`remarks` AS `remarks`,`o`.`create_at` AS `create_at`,timestampdiff(MINUTE,`o`.`open_time`,`o`.`close_time`) AS `holding_minute`,(select count(0) from (`order_signals` `os` join `signals` `s` on((`os`.`signal_id` = `s`.`id`))) where (`os`.`order_id` = `o`.`id`)) AS `total_signals`,(select count(0) from `order_signals` `os` where ((`os`.`order_id` = `o`.`id`) and (`os`.`action` = 'HOLDING'))) AS `holding_signals` from (((((`orders` `o` join `backtest` `b` on((`o`.`backtest_id` = `b`.`id`))) left join `order_signals` `os_open` on(((`o`.`id` = `os_open`.`order_id`) and (`os_open`.`action` = 'OPEN')))) left join `signals` `open_signal` on((`os_open`.`signal_id` = `open_signal`.`id`))) left join `order_signals` `os_close` on(((`o`.`id` = `os_close`.`order_id`) and (`os_close`.`action` = 'CLOSE')))) left join `signals` `close_signal` on((`os_close`.`signal_id` = `close_signal`.`id`)));

SET FOREIGN_KEY_CHECKS = 1;
