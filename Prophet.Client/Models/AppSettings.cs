using System;

namespace Prophet.Client.Models;

/// <summary>
/// 应用程序全局设置
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 颜色方案
    /// true: 红涨绿跌（中国习惯）
    /// false: 红跌绿涨（国际习惯）
    /// </summary>
    public bool IsRedRiseGreenFall { get; set; } = false; // 默认：红跌绿涨（国际习惯）

    /// <summary>
    /// 时区ID（如 "Asia/Shanghai", "UTC", "America/New_York" 等）
    /// 如果为空，则使用系统时区
    /// </summary>
    public string? TimeZoneId { get; set; } = null;

    /// <summary>
    /// 日期时间格式
    /// </summary>
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 日期格式
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    /// <summary>
    /// 时间格式
    /// </summary>
    public string TimeFormat { get; set; } = "HH:mm:ss";

    /// <summary>
    /// 数字格式（小数位数）
    /// </summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// 是否显示网格线
    /// </summary>
    public bool ShowGridLines { get; set; } = true;

    /// <summary>
    /// 是否显示十字线
    /// </summary>
    public bool ShowCrosshair { get; set; } = true;

    /// <summary>
    /// 语言设置（预留）
    /// </summary>
    public string Language { get; set; } = "zh-CN";

    // ========== UI配置 ==========
    
    /// <summary>
    /// 自动刷新间隔（毫秒）
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 30000; // 30秒

    /// <summary>
    /// 图表默认显示K线数量
    /// </summary>
    public int ChartDefaultVisibleCandles { get; set; } = 200;

    // ========== 市场数据配置 ==========
    
    /// <summary>
    /// 默认交易对
    /// </summary>
    public string DefaultSymbol { get; set; } = "BTCUSDT";

    /// <summary>
    /// 默认K线数量（1m K线）
    /// </summary>
    public int DefaultKlineLimit { get; set; } = 1440;

    // ========== 数据采集配置 ==========
    
    /// <summary>
    /// 是否启用代理（用于访问被限制的API）
    /// </summary>
    public bool EnableProxy { get; set; } = false;

    /// <summary>
    /// 代理服务器地址（例如：http://127.0.0.1:7890）
    /// 如果为空，则使用系统代理设置
    /// </summary>
    public string? ProxyAddress { get; set; } = null;

    /// <summary>
    /// 代理用户名（如果需要认证）
    /// </summary>
    public string? ProxyUsername { get; set; } = null;

    /// <summary>
    /// 代理密码（如果需要认证，存储时建议加密）
    /// </summary>
    public string? ProxyPassword { get; set; } = null;

    /// <summary>
    /// 是否跳过SSL证书验证（仅用于调试，生产环境应设为false）
    /// </summary>
    public bool SkipSslCertificateValidation { get; set; } = false;

    // ========== AI 配置 ==========

    /// <summary>
    /// 当前使用的 AI Provider (DeepSeek, OpenAI, Claude等)
    /// </summary>
    public string CurrentAiProvider { get; set; } = "DeepSeek";

    /// <summary>
    /// DeepSeek API 密钥
    /// </summary>
    public string DeepSeekApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI API 密钥
    /// </summary>
    public string OpenAiApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 通义千问 API 密钥
    /// </summary>
    public string QwenApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// ChatGLM API 密钥
    /// </summary>
    public string ChatGLMApiKey { get; set; } = string.Empty;

    /// <summary>
    /// AI API 基础地址
    /// </summary>
    public string AiApiBaseUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>
    /// AI 模型名称
    /// </summary>
    public string AiModel { get; set; } = "deepseek-chat";

    /// <summary>
    /// 温度参数（0-1，越高越随机）
    /// </summary>
    public double AiTemperature { get; set; } = 0.7;

    /// <summary>
    /// 最大 Token 数
    /// </summary>
    public int AiMaxTokens { get; set; } = 4096;

    /// <summary>
    /// 启用流式响应
    /// </summary>
    public bool AiEnableStreaming { get; set; } = true;

    // ========== 交易所配置 ==========

    /// <summary>
    /// 默认行情数据交易所（Data Plane 路由）
    /// 例如：Binance / Okx / Bybit
    /// </summary>
    public string DefaultMarketDataExchange { get; set; } = "Binance";

    /// <summary>
    /// 默认实盘交易交易所（Data Plane 路由）
    /// 例如：Binance / Okx / Bybit
    /// </summary>
    public string DefaultTradingExchange { get; set; } = "Binance";

    /// <summary>
    /// 币安API密钥
    /// </summary>
    public string BinanceApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 币安API密钥Secret
    /// </summary>
    public string BinanceApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用币安测试网
    /// </summary>
    public bool UseBinanceTestnet { get; set; } = true;

    /// <summary>
    /// 默认交易对
    /// </summary>
    public string DefaultTradingSymbol { get; set; } = "BTCUSDT";

    /// <summary>
    /// 默认杠杆倍数
    /// </summary>
    public int DefaultLeverage { get; set; } = 10;

    /// <summary>
    /// 获取涨的颜色（根据颜色方案）
    /// </summary>
    public string GetRisingColor()
    {
        return IsRedRiseGreenFall ? "#EF5350" : "#26A69A"; // 红涨绿跌：红色，红跌绿涨：绿色
    }

    /// <summary>
    /// 获取跌的颜色（根据颜色方案）
    /// </summary>
    public string GetFallingColor()
    {
        return IsRedRiseGreenFall ? "#26A69A" : "#EF5350"; // 红涨绿跌：绿色，红跌绿涨：红色
    }

    /// <summary>
    /// 获取时区信息
    /// </summary>
    public TimeZoneInfo GetTimeZone()
    {
        if (!string.IsNullOrEmpty(TimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            }
            catch
            {
                // 如果时区ID无效，返回系统时区
                return TimeZoneInfo.Local;
            }
        }
        return TimeZoneInfo.Local;
    }
}
