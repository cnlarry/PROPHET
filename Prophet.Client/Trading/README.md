# Prophet 实盘交易模块

## 概述

实盘交易模块为Prophet量化交易平台提供了与多个交易所对接的统一接口，目前已实现币安U本位合约交易支持。

## 架构设计

```
Trading/
├── Exchanges/                     # 交易所抽象层
│   ├── IExchange.cs              # 统一交易所接口
│   ├── ExchangeStatus.cs         # 交易所状态枚举
│   └── Binance/                  # 币安实现
│       ├── BinanceExchange.cs    # 币安适配器
│       ├── BinanceRestClient.cs  # REST API客户端
│       ├── BinanceWebSocketClient.cs  # WebSocket客户端
│       ├── BinanceAuthHelper.cs  # 签名认证工具
│       └── Models/               # 币安数据模型
├── Models/                       # 通用数据模型
│   ├── ExchangeConfig.cs         # 交易所配置
│   ├── AccountInfo.cs            # 账户信息
│   ├── Position.cs               # 持仓信息
│   ├── OrderRequest.cs           # 订单请求/响应
│   ├── OrderBook.cs              # 订单簿
│   └── Ticker.cs                 # 行情信息
└── Examples/                     # 使用示例
    └── ExchangeUsageExample.cs   # 示例代码
```

## 核心特性

### 1. 统一接口设计

所有交易所实现相同的 `IExchange` 接口，确保切换交易所时无需修改业务代码。

### 2. 完整的功能支持

- ✅ 账户信息查询
- ✅ 市场数据获取（K线、订单簿、Ticker）
- ✅ WebSocket实时数据流
- ✅ 订单管理（市价单、限价单）
- ✅ 止盈止损设置
- ✅ 杠杆设置
- ✅ 事件通知系统

### 3. 安全的认证机制

- HMAC SHA256签名
- API密钥加密存储
- 测试网和主网分离

### 4. 可靠的连接管理

- WebSocket自动重连
- 断线检测和恢复
- 指数退避重试策略

## 快速开始

### 1. 配置API密钥

在设置界面配置币安API密钥：

1. 打开 `设置` 页面
2. 找到 `币安交易所配置` 卡片
3. 输入API Key和API Secret
4. 勾选"使用测试网"（推荐先测试）
5. 点击"测试连接"验证配置
6. 点击"保存设置"

### 2. 代码示例

#### 基础使用

```csharp
using var exchange = new BinanceExchange();

var config = new ExchangeConfig
{
    ApiKey = "your_api_key",
    ApiSecret = "your_api_secret",
    UseTestnet = true
};

await exchange.InitializeAsync(config);

// 获取账户信息
var account = await exchange.GetAccountInfoAsync();
Console.WriteLine($"余额: {account.TotalBalance} USDT");
```

#### 下单交易

```csharp
// 市价买入
var order = new MarketOrderRequest
{
    Symbol = "BTCUSDT",
    Side = OrderSide.BUY,
    Quantity = 0.001m
};

var result = await exchange.PlaceMarketOrderAsync(order);
if (result.Success)
{
    Console.WriteLine($"成交价: {result.FilledPrice}");
}
```

#### 实时数据订阅

```csharp
await foreach (var candle in exchange.SubscribeCandlesAsync("BTCUSDT", "1m"))
{
    Console.WriteLine($"新K线: {candle.Close}");
}
```

## API密钥获取

### 币安测试网

1. 访问 https://testnet.binancefuture.com/
2. 登录账号（使用GitHub等）
3. 生成API密钥

### 币安主网

1. 访问 https://www.binance.com/
2. 登录账号
3. 进入API管理
4. 创建API密钥
5. **重要**: 仅勾选"读取"和"合约交易"权限，不要勾选"提现"

## 安全建议

1. ⚠️ **永远不要在代码中硬编码API密钥**
2. ✅ 使用AppSettings存储加密的API密钥
3. ✅ 先在测试网充分测试
4. ✅ 主网API密钥限制IP白名单
5. ✅ 定期更换API密钥
6. ✅ 最小权限原则（不要勾选提现权限）

## 支持的交易所

### 已实现

- ✅ 币安合约 (Binance Futures)

### 计划支持

- ⏳ OKX
- ⏳ Bybit
- ⏳ Gate.io

## 扩展新交易所

实现 `IExchange` 接口即可添加新交易所支持：

```csharp
public class NewExchange : IExchange
{
    public string Name => "NewExchange";
    
    public async Task<bool> InitializeAsync(ExchangeConfig config)
    {
        // 实现初始化逻辑
    }
    
    // 实现其他接口方法...
}
```

## 事件系统

交易所支持以下事件：

```csharp
exchange.OrderUpdated += (sender, args) => 
{
    Console.WriteLine($"订单更新: {args.Order.OrderId}");
};

exchange.PositionUpdated += (sender, args) =>
{
    Console.WriteLine($"仓位更新: {args.Position.Symbol}");
};

exchange.ErrorOccurred += (sender, args) =>
{
    Console.WriteLine($"错误: {args.GetException().Message}");
};
```

## 故障排查

### 连接失败

1. 检查API密钥是否正确
2. 确认网络连接正常
3. 检查是否使用了正确的测试网/主网配置
4. 查看Console日志获取详细错误信息

### 签名错误

1. 确认API Secret没有多余空格
2. 检查系统时间是否准确（币安要求时间误差<5秒）
3. 确认API密钥权限配置正确

### WebSocket断开

- 模块已实现自动重连机制
- 最多重试10次
- 使用指数退避策略

## 性能优化

1. **连接复用**: 复用HttpClient和WebSocket连接
2. **批量请求**: 尽可能批量查询数据
3. **缓存机制**: 缓存不常变化的数据
4. **异步操作**: 所有IO操作使用async/await

## 测试

运行测试前请确保：

1. 已配置测试网API密钥
2. 测试网账户有足够余额
3. 网络连接正常

## 版本历史

### v1.0.0 (2025-12-05)

- ✅ 实现IExchange统一接口
- ✅ 完成币安REST API集成
- ✅ 完成币安WebSocket集成
- ✅ 实现自动重连机制
- ✅ 集成到设置界面
- ✅ 添加使用示例

## 贡献指南

欢迎提交PR添加新交易所支持或改进现有功能！

## 许可证

本模块遵循Prophet项目的整体许可协议。

