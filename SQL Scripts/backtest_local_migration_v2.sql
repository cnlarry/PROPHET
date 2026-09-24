-- ============================================================================
-- Prophet 本地回测数据库升级脚本 (SQLite)
-- 从现有 backtest_runs 架构升级到 V2 (整合 Python backtest.py 精华设计)
-- 创建日期: 2025-11-21
-- 目标数据库: Prophet.Client/backtest.db
-- ============================================================================

-- 此脚本应该在 LocalBacktestStorage.InitializeDatabase() 方法中执行
-- 它会检查现有表结构并添加新的表和视图

-- ============================================================================
-- 1. 升级 backtest_runs 表 (添加新字段)
-- ============================================================================

-- 检查并添加 version_id 字段 (关联到API的strategy_dsl_versions.id)
-- 注意：SQLite不支持 ALTER TABLE ADD COLUMN IF NOT EXISTS，需要先检查列是否存在
-- 这段SQL将在C#代码中分别执行每个ALTER TABLE

-- ALTER TABLE backtest_runs ADD COLUMN version_id INTEGER;
-- ALTER TABLE backtest_runs ADD COLUMN version_string TEXT;
-- ALTER TABLE backtest_runs ADD COLUMN backtest_config TEXT;  -- 完整的回测参数配置 JSON
-- ALTER TABLE backtest_runs ADD COLUMN backtest_result TEXT;  -- 统计结果摘要 JSON
-- ALTER TABLE backtest_runs ADD COLUMN equity_curve_json TEXT;  -- 权益曲线数据 (用于上传到API)
-- ALTER TABLE backtest_runs ADD COLUMN synced_to_server INTEGER DEFAULT 0;  -- 是否已同步到服务器
-- ALTER TABLE backtest_runs ADD COLUMN synced_at TEXT;  -- 同步时间
-- ALTER TABLE backtest_runs ADD COLUMN progress REAL DEFAULT 0.0;  -- 进度百分比

-- 说明：这些ALTER TABLE语句将在C#代码中执行，因为SQLite不支持条件性添加列

