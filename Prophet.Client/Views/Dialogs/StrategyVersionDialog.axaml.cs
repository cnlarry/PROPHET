using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Prophet.Client.ViewModels;
using System.Threading.Tasks;

namespace Prophet.Client.Views.Dialogs;

public partial class StrategyVersionDialog : Window
{
    private readonly StrategyVersionViewModel _viewModel;

    public StrategyVersionDialog()
    {
        // 设计时构造函数
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        _viewModel = new StrategyVersionViewModel();
        DataContext = _viewModel;
    }

    public StrategyVersionDialog(string strategyId) : this()
    {
        _viewModel = new StrategyVersionViewModel();
        _viewModel.StrategyId = strategyId;
        DataContext = _viewModel;
        
        // 加载版本列表
        Loaded += async (s, e) => await _viewModel.LoadVersionsAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnCreateVersion(object? sender, RoutedEventArgs e)
    {
        var dialog = new CreateVersionDialog();
        var result = await dialog.ShowDialog<CreateVersionResult?>(this);
        
        if (result != null)
        {
            var success = await _viewModel.CreateVersionAsync(
                result.ChangeType,
                result.Dsl,
                result.ChangeDescription,
                result.BreakingChanges);

            if (success)
            {
                // 可以显示成功消息
            }
        }
    }

    private async void OnRefresh(object? sender, RoutedEventArgs e)
    {
        await _viewModel.LoadVersionsAsync();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class CreateVersionResult
{
    public string ChangeType { get; set; } = string.Empty;
    public string Dsl { get; set; } = string.Empty;
    public string? ChangeDescription { get; set; }
    public string? BreakingChanges { get; set; }
}

