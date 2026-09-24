using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Management;

/// <summary>
/// 指标管理器（统一管理所有指标）
/// </summary>
public class IndicatorManager
{
    /// <summary>
    /// 所有注册的指标（键：指标ID）
    /// </summary>
    private readonly Dictionary<string, IIndicator> _indicators = new();
    
    /// <summary>
    /// 渲染器注册表（键：渲染类型）
    /// </summary>
    private readonly Dictionary<RenderType, IIndicatorRenderer> _renderers = new();
    
    /// <summary>
    /// 构造函数（注册默认渲染器）
    /// </summary>
    public IndicatorManager()
    {
        RegisterDefaultRenderers();
    }
    
    /// <summary>
    /// 注册默认渲染器
    /// </summary>
    private void RegisterDefaultRenderers()
    {
        RegisterRenderer(RenderType.Line, new LineRenderer());
        RegisterRenderer(RenderType.Band, new BandRenderer());
        RegisterRenderer(RenderType.Bar, new BarRenderer());
        // RegisterRenderer(RenderType.Dot, new DotRenderer()); // 待实现
    }
    
    /// <summary>
    /// 注册指标
    /// </summary>
    /// <param name="indicator">指标对象</param>
    public void RegisterIndicator(IIndicator indicator)
    {
        if (indicator == null)
            throw new ArgumentNullException(nameof(indicator));
        
        _indicators[indicator.Id] = indicator;
    }
    
    /// <summary>
    /// 注册多个指标
    /// </summary>
    public void RegisterIndicators(params IIndicator[] indicators)
    {
        foreach (var indicator in indicators)
        {
            RegisterIndicator(indicator);
        }
    }
    
    /// <summary>
    /// 注销指标
    /// </summary>
    /// <param name="indicatorId">指标ID</param>
    /// <returns>是否成功注销</returns>
    public bool UnregisterIndicator(string indicatorId)
    {
        return _indicators.Remove(indicatorId);
    }
    
    /// <summary>
    /// 获取指标
    /// </summary>
    /// <param name="indicatorId">指标ID</param>
    /// <returns>指标对象，不存在则返回null</returns>
    public IIndicator? GetIndicator(string indicatorId)
    {
        return _indicators.GetValueOrDefault(indicatorId);
    }
    
    /// <summary>
    /// 获取所有指标
    /// </summary>
    public IEnumerable<IIndicator> GetAllIndicators()
    {
        return _indicators.Values;
    }
    
    /// <summary>
    /// 获取所有主图指标（已启用）
    /// </summary>
    public IEnumerable<IIndicator> GetMainChartIndicators()
    {
        return _indicators.Values
            .Where(i => i.Location == IndicatorLocation.MainChart && i.IsEnabled)
            .OrderBy(i => i.Type); // 按类型排序
    }
    
    /// <summary>
    /// 获取所有副图指标（已启用）
    /// </summary>
    public IEnumerable<IIndicator> GetSubChartIndicators()
    {
        return _indicators.Values
            .Where(i => i.Location == IndicatorLocation.SubChart && i.IsEnabled)
            .OrderBy(i => i.Type);
    }
    
    /// <summary>
    /// 渲染所有主图指标
    /// </summary>
    /// <param name="context">渲染上下文</param>
    public void RenderMainChartIndicators(IndicatorRenderContext context)
    {
        foreach (var indicator in GetMainChartIndicators())
        {
            RenderIndicator(indicator, context);
        }
    }
    
    /// <summary>
    /// 渲染所有副图指标
    /// </summary>
    /// <param name="contexts">渲染上下文列表（每个副图一个上下文）</param>
    public void RenderSubChartIndicators(List<IndicatorRenderContext> contexts)
    {
        var subChartIndicators = GetSubChartIndicators().ToList();
        
        for (int i = 0; i < Math.Min(subChartIndicators.Count, contexts.Count); i++)
        {
            RenderIndicator(subChartIndicators[i], contexts[i]);
        }
    }
    
    /// <summary>
    /// 渲染单个指标
    /// </summary>
    /// <param name="indicator">指标对象</param>
    /// <param name="context">渲染上下文</param>
    public void RenderIndicator(IIndicator indicator, IndicatorRenderContext context)
    {
        if (!indicator.IsEnabled || indicator.Data.Count == 0)
            return;
        
        try
        {
            // 如果配置了固定Y轴范围，使用固定值
            if (indicator.Config.FixedYRange != null)
            {
                (context.MinValue, context.MaxValue) = indicator.Config.FixedYRange.Value;
            }
            else
            {
                // 否则自动计算Y轴范围
                var (min, max) = indicator.GetYRange(context.StartIndex, context.VisibleCount);
                context.MinValue = min;
                context.MaxValue = max;
            }
            
            // 获取对应的渲染器
            var renderer = _renderers.GetValueOrDefault(indicator.Config.RenderType);
            
            if (renderer != null)
            {
                // 使用注册的渲染器
                renderer.Render(indicator, context);
            }
            else
            {
                // 使用指标自定义渲染方法
                indicator.Render(context);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"指标渲染失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 注册渲染器
    /// </summary>
    /// <param name="type">渲染类型</param>
    /// <param name="renderer">渲染器对象</param>
    public void RegisterRenderer(RenderType type, IIndicatorRenderer renderer)
    {
        _renderers[type] = renderer;
    }
    
    /// <summary>
    /// 清空所有指标
    /// </summary>
    public void Clear()
    {
        _indicators.Clear();
    }
    
    /// <summary>
    /// 清空所有指标数据
    /// </summary>
    public void ClearAllData()
    {
        foreach (var indicator in _indicators.Values)
        {
            indicator.ClearData();
        }
    }
    
    /// <summary>
    /// 启用/禁用指标
    /// </summary>
    public void SetIndicatorEnabled(string indicatorId, bool enabled)
    {
        var indicator = GetIndicator(indicatorId);
        if (indicator != null)
        {
            indicator.IsEnabled = enabled;
        }
    }
    
    /// <summary>
    /// 获取统计信息
    /// </summary>
    public string GetStatistics()
    {
        var total = _indicators.Count;
        var enabled = _indicators.Values.Count(i => i.IsEnabled);
        var mainChart = _indicators.Values.Count(i => i.Location == IndicatorLocation.MainChart);
        var subChart = _indicators.Values.Count(i => i.Location == IndicatorLocation.SubChart);
        
        return $"总计: {total}, 启用: {enabled}, 主图: {mainChart}, 副图: {subChart}";
    }
}

