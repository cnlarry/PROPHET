-- ============================================================================
-- Prophet 本地回测数据库架构 (SQLite)
-- 用于存储客户端本地的完整回测明细数据
-- 参考: Python backtest.py 的优秀设计
-- 创建日期: 2025-11-21
-- ============================================================================

-- 注意：SQLite 语法差异
-- 1. 不支持 ENUM，改用 TEXT + CHECK 约束
-- 2. 不支持 ON UPDATE CURRENT_TIMESTAMP，需要使用触发器
-- 3. JSON 字段类型为 TEXT (SQLite 3.9+ 支持 JSON 函数)

-- ============================================================================
-- 1. 回测历史主表
-- ============================================================================
DROP TABLE IF EXISTS backtest_history;
CREATE TABLE backtest_history (
    id TEXT PRIMARY KEY,                        -- bt_20251121_153045
    strategy_id TEXT NOT NULL,                  -- 策略ID
    version_id INTEGER NOT NULL,                -- 版本ID (对应API的strategy_dsl_versions.id)
    version_string TEXT NOT NULL,               -- 版本号 (v1.2.3)
    
    -- 回测配置 (JSON)
    backtest_config TEXT NOT NULL,              -- 完整的回测参数配置
    -- {
    --   "symbol": "BTCUSDT",
    --   "interval": "1m",
    --   "start_date": "2024-01-01",
    --   "end_date": "2024-12-31",
    --   "initial_capital": 10000,
    --   "taker_fee_rate": 0.0004,
    --   "slippage_rate": 0.0005,
    --   "signal_sampling_interval": "5m",
    --   "leverage": 50
    -- }
    
    -- 回测结果 (JSON 摘要)
    backtest_result TEXT,                       -- 统计结果摘要
    -- {
    --   "performance": {
    --     "total_return": 0.45,
    --     "max_drawdown": 0.15,
    --     "sharpe_ratio": 1.8,
    --     "win_rate": 0.62,
    --     "profit_factor": 2.1,
    --     "total_trades": 85,
    --     "winning_trades": 53,
    --     "losing_trades": 32,
    --     "final_equity": 14500.00,
    --     "total_fees": 120.50,
    --     "total_funding_fees": -15.30,
    --     "avg_trade_duration_hours": 48
    --   },
    --   "long_stats": {...},
    --   "short_stats": {...}
    -- }
    
    -- 权益曲线 (JSON 数组)
    equity_curve TEXT,                          -- 权益曲线数据点
    -- [
    --   {"timestamp": "2024-01-01T00:00:00", "equity": 10000, "drawdown": 0},
    --   {"timestamp": "2024-01-02T00:00:00", "equity": 10150, "drawdown": 0},
    --   ...
    -- ]
    
    -- 状态和进度
    status TEXT NOT NULL DEFAULT 'running' CHECK(status IN ('running', 'completed', 'failed', 'cancelled')),
    progress REAL DEFAULT 0.0,                  -- 进度百分比 (0-100)
    error_message TEXT,                         -- 错误信息
    
    -- 时间戳
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    started_at TEXT,
    completed_at TEXT,
    duration_seconds INTEGER,
    
    -- 标记是否已上传到服务器
    synced_to_server INTEGER DEFAULT 0,        -- 0-未同步，1-已同步
    synced_at TEXT                              -- 同步时间
);

CREATE INDEX idx_backtest_strategy ON backtest_history(strategy_id);
CREATE INDEX idx_backtest_version ON backtest_history(version_string);
CREATE INDEX idx_backtest_status ON backtest_history(status);
CREATE INDEX idx_backtest_created ON backtest_history(created_at DESC);
CREATE INDEX idx_backtest_synced ON backtest_history(synced_to_server);

-- ============================================================================
-- 2. 订单明细表 (复用 Python backtest.py 的设计)
-- ============================================================================
DROP TABLE IF EXISTS backtest_orders;
CREATE TABLE backtest_orders (
    id TEXT PRIMARY KEY,                        -- 订单ID (BUY_xxx or SELL_xxx)
    backtest_id TEXT NOT NULL,                  -- 关联回测ID
    symbol TEXT NOT NULL,                       -- 交易对
    side TEXT NOT NULL CHECK(side IN ('BUY', 'SELL')),
    order_type TEXT NOT NULL,                   -- MARKET, LIMIT等
    
    -- 开仓信息
    margin REAL NOT NULL,                       -- 保证金
    leverage INTEGER NOT NULL,                  -- 杠杆倍数
    quantity REAL NOT NULL,                     -- 数量
    open_price REAL NOT NULL,                   -- 开仓价
    open_time TEXT NOT NULL,                    -- 开仓时间
    
    -- 平仓信息
    close_price REAL NOT NULL DEFAULT 0,        -- 平仓价
    close_time TEXT NOT NULL,                   -- 平仓时间
    
    -- 费用和盈亏
    handling_fee REAL NOT NULL DEFAULT 0,       -- 手续费
    funding_fee REAL NOT NULL DEFAULT 0,        -- 资金费
    surplus REAL NOT NULL DEFAULT 0,            -- 净盈亏
    
    -- 止盈止损
    take_profit REAL NOT NULL DEFAULT 0,        -- 止盈线
    stop_loss REAL NOT NULL DEFAULT 0,          -- 止损线
    liquidation_price REAL NOT NULL DEFAULT 0,  -- 强平价
    
    -- 状态
    closed INTEGER NOT NULL DEFAULT 0,          -- 是否已平仓 (0-否，1-是)
    remarks TEXT,                               -- 备注
    create_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    
    FOREIGN KEY (backtest_id) REFERENCES backtest_history(id) ON DELETE CASCADE
);

