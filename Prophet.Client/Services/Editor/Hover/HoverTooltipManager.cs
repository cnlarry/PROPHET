using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using Prophet.Client.Controls;
using Prophet.Client.Services;
using Prophet.Client.Services.Rules;
using Prophet.Client.Services.Editor.Validation;

namespace Prophet.Client.Services.Editor.Hover;

/// <summary>
/// 悬停提示管理器（版本 2.0 - 基于统一规则系统）
/// </summary>
/// <remarks>
/// <para>
/// 负责在鼠标悬停时显示函数、指标的帮助信息。
/// 使用防抖机制（200ms延迟）避免频繁触发。
/// </para>
/// <para>
/// 主要特性：
/// <list type="bullet">
/// <item><description>智能单词识别：自动识别光标下的单词</description></item>
/// <item><description>防抖处理：移动距离小于5像素时不重置计时器</description></item>
/// <item><description>视口检测：只在单词所在行可见时显示提示</description></item>
/// <item><description>滚动同步：正确处理滚动偏移量，确保位置准确</description></item>
/// <item><description>智能定位：自动检测可用空间，下方不足时显示在上方，避免覆盖对象</description></item>
/// <item><description>类型和范围显示：显示字段类型和参数范围（新功能）</description></item>
/// </list>
/// </para>
/// </remarks>
public class HoverTooltipManager : IDisposable
{
    private readonly TextEditor _editor;
    private readonly IntelliSenseService _intelliSense;
    private readonly DSLRulesService _rulesService;
    private readonly ValidationManager? _validationManager;

    private Avalonia.Controls.Primitives.Popup? _hoverPopup;
    private HoverTooltip? _hoverContent;
    private System.Timers.Timer? _hoverTimer;
    private (int start, int end)? _currentHoverWordRange;
    private (int offset, int start, int end)? _pendingHoverWord;
    private Avalonia.Point? _lastHoverPosition;
    private bool _isDisposed;

    /// <summary>
    /// 初始化 <see cref="HoverTooltipManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    /// <param name="intelliSense">智能提示服务，用于获取悬停信息</param>
    /// <param name="rulesService">规则服务，用于获取详细的类型和范围信息</param>
    public HoverTooltipManager(TextEditor editor, IntelliSenseService intelliSense, DSLRulesService rulesService, ValidationManager? validationManager = null)
    {
        _editor = editor;
        _intelliSense = intelliSense;
        _rulesService = rulesService;
        _validationManager = validationManager;
    }

    /// <summary>
    /// 初始化悬停提示功能
    /// </summary>
    /// <remarks>
    /// 注册事件处理器：
    /// <list type="bullet">
    /// <item><description>PointerMoved: 鼠标移动事件</description></item>
    /// <item><description>LostFocus: 失去焦点时关闭提示</description></item>
    /// <item><description>TextEntering: 开始输入时关闭提示</description></item>
    /// <item><description>PointerPressed: 点击时关闭提示</description></item>
    /// </list>
    /// </remarks>
    public void Initialize()
    {
        if (_isDisposed) return;

        _editor.TextArea.PointerMoved += OnPointerMoved;
        _editor.LostFocus += OnEditorLostFocus;
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.PointerPressed += OnPointerPressed;
    }

