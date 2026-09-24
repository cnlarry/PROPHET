using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// MACD指标配置
/// </summary>
public class MACDIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Custom; // 组合渲染
    
    /// <summary>
    /// 快线周期
    /// </summary>
    public int FastPeriod { get; set; } = 12;
    
    /// <summary>
    /// 慢线周期
    /// </summary>
    public int SlowPeriod { get; set; } = 26;
    
    /// <summary>
    /// 信号线周期
    /// </summary>
    public int SignalPeriod { get; set; } = 9;
    
    /// <summary>
    /// 柱状图正值颜色
    /// </summary>
    public string HistogramPositiveColor { get; set; } = "#26A69A"; // 涨：绿色（与主图K线一致）
    
    /// <summary>
    /// 柱状图负值颜色
    /// </summary>
    public string HistogramNegativeColor { get; set; } = "#EF5350"; // 跌：红色（与主图K线一致）
    
    /// <summary>
    /// 柱状图透明度
    /// </summary>
    public double HistogramOpacity { get; set; } = 0.6;
    
    public MACDIndicatorConfig()
    {
        UseSubChart = true; // 副图
        SubChartHeightRatio = 0.25;
        FixedYRange = null; // 自动范围
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "macd", Label = "MACD", Color = "#FFFFFF", Thickness = 1.5 },
            new() { IsEnabled = true, Key = "signal", Label = "Signal", Color = "#FFD700", Thickness = 1.5 }
        };
    }
}

/// <summary>
/// MACD指标
/// </summary>
public class MACDIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _lineRenderer = new();
    private readonly BarRenderer _barRenderer = new();
    
    public MACDIndicator(MACDIndicatorConfig config)
        : base("MACD", "MACD", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as MACDIndicatorConfig;
        if (config == null) return;
        
        // 先渲染柱状图（histogram）
        RenderHistogram(config, context);
        
        // 再渲染MACD和Signal线
        _lineRenderer.Render(this, context);
    }
    
    /// <summary>
    /// 渲染MACD柱状图
    /// </summary>
    private void RenderHistogram(MACDIndicatorConfig config, IndicatorRenderContext context)
    {
        // 创建临时柱状图配置
        var barConfig = new BarIndicatorConfig
        {
            ValueKey = "histogram",
            PositiveColor = config.HistogramPositiveColor,
            NegativeColor = config.HistogramNegativeColor,
            Opacity = config.HistogramOpacity,
            BarWidthRatio = 0.6
        };
        
        // 创建临时指标用于渲染柱状
        var tempIndicator = new TempBarIndicator(barConfig, this.Data);
        _barRenderer.Render(tempIndicator, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as MACDIndicatorConfig;
        if (config != null)
        {
            return $"{Name} ({config.FastPeriod}, {config.SlowPeriod}, {config.SignalPeriod})";
        }
        return base.GetDescription();
    }
    
    /// <summary>
    /// 临时柱状指标（仅用于渲染）
    /// </summary>
    private class TempBarIndicator : IIndicator
    {
        public string Id => "MACD_Histogram";
        public string Name => "MACD Histogram";
        public IndicatorType Type => IndicatorType.Momentum;
        public IndicatorLocation Location => IndicatorLocation.SubChart;
        public bool IsEnabled { get; set; } = true;
        public IIndicatorConfig Config { get; }
        public Data.IndicatorDataSeries Data { get; set; }
        
        public TempBarIndicator(BarIndicatorConfig config, Data.IndicatorDataSeries data)
        {
            Config = config;
            Data = data;
        }
        
        public void Render(IndicatorRenderContext context) { }
        public Dictionary<string, double> GetCurrentValues(int dataIndex) => new();
        public (double min, double max) GetYRange(int startIndex, int count) => (0, 0);
        public void ClearData() { }
        public string GetDescription() => "";
    }
}

