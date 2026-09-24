using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Prophet.Client.Services;

namespace Prophet.Client.Services.Editor.Validation;

/// <summary>
/// 语法验证管理器
/// </summary>
/// <remarks>
/// <para>
/// 负责代码验证和错误标记渲染。集成了传统语法验证和语义高亮分析。
/// </para>
/// <para>
/// 主要功能：
/// <list type="bullet">
/// <item><description>使用 <see cref="DSLValidator"/> 进行语法验证</description></item>
/// <item><description>使用 <see cref="SemanticHighlighter"/> 进行语义分析</description></item>
/// <item><description>通过 <see cref="ErrorMarkerRenderer"/> 在编辑器中绘制波浪线和虚线</description></item>
/// <item><description>支持错误、警告、提示三种严重级别</description></item>
/// </list>
/// </para>
/// </remarks>
public class ValidationManager : IDisposable
{
    private readonly TextEditor _editor;
    private readonly DSLValidator _validator;
    private readonly SemanticHighlighter _semanticHighlighter;
    private ErrorMarkerRenderer? _errorRenderer;

    private CancellationTokenSource? _validationDebounceCts;
    private int _validationRequestId;
    private bool _isDisposed;

    public event EventHandler<EditorDiagnosticsSummary>? DiagnosticsUpdated;

    public EditorDiagnosticsSummary CurrentDiagnostics { get; private set; } = EditorDiagnosticsSummary.Empty;

    /// <summary>
    /// 初始化 <see cref="ValidationManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    /// <param name="validator">DSL 验证器</param>
    public ValidationManager(TextEditor editor, DSLValidator validator)
    {
        _editor = editor;
        _validator = validator;
        _semanticHighlighter = new SemanticHighlighter();
    }

    /// <summary>
    /// 初始化验证功能
    /// </summary>
    /// <remarks>
    /// 创建错误标记渲染器并添加到编辑器的背景渲染器列表中，
    /// 同时监听文本变化事件以触发延迟验证（延迟 500ms）。
    /// </remarks>
    public void Initialize()
    {
        if (_isDisposed) return;

        // 创建错误标记渲染器
        _errorRenderer = new ErrorMarkerRenderer();
        _editor.TextArea.TextView.BackgroundRenderers.Add(_errorRenderer);

        // 监听文本变化
        _editor.TextChanged += OnTextChanged;
    }

