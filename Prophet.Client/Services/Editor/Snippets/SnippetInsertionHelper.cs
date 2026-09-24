using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace Prophet.Client.Services.Editor.Snippets;

/// <summary>
/// 代码片段插入助手 - 处理插入和占位符跳转
/// </summary>
public class SnippetInsertionHelper
{
    private readonly TextEditor _editor;
    private SnippetSession? _currentSession;

    public SnippetInsertionHelper(TextEditor editor)
    {
        _editor = editor;
        
        // 监听 Tab 键用于占位符跳转
        _editor.TextArea.KeyDown += OnKeyDown;
    }

    /// <summary>
    /// 插入代码片段
    /// </summary>
    public void InsertSnippet(Snippet snippet, int offset)
    {
        try
        {
            var document = _editor.Document;
            
            // 获取插入文本
            var text = snippet.GetFullText();
            
            // 删除触发前缀（如果存在）
            var wordStart = FindWordStart(offset);
            if (wordStart < offset)
            {
                document.Remove(wordStart, offset - wordStart);
                offset = wordStart;
            }
            
            // 解析占位符
            var placeholders = ParsePlaceholders(text);
            
            // 替换占位符为默认文本
            var insertText = ReplacePlaceholdersWithDefaults(text, placeholders);
            
            // 插入文本
            document.Insert(offset, insertText);
            
            // 如果有占位符，创建会话并选中第一个
            if (placeholders.Count > 0)
            {
                _currentSession = new SnippetSession
                {
                    StartOffset = offset,
                    Placeholders = AdjustPlaceholderOffsets(placeholders, offset),
                    CurrentPlaceholderIndex = 0
                };
                
                SelectCurrentPlaceholder();
            }
            else
            {
                // 无占位符，将光标移到末尾
                _editor.CaretOffset = offset + insertText.Length;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"代码片段插入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析占位符
    /// </summary>
    private List<ParsedPlaceholder> ParsePlaceholders(string text)
    {
        var placeholders = new List<ParsedPlaceholder>();
        var regex = new Regex(@"\$\{(\d+):([^}]*)\}");
        var matches = regex.Matches(text);
        
        foreach (Match match in matches)
        {
            var index = int.Parse(match.Groups[1].Value);
            var label = match.Groups[2].Value;
            
            placeholders.Add(new ParsedPlaceholder
            {
                Index = index,
                Label = label,
                OriginalStart = match.Index,
                OriginalLength = match.Length
            });
        }
        
        return placeholders.OrderBy(p => p.Index).ToList();
    }

    /// <summary>
    /// 替换占位符为默认文本
    /// </summary>
    private string ReplacePlaceholdersWithDefaults(string text, List<ParsedPlaceholder> placeholders)
    {
        var result = text;
        
        // 从后往前替换，避免位置偏移
        foreach (var placeholder in placeholders.OrderByDescending(p => p.OriginalStart))
        {
            var pattern = $"${{{placeholder.Index}:{Regex.Escape(placeholder.Label)}}}";
            result = result.Remove(placeholder.OriginalStart, placeholder.OriginalLength);
            result = result.Insert(placeholder.OriginalStart, placeholder.Label);
        }
        
        return result;
    }

    /// <summary>
    /// 调整占位符偏移量（插入后的实际位置）
    /// </summary>
    private List<ParsedPlaceholder> AdjustPlaceholderOffsets(
        List<ParsedPlaceholder> placeholders, int baseOffset)
    {
        var offset = 0;
        
        foreach (var placeholder in placeholders.OrderBy(p => p.OriginalStart))
        {
            // 计算实际偏移（考虑前面占位符的长度变化）
            var lengthDiff = placeholder.Label.Length - placeholder.OriginalLength;
            
            placeholder.ActualStart = baseOffset + placeholder.OriginalStart + offset;
            placeholder.ActualLength = placeholder.Label.Length;
            
            offset += lengthDiff;
        }
        
        return placeholders;
    }

    /// <summary>
    /// 选中当前占位符
    /// </summary>
    private void SelectCurrentPlaceholder()
    {
        if (_currentSession == null || 
            _currentSession.CurrentPlaceholderIndex >= _currentSession.Placeholders.Count)
        {
            EndSession();
            return;
        }
        
        var placeholder = _currentSession.Placeholders[_currentSession.CurrentPlaceholderIndex];
        
        // 选中占位符文本
        _editor.Select(placeholder.ActualStart, placeholder.ActualLength);
        _editor.ScrollToLine(_editor.Document.GetLineByOffset(placeholder.ActualStart).LineNumber);
    }

    /// <summary>
    /// 跳转到下一个占位符
    /// </summary>
    public void JumpToNextPlaceholder()
    {
        if (_currentSession == null)
            return;
        
        _currentSession.CurrentPlaceholderIndex++;
        
        if (_currentSession.CurrentPlaceholderIndex >= _currentSession.Placeholders.Count)
        {
            // 所有占位符已处理完毕，结束会话
            EndSession();
        }
        else
        {
            SelectCurrentPlaceholder();
        }
    }

    /// <summary>
    /// 跳转到上一个占位符
    /// </summary>
    public void JumpToPreviousPlaceholder()
    {
        if (_currentSession == null || _currentSession.CurrentPlaceholderIndex <= 0)
            return;
        
        _currentSession.CurrentPlaceholderIndex--;
        SelectCurrentPlaceholder();
    }

    /// <summary>
    /// 结束代码片段会话
    /// </summary>
    private void EndSession()
    {
        if (_currentSession == null)
            return;
        
        // 将光标移到代码片段末尾
        var lastPlaceholder = _currentSession.Placeholders.LastOrDefault();
        if (lastPlaceholder != null)
        {
            _editor.CaretOffset = lastPlaceholder.ActualStart + lastPlaceholder.ActualLength;
        }
        
        _currentSession = null;
    }

    /// <summary>
    /// 键盘事件处理
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_currentSession == null)
            return;
        
        // Tab - 下一个占位符
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None)
        {
            JumpToNextPlaceholder();
            e.Handled = true;
        }
        // Shift+Tab - 上一个占位符
        else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift)
        {
            JumpToPreviousPlaceholder();
            e.Handled = true;
        }
        // Escape - 退出代码片段模式
        else if (e.Key == Key.Escape)
        {
            EndSession();
            e.Handled = true;
        }
        // 其他键 - 如果修改了文本，结束会话
        else if (IsEditingKey(e.Key))
        {
            // 允许用户编辑当前占位符，但不跟踪后续占位符
            // 可以选择在编辑时结束会话，或者继续保持会话
        }
    }

    /// <summary>
    /// 判断是否是编辑键
    /// </summary>
    private bool IsEditingKey(Key key)
    {
        return key == Key.Back || 
               key == Key.Delete || 
               key == Key.Enter ||
               (key >= Key.A && key <= Key.Z) ||
               (key >= Key.D0 && key <= Key.D9);
    }

    /// <summary>
    /// 查找单词起始位置
    /// </summary>
    private int FindWordStart(int offset)
    {
        var document = _editor.Document;
        int start = offset;
        
        while (start > 0)
        {
            var ch = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-')
                break;
            start--;
        }
        
        return start;
    }

    /// <summary>
    /// 检查是否有活动的代码片段会话
    /// </summary>
    public bool HasActiveSession => _currentSession != null;
}

#region Helper Classes

/// <summary>
/// 代码片段会话（跟踪当前插入的代码片段状态）
/// </summary>
internal class SnippetSession
{
    public int StartOffset { get; set; }
    public List<ParsedPlaceholder> Placeholders { get; set; } = new();
    public int CurrentPlaceholderIndex { get; set; }
}

/// <summary>
/// 解析后的占位符
/// </summary>
internal class ParsedPlaceholder
{
    public int Index { get; set; }
    public string Label { get; set; } = "";
    public int OriginalStart { get; set; }
    public int OriginalLength { get; set; }
    public int ActualStart { get; set; }
    public int ActualLength { get; set; }
}

#endregion

