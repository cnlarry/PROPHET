using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Prophet.Client.Services;

namespace Prophet.Client.Services.Editor.Snippets;

/// <summary>
/// 代码片段补全提供者
/// </summary>
public class SnippetProvider
{
    private readonly SnippetService _snippetService;

    public SnippetProvider()
    {
        _snippetService = SnippetService.Instance;
        var count = _snippetService.GetAllSnippets().Count();
    }

    /// <summary>
    /// 获取代码片段补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetSnippetCompletions(string? prefix = null)
    {
        var snippets = string.IsNullOrEmpty(prefix)
            ? _snippetService.GetAllSnippets()
            : _snippetService.GetSnippetsByPrefix(prefix);

        return snippets.Select(snippet => new CompletionItem
        {
            Label = snippet.Prefix,
            DisplayText = snippet.Label,
            Description = snippet.Description ?? "",
            InsertText = snippet.GetFullText(),
            Kind = CompletionKind.Snippet,
            Category = snippet.Category ?? "代码片段",
            // 标记为代码片段，以便后续处理占位符
            Tag = snippet
        });
    }

    /// <summary>
    /// 检查是否应该触发代码片段补全
    /// </summary>
    public bool ShouldTriggerSnippet(string lineText)
    {
        // 在行首或空白后触发代码片段
        var trimmed = lineText.TrimStart();
        
        // 检查是否有匹配的前缀
        var words = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return false;

        var lastWord = words[^1];
        return _snippetService.GetSnippetsByPrefix(lastWord).Any();
    }

    /// <summary>
    /// 获取指定前缀的代码片段
    /// </summary>
    public Snippet? GetSnippetByPrefix(string prefix)
    {
        return _snippetService.GetSnippetsByPrefix(prefix).FirstOrDefault();
    }

    /// <summary>
    /// 在补全项中标识代码片段类型
    /// </summary>
    public static bool IsSnippetCompletion(CompletionItem item)
    {
        return item.Kind == CompletionKind.Snippet && item.Tag is Snippet;
    }

    /// <summary>
    /// 从补全项中获取代码片段
    /// </summary>
    public static Snippet? GetSnippetFromCompletion(CompletionItem item)
    {
        return item.Tag as Snippet;
    }
}