    /// <summary>
    /// 验证代码并显示错误标记
    /// </summary>
    /// <remarks>
    /// <para>
    /// 执行两种类型的分析：
    /// <list type="number">
    /// <item><description>传统语法验证：使用 DSLValidator 检查语法错误</description></item>
    /// <item><description>语义高亮分析：使用 SemanticHighlighter 分析语义问题</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 验证结果会通过 <see cref="ErrorMarkerRenderer"/> 在编辑器中显示：
    /// <list type="bullet">
    /// <item><description>错误：红色波浪线</description></item>
    /// <item><description>警告：橙色波浪线</description></item>
    /// <item><description>提示：蓝色虚线</description></item>
    /// <item><description>未定义符号：灰色虚线</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void ValidateCode()
    {
        if (_isDisposed) return;
        if (_errorRenderer == null) return;

        var code = _editor.Text;

        // 1. 传统语法验证
        var result = _validator.Validate(code);

        _errorRenderer.Errors.Clear();
        foreach (var error in result.AllIssues)
        {
            _errorRenderer.Errors.Add(error);
        }

        // 2. 语义高亮分析
        _errorRenderer.SemanticHighlights.Clear();

        try
        {
            var semanticHighlights = _semanticHighlighter.AnalyzeDocument(_editor.Document, result);
            _errorRenderer.SemanticHighlights.AddRange(semanticHighlights);
        }
        catch (Exception ex)
        {
            Logger.Error($"Semantic highlight error: {ex.Message}", ex);
        }

        // 3. 汇总诊断信息（供状态栏/外部订阅使用）
        var warningCount = result.Warnings.Count(x => x.Severity == ValidationSeverity.Warning);
        var infoCount = result.Warnings.Count(x => x.Severity == ValidationSeverity.Info);
        var undefinedSymbolCount = _errorRenderer.SemanticHighlights.Count(x => x.Type == SemanticHighlightType.UndefinedSymbol);

        CurrentDiagnostics = new EditorDiagnosticsSummary(
            ErrorCount: result.Errors.Count,
            WarningCount: warningCount,
            InfoCount: infoCount,
            UndefinedSymbolCount: undefinedSymbolCount);

        DiagnosticsUpdated?.Invoke(this, CurrentDiagnostics);

        // 刷新编辑器视图
        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    /// <summary>
    /// 查找指定偏移量位置的错误
    /// </summary>
    /// <param name="offset">文档中的偏移量</param>
    /// <returns>
    /// 如果找到错误则返回 <see cref="ValidationError"/> 对象，否则返回 null。
    /// 只返回光标所在位置范围内的错误。
    /// </returns>
    /// <remarks>
    /// 用于快速修复功能，根据光标位置查找对应的错误信息。
    /// </remarks>
    public ValidationError? FindErrorAtOffset(int offset)
    {
        if (_errorRenderer == null)
            return null;

        var location = _editor.Document.GetLocation(offset);

        foreach (var error in _errorRenderer.Errors)
        {
            if (error.Line == location.Line)
            {
                var line = _editor.Document.GetLineByNumber(error.Line);
                var errorStart = line.Offset + error.Column - 1;
                var errorLength = error.Length > 0 ? error.Length : GetWordLength(_editor.Document, errorStart);
                var errorEnd = errorStart + errorLength;

                if (offset >= errorStart && offset <= errorEnd)
                    return error;
            }
        }

        return null;
    }

    public SemanticHighlight? FindSemanticHighlightAtOffset(int offset)
    {
        if (_errorRenderer == null)
            return null;

        foreach (var highlight in _errorRenderer.SemanticHighlights)
        {
            if (offset >= highlight.StartOffset && offset <= highlight.EndOffset)
                return highlight;
        }

        return null;
    }

    public IReadOnlyList<EditorDiagnosticItem> GetDiagnosticsSnapshot()
    {
        if (_errorRenderer == null || _editor.Document == null)
            return Array.Empty<EditorDiagnosticItem>();

        var document = _editor.Document;
        var items = new List<EditorDiagnosticItem>();

        // 1) 语法验证错误/警告/提示
        foreach (var issue in _errorRenderer.Errors)
        {
            if (issue.Line <= 0 || issue.Line > document.LineCount)
                continue;

            try
            {
                var line = document.GetLineByNumber(issue.Line);
                var startOffset = line.Offset + Math.Max(0, issue.Column - 1);
                if (startOffset < 0 || startOffset >= document.TextLength)
                    continue;

                var length = issue.Length > 0 ? issue.Length : GetWordLength(document, startOffset);
                var endOffset = Math.Min(line.EndOffset, startOffset + Math.Max(1, length));
                endOffset = Math.Max(startOffset + 1, endOffset);

                var kind = issue.Severity switch
                {
                    ValidationSeverity.Error => EditorDiagnosticKind.Error,
                    ValidationSeverity.Warning => EditorDiagnosticKind.Warning,
                    _ => EditorDiagnosticKind.Info
                };

                items.Add(new EditorDiagnosticItem(
                    Kind: kind,
                    Line: issue.Line,
                    Column: Math.Max(1, issue.Column),
                    Message: issue.Message,
                    StartOffset: startOffset,
                    EndOffset: endOffset));
            }
            catch
            {
                // 忽略无效行号/偏移
            }
        }

        // 2) 语义诊断（未定义符号等）
        foreach (var highlight in _errorRenderer.SemanticHighlights)
        {
            // SyntaxError 已由语法验证列表覆盖，避免重复
            if (highlight.Type == SemanticHighlightType.SyntaxError)
                continue;

            if (highlight.StartOffset < 0 || highlight.StartOffset >= document.TextLength)
                continue;

            var endOffset = Math.Min(document.TextLength, Math.Max(highlight.StartOffset + 1, highlight.EndOffset));
            var location = document.GetLocation(highlight.StartOffset);

            if (string.IsNullOrWhiteSpace(highlight.Message))
                continue;

            var kind = highlight.Type switch
            {
                SemanticHighlightType.UndefinedSymbol => EditorDiagnosticKind.UndefinedSymbol,
                SemanticHighlightType.Warning => EditorDiagnosticKind.Warning,
                _ => EditorDiagnosticKind.Info
            };

            items.Add(new EditorDiagnosticItem(
                Kind: kind,
                Line: location.Line,
                Column: location.Column,
                Message: highlight.Message!,
                StartOffset: highlight.StartOffset,
                EndOffset: endOffset));
        }

        // 排序：错误优先，其次行列
        return items
            .OrderBy(i => i.Kind == EditorDiagnosticKind.Error ? 0 :
                          i.Kind == EditorDiagnosticKind.Warning ? 1 :
                          i.Kind == EditorDiagnosticKind.UndefinedSymbol ? 2 : 3)
            .ThenBy(i => i.Line)
            .ThenBy(i => i.Column)
            .ToList();
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_isDisposed) return;

        // 可取消的防抖，避免频繁执行导致卡顿/重复验证
        var requestId = Interlocked.Increment(ref _validationRequestId);

        _validationDebounceCts?.Cancel();
        _validationDebounceCts?.Dispose();

        _validationDebounceCts = new CancellationTokenSource();
        _ = DebouncedValidateAsync(requestId, _validationDebounceCts.Token);
    }

