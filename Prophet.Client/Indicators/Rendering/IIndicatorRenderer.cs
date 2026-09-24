using Prophet.Client.Indicators.Core;

namespace Prophet.Client.Indicators.Rendering;

/// <summary>
/// 指标渲染器接口
/// </summary>
public interface IIndicatorRenderer
{
    /// <summary>
    /// 渲染指标
    /// </summary>
    /// <param name="indicator">指标对象</param>
    /// <param name="context">渲染上下文</param>
    void Render(IIndicator indicator, IndicatorRenderContext context);
}

