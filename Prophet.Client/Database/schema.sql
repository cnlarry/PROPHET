CREATE TABLE app_settings (
    key TEXT PRIMARY KEY,                       -- 配置键
    value TEXT,                                 -- 配置值
    description TEXT,                           -- 配置描述
    updated_at TEXT NOT NULL                    -- 更新时间
);

CREATE TABLE instruments (
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

CREATE TABLE "backtest_configs" (
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

CREATE TABLE backtest_db_meta (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
                );

CREATE TABLE "backtest_runs" (
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

CREATE TABLE [backtest_drawdown_periods](
  [id] INTEGER PRIMARY KEY AUTOINCREMENT, 
  [backtest_id] TEXT NOT NULL REFERENCES [backtest_runs]([id]) ON DELETE CASCADE ON UPDATE CASCADE, 
  [start_time] TEXT NOT NULL, 
  [end_time] TEXT NOT NULL, 
  [drawdown_percentage] REAL NOT NULL, 
  [duration_seconds] REAL NOT NULL, 
  [recovery_time] REAL);

CREATE TABLE [backtest_equity_curve](
  [id] INTEGER PRIMARY KEY AUTOINCREMENT, 
  [backtest_id] TEXT NOT NULL REFERENCES [backtest_runs]([id]) ON DELETE CASCADE ON UPDATE CASCADE, 
  [time] TEXT NOT NULL, 
  [equity] REAL NOT NULL, 
  [cash] REAL NOT NULL, 
  [position] REAL NOT NULL);

CREATE TABLE "backtest_orders" (
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

CREATE TABLE [backtest_signals](
  [id] INTEGER PRIMARY KEY AUTOINCREMENT, 
  [backtest_id] TEXT NOT NULL REFERENCES [backtest_runs]([id]) ON DELETE CASCADE ON UPDATE CASCADE, 
  [time] TEXT NOT NULL, 
  [action] TEXT NOT NULL, 
  [signal_price] REAL NOT NULL, 
  [was_executed] INTEGER NOT NULL DEFAULT 0, 
  [reason_if_not_executed] TEXT, 
  [confidence] REAL DEFAULT (0.0), 
  [strength] REAL DEFAULT (1.0), 
  [description] TEXT, 
  [take_profit] REAL, 
  [stop_loss] REAL, 
  [trend] TEXT, 
  [configs_json] TEXT, 
  [indicators_json] TEXT, 
  [patterns_json] TEXT, 
  [debug_json] TEXT, 
  [candle_index] INTEGER, 
  [global_index] INTEGER, 
  [created_at] TEXT DEFAULT CURRENT_TIMESTAMP, 
  [indicator_snapshots_json] TEXT);

CREATE TABLE [backtest_order_signals](
  [order_id] TEXT NOT NULL REFERENCES [backtest_orders]([id]) ON DELETE CASCADE ON UPDATE CASCADE, 
  [signal_id] INTEGER NOT NULL REFERENCES [backtest_signals]([id]) ON DELETE CASCADE ON UPDATE CASCADE, 
  [action] TEXT NOT NULL, 
  [created_at] TEXT NOT NULL DEFAULT (DATETIME ('now', 'localtime')), 
  CHECK([action] IN ('OPEN', 'CLOSE', 'HOLDING')));

CREATE TABLE "strategies" (
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

CREATE TABLE [fear_greed_index](
  [date] TEXT PRIMARY KEY NOT NULL UNIQUE, 
  [value] INTEGER NOT NULL, 
  [classification] TEXT NOT NULL);

CREATE TABLE "fundingrate"(
  symbol_key TEXT NOT NULL REFERENCES instruments(symbol_key),
  calc_time INTEGER NOT NULL,
  calc_time_str TEXT,
  funding_interval_hours INTEGER,
  last_funding_rate REAL NOT NULL,
  created_at TEXT NOT NULL,
  PRIMARY KEY(symbol_key, calc_time),
  UNIQUE(symbol_key, calc_time)
);

CREATE TABLE trading_instances (
    id TEXT PRIMARY KEY,                        -- 实例ID（UUID）
    name TEXT NOT NULL,                         -- 实例名称
    exchange TEXT NOT NULL,                     -- 交易所
    symbol TEXT NOT NULL,                       -- 交易对
    timeframe TEXT NOT NULL,                    -- 时间框架
    strategy_name TEXT NOT NULL,                -- 策略名称
    strategy_code TEXT,                         -- 策略代码
    risk_config TEXT,                           -- 风控配置（JSON）
    capital_config TEXT,                        -- 资金配置（JSON）
    exchange_config TEXT,                       -- 交易所配置（JSON）
    is_enabled INTEGER DEFAULT 1,               -- 是否启用（0=否，1=是）
    created_at TEXT NOT NULL,                   -- 创建时间
    last_run_at TEXT,                           -- 最后运行时间
    UNIQUE(exchange, symbol, strategy_name)     -- 同一交易所、交易对、策略名唯一
);

CREATE TABLE instance_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    instance_id TEXT NOT NULL,                  -- 实例ID
    timestamp TEXT NOT NULL,                    -- 快照时间
    status INTEGER NOT NULL,                    -- 运行状态
    total_equity REAL NOT NULL,                 -- 总权益
    available_balance REAL NOT NULL,            -- 可用余额
    used_margin REAL NOT NULL,                  -- 已用保证金
    unrealized_pnl REAL NOT NULL,               -- 未实现盈亏
    realized_pnl REAL NOT NULL,                 -- 已实现盈亏
    today_pnl REAL NOT NULL,                    -- 今日盈亏
    today_pnl_percent REAL NOT NULL,            -- 今日盈亏百分比
    position_count INTEGER NOT NULL,            -- 持仓数量
    open_order_count INTEGER NOT NULL,          -- 挂单数量
    current_drawdown REAL NOT NULL,             -- 当前回撤
    max_drawdown REAL NOT NULL,                 -- 最大回撤
    risk_level INTEGER NOT NULL,                -- 风险等级
    emergency_stop_activated INTEGER NOT NULL,  -- 紧急停止是否激活
    total_trades INTEGER NOT NULL,              -- 总交易次数
    today_trades INTEGER NOT NULL,              -- 今日交易次数
    win_rate REAL NOT NULL,                     -- 胜率
    consecutive_losses INTEGER NOT NULL,        -- 连续亏损次数
    consecutive_wins INTEGER NOT NULL,          -- 连续盈利次数
    error_message TEXT,                         -- 错误信息
    running_duration_seconds INTEGER NOT NULL,  -- 运行时长（秒）
    FOREIGN KEY (instance_id) REFERENCES trading_instances(id) ON DELETE CASCADE
);

CREATE TABLE "klines"(
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

CREATE TABLE live_orders (
    id TEXT PRIMARY KEY,                        -- 订单ID
    session_id TEXT NOT NULL,                   -- 会话ID（关联回测ID或实例ID）
    symbol TEXT NOT NULL,                       -- 交易对
    side TEXT NOT NULL,                         -- 方向（BUY/SELL）
    type TEXT NOT NULL,                         -- 订单类型
    status TEXT NOT NULL,                       -- 订单状态
    leverage REAL NOT NULL,                     -- 杠杆倍数
    margin REAL NOT NULL,                       -- 保证金
    quantity REAL NOT NULL,                     -- 数量
    open_price REAL NOT NULL,                   -- 开仓价格
    open_time TEXT NOT NULL,                    -- 开仓时间
    filled_price REAL,                          -- 成交价格
    filled_time TEXT,                           -- 成交时间
    close_price REAL,                           -- 平仓价格
    close_time TEXT,                            -- 平仓时间
    fee REAL NOT NULL DEFAULT 0,                -- 手续费
    funding_fee REAL NOT NULL DEFAULT 0,        -- 资金费率
    profit REAL,                                -- 盈亏
    take_profit REAL,                           -- 止盈价格
    stop_loss REAL,                             -- 止损价格
    liquidation_price REAL,                     -- 强平价格
    remarks TEXT,                               -- 备注
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, -- 创建时间
    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP  -- 更新时间
);

CREATE TABLE live_sessions (
    id TEXT PRIMARY KEY,                        -- 会话ID
    strategy_name TEXT NOT NULL,                -- 策略名称
    symbol TEXT NOT NULL,                       -- 交易对
    leverage REAL NOT NULL,                     -- 杠杆倍数
    initial_capital REAL NOT NULL,              -- 初始资金
    start_time TEXT NOT NULL,                   -- 开始时间
    end_time TEXT,                              -- 结束时间
    status TEXT NOT NULL,                       -- 状态
    final_equity REAL,                          -- 最终权益
    total_profit REAL,                          -- 总盈亏
    total_fees REAL,                            -- 总手续费
    total_trades INTEGER NOT NULL DEFAULT 0,    -- 总交易次数
    winning_trades INTEGER NOT NULL DEFAULT 0,  -- 盈利交易次数
    losing_trades INTEGER NOT NULL DEFAULT 0,   -- 亏损交易次数
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP -- 创建时间
);

CREATE TABLE "longshortratio"(
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

CREATE TABLE schema_migrations (
    id TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    applied_at TEXT NOT NULL DEFAULT (DATETIME('now', 'localtime'))
);

CREATE TABLE "schema_migrations_legacy_20251231023643" (
    version TEXT PRIMARY KEY,                   -- 版本号
    applied_at TEXT NOT NULL,                   -- 应用时间
    description TEXT                            -- 描述
);

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

CREATE INDEX [idx_backtest_drawdown_backtest_id]
ON [backtest_drawdown_periods]([backtest_id]);

CREATE INDEX [idx_backtest_equity_backtest_id]
ON [backtest_equity_curve]([backtest_id]);

CREATE INDEX idx_backtest_orders_backtest_id ON backtest_orders(backtest_id);

CREATE INDEX idx_backtest_orders_backtest_side_status ON backtest_orders(backtest_id, side, status);

CREATE INDEX idx_backtest_orders_backtest_status ON backtest_orders(backtest_id, status);

CREATE INDEX idx_backtest_orders_close_time ON backtest_orders(close_time);

CREATE INDEX idx_backtest_orders_open_time ON backtest_orders(open_time);

CREATE INDEX idx_backtest_orders_status ON backtest_orders(status);

CREATE INDEX idx_backtest_runs_created_at ON backtest_runs(created_at DESC);

CREATE INDEX idx_backtest_runs_strategy ON backtest_runs(strategy_id, created_at DESC);

CREATE INDEX idx_backtest_runs_synced ON backtest_runs(synced_to_server);

CREATE INDEX idx_backtest_runs_version ON backtest_runs(version_id);

CREATE INDEX [idx_backtest_signals_action]
ON [backtest_signals](
  [backtest_id], 
  [action]);

CREATE INDEX [idx_backtest_signals_backtest_id]
ON [backtest_signals]([backtest_id]);

CREATE INDEX [idx_backtest_signals_executed]
ON [backtest_signals](
  [backtest_id], 
  [was_executed]);

CREATE INDEX [idx_backtest_signals_time]
ON [backtest_signals](
  [backtest_id], 
  [time]);

CREATE INDEX idx_funding_rates_symbol_key ON fundingrate(symbol_key);

CREATE INDEX idx_funding_rates_time ON fundingrate(calc_time);

CREATE INDEX idx_instance_snapshots_instance_id 
ON instance_snapshots(instance_id);

CREATE INDEX idx_instance_snapshots_instance_time 
ON instance_snapshots(instance_id, timestamp DESC);

CREATE INDEX idx_instance_snapshots_timestamp 
ON instance_snapshots(timestamp);

CREATE INDEX idx_instruments_base_symbol ON instruments(base_symbol);

CREATE INDEX idx_instruments_enabled ON instruments(is_enabled);

CREATE INDEX idx_instruments_exchange_market ON instruments(exchange, market_type);

CREATE INDEX idx_klines_backtest_covering ON klines(symbol_key, interval, open_time, open, high, low, close, volume);

CREATE INDEX idx_klines_latest ON klines(symbol_key, interval, open_time DESC);

CREATE INDEX idx_klines_symbol_interval ON klines(symbol_key, interval);

CREATE INDEX idx_klines_symbol_interval_time_range ON klines(symbol_key, interval, open_time);

CREATE INDEX idx_klines_time ON klines(open_time);

CREATE INDEX idx_live_orders_open_time 
ON live_orders(open_time);

CREATE INDEX idx_live_orders_session 
ON live_orders(session_id);

CREATE INDEX idx_live_orders_status 
ON live_orders(status);

CREATE INDEX idx_live_orders_symbol 
ON live_orders(symbol);

CREATE INDEX idx_live_sessions_start_time 
ON live_sessions(start_time);

CREATE INDEX [idx_order_signals_action]
ON [backtest_order_signals]([action]);

CREATE INDEX [idx_order_signals_order]
ON [backtest_order_signals]([order_id]);

CREATE INDEX [idx_order_signals_signal]
ON [backtest_order_signals]([signal_id]);

CREATE INDEX idx_strategies_status ON strategies(status);

CREATE INDEX idx_strategies_updated_at ON strategies(updated_at DESC);

CREATE INDEX idx_trading_instances_created_at 
ON trading_instances(created_at DESC);

CREATE INDEX idx_trading_instances_enabled 
ON trading_instances(is_enabled);

CREATE INDEX idx_trading_instances_exchange_symbol 
ON trading_instances(exchange, symbol);

CREATE INDEX idx_versions_created_at ON strategy_versions(created_at DESC);

CREATE INDEX idx_versions_created_by ON strategy_versions(created_by);

CREATE INDEX idx_versions_dsl_hash ON strategy_versions(dsl_hash);

CREATE INDEX idx_versions_strategy_id ON strategy_versions(strategy_id);

CREATE INDEX idx_versions_strategy_version ON strategy_versions(strategy_id, major_version DESC, minor_version DESC, patch_version DESC);

CREATE INDEX idx_versions_validation_status ON strategy_versions(validation_status);

CREATE INDEX idx_versions_version_status ON strategy_versions(version_status);

CREATE INDEX idx_versions_version_string ON strategy_versions(version_string);
