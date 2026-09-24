using Prophet.Client.Models;

namespace Prophet.Client.Services;

/// <summary>
/// 策略编辑器宿主接口（用于解耦 AiOperationService 与具体 View，实现可测试/可替换）
/// </summary>
public interface IStrategyEditorHost
{
    /// <summary>
    /// 获取当前策略与当前编辑器 DSL 内容
    /// </summary>
    (StrategyInfo? strategy, string? dsl) GetCurrentStrategyInfo();

    /// <summary>
    /// 替换当前编辑器代码
    /// </summary>
    void ReplaceCurrentCode(string newCode);

    /// <summary>
    /// 打开/创建一个策略编辑 Tab
    /// </summary>
    void OpenEditorTab(StrategyInfo strategy, string? remarks = null);
}


