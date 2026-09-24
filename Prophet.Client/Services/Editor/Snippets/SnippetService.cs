using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform;

namespace Prophet.Client.Services.Editor.Snippets;

/// <summary>
/// 代码片段服务 - 加载和管理代码片段
/// </summary>
public class SnippetService
{
    private static SnippetService? _instance;
    private SnippetData? _data;

    public static SnippetService Instance => _instance ??= new SnippetService();

    private SnippetService()
    {
        LoadSnippets();
    }

    /// <summary>
    /// 加载代码片段
    /// </summary>
    private void LoadSnippets()
    {
        try
        {
            // 从资源文件加载
            var uri = new Uri("avares://Prophet.Client/Resources/Snippets.json");
            using var stream = AssetLoader.Open(uri);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            
            _data = JsonSerializer.Deserialize<SnippetData>(stream, options);
            
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载代码片段失败: {ex.Message}");
            _data = CreateDefaultSnippets();
        }
    }

    /// <summary>
    /// 创建默认代码片段（如果加载失败）
    /// </summary>
    private SnippetData CreateDefaultSnippets()
    {
        return new SnippetData
        {
            Version = "1.0.0",
            Snippets = new List<Snippet>
            {
                new Snippet
                {
                    Id = "all-block",
                    Prefix = "all",
                    Label = "ALL{} 条件块",
                    Description = "创建 ALL 多条件组合判断块",
                    Category = "信号函数",
                    Body = new List<string>
                    {
                        "ALL {",
                        "    ${1:$(5m).MACD().trend = BULLISH};",
                        "    ${2:$(5m).RSI().value < 70};",
                        "} = ${3:BUY};"
                    }
                }
            }
        };
    }

    /// <summary>
    /// 获取所有代码片段
    /// </summary>
    public IEnumerable<Snippet> GetAllSnippets()
    {
        return _data?.Snippets ?? Enumerable.Empty<Snippet>();
    }

    /// <summary>
    /// 根据前缀获取代码片段
    /// </summary>
    public IEnumerable<Snippet> GetSnippetsByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return GetAllSnippets();

        return GetAllSnippets().Where(s => 
            s.Prefix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 根据ID获取代码片段
    /// </summary>
    public Snippet? GetSnippetById(string id)
    {
        return GetAllSnippets().FirstOrDefault(s => s.Id == id);
    }

    /// <summary>
    /// 根据分类获取代码片段
    /// </summary>
    public IEnumerable<Snippet> GetSnippetsByCategory(string category)
    {
        return GetAllSnippets().Where(s => 
            s.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// 搜索代码片段（模糊匹配）
    /// </summary>
    public IEnumerable<Snippet> SearchSnippets(string query)
    {
        if (string.IsNullOrEmpty(query))
            return GetAllSnippets();

        query = query.ToLowerInvariant();
        
        return GetAllSnippets().Where(s =>
            s.Prefix.ToLowerInvariant().Contains(query) ||
            s.Label.ToLowerInvariant().Contains(query) ||
            s.Description?.ToLowerInvariant().Contains(query) == true);
    }
}

#region Data Models

/// <summary>
/// 代码片段数据容器
/// </summary>
public class SnippetData
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("snippets")]
    public List<Snippet> Snippets { get; set; } = new();
}

/// <summary>
/// 代码片段
/// </summary>
public class Snippet
{
    /// <summary>
    /// 唯一标识符
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// 触发前缀（输入此前缀后触发补全）
    /// </summary>
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = "";

    /// <summary>
    /// 显示标签
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    /// <summary>
    /// 描述信息
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 分类
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>
    /// 代码片段主体（多行）
    /// </summary>
    [JsonPropertyName("body")]
    public List<string> Body { get; set; } = new();

    /// <summary>
    /// 插入文本格式（snippet 或 plaintext）
    /// </summary>
    [JsonPropertyName("insertTextFormat")]
    public string InsertTextFormat { get; set; } = "snippet";

    /// <summary>
    /// 详细文档
    /// </summary>
    [JsonPropertyName("documentation")]
    public string? Documentation { get; set; }

    /// <summary>
    /// 获取完整的代码片段文本
    /// </summary>
    public string GetFullText()
    {
        return string.Join("\n", Body);
    }

    /// <summary>
    /// 获取占位符列表
    /// </summary>
    public List<Placeholder> GetPlaceholders()
    {
        var placeholders = new List<Placeholder>();
        var fullText = GetFullText();
        
        // 正则匹配 ${index:label} 格式的占位符
        var regex = new System.Text.RegularExpressions.Regex(@"\$\{(\d+):([^}]*)\}");
        var matches = regex.Matches(fullText);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var index = int.Parse(match.Groups[1].Value);
            var label = match.Groups[2].Value;
            
            placeholders.Add(new Placeholder
            {
                Index = index,
                Label = label,
                Start = match.Index,
                Length = match.Length
            });
        }
        
        return placeholders.OrderBy(p => p.Index).ToList();
    }
}

/// <summary>
/// 占位符
/// </summary>
public class Placeholder
{
    /// <summary>
    /// 占位符索引（Tab 跳转顺序）
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 占位符标签（默认文本）
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// 在完整文本中的起始位置
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// 占位符长度
    /// </summary>
    public int Length { get; set; }
}

#endregion