    /// <summary>
    /// 关闭悬停提示
    /// </summary>
    /// <remarks>
    /// 供外部调用（如补全窗口打开时），立即关闭当前显示的悬停提示。
    /// </remarks>
    public void Close()
    {
        CloseHoverTooltip();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try { _editor.TextArea.PointerMoved -= OnPointerMoved; } catch { }
        try { _editor.LostFocus -= OnEditorLostFocus; } catch { }
        try { _editor.TextArea.TextEntering -= OnTextEntering; } catch { }
        try { _editor.TextArea.PointerPressed -= OnPointerPressed; } catch { }

        try
        {
            if (_hoverTimer != null)
            {
                _hoverTimer.Stop();
                _hoverTimer.Elapsed -= OnHoverTimerElapsed;
                _hoverTimer.Dispose();
            }
        }
        catch { }

        _hoverTimer = null;

        try
        {
            CloseHoverTooltip();
        }
        catch { }

        _hoverPopup = null;
        _hoverContent = null;
        _currentHoverWordRange = null;
        _pendingHoverWord = null;
        _lastHoverPosition = null;

        GC.SuppressFinalize(this);
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs e) => CloseHoverTooltip();
    private void OnTextEntering(object? sender, Avalonia.Input.TextInputEventArgs e) => CloseHoverTooltip();
    private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => CloseHoverTooltip();

    private void OnPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            var position = e.GetPosition(_editor.TextArea.TextView);
            var textViewBounds = _editor.TextArea.TextView.Bounds;

            if (position.X < 0 || position.Y < 0 ||
                position.X > textViewBounds.Width || position.Y > textViewBounds.Height)
            {
                CloseHoverTooltip();
                return;
            }

            // 获取滚动偏移量，将视口坐标转换为文档坐标
            var scrollOffset = _editor.TextArea.TextView.ScrollOffset;
            var documentPosition = new Avalonia.Point(
                position.X + scrollOffset.X,
                position.Y + scrollOffset.Y
            );

            var textViewPosition = _editor.TextArea.TextView.GetPosition(documentPosition);
            if (textViewPosition == null)
            {
                CloseHoverTooltip();
                return;
            }

            var offset = _editor.Document.GetOffset(textViewPosition.Value.Location);
            
            // 1) 优先显示错误/警告/未定义符号提示（不要求命中有效 hover target）
            if (TryShowDiagnosticsTooltip(offset))
            {
                return;
            }

            var (word, wordStart, wordEnd) = GetWordAtOffset(_editor.Document, offset);
            if (string.IsNullOrEmpty(word))
            {
                CloseHoverTooltip();
                return;
            }

            // 检查单词所在行是否在视口内
            var wordLocation = _editor.Document.GetLocation(wordStart);
            var viewportLines = _editor.TextArea.TextView.VisualLines;
            bool isLineInViewport = false;

            if (viewportLines != null && viewportLines.Count > 0)
            {
                foreach (var visualLine in viewportLines)
                {
                    if (wordLocation.Line >= visualLine.FirstDocumentLine.LineNumber &&
                        wordLocation.Line <= visualLine.LastDocumentLine.LineNumber)
                    {
                        isLineInViewport = true;
                        break;
                    }
                }
            }

            if (!isLineInViewport)
            {
                CloseHoverTooltip();
                return;
            }

            // 如果悬停在同一个单词上，不重新触发
            if (_currentHoverWordRange.HasValue &&
                offset >= _currentHoverWordRange.Value.start &&
                offset <= _currentHoverWordRange.Value.end)
            {
                return;
            }

            // 防抖逻辑
            bool shouldResetTimer = true;
            if (_lastHoverPosition.HasValue && _pendingHoverWord.HasValue)
            {
                var (pendingOffset, pendingStart, pendingEnd) = _pendingHoverWord.Value;
                var distance = Math.Sqrt(
                    Math.Pow(position.X - _lastHoverPosition.Value.X, 2) +
                    Math.Pow(position.Y - _lastHoverPosition.Value.Y, 2)
                );

                if (distance < 5.0 && wordStart == pendingStart && wordEnd == pendingEnd)
                {
                    shouldResetTimer = false;
                }
            }

            _lastHoverPosition = position;

            // 移到新单词，关闭旧提示
            if (_hoverPopup != null && _hoverPopup.IsOpen)
            {
                _hoverPopup.IsOpen = false;
                _currentHoverWordRange = null;
            }

            _pendingHoverWord = (offset, wordStart, wordEnd);

