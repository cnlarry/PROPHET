using System;
using Avalonia.Input;
using AvaloniaEdit;

namespace Prophet.Client.Services.Editor.Menu;

/// <summary>
/// 右键菜单管理器
/// </summary>
/// <remarks>
/// <para>
/// 负责编辑器的右键上下文菜单。提供基本的编辑操作。
/// </para>
/// <para>
/// 支持的操作：
/// <list type="bullet">
/// <item><description>剪切 (Ctrl+X)</description></item>
/// <item><description>复制 (Ctrl+C)</description></item>
/// <item><description>粘贴 (Ctrl+V)</description></item>
/// <item><description>全选 (Ctrl+A)</description></item>
/// </list>
/// </para>
/// </remarks>
public class ContextMenuManager
{
    private readonly TextEditor _editor;

    /// <summary>
    /// 初始化 <see cref="ContextMenuManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    public ContextMenuManager(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// 初始化右键菜单
    /// </summary>
    /// <remarks>
    /// 注册 KeyDown 事件来处理常见的编辑快捷键（Ctrl+X, Ctrl+C, Ctrl+V, Ctrl+A）。
    /// </remarks>
    public void Initialize()
    {
        var contextMenu = new Avalonia.Controls.ContextMenu();

        // 剪切
        var cutItem = new Avalonia.Controls.MenuItem
        {
            Header = "剪切",
            InputGesture = new KeyGesture(Key.X, KeyModifiers.Control)
        };
        cutItem.Click += (s, e) => Cut();
        contextMenu.Items.Add(cutItem);

        // 复制
        var copyItem = new Avalonia.Controls.MenuItem
        {
            Header = "复制",
            InputGesture = new KeyGesture(Key.C, KeyModifiers.Control)
        };
        copyItem.Click += (s, e) => Copy();
        contextMenu.Items.Add(copyItem);

        // 粘贴
        var pasteItem = new Avalonia.Controls.MenuItem
        {
            Header = "粘贴",
            InputGesture = new KeyGesture(Key.V, KeyModifiers.Control)
        };
        pasteItem.Click += (s, e) => Paste();
        contextMenu.Items.Add(pasteItem);

        // 分隔符
        contextMenu.Items.Add(new Avalonia.Controls.Separator());

        // 全选
        var selectAllItem = new Avalonia.Controls.MenuItem
        {
            Header = "全选",
            InputGesture = new KeyGesture(Key.A, KeyModifiers.Control)
        };
        selectAllItem.Click += (s, e) => SelectAll();
        contextMenu.Items.Add(selectAllItem);

        _editor.ContextMenu = contextMenu;
    }

    private async void Cut()
    {
        if (_editor.SelectionLength > 0)
        {
            var text = _editor.SelectedText;
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(_editor);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
            _editor.Document.Replace(_editor.SelectionStart, _editor.SelectionLength, "");
        }
    }

    private async void Copy()
    {
        if (_editor.SelectionLength > 0)
        {
            var text = _editor.SelectedText;
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(_editor);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }
    }

    private async void Paste()
    {
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(_editor);
        if (topLevel?.Clipboard != null)
        {
            var text = await topLevel.Clipboard.GetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                var offset = _editor.CaretOffset;
                _editor.Document.Insert(offset, text);
            }
        }
    }

    private void SelectAll()
    {
        _editor.SelectAll();
    }
}

