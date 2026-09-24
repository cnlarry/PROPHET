using System;
using System.Collections.Generic;
using AvaloniaEdit;
using Prophet.Client.Core;
using Prophet.Client.Services.Editor;
using Prophet.Client.Services.Editor.Completion;
using Prophet.Client.Services.Editor.Validation;
using Prophet.Client.Services.Editor.Hover;
using Prophet.Client.Services.Editor.Parameter;
using Prophet.Client.Services.Editor.Menu;
using Prophet.Client.Services.Editor.Formatting;
using Prophet.Client.Services.Editor.QuickFix;
using Prophet.Client.Services.Editor.Folding;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services;

/// <summary>
/// 代码编辑器助手类（版本 2.0 - 基于统一规则系统）
/// </summary>
/// <remarks>
/// <para>
/// 这是 Prophet DSL 代码编辑器的核心协调类，集成了语法高亮、自动补全、
/// 语法验证、悬停提示、参数提示、代码折叠等多种编辑器功能。
/// </para>
/// <para>
/// 重构后的版本采用职责分离设计，将各项功能委托给专门的管理器类：
/// <list type="bullet">
/// <item><description><see cref="SyntaxHighlightingManager"/>: 语法高亮</description></item>
/// <item><description><see cref="CompletionManager"/>: 代码补全</description></item>
/// <item><description><see cref="ValidationManager"/>: 语法验证和错误标记</description></item>
/// <item><description><see cref="HoverTooltipManager"/>: 悬停提示</description></item>
/// <item><description><see cref="ParameterHintManager"/>: 参数提示</description></item>
/// <item><description><see cref="ContextMenuManager"/>: 右键菜单</description></item>
/// <item><description><see cref="FormattingManager"/>: 代码格式化</description></item>
/// <item><description><see cref="QuickFixManager"/>: 快速修复</description></item>
/// <item><description><see cref="FoldingManager"/>: 代码折叠</description></item>
/// </list>
/// </para>
/// <para>
/// 使用方式：
/// <code>
/// var editor = new TextEditor();
/// var helper = new CodeEditorHelper(editor);
/// // 所有功能已自动初始化完成
/// </code>
/// </para>
/// </remarks>
public class CodeEditorHelper : IDisposable
{
    private readonly TextEditor _editor;

    // 功能管理器
    private readonly SyntaxHighlightingManager _syntaxManager;
    private readonly CompletionManager _completionManager;
    private readonly ValidationManager _validationManager;
    private readonly HoverTooltipManager _hoverManager;
    private readonly ParameterHintManager _parameterManager;
    private readonly ContextMenuManager _contextMenuManager;
    private readonly FormattingManager _formattingManager;
    private readonly QuickFixManager _quickFixManager;
    private readonly FoldingManager _foldingManager;

    private bool _disposed = false;

    /// <summary>
    /// 初始化 <see cref="CodeEditorHelper"/> 类的新实例
    /// </summary>
    /// <param name="editor">要绑定功能的 <see cref="TextEditor"/> 实例</param>
    /// <remarks>
    /// 构造函数会自动：
    /// <list type="number">
    /// <item><description>获取必要的服务实例（从DI容器）</description></item>
    /// <item><description>创建所有功能管理器</description></item>
    /// <item><description>初始化所有功能（注册事件、加载资源等）</description></item>
    /// </list>
    /// 初始化完成后，编辑器即可使用所有功能。
    /// </remarks>
    public CodeEditorHelper(TextEditor editor)
    {
        _editor = editor;

        // 创建各个功能管理器（从DI容器获取服务）
        var intelliSense = ServiceContainer.GetService<IntelliSenseService>();
        var validator = ServiceContainer.GetService<DSLValidator>();
        var usageTracker = ServiceContainer.GetService<CompletionUsageTracker>();
        var rulesService = ServiceContainer.GetService<DSLRulesService>();

        _syntaxManager = new SyntaxHighlightingManager(editor);
        _completionManager = new CompletionManager(editor, intelliSense, usageTracker, rulesService);
        _validationManager = new ValidationManager(editor, validator);
        _hoverManager = new HoverTooltipManager(editor, intelliSense, rulesService, _validationManager);
        _parameterManager = new ParameterHintManager(editor, intelliSense, rulesService);
        _contextMenuManager = new ContextMenuManager(editor);
        _formattingManager = new FormattingManager(editor);
        _quickFixManager = new QuickFixManager(editor);
        _foldingManager = new FoldingManager(editor);

        // 初始化所有功能
        InitializeAll();
    }