CREATE INDEX idx_orders_backtest ON backtest_orders(backtest_id);
CREATE INDEX idx_orders_symbol ON backtest_orders(symbol);
CREATE INDEX idx_orders_side ON backtest_orders(side);
CREATE INDEX idx_orders_closed ON backtest_orders(closed);
CREATE INDEX idx_orders_open_time ON backtest_orders(open_time);

-- ============================================================================
-- 3. 信号明细表 (复用 Python backtest.py 的设计)
-- ============================================================================
DROP TABLE IF EXISTS backtest_signals;
CREATE TABLE backtest_signals (
    id TEXT PRIMARY KEY,                        -- 信号ID
    backtest_id TEXT NOT NULL,                  -- 关联回测ID
    strategy TEXT NOT NULL,                     -- 策略ID
    
    -- 信号内容
    action TEXT NOT NULL,                       -- BUY/SELL/HOLD
    confidence REAL NOT NULL DEFAULT 0.0,       -- 置信度 (0.0-1.0)
    reason TEXT NOT NULL,                       -- 原因说明
    trend TEXT NOT NULL,                        -- 趋势 (BULLISH/BEARISH/NEUTRAL)
    
    -- 止盈止损建议
    sl REAL NOT NULL DEFAULT 0,                 -- 止损线
    tp REAL NOT NULL DEFAULT 0,                 -- 止盈线
    
    -- 详细数据 (JSON)
    configs TEXT NOT NULL,                      -- 配置参数 JSON
    indicators TEXT NOT NULL,                   -- 技术指标 JSON
    debug TEXT NOT NULL,                        -- 调试信息 JSON
    
    create_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    
    FOREIGN KEY (backtest_id) REFERENCES backtest_history(id) ON DELETE CASCADE
);

CREATE INDEX idx_signals_backtest ON backtest_signals(backtest_id);
CREATE INDEX idx_signals_action ON backtest_signals(action);
CREATE INDEX idx_signals_create ON backtest_signals(create_at);

-- ============================================================================
-- 4. 订单-信号关联表 (复用 Python backtest.py 的核心设计 🔥)
-- ============================================================================
DROP TABLE IF EXISTS backtest_order_signals;
CREATE TABLE backtest_order_signals (
    order_id TEXT NOT NULL,                     -- 订单ID
    signal_id TEXT NOT NULL,                    -- 信号ID
    action TEXT NOT NULL CHECK(action IN ('OPEN', 'CLOSE', 'HOLDING')),  -- 🔥 关键设计
    -- OPEN: 信号触发开仓
    -- CLOSE: 信号触发平仓
    -- HOLDING: 持仓期间的确认信号
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    
    FOREIGN KEY (order_id) REFERENCES backtest_orders(id) ON DELETE CASCADE,
    FOREIGN KEY (signal_id) REFERENCES backtest_signals(id) ON DELETE CASCADE
);

CREATE INDEX idx_order_signals_order ON backtest_order_signals(order_id);
CREATE INDEX idx_order_signals_signal ON backtest_order_signals(signal_id);
CREATE INDEX idx_order_signals_action ON backtest_order_signals(action);

