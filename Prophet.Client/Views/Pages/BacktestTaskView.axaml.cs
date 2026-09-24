using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Backtest.Storage;
using Prophet.Client.Controls;
using Prophet.Client.Models;
using Prophet.Client.ViewModels;
using Prophet.Client.Views;
using Prophet.Client.Views.Dialogs;

namespace Prophet.Client.Views.Pages;

public partial class BacktestTaskView : UserControl
{
    private DataGrid? _signalsDataGrid;
    private DataGrid? _ordersDataGrid;

    private BacktestSessionViewModel? _attachedViewModel;

    public BacktestTaskView()
    {
        InitializeComponent();
        
        // 监听信号和订单集合变化，更新颜色
        Loaded += OnViewLoaded;
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        // 获取DataGrid控件引用并添加双击事件
        InitializeDataGrids();

        // 首次加载时绑定一次
        AttachViewModel();
    }

    private void InitializeDataGrids()
    {
        // 查找信号DataGrid
        _signalsDataGrid = this.FindControl<DataGrid>("SignalsDataGrid");
        if (_signalsDataGrid != null)
        {
            _signalsDataGrid.DoubleTapped += OnSignalCellDoubleTapped;
            
            // 注意：Avalonia DataGrid 的排序由用户点击列头触发
            // 默认排序已在 ViewModel 的 RecordSignal 方法中实现（按时间倒序插入）
        }

        // 查找订单DataGrid
        _ordersDataGrid = this.FindControl<DataGrid>("OrdersDataGrid");
        if (_ordersDataGrid != null)
        {
            _ordersDataGrid.DoubleTapped += OnOrderCellDoubleTapped;
        }
    }

    private void AttachViewModel()
    {
        if (_attachedViewModel != null)
        {
            _attachedViewModel.Signals.CollectionChanged -= OnSignalsCollectionChanged;
            _attachedViewModel.Orders.CollectionChanged -= OnOrdersCollectionChanged;
        }

        _attachedViewModel = DataContext as BacktestSessionViewModel;
        if (_attachedViewModel != null)
        {
            _attachedViewModel.Signals.CollectionChanged += OnSignalsCollectionChanged;
            _attachedViewModel.Orders.CollectionChanged += OnOrdersCollectionChanged;
        }
    }

