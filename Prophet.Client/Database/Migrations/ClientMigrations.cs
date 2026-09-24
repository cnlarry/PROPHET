using System.Collections.Generic;

namespace Prophet.Client.Database.Migrations;

public static class ClientMigrations
{
    public static IReadOnlyList<DbMigration> All { get; } = new List<DbMigration>
    {
        new DbMigration(
            Id: "2025-12-31_01_remove_strategies_symbol",
            Description: "strategies: 移除 symbol 字段，并移除 idx_strategies_symbol 索引",
            DisableForeignKeys: true,
            Sql: @"
CREATE TABLE IF NOT EXISTS strategies_new (
    id TEXT PRIMARY KEY,                          -- 策略ID（UUID）
    name TEXT NOT NULL,                           -- 策略名称
    dsl TEXT NOT NULL,                            -- DSL代码
    remarks TEXT,                                 -- 备注
    description TEXT,                             -- 描述
    strategy_type TEXT,                           -- 策略类型
    risk_level TEXT,                              -- 风险等级
    quality_score INTEGER,                        -- 质量评分（0-100）
    status TEXT NOT NULL DEFAULT 'draft',         -- 状态（draft/active/deprecated/archived/anomaly）
    status_changed_at TEXT,                       -- 状态变更时间
    anomaly_reason TEXT,                          -- 异常原因
    enabled INTEGER NOT NULL DEFAULT 0,           -- 是否启用
    public INTEGER NOT NULL DEFAULT 0,            -- 是否公开
    total_subscribers INTEGER DEFAULT 0,          -- 订阅人数
    total_backtest_count INTEGER DEFAULT 0,       -- 回测次数
    last_backtest_at TEXT,                        -- 最后回测时间
    current_version_string TEXT DEFAULT 'v1.0.0', -- 当前版本号
    last_compiled_at TEXT,                        -- 最后编译时间
    created_at TEXT NOT NULL,                     -- 创建时间
    updated_at TEXT NOT NULL                      -- 更新时间
);

INSERT INTO strategies_new (
    id, name, dsl, remarks, description,
    strategy_type, risk_level, quality_score,
    status, status_changed_at, anomaly_reason,
    enabled, public, total_subscribers, total_backtest_count,
    last_backtest_at, current_version_string, last_compiled_at,
    created_at, updated_at
)
SELECT
    id, name, dsl, remarks, description,
    strategy_type, risk_level, quality_score,
    status, status_changed_at, anomaly_reason,
    enabled, public, total_subscribers, total_backtest_count,
    last_backtest_at, current_version_string, last_compiled_at,
    created_at, updated_at
FROM strategies;

DROP TABLE strategies;
ALTER TABLE strategies_new RENAME TO strategies;

DROP INDEX IF EXISTS idx_strategies_symbol;
CREATE INDEX IF NOT EXISTS idx_strategies_status ON strategies(status);
CREATE INDEX IF NOT EXISTS idx_strategies_updated_at ON strategies(updated_at DESC);
"
        )
        ,
        new DbMigration(
            Id: "2025-12-31_02_backtest_symbol_to_symbol_key",
            Description: "backtest: symbol -> symbol_key，并外键关联 instruments(symbol_key)，同步更新视图/索引",
            DisableForeignKeys: true,
            Sql: @"
-- 先移除依赖 symbol 列的视图/索引
DROP VIEW IF EXISTS backtest_orders_detail;
DROP VIEW IF EXISTS backtest_with_stats;
DROP INDEX IF EXISTS idx_backtest_symbol;

-- backtest_configs: symbol -> symbol_key
CREATE TABLE IF NOT EXISTS backtest_configs_new (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    symbol_key TEXT NOT NULL DEFAULT 'BTCUSDT-BINANCE-SWAP' REFERENCES instruments(symbol_key),
    interval TEXT NOT NULL DEFAULT '5m',
    start_date TEXT NOT NULL,
    end_date TEXT NOT NULL,
    initial_capital REAL NOT NULL DEFAULT 10000,
    leverage REAL DEFAULT 10.0,
    position_size_percent REAL DEFAULT 0.05,
    allow_short INTEGER DEFAULT 1,
    position_size_method TEXT DEFAULT 'fixed',
    atr_period INTEGER DEFAULT 14,
    atr_multiplier REAL DEFAULT 2.0,
    risk_percent_per_trade REAL DEFAULT 0.01,
    max_kelly_fraction REAL DEFAULT 0.25,
    min_kelly_sample_size INTEGER DEFAULT 20,
    taker_fee_rate REAL DEFAULT 0.001,
    maker_fee_rate REAL DEFAULT 0.0005,
    slippage_rate REAL DEFAULT 0.0005,
    default_take_profit_percent REAL DEFAULT 0.40,
    default_stop_loss_percent REAL DEFAULT 0.20,
    enable_default_tpsl INTEGER DEFAULT 1,
    enable_trailing_stop INTEGER DEFAULT 0,
    enable_trailing_take_profit INTEGER DEFAULT 0,
    slippage_mode TEXT DEFAULT 'FixedBps',
    fixed_bps REAL DEFAULT 0.0005,
    fixed_price REAL DEFAULT 0.5,
    pct_of_spread REAL DEFAULT 0.10,
    impact_coefficient REAL DEFAULT 0.1,
    impact_exponent REAL DEFAULT 0.5,
    slippage_randomness INTEGER DEFAULT 1,
    slippage_random_factor REAL DEFAULT 0.2,
    market_order_multiplier REAL DEFAULT 1.0,
    stop_order_multiplier REAL DEFAULT 1.5,
    parameters_json TEXT,
    enable_kline_cache INTEGER DEFAULT 1,
    max_cache_size INTEGER DEFAULT 100,
    signal_sampling_interval TEXT DEFAULT '5m',
    use_api_data_source INTEGER DEFAULT 1,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

INSERT OR REPLACE INTO backtest_configs_new (
    id, symbol_key, interval, start_date, end_date,
    initial_capital, leverage, position_size_percent, allow_short,
    position_size_method, atr_period, atr_multiplier, risk_percent_per_trade,
    max_kelly_fraction, min_kelly_sample_size,
    taker_fee_rate, maker_fee_rate, slippage_rate,
    default_take_profit_percent, default_stop_loss_percent,
    enable_default_tpsl, enable_trailing_stop, enable_trailing_take_profit,
    slippage_mode, fixed_bps, fixed_price, pct_of_spread,
    impact_coefficient, impact_exponent, slippage_randomness,
    slippage_random_factor, market_order_multiplier, stop_order_multiplier,
    parameters_json, enable_kline_cache, max_cache_size,
    signal_sampling_interval, use_api_data_source,
    updated_at
)
SELECT
    id,
    CASE
        WHEN symbol LIKE '%-%-%' THEN symbol
        ELSE UPPER(symbol) || '-BINANCE-SWAP'
    END AS symbol_key,
    interval, start_date, end_date,
    initial_capital, leverage, position_size_percent, allow_short,
    position_size_method, atr_period, atr_multiplier, risk_percent_per_trade,
    max_kelly_fraction, min_kelly_sample_size,
    taker_fee_rate, maker_fee_rate, slippage_rate,
    default_take_profit_percent, default_stop_loss_percent,
    enable_default_tpsl, enable_trailing_stop, enable_trailing_take_profit,
    slippage_mode, fixed_bps, fixed_price, pct_of_spread,
    impact_coefficient, impact_exponent, slippage_randomness,
    slippage_random_factor, market_order_multiplier, stop_order_multiplier,
    parameters_json, enable_kline_cache, max_cache_size,
    signal_sampling_interval, use_api_data_source,
    updated_at
FROM backtest_configs;

DROP TABLE backtest_configs;
ALTER TABLE backtest_configs_new RENAME TO backtest_configs;

-- backtest_runs: symbol -> symbol_key
CREATE TABLE backtest_runs_new (
    id TEXT PRIMARY KEY,
    strategy_id TEXT NOT NULL,
    strategy_name TEXT NOT NULL,
    symbol_key TEXT NOT NULL DEFAULT 'BTCUSDT-BINANCE-SWAP' REFERENCES instruments(symbol_key),
    interval TEXT NOT NULL DEFAULT '5m',
    start_date TEXT NOT NULL,
    end_date TEXT NOT NULL,
    initial_capital REAL NOT NULL,
    leverage REAL DEFAULT 1.0,
    taker_fee_rate REAL DEFAULT 0.001,
    maker_fee_rate REAL DEFAULT 0.0005,
    slippage_rate REAL DEFAULT 0.0005,
    parameters TEXT,
    final_equity REAL,
    total_return REAL,
    annualized_return REAL,
    max_drawdown REAL,
    sharpe_ratio REAL,
    sortino_ratio REAL,
    calmar_ratio REAL,
    total_trades INTEGER,
    winning_trades INTEGER,
    losing_trades INTEGER,
    win_rate REAL,
    avg_profit REAL,
    avg_loss REAL,
    profit_factor REAL,
    max_consecutive_wins INTEGER,
    max_consecutive_losses INTEGER,
    max_single_profit REAL,
    max_single_loss REAL,
    avg_holding_time_seconds REAL,
    status TEXT DEFAULT 'completed',
    error_message TEXT,
    start_time TEXT NOT NULL,
    end_time TEXT,
    duration_seconds REAL,
    completed_at TEXT DEFAULT CURRENT_TIMESTAMP,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    version_id INTEGER,
    version_string TEXT,
    backtest_config TEXT,
    backtest_result TEXT,
    equity_curve_json TEXT,
    synced_to_server INTEGER DEFAULT 0,
    synced_at TEXT,
    progress REAL DEFAULT 0.0
);

INSERT INTO backtest_runs_new (
    id, strategy_id, strategy_name, symbol_key, interval, start_date, end_date, initial_capital, leverage,
    taker_fee_rate, maker_fee_rate, slippage_rate, parameters,
    final_equity, total_return, annualized_return, max_drawdown, sharpe_ratio, sortino_ratio, calmar_ratio,
    total_trades, winning_trades, losing_trades, win_rate, avg_profit, avg_loss, profit_factor,
    max_consecutive_wins, max_consecutive_losses, max_single_profit, max_single_loss, avg_holding_time_seconds,
    status, error_message, start_time, end_time, duration_seconds, completed_at, created_at,
    version_id, version_string, backtest_config, backtest_result, equity_curve_json, synced_to_server, synced_at, progress
)
SELECT
    id, strategy_id, strategy_name,
    CASE
        WHEN symbol LIKE '%-%-%' THEN symbol
        ELSE UPPER(symbol) || '-BINANCE-SWAP'
    END AS symbol_key,
    interval, start_date, end_date, initial_capital, leverage,
    taker_fee_rate, maker_fee_rate, slippage_rate, parameters,
    final_equity, total_return, annualized_return, max_drawdown, sharpe_ratio, sortino_ratio, calmar_ratio,
    total_trades, winning_trades, losing_trades, win_rate, avg_profit, avg_loss, profit_factor,
    max_consecutive_wins, max_consecutive_losses, max_single_profit, max_single_loss, avg_holding_time_seconds,
    status, error_message, start_time, end_time, duration_seconds, completed_at, created_at,
    version_id, version_string, backtest_config, backtest_result, equity_curve_json, synced_to_server, synced_at, progress
FROM backtest_runs;

DROP TABLE backtest_runs;
ALTER TABLE backtest_runs_new RENAME TO backtest_runs;

-- backtest_orders: symbol -> symbol_key（可为空）
CREATE TABLE backtest_orders_new (
  id TEXT PRIMARY KEY,
  backtest_id TEXT NOT NULL REFERENCES backtest_runs(id) ON DELETE CASCADE ON UPDATE CASCADE,
  symbol_key TEXT REFERENCES instruments(symbol_key),
  side TEXT NOT NULL,
  type TEXT NOT NULL,
  status TEXT NOT NULL,
  margin REAL NOT NULL DEFAULT 0,
  leverage INTEGER NOT NULL DEFAULT 10,
  quantity REAL NOT NULL,
  open_price REAL NOT NULL,
  open_time TEXT NOT NULL,
  filled_price REAL,
  filled_time TEXT,
  close_price REAL,
  close_time TEXT,
  fee REAL NOT NULL DEFAULT 0,
  funding_fee REAL NOT NULL DEFAULT 0,
  profit REAL,
  take_profit REAL,
  stop_loss REAL,
  liquidation_price REAL,
  remarks TEXT,
  handling_fee REAL DEFAULT 0,
  surplus REAL DEFAULT 0,
  closed INTEGER DEFAULT 0
);

INSERT INTO backtest_orders_new (
  id, backtest_id, symbol_key, side, type, status, margin, leverage, quantity,
  open_price, open_time, filled_price, filled_time, close_price, close_time,
  fee, funding_fee, profit, take_profit, stop_loss, liquidation_price, remarks,
  handling_fee, surplus, closed
)
SELECT
  id, backtest_id,
  CASE
    WHEN symbol IS NULL THEN NULL
    WHEN symbol LIKE '%-%-%' THEN symbol
    ELSE UPPER(symbol) || '-BINANCE-SWAP'
  END AS symbol_key,
  side, type, status, margin, leverage, quantity,
  open_price, open_time, filled_price, filled_time, close_price, close_time,
  fee, funding_fee, profit, take_profit, stop_loss, liquidation_price, remarks,
  handling_fee, surplus, closed
FROM backtest_orders;

DROP TABLE backtest_orders;
ALTER TABLE backtest_orders_new RENAME TO backtest_orders;

-- backtest_results: symbol -> symbol_key
CREATE TABLE backtest_results_new(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  backtest_id TEXT NOT NULL UNIQUE REFERENCES backtest_runs(id) ON DELETE CASCADE ON UPDATE CASCADE,
  strategy_id TEXT NOT NULL REFERENCES strategies(id) ON DELETE CASCADE ON UPDATE CASCADE,
  version_id INTEGER REFERENCES strategy_versions(id) ON DELETE CASCADE ON UPDATE CASCADE,
  symbol_key TEXT NOT NULL REFERENCES instruments(symbol_key),
  interval TEXT NOT NULL,
  start_time TEXT NOT NULL,
  end_time TEXT NOT NULL,
  initial_balance REAL NOT NULL,
  final_equity REAL NOT NULL,
  total_return REAL NOT NULL,
  max_drawdown REAL NOT NULL,
  sharpe_ratio REAL,
  win_rate REAL,
  profit_factor REAL,
  total_trades INTEGER,
  winning_trades INTEGER,
  losing_trades INTEGER,
  total_fees REAL,
  total_funding_fees REAL,
  avg_trade_duration_hours REAL,
  long_win_rate REAL,
  short_win_rate REAL,
  long_profit_sum REAL,
  short_profit_sum REAL,
  equity_curve_json TEXT,
  completed_at TEXT
);

INSERT INTO backtest_results_new (
  id, backtest_id, strategy_id, version_id, symbol_key, interval, start_time, end_time,
  initial_balance, final_equity, total_return, max_drawdown, sharpe_ratio, win_rate, profit_factor,
  total_trades, winning_trades, losing_trades, total_fees, total_funding_fees, avg_trade_duration_hours,
  long_win_rate, short_win_rate, long_profit_sum, short_profit_sum, equity_curve_json, completed_at
)
SELECT
  id, backtest_id, strategy_id, version_id,
  CASE
    WHEN symbol LIKE '%-%-%' THEN symbol
    ELSE UPPER(symbol) || '-BINANCE-SWAP'
  END AS symbol_key,
  interval, start_time, end_time,
  initial_balance, final_equity, total_return, max_drawdown, sharpe_ratio, win_rate, profit_factor,
  total_trades, winning_trades, losing_trades, total_fees, total_funding_fees, avg_trade_duration_hours,
  long_win_rate, short_win_rate, long_profit_sum, short_profit_sum, equity_curve_json, completed_at
FROM backtest_results;

DROP TABLE backtest_results;
ALTER TABLE backtest_results_new RENAME TO backtest_results;

-- 重建必要索引（保持原名，更新列）
CREATE INDEX IF NOT EXISTS idx_backtest_runs_created_at ON backtest_runs(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_backtest_runs_strategy ON backtest_runs(strategy_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_backtest_runs_synced ON backtest_runs(synced_to_server);
CREATE INDEX IF NOT EXISTS idx_backtest_runs_version ON backtest_runs(version_id);

CREATE INDEX IF NOT EXISTS idx_backtest_orders_backtest_id ON backtest_orders(backtest_id);
CREATE INDEX IF NOT EXISTS idx_backtest_orders_backtest_side_status ON backtest_orders(backtest_id, side, status);
CREATE INDEX IF NOT EXISTS idx_backtest_orders_backtest_status ON backtest_orders(backtest_id, status);
CREATE INDEX IF NOT EXISTS idx_backtest_orders_close_time ON backtest_orders(close_time);
CREATE INDEX IF NOT EXISTS idx_backtest_orders_open_time ON backtest_orders(open_time);
CREATE INDEX IF NOT EXISTS idx_backtest_orders_status ON backtest_orders(status);

CREATE INDEX IF NOT EXISTS idx_backtest_completed ON backtest_results(completed_at DESC);
CREATE INDEX IF NOT EXISTS idx_backtest_strategy ON backtest_results(strategy_id);
CREATE INDEX IF NOT EXISTS idx_backtest_symbol_key ON backtest_results(symbol_key);

-- 重新创建视图（使用 symbol_key）
CREATE VIEW backtest_orders_detail AS
    SELECT
        o.id,
        o.backtest_id,
        o.symbol_key,
        o.side,
        o.quantity,
        o.open_price,
        o.open_time,
        o.close_price,
        o.close_time,
        o.profit,
        o.fee,
        COALESCE(o.funding_fee, 0) AS funding_fee,
        COALESCE(o.surplus, o.profit) AS surplus,
        CAST((julianday(o.close_time) - julianday(o.open_time)) * 24 * 60 AS INTEGER) AS holding_minutes
    FROM backtest_orders o;

CREATE VIEW backtest_with_stats AS
    SELECT
        br.id,
        br.strategy_id,
        br.strategy_name,
        br.version_string,
        br.symbol_key,
        br.interval,
        br.start_date,
        br.end_date,
        br.status,
        br.created_at,
        br.completed_at,
        br.duration_seconds,
        COUNT(bo.id) AS total_orders,
        SUM(CASE WHEN bo.side = 'BUY' THEN 1 ELSE 0 END) AS long_orders,
        SUM(CASE WHEN bo.side = 'SELL' THEN 1 ELSE 0 END) AS short_orders,
        SUM(CASE WHEN bo.profit > 0 THEN 1 ELSE 0 END) AS winning_orders,
        SUM(CASE WHEN bo.profit <= 0 THEN 1 ELSE 0 END) AS losing_orders,
        ROUND(CAST(SUM(CASE WHEN bo.profit > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(COUNT(bo.id), 0), 4) AS total_win_rate,
        SUM(bo.profit) AS total_profit,
        MAX(bo.profit) AS max_profit,
        MIN(bo.profit) AS max_loss,
        SUM(bo.fee) AS total_fees,
        COALESCE(SUM(bo.funding_fee), 0) AS total_funding_fees,
        COALESCE(br.final_equity, br.initial_capital + SUM(bo.profit)) AS final_equity
    FROM backtest_runs br
    LEFT JOIN backtest_orders bo ON br.id = bo.backtest_id
    GROUP BY br.id;
"
        )
        ,
        new DbMigration(
            Id: "2025-12-31_03_core_market_tables_symbol_to_symbol_key",
            Description: "core market tables: klines/fundingrate/longshortratio 升级为 symbol_key + FK(instruments)，并迁移旧数据到 BINANCE-SWAP",
            DisableForeignKeys: true,
            Sql: @"
-- 1) 先确保 instruments 对应的 symbol_key 都存在（避免 FK 迁移失败）
INSERT OR IGNORE INTO instruments(symbol_key, base_symbol, exchange, market_type, venue_inst_id, is_enabled)
SELECT DISTINCT
  (UPPER(k.symbol) || '-BINANCE-SWAP') AS symbol_key,
  UPPER(k.symbol) AS base_symbol,
  'BINANCE' AS exchange,
  'SWAP' AS market_type,
  UPPER(k.symbol) AS venue_inst_id,
  1 AS is_enabled
FROM klines k
WHERE k.symbol IS NOT NULL
  AND instr(k.symbol, '-') = 0;

INSERT OR IGNORE INTO instruments(symbol_key, base_symbol, exchange, market_type, venue_inst_id, is_enabled)
SELECT DISTINCT
  (UPPER(f.symbol) || '-BINANCE-SWAP') AS symbol_key,
  UPPER(f.symbol) AS base_symbol,
  'BINANCE' AS exchange,
  'SWAP' AS market_type,
  UPPER(f.symbol) AS venue_inst_id,
  1 AS is_enabled
FROM fundingrate f
WHERE f.symbol IS NOT NULL
  AND instr(f.symbol, '-') = 0;

INSERT OR IGNORE INTO instruments(symbol_key, base_symbol, exchange, market_type, venue_inst_id, is_enabled)
SELECT DISTINCT
  (UPPER(l.symbol) || '-BINANCE-SWAP') AS symbol_key,
  UPPER(l.symbol) AS base_symbol,
  'BINANCE' AS exchange,
  'SWAP' AS market_type,
  UPPER(l.symbol) AS venue_inst_id,
  1 AS is_enabled
FROM longshortratio l
WHERE l.symbol IS NOT NULL
  AND instr(l.symbol, '-') = 0;

-- 2) klines: symbol -> symbol_key
DROP INDEX IF EXISTS idx_klines_symbol_interval;
DROP INDEX IF EXISTS idx_klines_time;
DROP INDEX IF EXISTS idx_klines_latest;
DROP INDEX IF EXISTS idx_klines_symbol_interval_time_range;
DROP INDEX IF EXISTS idx_klines_backtest_covering;

CREATE TABLE klines_new(
  symbol_key TEXT NOT NULL REFERENCES instruments(symbol_key),
  interval TEXT NOT NULL,
  open_time INTEGER NOT NULL,
  open REAL NOT NULL,
  high REAL NOT NULL,
  low REAL NOT NULL,
  close REAL NOT NULL,
  volume REAL NOT NULL,
  close_time INTEGER NOT NULL,
  quote_volume REAL,
  trade_count INTEGER,
  taker_buy_volume REAL,
  taker_buy_quote_volume REAL,
  created_at TEXT NOT NULL,
  PRIMARY KEY(symbol_key, interval, open_time, close_time)
);

INSERT OR IGNORE INTO klines_new(
  symbol_key, interval, open_time, open, high, low, close, volume,
  close_time, quote_volume, trade_count, taker_buy_volume, taker_buy_quote_volume, created_at
)
SELECT
  CASE
    WHEN instr(symbol, '-') > 0 THEN symbol
    ELSE UPPER(symbol) || '-BINANCE-SWAP'
  END AS symbol_key,
  interval, open_time, open, high, low, close, volume,
  close_time, quote_volume, trade_count, taker_buy_volume, taker_buy_quote_volume,
  created_at
FROM klines;

DROP TABLE klines;
ALTER TABLE klines_new RENAME TO klines;

CREATE INDEX IF NOT EXISTS idx_klines_symbol_interval ON klines(symbol_key, interval);
CREATE INDEX IF NOT EXISTS idx_klines_time ON klines(open_time);
CREATE INDEX IF NOT EXISTS idx_klines_latest ON klines(symbol_key, interval, open_time DESC);
CREATE INDEX IF NOT EXISTS idx_klines_symbol_interval_time_range ON klines(symbol_key, interval, open_time);
CREATE INDEX IF NOT EXISTS idx_klines_backtest_covering ON klines(symbol_key, interval, open_time, open, high, low, close, volume);

-- 3) fundingrate: symbol -> symbol_key
DROP INDEX IF EXISTS idx_funding_rates_symbol;
DROP INDEX IF EXISTS idx_funding_rates_symbol_key;
DROP INDEX IF EXISTS idx_funding_rates_time;

CREATE TABLE fundingrate_new(
  symbol_key TEXT NOT NULL REFERENCES instruments(symbol_key),
  calc_time INTEGER NOT NULL,
  calc_time_str TEXT,
  funding_interval_hours INTEGER,
  last_funding_rate REAL NOT NULL,
  created_at TEXT NOT NULL,
  PRIMARY KEY(symbol_key, calc_time),
  UNIQUE(symbol_key, calc_time)
);

INSERT OR IGNORE INTO fundingrate_new(
  symbol_key, calc_time, calc_time_str, funding_interval_hours, last_funding_rate, created_at
)
SELECT
  CASE
    WHEN instr(symbol, '-') > 0 THEN symbol
    ELSE UPPER(symbol) || '-BINANCE-SWAP'
  END AS symbol_key,
  calc_time, calc_time_str, funding_interval_hours, last_funding_rate, created_at
FROM fundingrate;

DROP TABLE fundingrate;
ALTER TABLE fundingrate_new RENAME TO fundingrate;

CREATE INDEX IF NOT EXISTS idx_funding_rates_symbol_key ON fundingrate(symbol_key);
CREATE INDEX IF NOT EXISTS idx_funding_rates_time ON fundingrate(calc_time);

-- 4) longshortratio: symbol -> symbol_key
CREATE TABLE longshortratio_new(
  symbol_key TEXT NOT NULL REFERENCES instruments(symbol_key),
  period TEXT NOT NULL,
  timestamp INTEGER NOT NULL,
  long_account_ratio REAL,
  long_position_ratio REAL,
  long_short_ratio REAL NOT NULL,
  update_time TEXT,
  created_at TEXT NOT NULL,
  PRIMARY KEY(symbol_key, period, timestamp),
  UNIQUE(symbol_key, period, timestamp)
);

INSERT OR IGNORE INTO longshortratio_new(
  symbol_key, period, timestamp, long_account_ratio, long_position_ratio, long_short_ratio, update_time, created_at
)
SELECT
  CASE
    WHEN instr(symbol, '-') > 0 THEN symbol
    ELSE UPPER(symbol) || '-BINANCE-SWAP'
  END AS symbol_key,
  period, timestamp, long_account_ratio, long_position_ratio, long_short_ratio, update_time, created_at
FROM longshortratio;

DROP TABLE longshortratio;
ALTER TABLE longshortratio_new RENAME TO longshortratio;
"
        )
        ,
        new DbMigration(
            Id: "2026-01-04_01_ensure_base_tables",
            Description: "确保基础表存在（app_settings, strategy_versions, instruments）",
            DisableForeignKeys: false,
            Sql: @"
-- app_settings 表
CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT PRIMARY KEY,
    value TEXT,
    description TEXT,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- strategy_versions 表
CREATE TABLE IF NOT EXISTS strategy_versions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    strategy_id TEXT NOT NULL,
    major_version INTEGER NOT NULL DEFAULT 1,
    minor_version INTEGER NOT NULL DEFAULT 0,
    patch_version INTEGER NOT NULL DEFAULT 0,
    version_string TEXT NOT NULL,
    change_type TEXT NOT NULL CHECK(change_type IN ('major', 'minor', 'patch', 'initial')),
    dsl TEXT NOT NULL,
    parameters TEXT NOT NULL DEFAULT '{}',
    dsl_hash TEXT,
    version_status TEXT NOT NULL DEFAULT 'draft' CHECK(version_status IN ('draft', 'active', 'inactive')),
    status_changed_at TEXT,
    validation_status TEXT NOT NULL DEFAULT 'pending' CHECK(validation_status IN ('pending', 'valid', 'invalid')),
    validation_message TEXT,
    validated_at TEXT,
    backtest_data TEXT,
    backtest_completed_at TEXT,
    change_description TEXT,
    breaking_changes TEXT,
    risk_disclosure TEXT,
    tags TEXT,
    deprecation_reason TEXT,
    force_upgrade INTEGER NOT NULL DEFAULT 0,
    upgrade_deadline TEXT,
    created_by TEXT NOT NULL DEFAULT 'system',
    created_at TEXT NOT NULL,
    activated_at TEXT,
    FOREIGN KEY (strategy_id) REFERENCES strategies(id) ON DELETE CASCADE,
    UNIQUE(strategy_id, major_version, minor_version, patch_version)
);

CREATE INDEX IF NOT EXISTS idx_versions_strategy_id ON strategy_versions(strategy_id);
CREATE INDEX IF NOT EXISTS idx_versions_version_status ON strategy_versions(version_status);
CREATE INDEX IF NOT EXISTS idx_versions_validation_status ON strategy_versions(validation_status);
CREATE INDEX IF NOT EXISTS idx_versions_created_at ON strategy_versions(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_versions_dsl_hash ON strategy_versions(dsl_hash);
CREATE INDEX IF NOT EXISTS idx_versions_created_by ON strategy_versions(created_by);
CREATE INDEX IF NOT EXISTS idx_versions_version_string ON strategy_versions(version_string);
CREATE INDEX IF NOT EXISTS idx_versions_strategy_version ON strategy_versions(strategy_id, major_version DESC, minor_version DESC, patch_version DESC);

-- instruments 表
CREATE TABLE IF NOT EXISTS instruments (
    symbol_key TEXT PRIMARY KEY,
    base_symbol TEXT NOT NULL,
    exchange TEXT NOT NULL,
    market_type TEXT NOT NULL,
    venue_inst_id TEXT NOT NULL,
    is_enabled INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    UNIQUE(exchange, market_type, base_symbol)
);

CREATE INDEX IF NOT EXISTS idx_instruments_enabled ON instruments(is_enabled);
CREATE INDEX IF NOT EXISTS idx_instruments_exchange_market ON instruments(exchange, market_type);
CREATE INDEX IF NOT EXISTS idx_instruments_base_symbol ON instruments(base_symbol);
"
        )
        ,
        new DbMigration(
            Id: "2026-01-04_02_ensure_backtest_auxiliary_tables",
            Description: "确保回测辅助表存在（signals, order_signals, drawdown, equity_curve）",
            DisableForeignKeys: false,
            Sql: @"
-- backtest_signals 表
CREATE TABLE IF NOT EXISTS backtest_signals (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    backtest_id TEXT NOT NULL REFERENCES backtest_runs(id) ON DELETE CASCADE ON UPDATE CASCADE,
    time TEXT NOT NULL,
    action TEXT NOT NULL,
    signal_price REAL NOT NULL,
    was_executed INTEGER NOT NULL DEFAULT 0,
    reason_if_not_executed TEXT,
    confidence REAL DEFAULT 0.0,
    strength REAL DEFAULT 1.0,
    description TEXT,
    take_profit REAL,
    stop_loss REAL,
    trend TEXT,
    configs_json TEXT,
    indicators_json TEXT,
    patterns_json TEXT,
    debug_json TEXT,
    candle_index INTEGER,
    global_index INTEGER,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    indicator_snapshots_json TEXT
);

CREATE INDEX IF NOT EXISTS idx_backtest_signals_backtest_id ON backtest_signals(backtest_id);
CREATE INDEX IF NOT EXISTS idx_backtest_signals_time ON backtest_signals(backtest_id, time);
CREATE INDEX IF NOT EXISTS idx_backtest_signals_action ON backtest_signals(backtest_id, action);
CREATE INDEX IF NOT EXISTS idx_backtest_signals_executed ON backtest_signals(backtest_id, was_executed);

-- backtest_order_signals 关联表
CREATE TABLE IF NOT EXISTS backtest_order_signals (
    order_id TEXT NOT NULL REFERENCES backtest_orders(id) ON DELETE CASCADE ON UPDATE CASCADE,
    signal_id INTEGER NOT NULL REFERENCES backtest_signals(id) ON DELETE CASCADE ON UPDATE CASCADE,
    action TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (DATETIME('now', 'localtime')),
    CHECK(action IN ('OPEN', 'CLOSE', 'HOLDING'))
);

CREATE INDEX IF NOT EXISTS idx_order_signals_order ON backtest_order_signals(order_id);
CREATE INDEX IF NOT EXISTS idx_order_signals_signal ON backtest_order_signals(signal_id);
CREATE INDEX IF NOT EXISTS idx_order_signals_action ON backtest_order_signals(action);

-- backtest_drawdown_periods 表
CREATE TABLE IF NOT EXISTS backtest_drawdown_periods (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    backtest_id TEXT NOT NULL REFERENCES backtest_runs(id) ON DELETE CASCADE ON UPDATE CASCADE,
    start_time TEXT NOT NULL,
    end_time TEXT NOT NULL,
    drawdown_percentage REAL NOT NULL,
    duration_seconds REAL NOT NULL,
    recovery_time REAL
);

CREATE INDEX IF NOT EXISTS idx_backtest_drawdown_backtest_id ON backtest_drawdown_periods(backtest_id);

-- backtest_equity_curve 表
CREATE TABLE IF NOT EXISTS backtest_equity_curve (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    backtest_id TEXT NOT NULL REFERENCES backtest_runs(id) ON DELETE CASCADE ON UPDATE CASCADE,
    time TEXT NOT NULL,
    equity REAL NOT NULL,
    cash REAL NOT NULL,
    position REAL NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_backtest_equity_backtest_id ON backtest_equity_curve(backtest_id);

-- backtest_db_meta 元数据表
CREATE TABLE IF NOT EXISTS backtest_db_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
"
        )
        ,
        new DbMigration(
            Id: "2026-01-04_03_ensure_live_trading_tables",
            Description: "确保实盘交易表存在（instances, snapshots, orders, sessions）",
            DisableForeignKeys: false,
            Sql: @"
-- trading_instances 表
CREATE TABLE IF NOT EXISTS trading_instances (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    exchange TEXT NOT NULL,
    symbol TEXT NOT NULL,
    timeframe TEXT NOT NULL,
    strategy_name TEXT NOT NULL,
    strategy_code TEXT,
    risk_config TEXT,
    capital_config TEXT,
    exchange_config TEXT,
    is_enabled INTEGER DEFAULT 1,
    created_at TEXT NOT NULL,
    last_run_at TEXT,
    UNIQUE(exchange, symbol, strategy_name)
);

CREATE INDEX IF NOT EXISTS idx_trading_instances_enabled ON trading_instances(is_enabled);
CREATE INDEX IF NOT EXISTS idx_trading_instances_exchange_symbol ON trading_instances(exchange, symbol);
CREATE INDEX IF NOT EXISTS idx_trading_instances_created_at ON trading_instances(created_at DESC);

-- instance_snapshots 表
CREATE TABLE IF NOT EXISTS instance_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    instance_id TEXT NOT NULL,
    timestamp TEXT NOT NULL,
    status INTEGER NOT NULL,
    total_equity REAL NOT NULL,
    available_balance REAL NOT NULL,
    used_margin REAL NOT NULL,
    unrealized_pnl REAL NOT NULL,
    realized_pnl REAL NOT NULL,
    today_pnl REAL NOT NULL,
    today_pnl_percent REAL NOT NULL,
    position_count INTEGER NOT NULL,
    open_order_count INTEGER NOT NULL,
    current_drawdown REAL NOT NULL,
    max_drawdown REAL NOT NULL,
    risk_level INTEGER NOT NULL,
    emergency_stop_activated INTEGER NOT NULL,
    total_trades INTEGER NOT NULL,
    today_trades INTEGER NOT NULL,
    win_rate REAL NOT NULL,
    consecutive_losses INTEGER NOT NULL,
    consecutive_wins INTEGER NOT NULL,
    error_message TEXT,
    running_duration_seconds INTEGER NOT NULL,
    FOREIGN KEY (instance_id) REFERENCES trading_instances(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_instance_snapshots_instance_id ON instance_snapshots(instance_id);
CREATE INDEX IF NOT EXISTS idx_instance_snapshots_timestamp ON instance_snapshots(timestamp);
CREATE INDEX IF NOT EXISTS idx_instance_snapshots_instance_time ON instance_snapshots(instance_id, timestamp DESC);

-- live_orders 表
CREATE TABLE IF NOT EXISTS live_orders (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    symbol TEXT NOT NULL,
    side TEXT NOT NULL,
    type TEXT NOT NULL,
    status TEXT NOT NULL,
    leverage REAL NOT NULL,
    margin REAL NOT NULL,
    quantity REAL NOT NULL,
    open_price REAL NOT NULL,
    open_time TEXT NOT NULL,
    filled_price REAL,
    filled_time TEXT,
    close_price REAL,
    close_time TEXT,
    fee REAL NOT NULL DEFAULT 0,
    funding_fee REAL NOT NULL DEFAULT 0,
    profit REAL,
    take_profit REAL,
    stop_loss REAL,
    liquidation_price REAL,
    remarks TEXT,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_live_orders_session ON live_orders(session_id);
CREATE INDEX IF NOT EXISTS idx_live_orders_symbol ON live_orders(symbol);
CREATE INDEX IF NOT EXISTS idx_live_orders_status ON live_orders(status);
CREATE INDEX IF NOT EXISTS idx_live_orders_open_time ON live_orders(open_time);

-- live_sessions 表
CREATE TABLE IF NOT EXISTS live_sessions (
    id TEXT PRIMARY KEY,
    strategy_name TEXT NOT NULL,
    symbol TEXT NOT NULL,
    leverage REAL NOT NULL,
    initial_capital REAL NOT NULL,
    start_time TEXT NOT NULL,
    end_time TEXT,
    status TEXT NOT NULL,
    final_equity REAL,
    total_profit REAL,
    total_fees REAL,
    total_trades INTEGER NOT NULL DEFAULT 0,
    winning_trades INTEGER NOT NULL DEFAULT 0,
    losing_trades INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_live_sessions_start_time ON live_sessions(start_time);
"
        )
        ,
        new DbMigration(
            Id: "2026-01-04_04_ensure_market_data_tables",
            Description: "确保市场数据表存在（fear_greed_index）",
            DisableForeignKeys: false,
            Sql: @"
-- fear_greed_index 恐慌贪婪指数表
CREATE TABLE IF NOT EXISTS fear_greed_index (
    date TEXT PRIMARY KEY NOT NULL UNIQUE,
    value INTEGER NOT NULL,
    classification TEXT NOT NULL
);
"
        )
        ,
        new DbMigration(
            Id: "2026-09-14_01_merge_backtest_schemas",
            Description: "合并回测 schema：废弃 backtest_results（无读取方，数据由 backtest_runs 系列全量承载）",
            DisableForeignKeys: true,
            Sql: @"
-- backtest_runs 系列（LocalBacktestStorage 全量写入）为主数据
-- backtest_results 是冗余摘要表，BacktestRepository 读方法零调用，写入路径不完整，废弃
DROP TABLE IF EXISTS backtest_results;
"
        )
    };
}

