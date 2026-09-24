using System.Collections.Generic;

namespace Prophet.Client.Models;

/// <summary>
/// 策略模板数据模型
/// </summary>
public class StrategyTemplate
{
    /// <summary>
    /// 模板唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板分类（trend, momentum, mean_revert, smc, volume, volatility等）
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 模板描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// DSL代码内容
    /// </summary>
    public string DslCode { get; set; } = string.Empty;

    /// <summary>
    /// 默认交易对（可选）
    /// </summary>
    public string? DefaultSymbol { get; set; }

    /// <summary>
    /// 标签列表
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 图标（emoji或路径）
    /// </summary>
    public string Icon { get; set; } = "📊";

    /// <summary>
    /// 评分（1-5星）
    /// </summary>
    public int Rating { get; set; } = 3;

    /// <summary>
    /// 是否内置模板
    /// </summary>
    public bool IsBuiltIn { get; set; } = true;

    /// <summary>
    /// 作者
    /// </summary>
    public string Author { get; set; } = "Prophet System";

    /// <summary>
    /// 适用时间周期
    /// </summary>
    public string? SuggestedTimeframe { get; set; }

    /// <summary>
    /// 模板说明（使用提示）
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// 显示用的分类名称（中文）
    /// </summary>
    public string CategoryDisplayName => Category switch
    {
        "trend" => "趋势跟踪",
        "momentum" => "动量突破",
        "mean_revert" => "均值回归",
        "smc" => "SMC策略",
        "volume" => "成交量",
        "volatility" => "波动率",
        "grid" => "网格交易",
        "arbitrage" => "套利",
        _ => "其他"
    };
}