-- ============================================================================
-- 5. K线形态表 (复用 Python backtest.py 的设计)
-- ============================================================================
DROP TABLE IF EXISTS backtest_patterns;
CREATE TABLE backtest_patterns (
    id TEXT PRIMARY KEY,                        -- 形态ID
    signal_id TEXT NOT NULL,                    -- 关联信号ID
    backtest_id TEXT NOT NULL,                  -- 关联回测ID
    
    name TEXT NOT NULL,                         -- 形态名称 (ENGULFING, HAMMER等)
    description TEXT,                           -- 中文描述
    side TEXT NOT NULL CHECK(side IN ('BULLISH', 'BEARISH', 'NEUTRAL')),
    
    raw INTEGER NOT NULL,                       -- TA-Lib 原始值 (±100/±200)
    score INTEGER NOT NULL,                     -- 绝对分值 (0-200)
    rank INTEGER NOT NULL,                      -- 按强度排序 (从1开始)
    
    kline_open_time INTEGER NOT NULL,           -- K线开盘时间 (毫秒时间戳)
    kline_close_time INTEGER NOT NULL,          -- K线收盘时间 (毫秒时间戳)
    
    create_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    
    FOREIGN KEY (signal_id) REFERENCES backtest_signals(id) ON DELETE CASCADE,
    FOREIGN KEY (backtest_id) REFERENCES backtest_history(id) ON DELETE CASCADE
);

CREATE INDEX idx_patterns_signal ON backtest_patterns(signal_id);
CREATE INDEX idx_patterns_backtest ON backtest_patterns(backtest_id);
CREATE INDEX idx_patterns_name ON backtest_patterns(name);
CREATE INDEX idx_patterns_name_side ON backtest_patterns(name, side);

