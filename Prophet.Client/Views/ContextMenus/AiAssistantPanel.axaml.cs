using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Prophet.Client.Controls;
using Prophet.Client.Core;
using Prophet.Client.Services;
using Prophet.Client.Services.AI.Core;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Views.ContextMenus;

public partial class AiAssistantPanel : UserControl
{
    private AiAssistantViewModel? _viewModel;
    private StrategyView? _strategyView;
    private BacktestViewModel? _backtestViewModel;
    private readonly AiOperationService _aiOps = ServiceContainer.GetService<AiOperationService>();
    
    public AiAssistantPanel()
    {
        InitializeComponent();
        _viewModel = new AiAssistantViewModel();
        DataContext = _viewModel;
        
        // 设置AI操作服务
        if (_strategyView != null)
        {
            _aiOps.SetStrategyHost(_strategyView);
        }
        
        // 启动加载动画
        StartLoadingAnimation();
    }
    
    /// <summary>
    /// 设置关联的策略视图（用于获取当前代码上下文）
    /// </summary>
    public void SetStrategyView(StrategyView strategyView)
    {
        _strategyView = strategyView;
        _aiOps.SetStrategyHost(strategyView);
    }
    
    /// <summary>
    /// 设置回测视图模型（用于场景3）
    /// </summary>
    public void SetBacktestViewModel(BacktestViewModel backtestViewModel)
    {
        _backtestViewModel = backtestViewModel;
        _aiOps.SetBacktestViewModel(backtestViewModel);
    }
    
    /// <summary>
    /// 快捷模板：生成趋势跟踪策略
    /// </summary>
    private async void OnQuickTemplate_Trend(object? sender, RoutedEventArgs e)
    {
        await SendMessageAsync("帮我生成一个基于EMA均线的趋势跟踪策略，使用快慢均线交叉作为入场信号。");
    }
    
    /// <summary>
    /// 快捷模板：添加止损止盈逻辑
    /// </summary>
    private async void OnQuickTemplate_StopLoss(object? sender, RoutedEventArgs e)
    {
        string currentCode = GetCurrentStrategyCode();
        if (string.IsNullOrWhiteSpace(currentCode))
        {
            await SendMessageAsync("帮我添加止损止盈逻辑，止损3%，止盈5%。");
        }
        else
        {
            await SendMessageAsync($"请在以下策略中添加止损止盈逻辑（止损3%，止盈5%）：\n\n```\n{currentCode}\n```");
        }
    }
    
    /// <summary>
    /// 快捷模板：优化入场条件
    /// </summary>
    private async void OnQuickTemplate_Optimize(object? sender, RoutedEventArgs e)
    {
        string currentCode = GetCurrentStrategyCode();
        if (string.IsNullOrWhiteSpace(currentCode))
        {
            await SendMessageAsync("帮我优化策略的入场条件，增加RSI和成交量过滤。");
        }
        else
        {
            await SendMessageAsync($"请帮我优化以下策略的入场条件，建议增加什么指标过滤：\n\n```\n{currentCode}\n```");
        }
    }
    
    /// <summary>
    /// 快捷模板：解释当前代码
    /// </summary>
    private async void OnQuickTemplate_Explain(object? sender, RoutedEventArgs e)
    {
        string currentCode = GetCurrentStrategyCode();
        if (string.IsNullOrWhiteSpace(currentCode))
        {
            await SendMessageAsync("请打开一个策略，我才能帮你解释代码哦。");
        }
        else
        {
            await SendMessageAsync($"请详细解释以下策略的逻辑：\n\n```\n{currentCode}\n```");
        }
    }
    
    /// <summary>
    /// 发送消息按钮点击事件
    /// </summary>
    private async void OnSendMessage(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        await SendMessageAsync(_viewModel.InputText);
    }
    
    /// <summary>
    /// 输入框键盘事件（Ctrl+Enter发送）
    /// </summary>
    private async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            if (_viewModel == null) return;
            await SendMessageAsync(_viewModel.InputText);
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// 清空对话
    /// </summary>
    private void OnClearChat(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ClearMessages();
    }
    
