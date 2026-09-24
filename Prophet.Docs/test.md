# 测试页面

这是一个测试页面，用于验证 Docsify 配置是否正确。

## 链接测试

- [首页](/)
- [Prophet DSL 函数](/doc/Prophet%20DSL%20规范/Prophet%20DSL%20函数.md)

## 代码示例

```javascript
// Prophet DSL 示例
IF KLINE(5m).close() > KLINE(5m).open() AND $(5m).RSI().value < 30 = BUY
```

## 表格示例

| 函数 | 描述 | 用法 |
|------|------|------|
| KLINE | 获取K线数据 | `KLINE(5m).close()` |
| RSI | 相对强弱指数 | `$(5m).RSI().value` |