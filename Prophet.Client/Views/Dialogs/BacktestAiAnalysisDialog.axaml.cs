using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using Prophet.Client.Controls;
using Prophet.Client.Core;
using Prophet.Client.Services.Editor;
using Prophet.Client.Services;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Views.Dialogs;

public partial class BacktestAiAnalysisDialog : Window
{
    private readonly BacktestSessionViewModel _session;
    private readonly BacktestViewModel? _backtestViewModel;
    private readonly HashSet<Guid>? _oldSessionIds;
    private readonly AiOperationService _aiOps = ServiceContainer.GetService<AiOperationService>();

    public BacktestAiAnalysisDialog(BacktestSessionViewModel session, BacktestViewModel? backtestViewModel = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _backtestViewModel = backtestViewModel;
        _oldSessionIds = backtestViewModel?.Sessions.Select(s => s.SessionId).ToHashSet();

        InitializeComponent();
        DataContext = _session;

        // 初始化 DSL 语法高亮（只读预览编辑器）
        try
        {
            var editor = this.FindControl<TextEditor>("OptimizedCodeEditor");
            if (editor != null)
            {
                var highlighting = new SyntaxHighlightingManager(editor);
                highlighting.Initialize();
                
                // 监听 AiOptimizedCode 属性变化，手动更新编辑器文本
                _session.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(_session.AiOptimizedCode))
                    {
                        editor.Text = _session.AiOptimizedCode ?? string.Empty;
                    }
                };
                
                // 初始化时设置文本
                editor.Text = _session.AiOptimizedCode ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [BacktestAiAnalysisDialog] 初始化语法高亮失败: {ex.Message}");
        }

        Opened += async (_, _) =>
        {
            // 默认打开即分析（如果还没有结果）
            if (!_session.HasAiAnalysis && !_session.IsAnalyzing && _session.Result != null)
            {
                await SafeRunAnalysisAsync();
            }
        };
    }

    private async void OnAnalyzeClicked(object? sender, RoutedEventArgs e)
    {
        await SafeRunAnalysisAsync();
    }

    private async Task SafeRunAnalysisAsync()
    {
        try
        {
            await _session.RunAiAnalysisAsync();
        }
        catch (Exception ex)
        {
            _session.AiAnalysisResult = $"AI分析失败: {ex.Message}";
        }
    }

    private async void OnCopyOptimizedCodeClicked(object? sender, RoutedEventArgs e)
    {
        var code = _session.AiOptimizedCode;
        if (string.IsNullOrWhiteSpace(code))
        {
            Console.WriteLine("⚠️ 没有可复制的优化代码");
            return;
        }

        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(code);
            Console.WriteLine("✅ AI优化代码已复制到剪贴板");
        }
    }

    private async void OnCopyAnalysisClicked(object? sender, RoutedEventArgs e)
    {
        var text = _session.AiAnalysisResult;
        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine("⚠️ 没有可复制的分析结果");
            return;
        }

        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
            Console.WriteLine("✅ AI分析结果已复制到剪贴板");
        }
    }

    private async void OnSaveAndBacktestClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_session.AiOptimizedCode))
        {
            Console.WriteLine("❌ 没有AI优化代码，无法保存并回测");
            return;
        }

        // 确认对话框
        var confirmed = false;
        var dialog = ModernDialogPresets.Confirm(
            "确认操作",
            "是否接受 AI 的优化 DSL？\n\n接受后将：\n1. 保存为新版本（patch）\n2. 立即使用当前回测配置再次回测",
            result => { confirmed = result; });

        await dialog.ShowDialog(this);
        if (!confirmed)
        {
            return;
        }

        try
        {
            var (success, versionString, message) = await _aiOps.SaveVersionAndBacktestAsync(
                _session.StrategyId,
                _session.AiOptimizedCode!,
                "AI回测分析优化",
                _session.Config);

            if (!success)
            {
                var errorDialog = ModernDialogPresets.Error("操作失败", message ?? "保存版本并回测失败");
                await errorDialog.ShowDialog(this);
                return;
            }

            var infoDialog = ModernDialogPresets.Info(
                "已启动新回测",
                message ?? $"已保存为新版本 {versionString}，回测任务已启动");
            await infoDialog.ShowDialog(this);

            // 自动切换到新任务：如果外层提供了 BacktestViewModel，就按 strategyId + versionString 精确匹配
            if (_backtestViewModel != null &&
                _oldSessionIds != null &&
                !string.IsNullOrWhiteSpace(versionString))
            {
                var newSession = await WaitForNewSessionAsync(
                    _backtestViewModel,
                    _session.StrategyId,
                    versionString!,
                    _oldSessionIds);

                Close(newSession);
                return;
            }

            // 没法精确切换也没关系：只关闭弹窗
            Close(null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 保存版本并回测失败: {ex.Message}");
            var errorDialog = ModernDialogPresets.Error("操作异常", ex.Message);
            await errorDialog.ShowDialog(this);
        }
    }

    private static async Task<BacktestSessionViewModel?> WaitForNewSessionAsync(
        BacktestViewModel backtestViewModel,
        string strategyId,
        string versionString,
        HashSet<Guid> oldSessionIds)
    {
        const int timeoutMs = 3500;
        const int intervalMs = 100;

        var waited = 0;
        while (waited <= timeoutMs)
        {
            var matched = backtestViewModel.Sessions.FirstOrDefault(s =>
                s.StrategyId == strategyId &&
                s.VersionString == versionString &&
                !oldSessionIds.Contains(s.SessionId));

            if (matched != null)
            {
                return matched;
            }

            await Task.Delay(intervalMs);
            waited += intervalMs;
        }

        // 回退：找“新增加的 session”（兼容某些版本字符串为空的情况）
        return backtestViewModel.Sessions.FirstOrDefault(s =>
            s.StrategyId == strategyId &&
            !oldSessionIds.Contains(s.SessionId));
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}


