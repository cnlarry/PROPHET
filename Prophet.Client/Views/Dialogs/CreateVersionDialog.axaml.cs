using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Prophet.Client.Views.Dialogs;

public partial class CreateVersionDialog : Window
{
    public string Dsl { get; set; } = string.Empty;

    public CreateVersionDialog()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        // 监听变更类型变化
        var changeTypeComboBox = this.FindControl<ComboBox>("ChangeTypeComboBox");
        if (changeTypeComboBox != null)
        {
            changeTypeComboBox.SelectionChanged += OnChangeTypeChanged;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnChangeTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        var comboBox = sender as ComboBox;
        var breakingChangesPanel = this.FindControl<StackPanel>("BreakingChangesPanel");
        
        if (comboBox != null && breakingChangesPanel != null)
        {
            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            var tag = selectedItem?.Tag as string;
            breakingChangesPanel.IsVisible = tag == "major";
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnCreate(object? sender, RoutedEventArgs e)
    {
        var changeTypeComboBox = this.FindControl<ComboBox>("ChangeTypeComboBox");
        var changeDescriptionTextBox = this.FindControl<TextBox>("ChangeDescriptionTextBox");
        var breakingChangesTextBox = this.FindControl<TextBox>("BreakingChangesTextBox");

        if (changeTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
        {
            var changeType = selectedItem.Tag as string ?? "patch";
            var changeDescription = changeDescriptionTextBox?.Text;
            var breakingChanges = breakingChangesTextBox?.Text;

            // 验证：Major版本必须填写破坏性变更
            if (changeType == "major" && string.IsNullOrWhiteSpace(breakingChanges))
            {
                // TODO: 显示错误提示
                return;
            }

            Close(new CreateVersionResult
            {
                ChangeType = changeType,
                Dsl = Dsl,
                ChangeDescription = changeDescription,
                BreakingChanges = breakingChanges
            });
        }
    }
}