    private async Task DebouncedValidateAsync(int requestId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Logger.Error($"Debounce error: {ex.Message}", ex);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_isDisposed) return;

            // 只执行最后一次请求
            if (requestId != _validationRequestId)
                return;

            ValidateCode();
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _editor.TextChanged -= OnTextChanged;
        }
        catch
        {
            // ignored
        }

        try
        {
            _validationDebounceCts?.Cancel();
            _validationDebounceCts?.Dispose();
            _validationDebounceCts = null;
        }
        catch
        {
            // ignored
        }

        try
        {
            if (_errorRenderer != null)
            {
                _editor.TextArea.TextView.BackgroundRenderers.Remove(_errorRenderer);
                _errorRenderer.Errors.Clear();
                _errorRenderer.SemanticHighlights.Clear();
            }
        }
        catch
        {
            // ignored
        }

        _errorRenderer = null;
        DiagnosticsUpdated = null;
        GC.SuppressFinalize(this);
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
}

public sealed class EditorDiagnosticsSummary
{
    public static EditorDiagnosticsSummary Empty { get; } = new(0, 0, 0, 0);

    public int ErrorCount { get; }
    public int WarningCount { get; }
    public int InfoCount { get; }
    public int UndefinedSymbolCount { get; }

    public EditorDiagnosticsSummary(int ErrorCount, int WarningCount, int InfoCount, int UndefinedSymbolCount)
    {
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.InfoCount = InfoCount;
        this.UndefinedSymbolCount = UndefinedSymbolCount;
    }
}

public enum EditorDiagnosticKind
{
    Error,
    Warning,
    Info,
    UndefinedSymbol
}

public sealed class EditorDiagnosticItem
{
    public EditorDiagnosticKind Kind { get; }
    public int Line { get; }
    public int Column { get; }
    public string Message { get; }
    public int StartOffset { get; }
    public int EndOffset { get; }

    public EditorDiagnosticItem(EditorDiagnosticKind Kind, int Line, int Column, string Message, int StartOffset, int EndOffset)
    {
        this.Kind = Kind;
        this.Line = Line;
        this.Column = Column;
        this.Message = Message;
        this.StartOffset = StartOffset;
        this.EndOffset = EndOffset;
    }
}

