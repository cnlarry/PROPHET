using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Prophet.Client.Controls;
using Prophet.Client.Core;
using Prophet.Client.Services.AI.Core;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Views.Dialogs;

public partial class AiChatDialog : Window
{
    private AiAssistantViewModel _viewModel;
    private StrategyView? _strategyView;
    private BacktestViewModel? _backtestViewModel;
    private string _sessionTitle = "AI助手";
    private readonly AiServiceManager _aiService;
    
    public AiChatDialog()
    {
        InitializeComponent();
        _viewModel = new AiAssistantViewModel();
        DataContext = _viewModel;
        
        // 获取 AI 服务
        _aiService = ServiceContainer.GetService<AiServiceManager>();
        
        // 启动加载动画
        StartLoadingAnimation();
        
        // 监听窗口状态变化
        PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(WindowState))
            {
                UpdateMaximizeButton();
            }
        };
    }
    
    /// <summary>
    /// 设置会话上下文（推荐使用此方法）
    /// </summary>
    /// <param name="sessionTitle">会话标题</param>
    /// <param name="viewModel">复用的ViewModel（用于保持会话历史）</param>
    /// <param name="strategyView">策略视图引用</param>
    /// <param name="backtestViewModel">回测视图模型引用</param>
    public void SetSessionContext(
        string sessionTitle,
        AiAssistantViewModel? viewModel = null,
        StrategyView? strategyView = null,
        BacktestViewModel? backtestViewModel = null)
    {
        _sessionTitle = sessionTitle;
        
        // 如果提供了已有的ViewModel，复用它（保持会话历史）
        if (viewModel != null)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
        }
        
        _strategyView = strategyView;
        _backtestViewModel = backtestViewModel;
        
        // 更新窗口标题
        UpdateWindowTitle();
        
        Console.WriteLine($"✅ AI会话上下文已设置: {_sessionTitle}");
    }
    
    /// <summary>
    /// 更新窗口标题
    /// </summary>
    private void UpdateWindowTitle()
    {
        var titleText = this.FindControl<TextBlock>("DialogTitle");
        if (titleText != null)
        {
            titleText.Text = _sessionTitle;
        }
    }
    
    #region 窗口控制
    
    private void OnMinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void OnMaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }
    
    private void OnCloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void UpdateMaximizeButton()
    {
        var maximizeIcon = this.FindControl<Path>("MaximizeIcon");
        if (maximizeIcon != null)
        {
            // 最大化时显示还原图标，否则显示最大化图标
            maximizeIcon.Data = WindowState == WindowState.Maximized
                ? Avalonia.Media.Geometry.Parse("M 0,2 L 8,2 L 8,10 L 0,10 Z M 2,0 L 10,0 L 10,8 L 8,8")
                : Avalonia.Media.Geometry.Parse("M 0,0 L 10,0 L 10,10 L 0,10 Z");
        }
        
        var maximizeButton = this.FindControl<Button>("MaximizeButton");
        if (maximizeButton != null)
        {
            ToolTip.SetTip(maximizeButton, WindowState == WindowState.Maximized ? "还原" : "最大化");
        }
    }
    
    #endregion
    
    #region 会话管理
    
    private void OnNewSession(object? sender, RoutedEventArgs e)
    {
        _viewModel?.CreateNewSession();
    }
    
    #endregion
    
    #region 快捷模板
    
    private async void OnQuickTemplate_Trend(object? sender, RoutedEventArgs e)
    {
        await SendMessageAsync("帮我生成一个基于EMA均线的趋势跟踪策略，使用快慢均线交叉作为入场信号。");
    }
    
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
    
    #endregion
    
    #region 消息发送
    
    private async void OnSendMessage(object? sender, RoutedEventArgs e)
    {
        await SendMessageAsync(_viewModel.InputText);
    }
    
    private async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            await SendMessageAsync(_viewModel.InputText);
            e.Handled = true;
        }
    }
    
    private async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        // 添加用户消息
        _viewModel.AddUserMessage(message);
        _viewModel.InputText = string.Empty;
        
        // 滚动到底部
        await ScrollToBottom();
        
        // 显示加载状态
        _viewModel.IsLoading = true;
        
        // 创建一个AI消息占位符用于流式更新
        var aiMessage = new AiMessageModel
        {
            Content = "",
            Code = string.Empty,
            Timestamp = DateTime.Now,
            OperationType = AiOperationType.None
        };
        _viewModel.Messages.Add(aiMessage);
        
        try
        {
            // 获取当前策略代码作为上下文
            string currentCode = GetCurrentStrategyCode();
            
            // 使用流式API
            var fullResponse = "";
            var cancellationTokenSource = new System.Threading.CancellationToken();
            
            // 加载AI配置
            await _aiService.LoadFromSettingsAsync();
            
            if (!_aiService.IsInitialized)
            {
                aiMessage.Content = @"请先在设置中配置 AI API 密钥。

步骤：
1. 点击右上角设置按钮
2. 选择 AI 设置选项
3. 输入你的 AI API 密钥
4. 保存配置后重新尝试";
                _viewModel.IsLoading = false;
                await ScrollToBottom();
                return;
            }
            
            // 使用流式API逐字输出
            await foreach (var chunk in _aiService.GenerateStrategyStreamAsync(
                message, currentCode, cancellationTokenSource))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    fullResponse += chunk.Content;
                    
                    // 实时更新UI（在UI线程上）
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        aiMessage.Content = fullResponse;
                    });
                    
                    // 滚动到底部
                    await ScrollToBottom();
                }
            }
            
            // 流式输出完成后，解析完整响应
            var (text, code) = ParseAiResponse(fullResponse);
            var operationType = DetermineOperationType(message, code);
            string? currentStrategyId = null;
            
            if (operationType != AiOperationType.None)
            {
                try
                {
                    if (_strategyView != null)
                    {
                        // TODO: 实现获取当前策略ID的逻辑
                        // currentStrategyId = _strategyView.GetCurrentStrategyId();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"获取当前策略ID失败: {ex.Message}");
                }
            }
            
            // 更新消息的最终状态
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                aiMessage.Content = text;
                aiMessage.Code = code;
                aiMessage.OperationType = operationType;
                aiMessage.CurrentStrategyId = currentStrategyId;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                aiMessage.Content = $"抱歉，发生了错误：{ex.Message}";
            });
            Console.WriteLine($"AI API 调用失败: {ex}");
        }
        finally
        {
            _viewModel.IsLoading = false;
            await ScrollToBottom();
        }
    }
    
    #endregion
    
    #region 代码操作
    
    private async void OnApplyCode(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            // 根据操作类型执行不同的操作
            switch (message.OperationType)
            {
                case AiOperationType.CreateNewTab:
                    await OnCreateNewTab(message);
                    break;
                case AiOperationType.ReplaceCode:
                    await OnReplaceCode(message);
                    break;
                case AiOperationType.SaveAndBacktest:
                    await OnSaveAndBacktest(message);
                    break;
                default:
                    // 默认：插入到光标位置
                    InsertCodeToEditor(message.Code);
                    break;
            }
        }
    }
    
    private async void OnCopyCode(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(message.Code);
                Console.WriteLine("✅ 代码已复制到剪贴板");
                // TODO: 显示Toast提示
            }
        }
    }
    
    private async void OnRegenerateCode(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AiMessageModel message)
        {
            await SendMessageAsync("请重新生成上面的代码，换一种实现方式。");
        }
    }
    
    private async Task OnCreateNewTab(AiMessageModel message)
    {
        if (_strategyView != null && !string.IsNullOrWhiteSpace(message.Code))
        {
            // 生成策略名称
            var strategyName = $"AI策略_{DateTime.Now:MMddHHmm}";
            
            // 创建新Tab
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _strategyView.CreateStrategyFromAiCode(strategyName, message.Code);
            });
            
            _viewModel.AddAiMessage($"✅ 已创建新策略「{strategyName}」，请保存后使用", string.Empty);
            Console.WriteLine($"✅ AI助手创建了新策略Tab: {strategyName}");
            
            // 显示Toast提示
            await ShowToast("✅ 代码已应用", "新策略已创建");
        }
        else
        {
            _viewModel.AddAiMessage("❌ 创建新Tab失败：没有可用的代码", string.Empty);
            Console.WriteLine("❌ 创建新Tab失败：没有可用的代码");
        }
    }
    
    private async Task OnReplaceCode(AiMessageModel message)
    {
        if (_strategyView != null && !string.IsNullOrWhiteSpace(message.Code))
        {
            // 替换当前编辑器代码
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _strategyView.ReplaceCurrentCode(message.Code);
            });
            
            _viewModel.AddAiMessage("✅ 代码已替换到当前策略，记得保存哦", string.Empty);
            Console.WriteLine("✅ 代码已替换到当前编辑器");
            
            // 显示Toast提示
            await ShowToast("✅ 代码已应用", "当前策略已更新");
        }
        else
        {
            _viewModel.AddAiMessage("❌ 替换代码失败：没有可用的代码", string.Empty);
            Console.WriteLine("❌ 替换代码失败");
        }
    }
    
    private async Task OnSaveAndBacktest(AiMessageModel message)
    {
        await Task.CompletedTask; // 异步占位
        // TODO: 实现保存并回测功能（需要与回测模块集成）
        _viewModel.AddAiMessage("✅ 保存并回测功能正在开发中", string.Empty);
        Console.WriteLine("⚠️ 保存并回测功能待实现");
    }
    
    /// <summary>
    /// 显示Toast提示（临时实现，后续可以用专门的Toast组件）
    /// </summary>
    private async Task ShowToast(string title, string message)
    {
        // 简单的终端输出，后续可以改为UI Toast
        Console.WriteLine($"[Toast] {title}: {message}");
        await Task.Delay(100);
    }
    
    #endregion
    
    #region 上下文管理
    
    private void OnAttachContext(object? sender, RoutedEventArgs e)
    {
        // TODO: 实现上下文选择对话框
        Console.WriteLine("📎 添加上下文功能待实现");
    }
    
    private void OnClearChat(object? sender, RoutedEventArgs e)
    {
        var dialog = ModernDialogPresets.Confirm(
            "确认清空",
            "是否清空当前会话的所有消息？",
            confirmed =>
            {
                if (confirmed)
                {
                    _viewModel.ClearMessages();
                }
            });
        
        _ = dialog.ShowDialog(this);
    }
    
    #endregion
    
    #region 辅助方法
    
    private async Task<string> CallAiApiAsync(string userMessage)
    {
        try
        {
            // 加载AI配置
            await _aiService.LoadFromSettingsAsync();
            
            if (!_aiService.IsInitialized)
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
            return await _aiService.GenerateStrategyAsync(userMessage, currentCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AI API 调用失败: {ex}");
            return $"API 调用失败：{ex.Message}。请检查你的 API 密钥是否正确，或稍后重试。";
        }
    }
    
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
    
    private string GetCurrentStrategyCode()
    {
        if (_strategyView != null)
        {
            return _strategyView.GetCurrentEditorText() ?? string.Empty;
        }
        return string.Empty;
    }
    
    private void InsertCodeToEditor(string code)
    {
        if (_strategyView != null && !string.IsNullOrWhiteSpace(code))
        {
            _strategyView.InsertTextAtCursor(code);
            Console.WriteLine("✅ 代码已插入到编辑器");
        }
    }
    
    private async Task ScrollToBottom()
    {
        await Task.Delay(100); // 等待UI更新
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ChatScrollViewer?.ScrollToEnd();
        });
    }
    
    private AiOperationType DetermineOperationType(string userMessage, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return AiOperationType.None;
        
        var messageLower = userMessage.ToLower();
        
        // 场景1：用户明确要求生成新策略
        if (messageLower.Contains("生成") && (messageLower.Contains("策略") || messageLower.Contains("新") || messageLower.Contains("创建")))
        {
            return AiOperationType.CreateNewTab;
        }
        
        if (messageLower.Contains("写一个") || messageLower.Contains("写个") || messageLower.Contains("帮我写"))
        {
            return AiOperationType.CreateNewTab;
        }
        
        // 场景2：用户要求解释代码（不需要操作按钮）
        if (messageLower.Contains("解释") || messageLower.Contains("说明") || 
            messageLower.Contains("什么意思") || messageLower.Contains("分析这"))
        {
            return AiOperationType.None;
        }
        
        // 场景3：用户要求优化/修改当前策略
        if (_strategyView != null)
        {
            if (messageLower.Contains("优化") || messageLower.Contains("改进") || 
                messageLower.Contains("修改") || messageLower.Contains("调整") ||
                messageLower.Contains("当前") || messageLower.Contains("这个策略"))
            {
                return AiOperationType.ReplaceCode;
            }
        }
        
        // 场景4：回测相关
        if (messageLower.Contains("回测") || messageLower.Contains("测试"))
        {
            if (_strategyView != null)
            {
                return AiOperationType.SaveAndBacktest;
            }
        }
        
        // 默认：如果有代码但无法明确判断，显示创建新Tab（更安全）
        return AiOperationType.CreateNewTab;
    }
    
    private void StartLoadingAnimation()
    {
        // TODO: 实现加载动画效果
        // 可以使用 DispatcherTimer 让三个点循环显示
    }
    
    #endregion
}

