using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Prophet.Client.Controls;
using Prophet.Client.Services.Editor.Shared;
using Prophet.Client.Services.Editor.Snippets;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services.Editor.Completion;

/// <summary>
/// 补全管理器
/// </summary>
/// <remarks>
/// <para>
/// 协调补全相关的所有子组件，是代码补全系统的核心管理类。
/// 负责监听用户输入事件，触发上下文分析，显示和管理补全窗口。
/// </para>
/// <para>
/// 管理的子组件包括：
/// <list type="bullet">
/// <item><description><see cref="CompletionContextAnalyzer"/>: 上下文分析器</description></item>
/// <item><description><see cref="CompletionItemProvider"/>: 补全项提供器</description></item>
/// <item><description><see cref="CompletionFilterSorter"/>: 过滤排序器</description></item>
/// <item><description><see cref="CompletionInsertionHandler"/>: 插入处理器</description></item>
/// </list>
/// </para>
/// <para>
/// 支持的触发方式：
/// <list type="bullet">
/// <item><description>自动触发：输入特定字符（如 $, ., @, = 等）</description></item>
/// <item><description>手动触发：Ctrl+Space 组合键</description></item>
/// <item><description>智能触发：Backspace 后根据上下文自动显示</description></item>
/// </list>
/// </para>
/// <para>
/// 智能定位特性：
/// <list type="bullet">
/// <item><description>自动检测屏幕可用空间</description></item>
/// <item><description>下方空间不足时显示在光标上方，避免窗口溢出屏幕</description></item>
/// </list>
/// </para>
/// </remarks>
public class CompletionManager
{
    private readonly TextEditor _editor;
    private readonly IntelliSenseService _intelliSense;
    private readonly CompletionContextAnalyzer _contextAnalyzer;
    private readonly CompletionItemProvider _itemProvider;
    private readonly CompletionFilterSorter _filterSorter;
    private readonly CompletionInsertionHandler _insertionHandler;
    private readonly CompletionUsageTracker _usageTracker;

    private CompletionWindow? _completionWindow;
    private bool _lastIncludeSnippets;
    private CompletionContext? _currentContext;

    /// <summary>
    /// 初始化 <see cref="CompletionManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    /// <param name="intelliSense">智能提示服务</param>
    /// <param name="usageTracker">使用频率跟踪器</param>
    /// <param name="rulesService">DSL 规则服务</param>
    public CompletionManager(
        TextEditor editor,
        IntelliSenseService intelliSense,
        CompletionUsageTracker usageTracker,
        DSLRulesService rulesService)
    {
        _editor = editor;
        _intelliSense = intelliSense;
        _usageTracker = usageTracker;

        _contextAnalyzer = new CompletionContextAnalyzer(intelliSense);
        _itemProvider = new CompletionItemProvider(intelliSense, new SnippetProvider(), rulesService);
        _filterSorter = new CompletionFilterSorter(usageTracker);
        _insertionHandler = new CompletionInsertionHandler(editor, intelliSense);
    }

    /// <summary>
    /// 初始化补全功能
    /// </summary>
    /// <remarks>
    /// <para>
    /// 注册以下事件处理器：
    /// <list type="bullet">
    /// <item><description>TextEntering: 文本输入前事件</description></item>
    /// <item><description>TextEntered: 文本输入后事件</description></item>
    /// <item><description>KeyDown: 按键事件（处理导航键和快捷键）</description></item>
    /// <item><description>PointerPressed: 鼠标点击事件（关闭补全窗口）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Initialize()
    {
        if (_editor.TextArea == null)
            return;

        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.KeyDown += OnEditorKeyDown;

        _editor.TextArea.PointerPressed += (s, e) =>
        {
            if (_completionWindow != null)
            {
                CloseCompletionWindow();
            }
        };
    }

    /// <summary>
    /// 手动触发代码片段选择窗口
    /// </summary>
    /// <remarks>
    /// 用于菜单按钮或快捷键触发，显示包含代码片段的补全窗口。
    /// 与自动触发的补全不同，此方法会包含代码片段（模板代码）。
    /// </remarks>
    public void ShowSnippetPicker()
    {
        ShowCompletionWindow(includeSnippets: true);
    }

