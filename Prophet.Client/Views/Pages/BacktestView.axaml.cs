using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Backtest.Storage;
using Prophet.Client.Models;
using Prophet.Client.Core;
using Prophet.Client.ViewModels;
using Prophet.Client.Views.Dialogs;

namespace Prophet.Client.Views.Pages;

public partial class BacktestView : UserControl
{
    public BacktestView()
    {
        InitializeComponent();
        
        // 在视图加载完成后加载策略列表
        Loaded += OnViewLoaded;
    }
    
    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BacktestViewModel viewModel)
        {
            Console.WriteLine("❌ [BacktestView] DataContext 不是 BacktestViewModel");
            return;
        }

        // ✅ 让AI操作服务能在“回测页面”直接触发回测（接受AI优化后立即回测）
        try
        {
            ServiceContainer.GetService<Services.AiOperationService>().SetBacktestViewModel(viewModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [BacktestView] 绑定 AiOperationService 失败: {ex.Message}");
        }

        // 注意：历史记录已经在BacktestViewModel构造函数中异步加载，这里不需要重复加载
        // 但如果集合为空，可能是加载失败，可以尝试重新加载
        if (viewModel.BacktestHistory.Count == 0)
        {
            if (viewModel.LoadHistoryCommand.CanExecute(null))
            {
                viewModel.LoadHistoryCommand.Execute(null);
            }
        }

        // 策略列表也已经在构造函数中加载，但可以在这里确保加载
        if (viewModel.Strategies.Count == 0)
        {
            if (viewModel.LoadStrategiesCommand.CanExecute(null))
            {
                viewModel.LoadStrategiesCommand.Execute(null);
            }
        }
    }

    private async void OnCreateBacktestClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BacktestViewModel viewModel)
        {
            return;
        }

        try
        {
            // 使用新的分步向导窗口
            var dialog = new StepWizardBacktestConfigWindow();

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                Console.WriteLine("⚠️ 无法找到父窗口，取消打开配置窗口");
                return;
            }

            var request = await dialog.ShowDialog<BacktestConfigRequest?>(owner);
            if (request != null)
            {
                await viewModel.StartBacktestAsync(request);
                
                // 启动回测后，直接进入回测任务界面
                if (viewModel.Sessions.Count > 0)
                {
                    var latestSession = viewModel.Sessions[0]; // 最新创建的session在索引0
                    NavigateToTaskView(latestSession);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 打开回测配置窗口失败: {ex.Message}");
            Console.WriteLine(ex);
        }
    }

    private async void OnBacktestCardClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not BacktestResult result)
        {
            return;
        }

        try
        {
            // 从数据库加载完整的回测数据
            var storage = new LocalBacktestStorage();
            var fullResult = await storage.GetBacktestResultAsync(result.BacktestId);
            
            if (fullResult == null)
            {
                Console.WriteLine($"⚠️ 未找到回测记录: {result.BacktestId}");
                return;
            }

            // 创建策略信息和版本信息
            var strategy = new StrategyInfo
            {
                Id = fullResult.StrategyId,
                Name = fullResult.StrategyName
            };
            
            var version = new VersionListItem
            {
                VersionString = fullResult.VersionString ?? "历史记录"
            };

            // 从BacktestResult重建BacktestConfig（因为Config可能为null）
            var config = new BacktestConfig
            {
                Symbol = fullResult.Symbol,
                Interval = fullResult.Interval,
                StartDate = fullResult.StartTime,
                EndDate = fullResult.EndTime,
                InitialCapital = fullResult.InitialCapital,
                SignalSamplingInterval = fullResult.Interval, // 使用时间框架作为默认值
                Leverage = 10m, // 默认值
                PositionSizePercent = 0.05m, // 默认值
                TakerFeeRate = 0.001m, // 默认值
                SlippageRate = 0.0005m, // 默认值
                AllowShort = true, // 默认值
                Parameters = new Dictionary<string, object>()
            };

            // 创建SessionViewModel并加载数据
            var sessionFactory = ServiceContainer.GetService<Services.BacktestSessionFactory>();
            var session = sessionFactory.Create(strategy, version, config);
            
            // 加载历史数据
            await LoadHistoricalBacktestDataAsync(session, fullResult);
            
            // 导航到回测任务界面
            NavigateToTaskView(session);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 加载回测详情失败: {ex.Message}");
            Console.WriteLine(ex);
        }
    }

    private Task LoadHistoricalBacktestDataAsync(BacktestSessionViewModel session, BacktestResult result)
    {
        // 设置完成状态
        session.IsRunning = false;
        session.Status = "已完成";
        session.ProgressPercentage = 100;
        session.ProgressText = "回测已完成";
        
        // 加载订单
        foreach (var order in result.Orders)
        {
            session.Orders.Add(order);
        }
        
        // 加载信号
        foreach (var signal in result.Signals)
        {
            session.Signals.Add(signal);
        }
        
        // 加载权益曲线
        foreach (var point in result.EquityCurve)
        {
            session.EquityCurve.Add(point);
        }
        
        // 设置结果
        session.Result = result;
        
        // 添加日志
        session.AppendLog($"回测时间: {result.StartTime:yyyy-MM-dd HH:mm} 至 {result.EndTime:yyyy-MM-dd HH:mm}");
        session.AppendLog($"总收益率: {result.TotalReturn:P2}");
        session.AppendLog($"最大回撤: {result.MaxDrawdown:P2}");
        session.AppendLog($"总交易数: {result.TotalTrades}");

        return Task.CompletedTask;
    }

    private void OnRunningTaskCardClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not BacktestSessionViewModel session)
        {
            return;
        }

        // 导航到回测任务界面
        NavigateToTaskView(session);
    }

    private void NavigateToTaskView(BacktestSessionViewModel session)
    {
        try
        {
            var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
            if (mainWindow == null)
            {
                Console.WriteLine("⚠️ 无法找到主窗口");
                return;
            }

            // 隐藏回测主界面
            var backtestView = mainWindow.FindControl<Grid>("ViewBacktest");
            if (backtestView != null)
            {
                backtestView.IsVisible = false;
            }

            // 显示任务界面
            var taskView = mainWindow.FindControl<Grid>("ViewBacktestTask");
            if (taskView != null)
            {
                var taskViewControl = taskView.FindDescendantOfType<BacktestTaskView>();
                if (taskViewControl != null)
                {
                    taskViewControl.DataContext = session;
                }
                taskView.IsVisible = true;
            }
            else
            {
                Console.WriteLine("⚠️ 未找到 ViewBacktestTask");
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 导航到任务界面失败: {ex.Message}");
            Console.WriteLine(ex);
        }
    }
}