-- ============================================================================
-- 6. 统计视图 (复用 Python backtest.py 的 backtest_with_stats 设计 🔥)
-- ============================================================================
DROP VIEW IF EXISTS backtest_with_stats;
CREATE VIEW backtest_with_stats AS
SELECT 
    bh.id,
    bh.strategy_id,
    bh.version_string,
    bh.status,
    bh.created_at,
    bh.completed_at,
    bh.duration_seconds,
    
    -- 从 orders 表实时计算统计指标
    COUNT(bo.id) AS total_orders,
    SUM(CASE WHEN bo.side = 'BUY' THEN 1 ELSE 0 END) AS long_orders,
    SUM(CASE WHEN bo.side = 'SELL' THEN 1 ELSE 0 END) AS short_orders,
    SUM(CASE WHEN bo.surplus > 0 THEN 1 ELSE 0 END) AS winning_orders,
    SUM(CASE WHEN bo.surplus <= 0 THEN 1 ELSE 0 END) AS losing_orders,
    
    -- 多空分离统计
    SUM(CASE WHEN bo.side = 'BUY' AND bo.surplus > 0 THEN 1 ELSE 0 END) AS long_wins,
    SUM(CASE WHEN bo.side = 'BUY' AND bo.surplus <= 0 THEN 1 ELSE 0 END) AS long_losses,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.surplus > 0 THEN 1 ELSE 0 END) AS short_wins,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.surplus <= 0 THEN 1 ELSE 0 END) AS short_losses,
    
    -- 胜率
    ROUND(CAST(SUM(CASE WHEN bo.surplus > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(COUNT(bo.id), 0), 4) AS total_win_rate,
    ROUND(CAST(SUM(CASE WHEN bo.side = 'BUY' AND bo.surplus > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(SUM(CASE WHEN bo.side = 'BUY' THEN 1 ELSE 0 END), 0), 4) AS long_win_rate,
    ROUND(CAST(SUM(CASE WHEN bo.side = 'SELL' AND bo.surplus > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(SUM(CASE WHEN bo.side = 'SELL' THEN 1 ELSE 0 END), 0), 4) AS short_win_rate,
    
    -- 盈亏统计
    SUM(bo.surplus) AS total_profit,
    SUM(CASE WHEN bo.side = 'BUY' AND bo.surplus > 0 THEN bo.surplus ELSE 0 END) AS long_profit_sum,
    SUM(CASE WHEN bo.side = 'BUY' AND bo.surplus <= 0 THEN bo.surplus ELSE 0 END) AS long_loss_sum,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.surplus > 0 THEN bo.surplus ELSE 0 END) AS short_profit_sum,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.surplus <= 0 THEN bo.surplus ELSE 0 END) AS short_loss_sum,
    
    MAX(bo.surplus) AS max_profit,
    MIN(bo.surplus) AS max_loss,
    
    -- 费用统计
    SUM(bo.handling_fee) AS total_fees,
    SUM(bo.funding_fee) AS total_funding_fees,
    SUM(CASE WHEN bo.side = 'BUY' THEN bo.funding_fee ELSE 0 END) AS long_funding_fees,
    SUM(CASE WHEN bo.side = 'SELL' THEN bo.funding_fee ELSE 0 END) AS short_funding_fees,
    
    -- 衍生指标
    -- 盈亏比
    ROUND(AVG(CASE WHEN bo.surplus > 0 THEN bo.surplus END) / NULLIF(ABS(AVG(CASE WHEN bo.surplus <= 0 THEN bo.surplus END)), 0), 4) AS avg_pl_ratio,
    
    -- 最终权益
    (json_extract(bh.backtest_config, '$.initial_capital') + SUM(bo.surplus)) AS final_equity
    
FROM backtest_history bh
LEFT JOIN backtest_orders bo ON bh.id = bo.backtest_id AND bo.closed = 1
GROUP BY bh.id;

-- ============================================================================
-- 7. 权益曲线视图 (复用 Python backtest.py 的 orders_equity 设计 🔥)
-- ============================================================================
DROP VIEW IF EXISTS backtest_equity_curve;
CREATE VIEW backtest_equity_curve AS
SELECT 
    backtest_id,
    create_at,
    order_id,
    cum_pnl,
    -- SQLite 不支持窗口函数中的 MAX() OVER()，需要子查询实现
    (SELECT MAX(cum_pnl) FROM (
        SELECT 
            id AS order_id,
            SUM(surplus) OVER (ORDER BY create_at, id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS cum_pnl
        FROM backtest_orders 
        WHERE backtest_id = o.backtest_id AND closed = 1
        ORDER BY create_at, id
    ) WHERE create_at <= o.create_at) AS running_max_cum_pnl
FROM (
    SELECT 
        backtest_id,
        create_at,
        id AS order_id,
        SUM(surplus) OVER (ORDER BY create_at, id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS cum_pnl
    FROM backtest_orders o
    WHERE closed = 1
    ORDER BY create_at, id
) o;

-- 注意：SQLite 窗口函数支持需要 3.25+ 版本

-- ============================================================================
-- 8. 订单详情视图 (复用 Python backtest.py 的 orders_with_signals 设计)
-- ============================================================================
DROP VIEW IF EXISTS backtest_orders_detail;
CREATE VIEW backtest_orders_detail AS
SELECT 
    o.id,
    o.backtest_id,
    o.symbol,
    o.side,
    o.order_type,
    o.quantity,
    o.open_price,
    o.open_time,
    o.close_price,
    o.close_time,
    o.surplus,
    o.handling_fee,
    o.funding_fee,
    o.closed,
    o.remarks,
    
    -- 开仓信号
    open_signal.reason AS open_reason,
    COALESCE(open_signal.confidence, 0) AS open_confidence,
    
    -- 平仓信号
    close_signal.reason AS close_reason,
    COALESCE(close_signal.confidence, 0) AS close_confidence,
    
    -- 持仓时长 (分钟)
    CAST((julianday(o.close_time) - julianday(o.open_time)) * 24 * 60 AS INTEGER) AS holding_minutes,
    
    -- 信号统计
    (SELECT COUNT(*) FROM backtest_order_signals os WHERE os.order_id = o.id) AS total_signals,
    (SELECT COUNT(*) FROM backtest_order_signals os WHERE os.order_id = o.id AND os.action = 'HOLDING') AS holding_signals
    
FROM backtest_orders o
LEFT JOIN backtest_order_signals os_open ON o.id = os_open.order_id AND os_open.action = 'OPEN'
LEFT JOIN backtest_signals open_signal ON os_open.signal_id = open_signal.id
LEFT JOIN backtest_order_signals os_close ON o.id = os_close.order_id AND os_close.action = 'CLOSE'
LEFT JOIN backtest_signals close_signal ON os_close.signal_id = close_signal.id;

-- ============================================================================
-- 9. 初始化完成
-- ============================================================================
-- 创建一个 meta 表记录数据库版本
DROP TABLE IF EXISTS backtest_db_meta;
CREATE TABLE backtest_db_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

INSERT INTO backtest_db_meta (key, value) VALUES 
    ('schema_version', '2.0'),
    ('created_at', datetime('now', 'localtime')),
    ('description', 'Prophet 本地回测数据库 - 整合 Python backtest.py 精华设计');

-- ============================================================================
-- 说明文档
-- ============================================================================
-- 本数据库架构整合了 Python backtest.py 的精华设计：
-- 
-- 1. ✅ 批量落库优化 - 客户端使用 BacktestCache 缓存后批量插入
-- 2. ✅ 订单-信号关联 - order_signals 表记录 OPEN/CLOSE/HOLDING 动作
-- 3. ✅ K线形态存储 - patterns 表独立存储形态识别结果
-- 4. ✅ 统计视图 - backtest_with_stats 实时计算所有指标
-- 5. ✅ 权益曲线 - backtest_equity_curve 计算累计盈亏和回撤
-- 
-- 使用方式：
-- 1. 回测过程中：先缓存数据到内存 (BacktestCache)
-- 2. 回测完成后：批量插入到本地 SQLite (executemany)
-- 3. 提取摘要数据：从 backtest_with_stats 视图读取统计结果
-- 4. 上传到服务器：调用 API 只上传摘要 (backtest_result JSON)
-- ============================================================================