    /// <summary>
    /// 插入代码到编辑器
    /// </summary>
    private void OnInsertCode(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            InsertCodeToEditor(message.Code);
        }
    }
    
    /// <summary>
    /// 复制代码到剪贴板
    /// </summary>
    private async void OnCopyCode(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(message.Code);
                // TODO: 显示复制成功提示
                Console.WriteLine("代码已复制到剪贴板");
            }
        }
    }
    
    /// <summary>
    /// 重新生成代码
    /// </summary>
    private async void OnRegenerateCode(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            await SendMessageAsync($"请重新生成上面的代码，换一种实现方式。");
        }
    }
    
    /// <summary>
    /// 发送消息（核心方法）
    /// </summary>
    private async Task SendMessageAsync(string message)
    {
        if (_viewModel == null || string.IsNullOrWhiteSpace(message)) return;
        
        // 添加用户消息
        _viewModel.AddUserMessage(message);
        _viewModel.InputText = string.Empty;
        
        // 滚动到底部
        await ScrollToBottom();
        
        // 显示加载状态
        _viewModel.IsLoading = true;
        
        try
        {
            // 调用 AI API（这里是模拟）
            string response = await CallAiApiAsync(message);
            
            // 解析响应，提取代码和文本
            var (text, code) = ParseAiResponse(response);
            
            // 判断操作类型（根据用户消息和是否有代码）
            var operationType = DetermineOperationType(message, code);
            string? currentStrategyId = null;
            
            if (operationType != AiOperationType.None)
            {
                var (strategy, _) = _aiOps.GetCurrentStrategy();
                currentStrategyId = strategy?.Id;
            }
            
            // 添加 AI 消息
            _viewModel.AddAiMessage(text, code, operationType, currentStrategyId);
        }
        catch (Exception ex)
        {
            _viewModel.AddAiMessage($"抱歉，发生了错误：{ex.Message}", string.Empty);
            Console.WriteLine($"AI API 调用失败: {ex}");
        }
        finally
        {
            _viewModel.IsLoading = false;
            await ScrollToBottom();
        }
    }
    
    /// <summary>
    /// 调用 AI API
    /// </summary>
    private async Task<string> CallAiApiAsync(string userMessage)
    {
        try
        {
            // 加载AI配置
            await ServiceContainer.GetService<AiServiceManager>().LoadFromSettingsAsync();
            
            if (!ServiceContainer.GetService<AiServiceManager>().IsInitialized)
            {
                return @"请先在设置中配置 AI API 密钥。

步骤：
1. 点击右上角设置按钮
2. 选择 AI 设置选项
3. 输入你的 AI API 密钥
4. 保存配置后重新尝试";
            }
            
            // 获取当前策略代码作为上下文
            string currentCode = GetCurrentStrategyCode();
            
            // 调用 AI API
            return await ServiceContainer.GetService<AiServiceManager>().GenerateStrategyAsync(userMessage, currentCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AI API 调用失败: {ex}");
            return $"API 调用失败：{ex.Message}。请检查你的 API 密钥是否正确，或稍后重试。";
        }
    }
    
    /// <summary>
    /// 解析 AI 响应，分离文本和代码
    /// </summary>
    private (string text, string code) ParseAiResponse(string response)
    {
        // 查找代码块（```...```）
        int codeStart = response.IndexOf("```");
        if (codeStart == -1)
        {
            return (response, string.Empty);
        }
        
        // 跳过语言标识（如 ```dsl）
        int codeContentStart = response.IndexOf('\n', codeStart) + 1;
        int codeEnd = response.IndexOf("```", codeContentStart);
        
        if (codeEnd == -1)
        {
            return (response, string.Empty);
        }
        
        string text = response.Substring(0, codeStart).Trim() + "\n\n" + 
                     response.Substring(codeEnd + 3).Trim();
        string code = response.Substring(codeContentStart, codeEnd - codeContentStart).Trim();
        
        return (text, code);
    }
    
    /// <summary>
    /// 获取当前策略代码
    /// </summary>
    private string GetCurrentStrategyCode()
    {
        // TODO: 从 StrategyView 获取当前编辑器中的代码
        if (_strategyView != null)
        {
            return _strategyView.GetCurrentEditorText() ?? string.Empty;
        }
        return string.Empty;
    }
    
    /// <summary>
    /// 插入代码到编辑器
    /// </summary>
    private void InsertCodeToEditor(string code)
    {
        if (_strategyView != null && !string.IsNullOrWhiteSpace(code))
        {
            _strategyView.InsertTextAtCursor(code);
            Console.WriteLine("代码已插入到编辑器");
        }
    }
    
    /// <summary>
    /// 滚动到底部
    /// </summary>
    private async Task ScrollToBottom()
    {
        await Task.Delay(100); // 等待UI更新
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ChatScrollViewer.ScrollToEnd();
        });
    }
    
    /// <summary>
    /// 判断操作类型
    /// </summary>
    private AiOperationType DetermineOperationType(string userMessage, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return AiOperationType.None;
        
        var messageLower = userMessage.ToLower();
        
        // 场景1：生成新策略
        if (messageLower.Contains("生成") || messageLower.Contains("写") || messageLower.Contains("创建") ||
            messageLower.Contains("新策略") || messageLower.Contains("帮我写"))
        {
            return AiOperationType.CreateNewTab;
        }
        
        // 场景2：审查/优化当前策略
        if (messageLower.Contains("审查") || messageLower.Contains("优化") || messageLower.Contains("改进") ||
            messageLower.Contains("修改") || messageLower.Contains("调整") || messageLower.Contains("当前策略"))
        {
            var (strategy, _) = _aiOps.GetCurrentStrategy();
            if (strategy != null)
            {
                return AiOperationType.ReplaceCode;
            }
        }
        
        // 场景3：分析回测结果并改进
        if (messageLower.Contains("回测") || messageLower.Contains("分析") || messageLower.Contains("测试结果"))
        {
            var (strategy, _) = _aiOps.GetCurrentStrategy();
            if (strategy != null)
            {
                return AiOperationType.SaveAndBacktest;
            }
        }
        
        return AiOperationType.None;
    }
    
    /// <summary>
    /// 场景1：创建新Tab并插入代码
    /// </summary>
    private async void OnCreateNewTab(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            var success = await _aiOps.CreateNewTabWithCodeAsync(
                message.Code, 
                "AI生成策略");
            
            if (success)
            {
                // 显示成功提示
                Console.WriteLine("✅ 已创建新Tab");
            }
        }
    }
    
    /// <summary>
    /// 场景2：替换当前编辑器代码
    /// </summary>
    private async void OnReplaceCode(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            var success = await _aiOps.ReplaceCurrentCodeAsync(message.Code);
            
            if (success)
            {
                Console.WriteLine("✅ 已替换当前编辑器代码");
            }
        }
    }
    
    /// <summary>
    /// 场景3：保存新版本并回测
    /// </summary>
    private async void OnSaveAndBacktest(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            if (string.IsNullOrWhiteSpace(message.CurrentStrategyId))
            {
                Console.WriteLine("❌ 无法获取当前策略ID");
                return;
            }
            
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null)
            {
                Console.WriteLine("❌ 无法找到父窗口");
                return;
            }
            
            // 显示确认对话框
            bool confirmed = false;
            var dialog = ModernDialogPresets.Confirm(
                "确认操作",
                "是否接受AI的建议代码？\n\n接受后将：\n1. 保存为新版本\n2. 立即启动回测",
                result => { confirmed = result; });
            
            await dialog.ShowDialog(window);
            
            if (confirmed)
            {
                var (success, versionString, resultMessage) = await _aiOps
                    .SaveVersionAndBacktestAsync(
                        message.CurrentStrategyId, 
                        message.Code, 
                        "AI优化建议");
                
                if (success)
                {
                    // 显示成功消息
                    _viewModel?.AddAiMessage(
                        $"✅ {resultMessage}\n\n版本号：{versionString}\n\n回测任务已启动，请切换到回测页面查看结果。", 
                        string.Empty);
                }
                else
                {
                    _viewModel?.AddAiMessage($"❌ {resultMessage}", string.Empty);
                }
            }
        }
    }
    
    /// <summary>
    /// 启动加载动画（加载中的三个点）
    /// </summary>
    private void StartLoadingAnimation()
    {
        // TODO: 实现加载动画效果
        // 可以使用 DispatcherTimer 让三个点循环显示
    }
}