    private void OnSignalsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateSignalRowColors();
    }

    private void OnOrdersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateOrderRowColors();
    }

    private async void OnSignalCellDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && e.Source is Control source)
        {
            // 获取被点击的单元格
            var cell = source.FindAncestorOfType<DataGridCell>();
            if (cell != null && cell.Content is TextBlock textBlock)
            {
                var cellValue = textBlock.Text ?? string.Empty;
                
                // 复制到剪贴板
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(cellValue);
                    Console.WriteLine($"✅ 已复制到剪贴板: {cellValue}");
                }
            }
        }
    }

    private async void OnOrderCellDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && e.Source is Control source)
        {
            // 获取被点击的单元格
            var cell = source.FindAncestorOfType<DataGridCell>();
            if (cell != null && cell.Content is TextBlock textBlock)
            {
                var cellValue = textBlock.Text ?? string.Empty;
                
                // 复制到剪贴板
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(cellValue);
                    Console.WriteLine($"✅ 已复制到剪贴板: {cellValue}");
                }
            }
        }
    }

    private void UpdateSignalRowColors()
    {
        // 可以通过代码后置更新颜色，但暂时使用默认颜色
    }

    private void UpdateOrderRowColors()
    {
        // 可以通过代码后置更新颜色，但暂时使用默认颜色
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        // 返回回测主界面
        try
        {
            var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
            if (mainWindow == null)
            {
                Console.WriteLine("⚠️ 无法找到主窗口");
                return;
            }

            // 隐藏任务界面
            var taskView = mainWindow.FindControl<Grid>("ViewBacktestTask");
            if (taskView != null)
            {
                taskView.IsVisible = false;
            }

            // 显示回测主界面
            var backtestView = mainWindow.FindControl<Grid>("ViewBacktest");
            if (backtestView != null)
            {
                backtestView.IsVisible = true;
            }

            Console.WriteLine("✅ 返回回测主界面");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 返回主界面失败: {ex.Message}");
            Console.WriteLine(ex);
        }
    }

    private void OnClearLogsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BacktestSessionViewModel viewModel)
        {
            viewModel.Logs.Clear();
            Console.WriteLine("清空日志");
        }
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BacktestSessionViewModel viewModel || viewModel.Result == null)
        {
            return;
        }

        var backtestId = viewModel.Result.BacktestId;
        var strategyName = viewModel.StrategyName;

        try
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null)
            {
                Console.WriteLine("⚠️ 无法找到父窗口");
                return;
            }

            // 显示确认对话框（自定义大小以确保所有信息都能显示）
            var dialog = ModernDialogBuilder.Create()
                .WithTitle("删除回测记录")
                .WithSize(480, 235)  // 增加宽度和高度
                .AddText($"确定要删除回测记录「{strategyName}」吗？\n\n此操作不可恢复，将删除所有相关数据（订单、信号、权益曲线等）。")
                .Build();
            
            dialog.AddButton("取消", () =>
            {
                dialog.Close(false);
            });
            
            dialog.AddButton("确定", async () =>
            {
                try
                {
                    // 删除数据库记录（级联删除）
                    var storage = new LocalBacktestStorage();
                    await storage.DeleteBacktestAsync(backtestId);

                    Console.WriteLine($"✅ 已删除回测记录: {backtestId}");

                    // 刷新回测历史列表
                    var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
                    if (mainWindow != null)
                    {
                        var backtestView = mainWindow.FindControl<Grid>("ViewBacktest")
                            ?.FindDescendantOfType<BacktestView>();
                        if (backtestView?.DataContext is BacktestViewModel backtestViewModel)
                        {
                            // 重新加载历史记录（在UI线程上执行）
                            if (backtestViewModel.LoadHistoryCommand.CanExecute(null))
                            {
                                backtestViewModel.LoadHistoryCommand.Execute(null);
                            }
                        }
                    }

                    // 返回回测主界面
                    OnBackClicked(sender, e);
                    dialog.Close(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 删除回测记录失败: {ex.Message}");
                    Console.WriteLine(ex);

                    // 显示错误对话框
                    var errorDialog = ModernDialogPresets.Error(
                        "删除失败",
                        $"删除回测记录时发生错误：\n{ex.Message}"
                    );
                    await errorDialog.ShowDialog(window);
                }
            }, isPrimary: true);

            await dialog.ShowDialog(window);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 显示删除确认对话框失败: {ex.Message}");
            Console.WriteLine(ex);
        }
    }
    
    /// <summary>
    /// 让AI分析回测结果（弹窗）
    /// </summary>
    private async void OnAskAiToAnalyze(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BacktestSessionViewModel viewModel || viewModel.Result == null)
        {
            Console.WriteLine("❌ 没有回测结果，无法进行AI分析");
            return;
        }

        try
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                Console.WriteLine("⚠️ 无法找到父窗口");
                return;
            }

            var backtestViewModel = TryGetBacktestViewModel();
            var dialog = new BacktestAiAnalysisDialog(viewModel, backtestViewModel);
            var newSession = await dialog.ShowDialog<BacktestSessionViewModel?>(owner);

            if (newSession != null)
            {
                DataContext = newSession;
                Console.WriteLine($"✅ 已自动切换到新回测任务: {newSession.SessionTitle}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 打开AI分析弹窗失败: {ex.Message}");
        }
    }

    private BacktestViewModel? TryGetBacktestViewModel()
    {
        try
        {
            var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
            var backtestView = mainWindow?.FindControl<Grid>("ViewBacktest")
                ?.FindDescendantOfType<BacktestView>();

            return backtestView?.DataContext as BacktestViewModel;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 扩展方法：查找祖先控件
/// </summary>
public static class ControlExtensions
{
    public static T? FindAncestorOfType<T>(this Control control) where T : Control
    {
        var parent = control.Parent;
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            
            if (parent is Control parentControl)
                parent = parentControl.Parent;
            else
                break;
        }
        return null;
    }
}

