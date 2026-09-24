using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Prophet.Client.Controls;

/// <summary>
/// ModernDialog 流畅构建器
/// 提供链式 API 来构建对话框
/// </summary>
public class ModernDialogBuilder
{
    private readonly ModernDialog _dialog;
    
    private ModernDialogBuilder()
    {
        _dialog = new ModernDialog();
    }
    
    /// <summary>
    /// 创建新的对话框构建器
    /// </summary>
    public static ModernDialogBuilder Create()
    {
        return new ModernDialogBuilder();
    }
    
    /// <summary>
    /// 设置标题
    /// </summary>
    public ModernDialogBuilder WithTitle(string title)
    {
        _dialog.Title = title;
        return this;
    }
    
    /// <summary>
    /// 设置尺寸
    /// </summary>
    public ModernDialogBuilder WithSize(double width, double height)
    {
        _dialog.Width = width;
        _dialog.Height = height;
        return this;
    }
    
    /// <summary>
    /// 设置宽度
    /// </summary>
    public ModernDialogBuilder WithWidth(double width)
    {
        _dialog.Width = width;
        return this;
    }
    
    /// <summary>
    /// 设置高度
    /// </summary>
    public ModernDialogBuilder WithHeight(double height)
    {
        _dialog.Height = height;
        return this;
    }
    
    /// <summary>
    /// 设置拥有者窗口
    /// </summary>
    public ModernDialogBuilder WithOwner(Window owner)
    {
        // 注意：直接设置Owner属性可能不可访问，使用ShowDialog时传入owner
        // 这里暂时保存引用，在ShowDialog时使用
        // _dialog.Owner = owner;  // 如果Owner setter不可访问，则移除此行
        return this;
    }
    
    /// <summary>
    /// 添加文本内容
    /// </summary>
    public ModernDialogBuilder AddText(string text, double fontSize = 13, bool wrap = true)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            LineHeight = 22,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
        };
        
        _dialog.AddContent(textBlock);
        return this;
    }
    
    /// <summary>
    /// 添加标签
    /// </summary>
    public ModernDialogBuilder AddLabel(string text, double fontSize = 13, Thickness? margin = null)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            Margin = margin ?? new Thickness(0, 0, 0, 6)
        };
        
        _dialog.AddContent(label);
        return this;
    }
    
    /// <summary>
    /// 添加文本框
    /// </summary>
    public ModernDialogBuilder AddTextBox(out TextBox textBox, string placeholder = "", string defaultValue = "")
    {
        textBox = new TextBox
        {
            Text = defaultValue,
            Watermark = placeholder,
            Padding = new Thickness(10, 8),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E42")),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            CornerRadius = new CornerRadius(4)
        };
        
        _dialog.AddContent(textBox);
        return this;
    }
    
    /// <summary>
    /// 添加多行文本框
    /// </summary>
    public ModernDialogBuilder AddMultilineTextBox(out TextBox textBox, string placeholder = "", string defaultValue = "", double height = 80)
    {
        textBox = new TextBox
        {
            Text = defaultValue,
            Watermark = placeholder,
            Height = height,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Padding = new Thickness(10, 8),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E42")),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            CornerRadius = new CornerRadius(4)
        };
        
        _dialog.AddContent(textBox);
        return this;
    }
    
    /// <summary>
    /// 添加下拉框
    /// </summary>
    public ModernDialogBuilder AddComboBox(out ComboBox comboBox, object[] items, int selectedIndex = 0)
    {
        comboBox = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = selectedIndex,
            Background = new SolidColorBrush(Color.Parse("#3C3C3C")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 13
        };
        
        _dialog.AddContent(comboBox);
        return this;
    }
    
    /// <summary>
    /// 添加提示框
    /// </summary>
    public ModernDialogBuilder AddTip(string text)
    {
        var tipBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 16, 0, 0)
        };
        
        var tipText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            Text = text
        };
        
        tipBorder.Child = tipText;
        _dialog.AddContent(tipBorder);
        return this;
    }
    
    /// <summary>
    /// 添加自定义控件
    /// </summary>
    public ModernDialogBuilder AddControl(Control control)
    {
        _dialog.AddContent(control);
        return this;
    }
    
    /// <summary>
    /// 添加取消按钮
    /// </summary>
    public ModernDialogBuilder AddCancelButton(string text = "取消", Action? onClick = null)
    {
        _dialog.AddButton(text, () =>
        {
            onClick?.Invoke();
            _dialog.Close(false);
        }, isPrimary: false);
        
        return this;
    }
    
    /// <summary>
    /// 添加确认按钮
    /// </summary>
    public ModernDialogBuilder AddConfirmButton(string text = "确定", Action? onClick = null, bool closeAfterClick = true)
    {
        _dialog.AddButton(text, () =>
        {
            onClick?.Invoke();
            if (closeAfterClick)
            {
                _dialog.Close(true);
            }
        }, isPrimary: true);
        
        return this;
    }
    
    /// <summary>
    /// 添加自定义按钮
    /// </summary>
    public ModernDialogBuilder AddButton(string text, Action? onClick = null, bool isPrimary = false)
    {
        _dialog.AddButton(text, onClick, isPrimary);
        return this;
    }
    
    /// <summary>
    /// 设置内容间距
    /// </summary>
    public ModernDialogBuilder WithContentSpacing(double spacing)
    {
        _dialog.ContentSpacing = spacing;
        return this;
    }
    
    /// <summary>
    /// 设置按钮间距
    /// </summary>
    public ModernDialogBuilder WithButtonSpacing(double spacing)
    {
        _dialog.ButtonSpacing = spacing;
        return this;
    }
    
    /// <summary>
    /// 构建对话框
    /// </summary>
    public ModernDialog Build()
    {
        return _dialog;
    }
    
    /// <summary>
    /// 构建并显示对话框
    /// </summary>
    public void Show()
    {
        _dialog.Show();
    }
    
    /// <summary>
    /// 构建并以模态方式显示对话框
    /// </summary>
    public async System.Threading.Tasks.Task<bool?> ShowDialog(Window owner)
    {
        return await _dialog.ShowDialog<bool?>(owner);
    }
}

