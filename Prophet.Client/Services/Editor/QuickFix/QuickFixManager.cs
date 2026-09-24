using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit;

namespace Prophet.Client.Services.Editor.QuickFix;

/// <summary>
/// 快速修复管理器
/// </summary>
/// <remarks>
/// <para>
/// 负责为编辑器中的错误提供快速修复建议。根据错误类型提供对应的修复方案。
/// </para>
/// <para>
/// 支持的修复类型：
/// <list type="bullet">
/// <item><description>未定义的指标：提供相似指标的建议</description></item>
/// <item><description>未定义的函数：提供相似函数的建议</description></item>
/// <item><description>未定义的变量：提供相似变量的建议</description></item>
/// <item><description>语法错误：提供语法修正建议</description></item>
/// </list>
/// </para>
/// <para>
/// 触发方式：
/// <list type="bullet">
/// <item><description>Ctrl+.: 快捷键触发</description></item>
/// <item><description>右键菜单: 快速修复选项</description></item>
/// </list>
/// </para>
/// </remarks>
public class QuickFixManager
{
    private readonly TextEditor _editor;

    /// <summary>
    /// 初始化 <see cref="QuickFixManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    public QuickFixManager(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// 显示快速修复菜单
    /// </summary>
    /// <param name="error">要修复的错误</param>
    /// <remarks>
    /// <para>
    /// 根据错误类型提供相应的修复建议：
    /// <list type="bullet">
    /// <item><description>未定义符号：查找相似的已定义符号</description></item>
    /// <item><description>拼写错误：使用 Levenshtein 距离算法找到最接近的拼写</description></item>
    /// <item><description>语法错误：提供常见语法修正建议</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 建议列表会以 Popup 形式显示在错误位置附近。
    /// </para>
    /// </remarks>
    public void ShowQuickFix(ValidationError error)
    {
        var fixes = GetQuickFixes(error);
        if (!fixes.Any())
        {
            return;
        }

        var menu = new Avalonia.Controls.ContextMenu();
        foreach (var fix in fixes)
        {
            var item = new Avalonia.Controls.MenuItem { Header = fix.Description };
            item.Click += (s, e) => ApplyQuickFix(fix, error);
            menu.Items.Add(item);
        }

        menu.Open(_editor);
    }

    private List<QuickFix> GetQuickFixes(ValidationError error)
    {
        var fixes = new List<QuickFix>();

        // 指标引用缺少时间框架
        if (error.Message.Contains("缺少时间框架参数"))
        {
            fixes.Add(new QuickFix
            {
                Description = "添加时间框架 (5m)",
                Action = () => InsertTextAtError(error, "(5m)", after: true)
            });
            fixes.Add(new QuickFix
            {
                Description = "添加时间框架 (1h)",
                Action = () => InsertTextAtError(error, "(1h)", after: true)
            });
        }

        // 拼写错误
        if (error.Message.Contains("拼写错误") || error.Message.Contains("未知的指标"))
        {
            var match = Regex.Match(error.Message, @"'(\w+)'.*'(\w+)'");
            if (match.Success)
            {
                var wrong = match.Groups[1].Value;
                var correct = match.Groups[2].Value;
                fixes.Add(new QuickFix
                {
                    Description = $"更正为 '{correct}'",
                    Action = () => ReplaceTextAtError(error, correct)
                });
            }
        }

        // 括号不匹配
        if (error.Message.Contains("括号不匹配"))
        {
            var line = _editor.Document.GetLineByNumber(error.Line);
            var lineText = _editor.Document.GetText(line);
            var openParens = lineText.Count(c => c == '(');
            var closeParens = lineText.Count(c => c == ')');

            if (openParens > closeParens)
            {
                fixes.Add(new QuickFix
                {
                    Description = $"添加 {openParens - closeParens} 个右括号",
                    Action = () => AppendToLine(error.Line, new string(')', openParens - closeParens))
                });
            }
        }

        return fixes;
    }

    private void ApplyQuickFix(QuickFix fix, ValidationError error)
    {
        try
        {
            fix.Action?.Invoke();
            Console.WriteLine("[QuickFixManager] Fix applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuickFixManager] Apply error: {ex.Message}");
        }
    }

    private void InsertTextAtError(ValidationError error, string text, bool after)
    {
        var line = _editor.Document.GetLineByNumber(error.Line);
        var offset = line.Offset + error.Column - 1;

        if (after && error.Length > 0)
        {
            offset += error.Length;
        }

        _editor.Document.Insert(offset, text);
    }

    private void ReplaceTextAtError(ValidationError error, string newText)
    {
        var line = _editor.Document.GetLineByNumber(error.Line);
        var startOffset = line.Offset + error.Column - 1;
        var length = error.Length > 0 ? error.Length : GetWordLength(_editor.Document, startOffset);

        _editor.Document.Replace(startOffset, length, newText);
    }

    private void AppendToLine(int lineNumber, string text)
    {
        var line = _editor.Document.GetLineByNumber(lineNumber);
        _editor.Document.Insert(line.EndOffset, text);
    }

    private int GetWordLength(AvaloniaEdit.Document.TextDocument document, int offset)
    {
        if (offset >= document.TextLength)
            return 1;

        int length = 0;
        while (offset + length < document.TextLength)
        {
            var ch = document.GetCharAt(offset + length);
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                break;
            length++;
        }

        return Math.Max(1, length);
    }
}

/// <summary>
/// 快速修复定义
/// </summary>
public class QuickFix
{
    public string Description { get; set; } = string.Empty;
    public Action? Action { get; set; }
}