-- ============================================================================
-- 2. 创建订单-信号关联表 (🔥 Python backtest.py 的核心设计)
-- ============================================================================
CREATE TABLE IF NOT EXISTS backtest_order_signals (
    order_id TEXT NOT NULL,                     -- 订单ID (关联 backtest_orders.id)
    signal_id INTEGER NOT NULL,                 -- 信号ID (关联 backtest_signals.id)
    action TEXT NOT NULL CHECK(action IN ('OPEN', 'CLOSE', 'HOLDING')),
    -- OPEN: 信号触发开仓
    -- CLOSE: 信号触发平仓
    -- HOLDING: 持仓期间的确认信号
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    
    FOREIGN KEY (order_id) REFERENCES backtest_orders(id) ON DELETE CASCADE,
    FOREIGN KEY (signal_id) REFERENCES backtest_signals(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_order_signals_order ON backtest_order_signals(order_id);
CREATE INDEX IF NOT EXISTS idx_order_signals_signal ON backtest_order_signals(signal_id);
CREATE INDEX IF NOT EXISTS idx_order_signals_action ON backtest_order_signals(action);

-- ============================================================================
-- 3. 创建K线形态表 (🔥 Python backtest.py 的设计)
-- ============================================================================
CREATE TABLE IF NOT EXISTS backtest_patterns (
    id TEXT PRIMARY KEY,                        -- 形态ID
    signal_id INTEGER NOT NULL,                 -- 关联信号ID (backtest_signals.id)
    backtest_id TEXT NOT NULL,                  -- 关联回测ID (backtest_runs.id)
    
    name TEXT NOT NULL,                         -- 形态名称 (ENGULFING, HAMMER等)
    description TEXT,                           -- 中文描述
    side TEXT NOT NULL CHECK(side IN ('BULLISH', 'BEARISH', 'NEUTRAL')),
    
    raw INTEGER NOT NULL,                       -- TA-Lib 原始值 (±100/±200)
    score INTEGER NOT NULL,                     -- 绝对分值 (0-200)
    rank INTEGER NOT NULL,                      -- 按强度排序 (从1开始)
    
    kline_open_time INTEGER NOT NULL,           -- K线开盘时间 (毫秒时间戳)
    kline_close_time INTEGER NOT NULL,          -- K线收盘时间 (毫秒时间戳)
    
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    
    FOREIGN KEY (signal_id) REFERENCES backtest_signals(id) ON DELETE CASCADE,
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_patterns_signal ON backtest_patterns(signal_id);
CREATE INDEX IF NOT EXISTS idx_patterns_backtest ON backtest_patterns(backtest_id);
CREATE INDEX IF NOT EXISTS idx_patterns_name ON backtest_patterns(name);
CREATE INDEX IF NOT EXISTS idx_patterns_name_side ON backtest_patterns(name, side);

-- ============================================================================
-- 4. 升级 backtest_signals 表 (添加新字段以兼容Python设计)
-- ============================================================================

-- 说明：现有的backtest_signals表字段：
-- - id, backtest_id, time, action, was_executed, reason_if_not_executed, signal_price

-- 需要添加的字段 (与Python backtest.py对齐):
-- ALTER TABLE backtest_signals ADD COLUMN strategy TEXT;  -- 策略ID
-- ALTER TABLE backtest_signals ADD COLUMN confidence REAL DEFAULT 0.0;  -- 置信度
-- ALTER TABLE backtest_signals ADD COLUMN reason TEXT;  -- 原因说明
-- ALTER TABLE backtest_signals ADD COLUMN trend TEXT;  -- 趋势 (BULLISH/BEARISH/NEUTRAL)
-- ALTER TABLE backtest_signals ADD COLUMN sl REAL DEFAULT 0;  -- 止损线
-- ALTER TABLE backtest_signals ADD COLUMN tp REAL DEFAULT 0;  -- 止盈线
-- ALTER TABLE backtest_signals ADD COLUMN configs TEXT;  -- 配置参数 JSON
-- ALTER TABLE backtest_signals ADD COLUMN indicators TEXT;  -- 技术指标 JSON
-- ALTER TABLE backtest_signals ADD COLUMN debug TEXT;  -- 调试信息 JSON

-- 这些ALTER TABLE语句将在C#代码中执行

-- ============================================================================
-- 5. 升级 backtest_orders 表 (添加新字段以兼容Python设计)
-- ============================================================================

-- 现有字段：
-- - id, backtest_id, side, type, status, quantity
-- - open_price, open_time, filled_price, filled_time
-- - close_price, close_time, profit, fee
-- - take_profit, stop_loss, liquidation_price, remarks

-- 需要添加的字段 (与Python backtest.py对齐):
-- ALTER TABLE backtest_orders ADD COLUMN margin REAL;  -- 保证金
-- ALTER TABLE backtest_orders ADD COLUMN leverage INTEGER;  -- 杠杆倍数
-- ALTER TABLE backtest_orders ADD COLUMN handling_fee REAL DEFAULT 0;  -- 手续费 (明确)
-- ALTER TABLE backtest_orders ADD COLUMN funding_fee REAL DEFAULT 0;  -- 资金费 (🔥 关键)
-- ALTER TABLE backtest_orders ADD COLUMN surplus REAL DEFAULT 0;  -- 净盈亏 (profit - fees)
-- ALTER TABLE backtest_orders ADD COLUMN closed INTEGER DEFAULT 0;  -- 是否已平仓 (0/1)
-- ALTER TABLE backtest_orders ADD COLUMN symbol TEXT;  -- 交易对

-- 这些ALTER TABLE语句将在C#代码中执行

-- ============================================================================
-- 6. 创建统计视图 (🔥 复用 Python backtest.py 的 backtest_with_stats 设计)
-- ============================================================================
DROP VIEW IF EXISTS backtest_with_stats;
CREATE VIEW backtest_with_stats AS
SELECT 
    br.id,
    br.strategy_id,
    br.strategy_name,
    br.version_string,
    br.symbol,
    br.interval,
    br.start_date,
    br.end_date,
    br.status,
    br.created_at,
    br.completed_at,
    br.duration_seconds,
    
    -- 从 backtest_orders 表实时计算统计指标
    COUNT(bo.id) AS total_orders,
    SUM(CASE WHEN bo.side = 'BUY' THEN 1 ELSE 0 END) AS long_orders,
    SUM(CASE WHEN bo.side = 'SELL' THEN 1 ELSE 0 END) AS short_orders,
    
    -- 胜负统计 (使用profit字段)
    SUM(CASE WHEN bo.profit > 0 THEN 1 ELSE 0 END) AS winning_orders,
    SUM(CASE WHEN bo.profit <= 0 THEN 1 ELSE 0 END) AS losing_orders,
    
    -- 多空分离统计
    SUM(CASE WHEN bo.side = 'BUY' AND bo.profit > 0 THEN 1 ELSE 0 END) AS long_wins,
    SUM(CASE WHEN bo.side = 'BUY' AND bo.profit <= 0 THEN 1 ELSE 0 END) AS long_losses,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.profit > 0 THEN 1 ELSE 0 END) AS short_wins,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.profit <= 0 THEN 1 ELSE 0 END) AS short_losses,
    
    -- 胜率
    ROUND(CAST(SUM(CASE WHEN bo.profit > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(COUNT(bo.id), 0), 4) AS total_win_rate,
    ROUND(CAST(SUM(CASE WHEN bo.side = 'BUY' AND bo.profit > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(SUM(CASE WHEN bo.side = 'BUY' THEN 1 ELSE 0 END), 0), 4) AS long_win_rate,
    ROUND(CAST(SUM(CASE WHEN bo.side = 'SELL' AND bo.profit > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(SUM(CASE WHEN bo.side = 'SELL' THEN 1 ELSE 0 END), 0), 4) AS short_win_rate,
    
    -- 盈亏统计
    SUM(bo.profit) AS total_profit,
    SUM(CASE WHEN bo.side = 'BUY' AND bo.profit > 0 THEN bo.profit ELSE 0 END) AS long_profit_sum,
    SUM(CASE WHEN bo.side = 'BUY' AND bo.profit <= 0 THEN bo.profit ELSE 0 END) AS long_loss_sum,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.profit > 0 THEN bo.profit ELSE 0 END) AS short_profit_sum,
    SUM(CASE WHEN bo.side = 'SELL' AND bo.profit <= 0 THEN bo.profit ELSE 0 END) AS short_loss_sum,
    
    MAX(bo.profit) AS max_profit,
    MIN(bo.profit) AS max_loss,
    
    -- 费用统计 (🔥 关键增强: 资金费统计)
    SUM(bo.fee) AS total_fees,
    COALESCE(SUM(bo.funding_fee), 0) AS total_funding_fees,
    COALESCE(SUM(CASE WHEN bo.side = 'BUY' THEN bo.funding_fee ELSE 0 END), 0) AS long_funding_fees,
    COALESCE(SUM(CASE WHEN bo.side = 'SELL' THEN bo.funding_fee ELSE 0 END), 0) AS short_funding_fees,
    
    -- 盈亏比
    ROUND(AVG(CASE WHEN bo.profit > 0 THEN bo.profit END) / NULLIF(ABS(AVG(CASE WHEN bo.profit <= 0 THEN bo.profit END)), 0), 4) AS avg_pl_ratio,
    
    -- 最终权益 (从 backtest_runs 表读取，或从订单计算)
    COALESCE(br.final_equity, br.initial_capital + SUM(bo.profit)) AS final_equity
    
FROM backtest_runs br
LEFT JOIN backtest_orders bo ON br.id = bo.backtest_id
GROUP BY br.id;

-- ============================================================================
-- 7. 创建权益曲线视图 (简化版，因为现有的equity_curve表已经存储了数据)
-- ============================================================================
-- 说明：现有的backtest_equity_curve表已经存储了权益曲线数据
-- 这里创建一个视图来计算回撤 (与Python的orders_equity对齐)

DROP VIEW IF EXISTS backtest_equity_with_drawdown;
CREATE VIEW backtest_equity_with_drawdown AS
SELECT 
    bec.backtest_id,
    bec.time,
    bec.equity,
    bec.cash,
    bec.position,
    -- 计算累计最大权益 (用于回撤计算)
    (SELECT MAX(equity) FROM backtest_equity_curve 
     WHERE backtest_id = bec.backtest_id AND time <= bec.time) AS running_max_equity,
    -- 计算当前回撤
    ROUND((bec.equity - (SELECT MAX(equity) FROM backtest_equity_curve 
                         WHERE backtest_id = bec.backtest_id AND time <= bec.time)) 
          / NULLIF((SELECT MAX(equity) FROM backtest_equity_curve 
                    WHERE backtest_id = bec.backtest_id AND time <= bec.time), 0), 4) AS drawdown_pct
FROM backtest_equity_curve bec
ORDER BY bec.backtest_id, bec.time;

-- ============================================================================
-- 8. 创建订单详情视图 (🔥 复用 Python backtest.py 的 orders_with_signals 设计)
-- ============================================================================
DROP VIEW IF EXISTS backtest_orders_detail;
CREATE VIEW backtest_orders_detail AS
SELECT 
    o.id,
    o.backtest_id,
    o.symbol,
    o.side,
    o.type,
    o.quantity,
    o.open_price,
    o.open_time,
    o.close_price,
    o.close_time,
    o.profit,
    o.fee,
    COALESCE(o.funding_fee, 0) AS funding_fee,
    COALESCE(o.surplus, o.profit) AS surplus,  -- surplus = profit - fees
    o.closed,
    o.remarks,
    
    -- 开仓信号 (从 backtest_order_signals 关联)
    open_signal.reason AS open_reason,
    COALESCE(open_signal.confidence, 0) AS open_confidence,
    open_signal.action AS open_action,
    
    -- 平仓信号
    close_signal.reason AS close_reason,
    COALESCE(close_signal.confidence, 0) AS close_confidence,
    close_signal.action AS close_action,
    
    -- 持仓时长 (分钟)
    CAST((julianday(o.close_time) - julianday(o.open_time)) * 24 * 60 AS INTEGER) AS holding_minutes,
    
    -- 信号统计
    (SELECT COUNT(*) FROM backtest_order_signals os WHERE os.order_id = o.id) AS total_signals,
    (SELECT COUNT(*) FROM backtest_order_signals os WHERE os.order_id = o.id AND os.action = 'HOLDING') AS holding_signals
    
FROM backtest_orders o
LEFT JOIN backtest_order_signals os_open ON o.id = os_open.order_id AND os_open.action = 'OPEN'
LEFT JOIN backtest_signals open_signal ON os_open.signal_id = CAST(open_signal.id AS TEXT)
LEFT JOIN backtest_order_signals os_close ON o.id = os_close.order_id AND os_close.action = 'CLOSE'
LEFT JOIN backtest_signals close_signal ON os_close.signal_id = CAST(close_signal.id AS TEXT);

-- ============================================================================
-- 9. 创建数据库版本标记表 (用于迁移管理)
-- ============================================================================
CREATE TABLE IF NOT EXISTS backtest_db_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- 插入或更新版本信息
INSERT OR REPLACE INTO backtest_db_meta (key, value, updated_at) VALUES 
    ('schema_version', '2.0', datetime('now', 'localtime')),
    ('migration_applied', 'backtest_local_migration_v2.sql', datetime('now', 'localtime')),
    ('description', 'Prophet 本地回测数据库 V2 - 整合 Python backtest.py 精华设计', datetime('now', 'localtime'));

-- ============================================================================
-- 10. 创建索引 (补充新表的索引)
-- ============================================================================
CREATE INDEX IF NOT EXISTS idx_backtest_runs_version ON backtest_runs(version_id);
CREATE INDEX IF NOT EXISTS idx_backtest_runs_synced ON backtest_runs(synced_to_server);

-- ============================================================================
-- 说明文档
-- ============================================================================
-- 本迁移脚本整合了 Python backtest.py 的精华设计：
-- 
-- ✅ 1. 订单-信号关联 (backtest_order_signals)
--    - 记录 OPEN/CLOSE/HOLDING 三种动作
--    - 清晰追溯每笔订单的触发信号
-- 
-- ✅ 2. K线形态存储 (backtest_patterns)
--    - 独立存储 TA-Lib 识别的形态
--    - 支持按强度排序和多空分类
-- 
-- ✅ 3. 增强的统计视图 (backtest_with_stats)
--    - 实时计算所有指标，无需冗余存储
--    - 多空分离统计
--    - 资金费统计 (🔥 关键)
-- 
-- ✅ 4. 权益曲线视图 (backtest_equity_with_drawdown)
--    - 计算累计盈亏和回撤
-- 
-- ✅ 5. 订单详情视图 (backtest_orders_detail)
--    - 关联开仓/平仓信号
--    - 自动计算持仓时长
-- 
-- ✅ 6. 版本关联 (version_id)
--    - 回测结果与策略版本强绑定
--    - 支持回溯历史版本的表现
-- 
-- ✅ 7. 云端同步标记 (synced_to_server)
--    - 本地存储明细，只上传摘要
-- 
-- 使用方式：
-- 1. 在 LocalBacktestStorage.InitializeDatabase() 中执行本脚本
-- 2. 先执行 ALTER TABLE 语句 (C#代码中处理)
-- 3. 再执行 CREATE TABLE/VIEW 语句
-- 4. 检查 backtest_db_meta 表确认迁移成功
-- ============================================================================

