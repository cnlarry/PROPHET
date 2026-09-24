-- ==========================================
-- Prophet 回测订单表结构（SQLite版本）
-- 基于 MySQL 的 orders.sql 转换而来
-- 符合币安合约交易标准
-- ==========================================

-- 删除旧表（如果存在）
DROP TABLE IF EXISTS backtest_orders;

-- 创建订单记录表
CREATE TABLE backtest_orders (
    -- 基础信息
    id TEXT PRIMARY KEY,                           -- 订单ID
    backtest_id TEXT NOT NULL,                     -- 回测批次ID
    
    -- 交易信息
    side TEXT NOT NULL,                            -- 方向(BUY/SELL)
    type TEXT NOT NULL,                            -- 订单类型(MARKET/LIMIT)
    status TEXT NOT NULL,                          -- 订单状态(Filled/Closed/Cancelled)
    
    -- 杠杆和保证金（币安合约标准）
    margin REAL NOT NULL DEFAULT 0,                -- 保证金（quantity * price / leverage + fee）
    leverage INTEGER NOT NULL DEFAULT 10,          -- 杠杆倍数
    
    -- 仓位信息
    quantity REAL NOT NULL,                        -- 下单数量
    
    -- 价格信息
    open_price REAL NOT NULL,                      -- 开仓价
    open_time TEXT NOT NULL,                       -- 开仓时间
    filled_price REAL,                             -- 实际成交价（含滑点）
    filled_time TEXT,                              -- 成交时间
    close_price REAL,                              -- 平仓价
    close_time TEXT,                               -- 平仓时间
    
    -- 费用信息
    fee REAL NOT NULL DEFAULT 0,                   -- 手续费（开仓+平仓）
    funding_fee REAL NOT NULL DEFAULT 0,           -- 资金费用
    
    -- 盈亏信息
    profit REAL,                                   -- 净盈亏（已扣除手续费和资金费）
    
    -- 止盈止损（基于保证金比例计算）
    take_profit REAL,                              -- 止盈价格
    stop_loss REAL,                                -- 止损价格
    
    -- 风险控制
    liquidation_price REAL,                        -- 强平价（币安标准计算）
    
    -- 备注
    remarks TEXT,                                  -- 备注信息
    
    -- 外键约束
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

-- ==========================================
-- 索引优化
-- ==========================================

-- 回测ID索引（最常用）
CREATE INDEX idx_backtest_orders_backtest_id ON backtest_orders(backtest_id);

-- 订单状态索引
CREATE INDEX idx_backtest_orders_status ON backtest_orders(status);

-- 开仓时间索引
CREATE INDEX idx_backtest_orders_open_time ON backtest_orders(open_time);

-- 平仓时间索引
CREATE INDEX idx_backtest_orders_close_time ON backtest_orders(close_time);

-- 组合索引：回测ID + 状态
CREATE INDEX idx_backtest_orders_backtest_status ON backtest_orders(backtest_id, status);

-- 组合索引：回测ID + 方向 + 状态
CREATE INDEX idx_backtest_orders_backtest_side_status ON backtest_orders(backtest_id, side, status);

-- ==========================================
-- 字段说明和币安标准计算公式
-- ==========================================

/*
【保证金计算】（基于币安合约标准）
margin = (quantity × open_price / leverage) + opening_fee

示例（10倍杠杆）：
- 开仓数量：1 BTC
- 开仓价：50000 USDT
- 杠杆：10倍
- 手续费率：0.04%
- 保证金 = (1 × 50000 / 10) + (1 × 50000 × 0.0004) = 5000 + 20 = 5020 USDT

【强平价计算】（基于币安合约标准）
多单强平价 = open_price - margin
空单强平价 = open_price + margin

示例（多单，10倍杠杆）：
- 开仓价：50000 USDT
- 保证金：5020 USDT
- 强平价 = 50000 - 5020 = 44980 USDT
- 含义：价格跌到44980时，亏损达到保证金金额，触发强平

【止盈止损计算】（基于保证金比例）
多单止盈 = open_price + (open_price / leverage × take_profit_percent)
多单止损 = open_price - (open_price / leverage × stop_loss_percent)

空单止盈 = open_price - (open_price / leverage × take_profit_percent)
空单止损 = open_price + (open_price / leverage × stop_loss_percent)

示例（多单，10倍杠杆，止盈40%，止损20%）：
- 开仓价：50000 USDT
- 止盈价 = 50000 + (50000 / 10 × 0.4) = 50000 + 2000 = 52000 USDT
- 止损价 = 50000 - (50000 / 10 × 0.2) = 50000 - 1000 = 49000 USDT
- 止盈盈利：(52000 - 50000) × 1 = 2000 USDT（40%保证金）
- 止损亏损：(50000 - 49000) × 1 = 1000 USDT（20%保证金）

【资金费用计算】
funding_fee = position_value × funding_rate × periods
- 币安每8小时收取一次资金费（00:00, 08:00, 16:00 UTC）
- 多单支付正费率，收取负费率
- 空单收取正费率，支付负费率

【净盈亏计算】
多单：profit = (close_price - open_price) × quantity - total_fee - funding_fee
空单：profit = (open_price - close_price) × quantity - total_fee - funding_fee

其中：total_fee = opening_fee + closing_fee
*/

-- ==========================================
-- 数据迁移说明
-- ==========================================

/*
如果从旧版本升级，需要执行以下步骤：

1. 备份旧数据
   CREATE TABLE backtest_orders_backup AS SELECT * FROM backtest_orders;

2. 删除旧表
   DROP TABLE backtest_orders;

3. 执行本脚本创建新表

4. 迁移数据（根据实际情况调整）
   INSERT INTO backtest_orders (
       id, backtest_id, side, type, status, quantity,
       open_price, open_time, filled_price, filled_time,
       close_price, close_time, fee, profit,
       take_profit, stop_loss, liquidation_price, remarks
   )
   SELECT 
       id, backtest_id, side, type, status, quantity,
       open_price, open_time, filled_price, filled_time,
       close_price, close_time, fee, profit,
       take_profit, stop_loss, liquidation_price, remarks
   FROM backtest_orders_backup;
   
   -- 更新缺失的字段（使用默认值或计算）
   UPDATE backtest_orders
   SET 
       leverage = 10,  -- 默认10倍杠杆
       margin = quantity * open_price / 10,  -- 估算保证金（需要根据实际情况调整）
       funding_fee = 0  -- 旧数据没有资金费用
   WHERE margin = 0;

5. 验证数据完整性
   SELECT COUNT(*) FROM backtest_orders;
   SELECT COUNT(*) FROM backtest_orders_backup;
*/