    #region 事件处理

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // 如果补全窗口打开，处理导航键
        if (_completionWindow != null)
        {
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.PageUp || e.Key == Key.PageDown ||
                e.Key == Key.Home || e.Key == Key.End || e.Key == Key.Enter || e.Key == Key.Tab || e.Key == Key.Escape)
            {
                _completionWindow.HandleKey(e.Key);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                RefreshCompletionAfterEdit();
                return;
            }

            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                CloseCompletionWindow();
                return;
            }
        }

        // Backspace 智能处理
        if (e.Key == Key.Back)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var offset = _editor.CaretOffset;
                var document = _editor.Document;
                
                // 检查是否在注释中，如果是则不触发补全
                if (IsInComment(document, offset))
                {
                    return;
                }
                
                var line = document.GetLineByOffset(offset);
                var lineText = document.GetText(line.Offset, line.Length);
                
                // 检查是否在空行内（整行只有空白字符）
                if (string.IsNullOrWhiteSpace(lineText))
                {
                    // 在空行内，不触发补全
                    return;
                }
                
                // 检查光标前的文本
                var beforeCaret = document.GetText(line.Offset, offset - line.Offset);
                var trimmed = beforeCaret.TrimEnd();
                
                // 检查是否在完整表达式后删除空格，如果是则不触发补全
                if (trimmed.EndsWith(";"))
                {
                    // 在完整表达式后，不触发补全
                    return;
                }
                
                var context = _contextAnalyzer.AnalyzeContext(document, offset);
                if (ShouldShowCompletionForContext(context))
                {
                    ShowCompletionWindow(includeSnippets: false);
                }
            }, Avalonia.Threading.DispatcherPriority.Input);
        }

        // Ctrl+Space 手动触发补全
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            var offset = _editor.CaretOffset;
            var document = _editor.Document;
            
            // 检查是否在注释中，如果是则不触发补全
            if (!IsInComment(document, offset))
            {
                ShowCompletionWindow();
                e.Handled = true;
            }
        }

        // Enter 键不触发补全（除非在特定上下文中）
        // 回车键不应该触发补全，补全应该通过字母输入触发
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completionWindow != null && e.Text?.Length > 0)
        {
            var ch = e.Text[0];
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.' && ch != ' ')
            {
                CloseCompletionWindow();
            }
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text == null) return;

        var text = e.Text;
        var offset = _editor.CaretOffset;
        var document = _editor.Document;

        // 检查是否在注释中，如果是则不触发补全
        if (IsInComment(document, offset))
        {
            return;
        }

        // 检查是否是换行符（Enter 键）
        if (text == "\n" || text == "\r\n" || text == "\r")
        {
            // 获取当前行（换行前的行）
            var line = document.GetLineByOffset(offset);
            var lineText = document.GetText(line.Offset, line.Length);
            
            // 如果当前行是空行（只有空白字符），不触发补全
            if (string.IsNullOrWhiteSpace(lineText))
            {
                return;
            }
        }

        // 触发字符处理
        if (text == "$")
        {
            HandleDollarSign(offset, document);
            return;
        }

        if (text == "," || text == "@" || text == "=" || text == "<" || text == ">" || text == "!" || text == "(" || text == ":")
        {
            ShowCompletionWindow(includeSnippets: false);
            return;
        }

        if (text == " ")
        {
            HandleSpaceKey(offset, document);
            return;
        }

        if (text == ".")
        {
            HandleDotKey(offset, document);
            return;
        }

        // 字母/数字输入 - 触发补全（在无上下文或表达式内）
        if (char.IsLetterOrDigit(text[0]))
        {
            var context = _contextAnalyzer.AnalyzeContext(document, offset);
            if (ShouldShowCompletionForContext(context) || _completionWindow != null)
            {
                ShowCompletionWindow(includeSnippets: false);
            }
        }
    }

    #endregion

    #region 特殊键处理

    private void HandleDollarSign(int offset, AvaloniaEdit.Document.TextDocument document)
    {
        // 检查是否在注释中，如果是则不触发补全
        if (IsInComment(document, offset))
        {
            return;
        }
        
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, offset - line.Offset);

        // 检查是否在运算符右侧
        var operatorMatch = System.Text.RegularExpressions.Regex.Match(lineText, @"([=<>!]+)\s*\$");
        if (operatorMatch.Success)
        {
            // 智能补全逻辑（从原代码迁移）
            // 暂时简化
        }

        // 自动插入 (
        if (offset > 0 && document.GetCharAt(offset - 1) == '$')
        {
            document.Insert(offset, "(");
            _editor.CaretOffset = offset + 1;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.Focus();
                ShowCompletionWindow(includeSnippets: false);
            }, Avalonia.Threading.DispatcherPriority.Input);
        }
    }

    private void HandleSpaceKey(int offset, AvaloniaEdit.Document.TextDocument document)
    {
        // 表达式右边的补全一律靠表达式符号（==, !=, >, < 等）唤起，空格不再唤起表达式右边的补全
        // 空格不触发补全，补全应该通过字母输入或其他符号触发
        return;
    }

    private void HandleDotKey(int offset, AvaloniaEdit.Document.TextDocument document)
    {
        // 检查是否在注释中，如果是则不触发补全
        if (IsInComment(document, offset))
        {
            return;
        }
        
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, offset - line.Offset);

        // 检查各种点号触发场景
        var beforeDot = lineText.TrimEnd('.');
        bool shouldTriggerCompletion =
            System.Text.RegularExpressions.Regex.IsMatch(beforeDot, @"\$\([^)]*\)\.(\w*)$") ||
            System.Text.RegularExpressions.Regex.IsMatch(beforeDot, @"\$\([^)]*\)\.(\w+)\([^)]*\)$") ||
            System.Text.RegularExpressions.Regex.IsMatch(beforeDot, @"\b(KLINE|HIGHEST|LOWEST|AVERAGE|STD|MEDIAN|VARIANCE|CHANGE|RANK|VOLA|ATR|SLOPE|SUM|PRICE|PATTERN|ZSCORE|PERCENTILE|CROSS|HT|POWER|CONSECUTIVE|CONSEC|COUNT|FVG|PREMIUM|ORDERBLOCK|SWING|BOS|CHOCH|LIQUIDITY|BREAKER)\([^)]+\)$");

        if (shouldTriggerCompletion)
        {
            ShowCompletionWindow(includeSnippets: false);
        }
    }

    #endregion

    #region 补全窗口管理

    private void ShowCompletionWindow(bool includeSnippets = true)
    {
        _lastIncludeSnippets = includeSnippets;

        var caretOffset = _editor.CaretOffset;
        var document = _editor.Document;

        // 检查是否在注释中，如果是则不显示补全窗口
        if (IsInComment(document, caretOffset))
        {
            CloseCompletionWindow();
            return;
        }

        // 更新当前代码（用于提取自定义函数）
        _itemProvider.SetCurrentCode(document.Text);

        // 分析上下文
        var context = _contextAnalyzer.AnalyzeContext(document, caretOffset);
        _currentContext = context;
        
        // 获取补全项
        var completions = _itemProvider.GetCompletionsForContext(context, includeSnippets);
       
        if (completions.Count == 0)
        {
            CloseCompletionWindow();
            return;
        }

        // 过滤和排序
        completions = _filterSorter.FilterAndSort(completions, context.FilterPrefix, context);

        // 如果窗口已打开，只更新补全项
        if (_completionWindow != null)
        {
            _completionWindow.UpdateCompletions(completions);
            return;
        }

        // 获取光标位置（智能定位）
        var caretPos = _editor.TextArea.Caret.Position;
        var visualPosBottom = _editor.TextArea.TextView.GetVisualPosition(caretPos, AvaloniaEdit.Rendering.VisualYPosition.LineBottom);
        var visualPosTop = _editor.TextArea.TextView.GetVisualPosition(caretPos, AvaloniaEdit.Rendering.VisualYPosition.LineTop);
        
        // 转换为屏幕坐标
        var screenPosBottom = _editor.TextArea.TextView.PointToScreen(new Avalonia.Point(visualPosBottom.X, visualPosBottom.Y));
        var screenPosTop = _editor.TextArea.TextView.PointToScreen(new Avalonia.Point(visualPosTop.X, visualPosTop.Y));
        
        // 计算下方可用空间
        var parentWindow = Avalonia.Controls.TopLevel.GetTopLevel(_editor) as Avalonia.Controls.Window;
        var screenBounds = parentWindow?.Screens?.ScreenFromWindow(parentWindow)?.WorkingArea ?? new Avalonia.PixelRect(0, 0, 1920, 1080);
        var spaceBelow = screenBounds.Bottom - screenPosBottom.Y;
        
        // 估算补全窗口高度（最多10项 × 24像素高度 + 边距）
        var estimatedHeight = Math.Min(completions.Count, 10) * 24 + 20;
        
        // 智能选择显示位置
        Avalonia.Point windowPosition;
        
        if (spaceBelow >= estimatedHeight + 10) // 下方空间充足（加10像素余量）
        {
            // 显示在光标下方
            windowPosition = new Avalonia.Point(screenPosBottom.X, screenPosBottom.Y);
        }
        else
        {
            // 显示在光标上方（行顶部）
            windowPosition = new Avalonia.Point(screenPosTop.X, screenPosTop.Y - estimatedHeight);
        }

        // 创建补全窗口
        _completionWindow = new CompletionWindow(parentWindow, _editor);
        _completionWindow.CompletionConfirmed += OnCompletionConfirmed;
        _completionWindow.Closed += (s, e) => { _completionWindow = null; };

        _completionWindow.ShowCompletions(completions, windowPosition);

        // 确保 TextArea 保持焦点
        if (_editor.TextArea != null)
        {
            _editor.TextArea.Focus();
        }
    }

    private void CloseCompletionWindow()
    {
        if (_completionWindow != null)
        {
            _completionWindow.SafeClose();
            _completionWindow = null;
        }
    }

    private void RefreshCompletionAfterEdit()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var offset = _editor.CaretOffset;
            var document = _editor.Document;
            
            // 检查是否在注释中，如果是则关闭补全窗口
            if (IsInComment(document, offset))
            {
                CloseCompletionWindow();
                return;
            }
            
            var line = document.GetLineByOffset(offset);
            var lineText = document.GetText(line.Offset, offset - line.Offset);

            if (string.IsNullOrWhiteSpace(lineText.Trim()))
            {
                CloseCompletionWindow();
                return;
            }

            // 检查是否在完整表达式后（以分号结尾，后面只有空格）
            var trimmed = lineText.TrimEnd();
            if (trimmed.EndsWith(";"))
            {
                // 检查分号后是否只有空格
                var afterSemicolon = lineText.Substring(trimmed.Length);
                if (string.IsNullOrWhiteSpace(afterSemicolon))
                {
                    // 在完整表达式后，不触发补全
                    CloseCompletionWindow();
                    return;
                }
            }

            var context = _contextAnalyzer.AnalyzeContext(document, offset);
            if (ShouldShowCompletionForContext(context))
            {
                ShowCompletionWindow(_lastIncludeSnippets);
            }
            else
            {
                CloseCompletionWindow();
            }
        }, Avalonia.Threading.DispatcherPriority.Input);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查指定位置是否在注释中
    /// </summary>
    /// <param name="document">文档对象</param>
    /// <param name="offset">光标在文档中的偏移量</param>
    /// <returns>如果在注释中返回 true，否则返回 false</returns>
    private bool IsInComment(AvaloniaEdit.Document.TextDocument document, int offset)
    {
        if (offset <= 0)
            return false;

        // 获取当前行
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, line.Length);
        var columnOffset = offset - line.Offset;

        // 1. 检查单行注释 //
        var singleLineCommentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
        if (singleLineCommentIndex >= 0 && columnOffset >= singleLineCommentIndex)
        {
            return true;
        }

        // 2. 检查多行注释 /* */
        // 从当前位置向前查找最近的 /* 和 */
        var textBeforeOffset = document.GetText(0, offset);
        
        // 从后向前查找最近的 /* 和 */
        int nearestCommentStart = -1;
        int nearestCommentEnd = -1;
        
        for (int i = textBeforeOffset.Length - 2; i >= 0; i--)
        {
            if (textBeforeOffset[i] == '/' && textBeforeOffset[i + 1] == '*')
            {
                if (nearestCommentStart < 0)
                {
                    nearestCommentStart = i;
                }
            }
            else if (textBeforeOffset[i] == '*' && textBeforeOffset[i + 1] == '/')
            {
                if (nearestCommentEnd < 0)
                {
                    nearestCommentEnd = i + 2; // +2 因为 */ 占两个字符
                }
            }
            
            // 如果两个都找到了，可以提前退出
            if (nearestCommentStart >= 0 && nearestCommentEnd >= 0)
            {
                break;
            }
        }

        // 如果找到了 /*，检查它是否在最近的 */ 之后（或没有 */）
        // 如果是，说明当前位置在注释中
        if (nearestCommentStart >= 0)
        {
            if (nearestCommentEnd < 0 || nearestCommentStart > nearestCommentEnd - 2)
            {
                // 注释未关闭，或者最近的 /* 在最近的 */ 之后
                return true;
            }
        }

        return false;
    }

    private bool ShouldShowCompletionForContext(CompletionContext context)
    {
        return context.Type switch
        {
            CompletionContextType.Timeframe => true,
            CompletionContextType.Indicator => true,
            CompletionContextType.IndicatorField => true,
            CompletionContextType.DataFunctionField => true,
            CompletionContextType.ChainedMethodCall => true,
            CompletionContextType.ThreeLevelChainedCall => true,
            CompletionContextType.EnvVariable => true,
            CompletionContextType.SignalFunctionOrCustom => true,
            CompletionContextType.InsideSignalFunction => true, // 信号函数内：显示数据函数、数学函数、时间序列函数
            CompletionContextType.FieldValue => true,
            CompletionContextType.DataFunctionFieldValue => true,
            CompletionContextType.ThreeLevelFieldValue => true,
            CompletionContextType.TimeSeriesFunctionMethodValue => true,
            CompletionContextType.SignalValue => true,
            CompletionContextType.General => true, // 通用上下文：显示数据函数、数学函数、时间序列函数
            _ => false
        };
    }

    private void OnCompletionConfirmed(object? sender, CompletionItemEventArgs e)
    {
        // 记录使用频率
        if (_currentContext != null)
        {
            _usageTracker.RecordUsage(e.Item.Label, _currentContext.Type);
        }

        // 处理补全插入
        _insertionHandler.HandleCompletionConfirmed(
            e.Item,
            _editor.CaretOffset,
            CloseCompletionWindow,
            ShowCompletionWindow
        );
    }

    #endregion
}