    /// <summary>
    /// 初始化所有功能
    /// </summary>
    private void InitializeAll()
    {
        _syntaxManager.Initialize();
        _completionManager.Initialize();
        _validationManager.Initialize();
        _hoverManager.Initialize();
        _parameterManager.Initialize();
        _contextMenuManager.Initialize();
        _foldingManager.Initialize();
    }

    #region 公共API

    /// <summary>
    /// 手动触发代码片段选择窗口
    /// </summary>
    /// <remarks>
    /// 用于菜单按钮或快捷键触发。显示包含代码片段的补全窗口。
    /// </remarks>
    public void ShowSnippetPicker()
    {
        _completionManager.ShowSnippetPicker();
    }

    /// <summary>
    /// 验证代码并显示错误标记
    /// </summary>
    /// <remarks>
    /// 执行语法验证和语义高亮分析，将结果以视觉标记（波浪线、虚线）显示在编辑器中。
    /// </remarks>
    public void ValidateCode()
    {
        _validationManager.ValidateCode();
    }

    public EditorDiagnosticsSummary CurrentDiagnostics => _validationManager.CurrentDiagnostics;
    public IReadOnlyList<EditorDiagnosticItem> GetDiagnosticsSnapshot() => _validationManager.GetDiagnosticsSnapshot();

    public event EventHandler<EditorDiagnosticsSummary> DiagnosticsUpdated
    {
        add => _validationManager.DiagnosticsUpdated += value;
        remove => _validationManager.DiagnosticsUpdated -= value;
    }

    /// <summary>
    /// 格式化代码
    /// </summary>
    /// <remarks>
    /// 根据 Prophet DSL 格式化规则对当前代码进行格式化，包括缩进、空格、换行等。
    /// </remarks>
    public void FormatCode()
    {
        _formattingManager.FormatCode();
    }

    /// <summary>
    /// 显示快速修复菜单
    /// </summary>
    /// <remarks>
    /// 根据当前光标位置的错误，显示可用的快速修复建议。
    /// 如果光标位置没有错误，则不会显示任何内容。
    /// </remarks>
    public void ShowQuickFix()
    {
        var offset = _editor.CaretOffset;
        var error = _validationManager.FindErrorAtOffset(offset);

        if (error != null)
        {
            _quickFixManager.ShowQuickFix(error);
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// 释放编辑器助手占用的资源
    /// </summary>
    /// <remarks>
    /// 释放各功能管理器的资源，包括：
    /// <list type="bullet">
    /// <item><description>ValidationManager: 解绑事件、取消验证任务</description></item>
    /// <item><description>HoverTooltipManager: 停止计时器、关闭提示框、解绑事件</description></item>
    /// </list>
    /// 在关闭 Tab 或编辑器时应调用此方法，确保不会发生内存泄漏。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // 释放关键管理器资源
            _validationManager?.Dispose();
            _hoverManager?.Dispose();

            // 其他管理器暂时无需显式释放（无事件订阅/Timer等长生命周期资源）
            // 如果未来增加了需要释放的资源，在此添加

            _disposed = true;
            Logger.Info("编辑器资源已释放");
        }
        catch (Exception ex)
        {
            Logger.Error("释放编辑器资源时出错", ex);
        }
    }

    #endregion
}
