using System;
using AvaloniaEdit;
using AvaloniaEdit.Folding;

namespace Prophet.Client.Services.Editor.Folding;

/// <summary>
/// 代码折叠管理器
/// </summary>
/// <remarks>
/// <para>
/// 负责编辑器的代码折叠功能。支持基于缩进的智能折叠。
/// </para>
/// <para>
/// 折叠规则：
/// <list type="bullet">
/// <item><description>信号定义块：SIGNAL ... END</description></item>
/// <item><description>参数定义块：PARAM ... END</description></item>
/// <item><description>变量定义块：VAR ... END</description></item>
/// <item><description>多行表达式：基于缩进自动识别</description></item>
/// </list>
/// </para>
/// <para>
/// 触发方式：
/// <list type="bullet">
/// <item><description>自动：文本变化后自动更新折叠区域</description></item>
/// <item><description>延迟：500ms 防抖，避免频繁更新</description></item>
/// </list>
/// </para>
/// </remarks>
public class FoldingManager
{
    private readonly TextEditor _editor;
    private AvaloniaEdit.Folding.FoldingManager? _foldingManager;
    private DSLFoldingStrategy? _foldingStrategy;

    /// <summary>
    /// 初始化 <see cref="FoldingManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    public FoldingManager(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// 初始化代码折叠功能
    /// </summary>
    /// <remarks>
    /// <para>
    /// 创建 AvaloniaEdit 的 FoldingManager 和 DSL 折叠策略。
    /// 监听文本变化事件，延迟 500ms 后更新折叠区域（防抖）。
    /// </para>
    /// </remarks>
    public void Initialize()
    {
        _foldingManager = AvaloniaEdit.Folding.FoldingManager.Install(_editor.TextArea);
        _foldingStrategy = new DSLFoldingStrategy();

        // 初始更新
        UpdateFoldings();

        // 文本变化时更新折叠
        _editor.TextChanged += (s, e) =>
        {
            System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => UpdateFoldings());
            });
        };
    }

    /// <summary>
    /// 更新折叠信息
    /// </summary>
    public void UpdateFoldings()
    {
        if (_foldingManager != null && _foldingStrategy != null)
        {
            _foldingStrategy.UpdateFoldings(_foldingManager, _editor.Document);
        }
    }
}