            if (shouldResetTimer)
            {
                _hoverTimer?.Stop();

                if (_hoverTimer == null)
                {
                    _hoverTimer = new System.Timers.Timer(200);
                    _hoverTimer.AutoReset = false;
                    _hoverTimer.Elapsed += OnHoverTimerElapsed;
                }
                else
                {
                    _hoverTimer.Interval = 200;
                }

                _hoverTimer.Start();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in pointer moved handler: {ex.Message}", ex);
            CloseHoverTooltip();
        }
    }

    private bool TryShowDiagnosticsTooltip(int offset)
    {
        if (_validationManager == null)
        {
            return false;
        }

        // 验证错误/警告/提示
        var error = _validationManager.FindErrorAtOffset(offset);
        if (error != null)
        {
            // 防止旧的 hover 计时器触发普通 tooltip 覆盖诊断提示
            _hoverTimer?.Stop();
            _pendingHoverWord = null;

            var (rangeStart, rangeEnd) = GetErrorRange(error, offset);
            if (_currentHoverWordRange.HasValue &&
                rangeStart >= _currentHoverWordRange.Value.start &&
                rangeEnd <= _currentHoverWordRange.Value.end)
            {
                return true;
            }

            var title = error.Severity == ValidationSeverity.Error ? "错误" :
                        error.Severity == ValidationSeverity.Warning ? "警告" : "提示";

            var content = $"第 {error.Line} 行，第 {error.Column} 列：{error.Message}";
            ShowCustomTooltipAtOffset(rangeStart, title, content, null);
            _currentHoverWordRange = (rangeStart, Math.Max(rangeStart + 1, rangeEnd));
            return true;
        }

        // 语义高亮（未定义符号等）
        var highlight = _validationManager.FindSemanticHighlightAtOffset(offset);
        if (highlight != null && !string.IsNullOrWhiteSpace(highlight.Message))
        {
            // 防止旧的 hover 计时器触发普通 tooltip 覆盖诊断提示
            _hoverTimer?.Stop();
            _pendingHoverWord = null;

            var title = highlight.Type switch
            {
                SemanticHighlightType.UndefinedSymbol => "未定义符号",
                SemanticHighlightType.Warning => "警告",
                SemanticHighlightType.Info => "提示",
                _ => "提示"
            };

            var start = Math.Max(0, highlight.StartOffset);
            var end = Math.Max(start + 1, highlight.EndOffset);

            if (_currentHoverWordRange.HasValue &&
                start >= _currentHoverWordRange.Value.start &&
                end <= _currentHoverWordRange.Value.end)
            {
                return true;
            }

            ShowCustomTooltipAtOffset(start, title, highlight.Message!, null);
            _currentHoverWordRange = (start, end);
            return true;
        }

        return false;
    }

    private (int start, int end) GetErrorRange(ValidationError error, int fallbackOffset)
    {
        try
        {
            if (error.Line < 1 || error.Line > _editor.Document.LineCount)
            {
                return (fallbackOffset, Math.Min(fallbackOffset + 1, _editor.Document.TextLength));
            }

            var line = _editor.Document.GetLineByNumber(error.Line);
            var startOffset = line.Offset + Math.Max(0, error.Column - 1);
            if (startOffset >= _editor.Document.TextLength)
            {
                return (fallbackOffset, Math.Min(fallbackOffset + 1, _editor.Document.TextLength));
            }

            var length = error.Length > 0 ? error.Length : GetWordLength(_editor.Document, startOffset);
            var endOffset = Math.Min(line.EndOffset, startOffset + Math.Max(1, length));

            return (startOffset, Math.Max(startOffset + 1, endOffset));
        }
        catch
        {
            return (fallbackOffset, Math.Min(fallbackOffset + 1, _editor.Document.TextLength));
        }
    }

    private int GetWordLength(AvaloniaEdit.Document.TextDocument document, int offset)
    {
        if (offset >= document.TextLength)
            return 1;

        int length = 0;
        while (offset + length < document.TextLength)
        {
            var ch = document.GetCharAt(offset + length);
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.' && ch != '$' && ch != '@')
                break;
            length++;
        }

        return Math.Max(1, length);
    }

    private void OnHoverTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isDisposed) return;

                if (!_pendingHoverWord.HasValue)
                    return;

                var (offset, start, end) = _pendingHoverWord.Value;
                ShowHoverTooltipAtOffset(offset, (start, end));
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Timer elapsed error: {ex.Message}", ex);
        }
    }

    private void ShowHoverTooltipAtOffset(int offset, (int start, int end) wordRange)
    {
        try
        {
            if (_isDisposed) return;

            var word = _editor.Document.GetText(wordRange.start, wordRange.end - wordRange.start);
            if (string.IsNullOrEmpty(word))
                return;

            var line = _editor.Document.GetLineByOffset(offset);
            var context = _editor.Document.GetText(line.Offset, line.Length);
            
            // 计算单词在行中的列偏移量（相对于行开始）
            var wordColumnOffset = wordRange.start - line.Offset;

            // 检查是否在注释中
            if (IsInComment(context, wordColumnOffset))
                return;

            // 检查是否是特定元素：环境变量、函数、指标、时间框架、字段、参数、枚举
            if (!IsValidHoverTarget(word, context, wordColumnOffset))
                return;

            // 获取完整代码以支持自定义函数悬停提示
            var fullCode = _editor.Document.Text;
            var (title, content, example) = _intelliSense.GetHoverInfo(word, context, fullCode);

            if (string.IsNullOrEmpty(title))
                return;

            if (_hoverContent == null)
            {
                _hoverContent = new HoverTooltip();
            }

            _hoverContent.SetContent(title, content, example);

            ShowPopupAtOffset(wordRange.start, wordRange);
        }
        catch (Exception ex)
        {
            Logger.Error($"Show tooltip error: {ex.Message}", ex);
        }
    }

    private void ShowCustomTooltipAtOffset(int anchorOffset, string title, string content, string? example)
    {
        try
        {
            if (_isDisposed) return;

            if (_hoverContent == null)
            {
                _hoverContent = new HoverTooltip();
            }

            _hoverContent.SetContent(title, content, example);
            ShowPopupAtOffset(anchorOffset, null);
        }
        catch (Exception ex)
        {
            Logger.Error($"Show custom tooltip error: {ex.Message}", ex);
        }
    }

    private void ShowPopupAtOffset(int anchorOffset, (int start, int end)? hoverRange)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_isDisposed) return;

                var location = _editor.Document.GetLocation(anchorOffset);
                var textViewPosition = new AvaloniaEdit.TextViewPosition(location);

                // 获取行底部和行顶部的位置
                var visualPosBottom = _editor.TextArea.TextView.GetVisualPosition(textViewPosition, AvaloniaEdit.Rendering.VisualYPosition.LineBottom);
                var visualPosTop = _editor.TextArea.TextView.GetVisualPosition(textViewPosition, AvaloniaEdit.Rendering.VisualYPosition.LineTop);

                if (double.IsNaN(visualPosBottom.X) || double.IsNaN(visualPosBottom.Y) ||
                    double.IsNaN(visualPosTop.X) || double.IsNaN(visualPosTop.Y))
                    return;

                // GetVisualPosition 返回的是文档坐标，需要转换为相对于 TextView 的屏幕坐标
                var scrollOffset = _editor.TextArea.TextView.ScrollOffset;
                var relativeBottomY = visualPosBottom.Y - scrollOffset.Y;
                var relativeTopY = visualPosTop.Y - scrollOffset.Y;
                var relativeX = visualPosBottom.X - scrollOffset.X;

                // 计算下方可用空间
                var viewportHeight = _editor.TextArea.TextView.Bounds.Height;
                var spaceBelow = viewportHeight - relativeBottomY;

                // 估算提示窗口高度（如果已创建则使用实际高度，否则使用估算值）
                var tooltipHeight = _hoverContent?.DesiredSize.Height ?? 150.0;

                // 智能选择显示位置
                Avalonia.Rect placementRect;
                Avalonia.Controls.PlacementMode placement;

                if (spaceBelow >= tooltipHeight + 5) // 下方空间足够（加5像素余量）
                {
                    // 显示在对象下方
                    placementRect = new Avalonia.Rect(relativeX, relativeBottomY, 1, 1);
                    placement = Avalonia.Controls.PlacementMode.BottomEdgeAlignedLeft;
                }
                else
                {
                    // 显示在对象上方（行顶部）
                    placementRect = new Avalonia.Rect(relativeX, relativeTopY, 1, 1);
                    placement = Avalonia.Controls.PlacementMode.TopEdgeAlignedLeft;
                }

                if (_hoverPopup == null)
                {
                    _hoverPopup = new Avalonia.Controls.Primitives.Popup
                    {
                        Child = _hoverContent,
                        PlacementTarget = _editor.TextArea.TextView,
                        PlacementRect = placementRect,
                        Placement = placement,
                        IsLightDismissEnabled = false,
                        Focusable = false,
                        IsHitTestVisible = false
                    };
                }
                else
                {
                    _hoverPopup.PlacementRect = placementRect;
                    _hoverPopup.Placement = placement;
                }

                if (!_hoverPopup.IsOpen)
                {
                    _hoverPopup.IsOpen = true;
                }

                if (hoverRange.HasValue)
                {
                    _currentHoverWordRange = hoverRange.Value;
                }
            }
                catch (Exception ex)
                {
                    Logger.Error($"Show tooltip at offset error: {ex.Message}", ex);
                }
            }, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void CloseHoverTooltip()
    {
        try
        {
            _hoverTimer?.Stop();

            if (_hoverPopup != null && _hoverPopup.IsOpen)
            {
                _hoverPopup.IsOpen = false;
            }

            _currentHoverWordRange = null;
            _pendingHoverWord = null;
            _lastHoverPosition = null;
        }
        catch (Exception ex)
        {
            Logger.Error($"Close tooltip error: {ex.Message}", ex);
        }
    }

    private (string word, int start, int end) GetWordAtOffset(AvaloniaEdit.Document.TextDocument document, int offset)
    {
        if (offset < 0 || offset >= document.TextLength)
            return ("", -1, -1);

        int start = offset;
        while (start > 0)
        {
            var ch = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '@')
                break;
            start--;
        }

        int end = offset;
        while (end < document.TextLength)
        {
            var ch = document.GetCharAt(end);
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                break;
            end++;
        }

        if (end <= start)
            return ("", -1, -1);

        var word = document.GetText(start, end - start);

        if (start > 0 && document.GetCharAt(start - 1) == '@')
        {
            return (word, start, end);
        }

        return (word, start, end);
    }

    /// <summary>
    /// 检查指定位置是否在注释中
    /// </summary>
    /// <param name="lineText">行文本</param>
    /// <param name="columnOffset">列偏移量（相对于行开始）</param>
    /// <returns>如果在注释中返回 true，否则返回 false</returns>
    private bool IsInComment(string lineText, int columnOffset)
    {
        // 查找行注释符号 "//"
        var commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
        
        // 如果找到注释符号，且当前位置在注释符号之后
        if (commentIndex >= 0 && columnOffset >= commentIndex)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查单词是否是有效的悬停目标
    /// </summary>
    /// <param name="word">单词</param>
    /// <param name="context">上下文（行文本）</param>
    /// <param name="columnOffset">列偏移量</param>
    /// <returns>如果是有效目标返回 true</returns>
    private bool IsValidHoverTarget(string word, string context, int columnOffset)
    {
        if (string.IsNullOrEmpty(word))
            return false;

        // 1. 环境变量 (@开头) - 允许
        if (columnOffset > 0 && context[columnOffset - 1] == '@')
        {
            // Console.WriteLine($"[Hover] 环境变量: @{word}");
            return true;
        }

        // 2. 指标 ($.XXX 或 $(tf).XXX 格式) - 允许
        if (columnOffset >= 2 && context[columnOffset - 1] == '.' && context[columnOffset - 2] == '$')
        {
            // Console.WriteLine($"[Hover] 指标: $.{word}");
            return true;
        }

        // 检查 $(tf). 格式
        if (columnOffset >= 2 && context[columnOffset - 1] == '.')
        {
            // 向前查找是否有 $(...) 格式
            var beforeDot = context.Substring(0, columnOffset - 1);
            if (System.Text.RegularExpressions.Regex.IsMatch(beforeDot, @"\$\([^)]+\)\s*$"))
            {
                // Console.WriteLine($"[Hover] 指标（带时间框架）: $(tf).{word}");
                return true;
            }
        }

        // 3. 时间框架 (在 $() 中) - 允许
        if (columnOffset > 0 && columnOffset < context.Length)
        {
            var beforeParen = columnOffset >= 2 && context[columnOffset - 2] == '$' && context[columnOffset - 1] == '(';
            var afterParen = columnOffset + word.Length < context.Length && context[columnOffset + word.Length] == ')';
            
            if (beforeParen && afterParen)
            {
                // Console.WriteLine($"[Hover] 时间框架: $(word)");
                return true;
            }
        }

        // 4. 函数调用 (后面跟着括号) - 允许
        var nextCharIndex = columnOffset + word.Length;
        if (nextCharIndex < context.Length && context[nextCharIndex] == '(')
        {
            // Console.WriteLine($"[Hover] 函数调用: {word}()");
            return true;
        }

        // 5. 字段访问 (前面有点号) - 允许
        if (columnOffset > 0 && context[columnOffset - 1] == '.')
        {
            // Console.WriteLine($"[Hover] 字段访问: .{word}");
            return true;
        }

        // 6. 枚举值 - 检查是否是已知的枚举值
        var allEnumValues = _rulesService.GetAllEnums()
            .SelectMany(e => e.Values ?? System.Linq.Enumerable.Empty<string>());
        
        if (allEnumValues.Any(v => v.Equals(word, StringComparison.OrdinalIgnoreCase)))
        {
            // Console.WriteLine($"[Hover] 枚举值: {word}");
            return true;
        }

        // 7. 检查是否是已知的指标、函数或环境变量（即使不在特定上下文中也允许）
        if (_rulesService.GetAllIndicators().Any(i => i.Id.Equals(word, StringComparison.OrdinalIgnoreCase)))
        {
            // Console.WriteLine($"[Hover] 已知指标: {word}");
            return true;
        }

        if (_rulesService.GetAllDataFunctions().Any(f => f.Id.Equals(word, StringComparison.OrdinalIgnoreCase)) ||
            _rulesService.GetAllMathFunctions().Any(f => f.Id.Equals(word, StringComparison.OrdinalIgnoreCase)) ||
            _rulesService.GetAllSignalFunctions().Any(f => f.Id.Equals(word, StringComparison.OrdinalIgnoreCase)))
        {
            // Console.WriteLine($"[Hover] 已知函数: {word}");
            return true;
        }

        // 环境变量只有在带 @ 前缀时才能被识别（在上面第433行已处理）
        // 这里不应该识别没有 @ 前缀的单词为环境变量
        // if (_rulesService.GetAllEnvironmentVariables().Any(e => e.Name.Equals(word, StringComparison.OrdinalIgnoreCase)))
        // {
        //     // Console.WriteLine($"[Hover] 已知环境变量: {word}");
        //     return true;
        // }

        // Console.WriteLine($"[Hover] 未匹配: '{word}' at column {columnOffset}, context: '{context}'");
        // 其他情况不触发悬停提示
        return false;
    }
}

