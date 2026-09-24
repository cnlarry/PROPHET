using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace Prophet.Client.Views;

public partial class EditorTestWindow : Window
{
    private TextEditor? _textEditor;
    
    public EditorTestWindow()
    {
        InitializeComponent();
        
        Console.WriteLine("=== EditorTestWindow 构造函数 ===");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        // 使用 FindControl 查找编辑器（与示例代码一致）
        _textEditor = this.FindControl<TextEditor>("TestEditor");
        
        Console.WriteLine($"FindControl 结果: {(_textEditor != null ? "✓ 找到" : "✗ 未找到")}");
        
        if (_textEditor != null)
        {
            try
            {
                // 设置编辑器属性（参考示例代码）
                _textEditor.ShowLineNumbers = true;
                _textEditor.Background = Brushes.Transparent;
                _textEditor.Options.ConvertTabsToSpaces = true;
                _textEditor.Options.IndentationSize = 4;
                _textEditor.Options.EnableTextDragDrop = true;
                _textEditor.Options.HighlightCurrentLine = true;
                
                // 关键：创建 TextDocument 对象（与示例代码一致）
                _textEditor.Document = new TextDocument(@"// AvaloniaEdit 测试成功！
// 这是一个测试窗口

function test() {
    console.log('Hello, AvaloniaEdit!');
    return 42;
}

// 你可以编辑这段代码
// 尝试：
// 1. 输入一些文本
// 2. 使用 Ctrl+Z 撤销
// 3. 使用 Ctrl+Y 重做
// 4. 选择文本并复制/粘贴
");
                
                Console.WriteLine($"✓ 编辑器初始化完成，Document 长度: {_textEditor.Document.TextLength}");
                UpdateStatus($"✓ 编辑器加载成功 (文档长度: {_textEditor.Document.TextLength} 字符)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 初始化失败: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
                UpdateStatus($"✗ 初始化失败: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("✗ _textEditor 为 null！");
            UpdateStatus("✗ 编辑器未找到");
        }
    }

    private void OnSetSampleCode(object? sender, RoutedEventArgs e)
    {
        if (_textEditor != null)
        {
            _textEditor.Document = new TextDocument(@"// Prophet DSL 策略示例

// 策略参数
param FAST_PERIOD = 5;
param SLOW_PERIOD = 20;

// 初始化函数
function OnInit() {
    print(""策略初始化"");
}

// 每个K线触发
function OnTick(bar) {
    // 计算移动平均线
    var fast_ma = MA(close, FAST_PERIOD);
    var slow_ma = MA(close, SLOW_PERIOD);
    
    // 交易信号
    if (fast_ma > slow_ma) {
        Buy(100);  // 买入
    } else if (fast_ma < slow_ma) {
        Sell(100);  // 卖出
    }
}
");
            UpdateStatus($"✓ 已设置示例代码 ({_textEditor.Document.TextLength} 字符)");
        }
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        if (_textEditor != null)
        {
            _textEditor.Clear();
            UpdateStatus("✓ 已清空编辑器");
        }
    }

    private void OnToggleLineNumbers(object? sender, RoutedEventArgs e)
    {
        if (_textEditor != null)
        {
            _textEditor.ShowLineNumbers = !_textEditor.ShowLineNumbers;
            UpdateStatus($"行号显示: {(_textEditor.ShowLineNumbers ? "开" : "关")}");
        }
    }

    private void UpdateStatus(string message)
    {
        var statusText = this.FindControl<TextBlock>("StatusText");
        if (statusText != null)
        {
            statusText.Text = message;
        }
        Console.WriteLine($"状态: {message}");
    }
}

