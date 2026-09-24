using System;
using System.IO;
using System.Xml;
using Avalonia.Platform;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Prophet.Client.Services.Editor;

/// <summary>
/// 语法高亮管理器
/// </summary>
/// <remarks>
/// 负责加载和应用 Prophet DSL 的语法高亮定义。
/// 从嵌入资源中读取 XSHD (XML Syntax Highlighting Definition) 文件并应用到编辑器。
/// </remarks>
public class SyntaxHighlightingManager
{
    private readonly TextEditor _editor;

    /// <summary>
    /// 初始化 <see cref="SyntaxHighlightingManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">要应用语法高亮的文本编辑器实例</param>
    public SyntaxHighlightingManager(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// 初始化语法高亮功能
    /// </summary>
    /// <remarks>
    /// 从嵌入资源 "avares://Prophet.Client/Resources/StrategyDSL.xshd" 加载语法定义，
    /// 并应用到编辑器。如果加载失败，会在控制台输出错误信息但不会抛出异常。
    /// </remarks>
    public void Initialize()
    {
        try
        {
            // 从嵌入资源加载 XSHD 语法定义
            var uri = new Uri("avares://Prophet.Client/Resources/StrategyDSL.xshd");
            
            using var stream = AssetLoader.Open(uri);
            using var reader = new XmlTextReader(stream);
            
            var xshd = HighlightingLoader.LoadXshd(reader);
            var highlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            
            _editor.SyntaxHighlighting = highlighting;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SyntaxHighlighting] Initialization failed: {ex.Message}");
        }
    }
}