/// <summary>
/// 错误标记渲染器 - 在编辑器中显示波浪线
/// </summary>
public class ErrorMarkerRenderer : IBackgroundRenderer
{
    public List<ValidationError> Errors { get; } = new();
    public List<SemanticHighlight> SemanticHighlights { get; } = new();

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView.Document == null)
            return;

        // 1. 绘制验证错误
        if (Errors.Any())
        {
            DrawValidationErrors(textView, drawingContext);
        }

        // 2. 绘制语义高亮
        if (SemanticHighlights.Any())
        {
            DrawSemanticHighlights(textView, drawingContext);
        }
    }

    private void DrawValidationErrors(TextView textView, DrawingContext drawingContext)
    {
        foreach (var error in Errors)
        {
            if (error.Line <= 0 || error.Line > textView.Document.LineCount)
                continue;

            var line = textView.Document.GetLineByNumber(error.Line);
            var startOffset = line.Offset + Math.Max(0, error.Column - 1);
            var length = error.Length > 0 ? error.Length : GetWordLength(textView.Document, startOffset);
            var endOffset = Math.Min(line.EndOffset, startOffset + length);

            if (endOffset <= startOffset || startOffset >= textView.Document.TextLength)
                continue;

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView,
                new AvaloniaEdit.Document.TextSegment { StartOffset = startOffset, EndOffset = endOffset }))
            {
                var brush = error.Severity == ValidationSeverity.Error
                    ? new SolidColorBrush(Color.Parse("#FF5555"))
                    : error.Severity == ValidationSeverity.Warning
                        ? new SolidColorBrush(Color.Parse("#FFA500"))
                        : new SolidColorBrush(Color.Parse("#4FC1FF"));

                DrawWavyLine(drawingContext, brush, rect);
            }
        }
    }

    private void DrawSemanticHighlights(TextView textView, DrawingContext drawingContext)
    {
        foreach (var highlight in SemanticHighlights)
        {
            if (highlight.StartOffset >= textView.Document!.TextLength ||
                highlight.EndOffset > textView.Document.TextLength ||
                highlight.StartOffset >= highlight.EndOffset)
            {
                continue;
            }

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView,
                new AvaloniaEdit.Document.TextSegment { StartOffset = highlight.StartOffset, EndOffset = highlight.EndOffset }))
            {
                IBrush brush;

                switch (highlight.Type)
                {
                    case SemanticHighlightType.SyntaxError:
                        brush = new SolidColorBrush(Color.Parse("#FF5555"));
                        DrawWavyLine(drawingContext, brush, rect);
                        break;

                    case SemanticHighlightType.UndefinedSymbol:
                        brush = new SolidColorBrush(Color.Parse("#808080"));
                        DrawDashedLine(drawingContext, brush, rect);
                        break;

                    case SemanticHighlightType.Warning:
                        brush = new SolidColorBrush(Color.Parse("#FFA500"));
                        DrawWavyLine(drawingContext, brush, rect);
                        break;

                    case SemanticHighlightType.Info:
                        brush = new SolidColorBrush(Color.Parse("#4FC1FF"));
                        DrawDashedLine(drawingContext, brush, rect);
                        break;
                }
            }
        }
    }

    private void DrawWavyLine(DrawingContext dc, IBrush brush, Avalonia.Rect rect)
    {
        var y = rect.Bottom - 1;
        var pen = new Pen(brush, 1.0);
        var x = rect.Left;
        var waveLength = 4.0;
        var amplitude = 1.5;

        var points = new List<Avalonia.Point>();
        while (x < rect.Right)
        {
            var offset = Math.Sin((x - rect.Left) / waveLength * Math.PI) * amplitude;
            points.Add(new Avalonia.Point(x, y + offset));
            x += 1;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            dc.DrawLine(pen, points[i], points[i + 1]);
        }
    }

    private void DrawDashedLine(DrawingContext dc, IBrush brush, Avalonia.Rect rect)
    {
        var y = rect.Bottom - 1;
        var pen = new Pen(brush, 1.0)
        {
            DashStyle = new DashStyle(new double[] { 2, 2 }, 0)
        };

        dc.DrawLine(pen, new Avalonia.Point(rect.Left, y), new Avalonia.Point(rect.Right, y));
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
}

