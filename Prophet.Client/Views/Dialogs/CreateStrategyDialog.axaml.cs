using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Prophet.Client.Views.Dialogs;

public partial class CreateStrategyDialog : Window
{
    public string StrategyName { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = "BTCUSDT";
    public string? Remarks { get; private set; }

    public CreateStrategyDialog()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnCreate(object? sender, RoutedEventArgs e)
    {
        var nameTextBox = this.FindControl<TextBox>("NameTextBox");
        var symbolComboBox = this.FindControl<ComboBox>("SymbolComboBox");
        var remarksTextBox = this.FindControl<TextBox>("RemarksTextBox");

        var name = nameTextBox?.Text?.Trim();
        
        if (string.IsNullOrEmpty(name))
        {
            // TODO: 显示错误提示
            return;
        }

        StrategyName = name;
        
        if (symbolComboBox?.SelectedItem is ComboBoxItem selectedItem)
        {
            Symbol = selectedItem.Content?.ToString() ?? "BTCUSDT";
        }
        
        Remarks = remarksTextBox?.Text;

        Close(this);
    }
}

