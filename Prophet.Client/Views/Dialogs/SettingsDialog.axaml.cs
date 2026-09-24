using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Layout;
using Prophet.Client.Core;
using Prophet.Client.Data;
using Prophet.Client.Models;
using Prophet.Client.Services.Settings;
using Prophet.Client.Trading.Exchanges.Binance;
using Prophet.Client.Trading.Models;
using Prophet.Client.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Prophet.Client.Views.Dialogs;

public class SettingsDialog : ModernDialog
{
    private AppSettings _tempSettings; // 临时设置，点击保存后才会应用
    private readonly AppSettingsService _appSettingsService;
    private DispatcherTimer? _timeUpdateTimer;
    private Border? _currentSelectedCategory;
    private ScrollViewer? _contentScrollViewer;
    private readonly List<(Border CategoryButton, Border Section, string Name)> _categoryMappings = new();
    private bool _isManualScrolling = false; // 标志位：是否是手动点击触发的滚动
    
    // UI 控件引用
    private RadioButton? ColorSchemeRedFallGreenRise;
    private RadioButton? ColorSchemeRedRiseGreenFall;
    private Border? RisingColorPreview;
    private Border? FallingColorPreview;
    private ComboBox? TimeZoneComboBox;
    private TextBlock? CurrentTimeTextBlock;
    private CheckBox? ShowGridLinesCheckBox;
    private CheckBox? ShowCrosshairCheckBox;
    private NumericUpDown? DecimalPlacesNumericUpDown;
    private NumericUpDown? AutoRefreshIntervalNumericUpDown;
    private NumericUpDown? ChartDefaultVisibleCandlesNumericUpDown;
    private TextBox? DefaultSymbolTextBox;
    private NumericUpDown? DefaultKlineLimitNumericUpDown;
    private CheckBox? EnableProxyCheckBox;
    private ComboBox? ProxyTypeComboBox;
    private TextBox? ProxyAddressTextBox;
    private NumericUpDown? ProxyPortNumericUpDown;
    private TextBox? ProxyUsernameTextBox;
    private TextBox? ProxyPasswordTextBox;
    private CheckBox? SkipSslCertificateValidationCheckBox;
    private Button? TestProxyButton;
    private TextBlock? ProxyStatusTextBlock;
    private TextBlock? ProxyLatencyTextBlock;
    private ComboBox? AiProviderComboBox;
    private TextBox? DeepSeekApiKeyTextBox;
    private TextBox? OpenAiApiKeyTextBox;
    private TextBox? QwenApiKeyTextBox;
    private TextBox? ChatGLMApiKeyTextBox;
    private TextBox? AiApiBaseUrlTextBox;
    private TextBox? AiModelTextBox;
    private NumericUpDown? AiTemperatureNumericUpDown;
    private NumericUpDown? AiMaxTokensNumericUpDown;
    private CheckBox? AiEnableStreamingCheckBox;
    private Button? TestAiConnectionButton;
    private TextBlock? AiConnectionStatusTextBlock;
    private TextBox? BinanceApiKeyTextBox;
    private TextBox? BinanceApiSecretTextBox;
    private CheckBox? UseBinanceTestnetCheckBox;
    private ComboBox? DefaultMarketDataExchangeComboBox;
    private ComboBox? DefaultTradingExchangeComboBox;
    private TextBox? DefaultTradingSymbolTextBox;
    private NumericUpDown? DefaultLeverageNumericUpDown;
    private Button? TestBinanceConnectionButton;
    private TextBlock? BinanceConnectionStatusTextBlock;
    
    // 分类和内容区域的映射
    private readonly Dictionary<string, Border> _categorySectionMap = new();

    public SettingsDialog()
    {
        // 设置窗口属性
        Title = "设置";
        Width = 800;
        Height = 600;
        CanResize = true;
        
        // 获取设置服务
        _appSettingsService = ServiceContainer.GetService<AppSettingsService>();
        
        // 创建当前设置的深拷贝
        _tempSettings = CloneSettings(_appSettingsService.Settings);
        
        // 构建UI
        BuildDialogContent();
        
        // 添加底部按钮
        AddButtons();
        
        // 启动时间更新定时器
        StartTimeUpdateTimer();
    }

    /// <summary>
    /// 构建对话框内容
    /// </summary>
    private void BuildDialogContent()
    {
        // 创建主容器（左右布局）
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180,*"),
        };
        
        // 先创建所有配置区块
        var colorSchemeSection = CreateColorSchemeSection();
        var timeZoneSection = CreateTimeZoneSection();
        var displaySection = CreateDisplaySection();
        var numberFormatSection = CreateNumberFormatSection();
        var uiSection = CreateUISection();
        var marketDataSection = CreateMarketDataSection();
        var networkSection = CreateNetworkSection();
        var aiSection = CreateAISection();
        var exchangeSection = CreateExchangeSection();
        
        // 左侧分类列表 - 必须在创建配置区块之后创建，因为需要引用 _categorySectionMap
        var categoryPanel = CreateCategoryPanel();
        categoryPanel.SetValue(Grid.ColumnProperty, 0);
        mainGrid.Children.Add(categoryPanel);
        
        // 右侧内容区域 - 独立的 ScrollViewer
        _contentScrollViewer = new ScrollViewer
        {
            [Grid.ColumnProperty] = 1,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        
        // 监听滚动事件
        _contentScrollViewer.ScrollChanged += ContentScrollViewer_OnScrollChanged;
        
        var contentStack = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(20, 20, 20, 20) // 右侧内容区域保持内边距
        };
        
        // 添加所有配置区块
        contentStack.Children.Add(colorSchemeSection);
        contentStack.Children.Add(timeZoneSection);
        contentStack.Children.Add(displaySection);
        contentStack.Children.Add(numberFormatSection);
        contentStack.Children.Add(uiSection);
        contentStack.Children.Add(marketDataSection);
        contentStack.Children.Add(networkSection);
        contentStack.Children.Add(aiSection);
        contentStack.Children.Add(exchangeSection);
        
        _contentScrollViewer.Content = contentStack;
        mainGrid.Children.Add(_contentScrollViewer);
        
        // 保存分类和区块的映射关系（延迟到布局完成后）
        Dispatcher.UIThread.Post(() => BuildCategoryMappings(categoryPanel), DispatcherPriority.Loaded);
        
        // 不使用 SetContent，直接替换 ModernDialog 的内容区域
        ReplaceDialogContent(mainGrid);
        
        // 初始化UI值
        InitializeUI();
    }
    
    /// <summary>
    /// 构建分类按钮和区块的映射关系
    /// </summary>
    private void BuildCategoryMappings(Border categoryPanel)
    {
        _categoryMappings.Clear();
        
        // 获取左侧分类按钮
        if (categoryPanel.Child is StackPanel categoryStack)
        {
            var categoryButtons = categoryStack.Children.OfType<Border>().ToList();
            var sections = new[]
            {
                ("颜色方案", _categorySectionMap.GetValueOrDefault("SectionColorScheme")),
                ("时区设置", _categorySectionMap.GetValueOrDefault("SectionTimeZone")),
                ("显示设置", _categorySectionMap.GetValueOrDefault("SectionDisplay")),
                ("数字格式", _categorySectionMap.GetValueOrDefault("SectionNumberFormat")),
                ("界面设置", _categorySectionMap.GetValueOrDefault("SectionUI")),
                ("市场数据", _categorySectionMap.GetValueOrDefault("SectionMarketData")),
                ("网络设置", _categorySectionMap.GetValueOrDefault("SectionNetwork")),
                ("AI 设置", _categorySectionMap.GetValueOrDefault("SectionAI")),
                ("交易所配置", _categorySectionMap.GetValueOrDefault("SectionExchange"))
            };
            
            for (int i = 0; i < categoryButtons.Count && i < sections.Length; i++)
            {
                var button = categoryButtons[i];
                var (name, section) = sections[i];
                if (section != null)
                {
                    _categoryMappings.Add((button, section, name));
                }
            }
        }
    }
    
    /// <summary>
    /// 滚动事件处理 - 根据滚动位置更新左侧菜单选中状态
    /// </summary>
    private void ContentScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // 如果是手动点击触发的滚动，不更新菜单选中状态
        if (_isManualScrolling) return;
        
        if (_contentScrollViewer == null || _categoryMappings.Count == 0) return;
        
        // 获取视口信息
        var viewportHeight = _contentScrollViewer.Viewport.Height;
        var scrollOffset = _contentScrollViewer.Offset.Y;
        
        // 找到当前视口中可见面积最大的区块
        Border? bestSection = null;
        double maxVisibleHeight = 0;
        
        foreach (var (button, section, name) in _categoryMappings)
        {
            // 获取区块的边界
            if (section.Bounds.Height <= 0) continue;
            
            // 获取区块相对于内容容器的位置（通过父容器）
            var parent = _contentScrollViewer.Content as Control;
            if (parent == null) continue;
            
            var sectionTransform = section.TranslatePoint(new Point(0, 0), parent);
            if (!sectionTransform.HasValue) continue;
            
            // 区块在内容中的绝对位置
            var sectionTopInContent = sectionTransform.Value.Y;
            var sectionBottomInContent = sectionTopInContent + section.Bounds.Height;
            
            // 计算相对于视口的位置（减去滚动偏移）
            var sectionTopInViewport = sectionTopInContent - scrollOffset;
            var sectionBottomInViewport = sectionBottomInContent - scrollOffset;
            
            // 计算在视口中的可见高度
            var visibleTop = Math.Max(0, sectionTopInViewport);
            var visibleBottom = Math.Min(viewportHeight, sectionBottomInViewport);
            var visibleHeight = Math.Max(0, visibleBottom - visibleTop);
            
            // 找到可见高度最大的区块
            if (visibleHeight > maxVisibleHeight)
            {
                maxVisibleHeight = visibleHeight;
                bestSection = section;
            }
        }
        
        // 更新菜单选中状态
        if (bestSection != null)
        {
            var matchingButton = _categoryMappings.FirstOrDefault(m => m.Section == bestSection).CategoryButton;
            if (matchingButton != null)
            {
                UpdateCategorySelection(matchingButton);
            }
        }
    }
    
    /// <summary>
    /// 直接替换 ModernDialog 的内容区域，使用 Grid 替代 StackPanel+ScrollViewer
    /// </summary>
    private void ReplaceDialogContent(Grid content)
    {
        // 查找并替换 ModernDialog 的内容区域
        if (Content is Panel rootPanel)
        {
            foreach (var child in rootPanel.Children)
            {
                if (child is Border outerContainer && outerContainer.Child is Grid layeredContainer)
                {
                    foreach (var layer in layeredContainer.Children)
                    {
                        if (layer is Border mainBorder && mainBorder.Child is Grid mainGrid)
                        {
                            // 找到中间行（内容区域的 ScrollViewer）
                            foreach (var gridChild in mainGrid.Children.ToList())
                            {
                                if (gridChild is ScrollViewer scrollViewer && scrollViewer.GetValue(Grid.RowProperty) is int row && row == 1)
                                {
                                    // 移除默认的 ScrollViewer
                                    mainGrid.Children.Remove(scrollViewer);
                                    
                                    // 添加我们的主容器
                                    content.SetValue(Grid.RowProperty, 1);
                                    mainGrid.Children.Add(content);
                                    
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 创建左侧分类面板
    /// </summary>
    private Border CreateCategoryPanel()
    {
        var categoryStack = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(10, 0, 10, 0), // 左右各10px，上下由外层Border的Padding控制
            VerticalAlignment = VerticalAlignment.Top
        };
        
        var categories = new[]
        {
            ("CategoryColorScheme", "颜色方案"),
            ("CategoryTimeZone", "时区设置"),
            ("CategoryDisplay", "显示设置"),
            ("CategoryNumberFormat", "数字格式"),
            ("CategoryUI", "界面设置"),
            ("CategoryMarketData", "市场数据"),
            ("CategoryNetwork", "网络设置"),
            ("CategoryAI", "AI 设置"),
            ("CategoryExchange", "交易所配置")
        };
        
        bool isFirst = true;
        foreach (var (name, label) in categories)
        {
            var categoryItem = new Border
            {
                Padding = new Thickness(16, 12),
                Margin = new Thickness(0, 2),
                CornerRadius = new CornerRadius(4),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Background = Brushes.Transparent, // 设置透明背景确保能接收点击事件
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
                    IsHitTestVisible = false // 让点击事件直接传递到Border
                }
            };
            
            // 第一项默认选中
            if (isFirst)
            {
                categoryItem.Background = new SolidColorBrush(Color.Parse("#F0B90B"));
                if (categoryItem.Child is TextBlock tb)
                {
                    tb.Foreground = Brushes.White;
                    tb.FontWeight = FontWeight.SemiBold;
                }
                _currentSelectedCategory = categoryItem;
                isFirst = false;
            }
            
            // 添加鼠标悬停效果
            categoryItem.PointerEntered += (s, e) =>
            {
                if (s is Border b && b != _currentSelectedCategory)
                {
                    b.Background = new SolidColorBrush(Color.Parse("#2B3139"));
                }
            };
            
            categoryItem.PointerExited += (s, e) =>
            {
                if (s is Border b && b != _currentSelectedCategory)
                {
                    b.Background = Brushes.Transparent;
                }
            };
            
            // 点击事件
            var sectionName = "Section" + name.Substring(8); // CategoryColorScheme -> SectionColorScheme
            categoryItem.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && s is Border clickedBorder)
                {
                    // 设置标志位，表示这是手动点击触发的滚动
                    _isManualScrolling = true;
                    
                    // 更新选中状态
                    UpdateCategorySelection(clickedBorder);
                    
                    // 滚动到对应区域
                    if (_categorySectionMap.TryGetValue(sectionName, out var targetSection))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            targetSection.BringIntoView();
                            
                            // 延迟重置标志位，确保滚动完成
                            Dispatcher.UIThread.Post(() =>
                            {
                                _isManualScrolling = false;
                            }, DispatcherPriority.Background);
                        }, DispatcherPriority.Background);
                    }
                    else
                    {
                        _isManualScrolling = false;
                    }
                }
            };
            
            categoryStack.Children.Add(categoryItem);
        }
        
        return new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(0, 10, 0, 10), // 上下各10px
            Child = categoryStack
        };
    }
    
    /// <summary>
    /// 更新分类选中状态
    /// </summary>
    private void UpdateCategorySelection(Border selectedCategory)
    {
        if (_currentSelectedCategory == selectedCategory) return;
        
        // 取消之前的选中状态
        if (_currentSelectedCategory != null)
        {
            _currentSelectedCategory.Background = Brushes.Transparent;
            if (_currentSelectedCategory.Child is TextBlock oldTb)
            {
                oldTb.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
                oldTb.FontWeight = FontWeight.Normal;
            }
        }
        
        // 设置新的选中状态
        selectedCategory.Background = new SolidColorBrush(Color.Parse("#F0B90B"));
        if (selectedCategory.Child is TextBlock newTb)
        {
            newTb.Foreground = Brushes.White;
            newTb.FontWeight = FontWeight.SemiBold;
        }
        _currentSelectedCategory = selectedCategory;
    }
    
    /// <summary>
    /// 添加底部按钮
    /// </summary>
    private void AddButtons()
    {
        AddButton("重置为默认", () =>
        {
            _tempSettings = new AppSettings();
            InitializeUI();
            Console.WriteLine("ℹ️ [SettingsDialog] 设置已重置为默认值（未保存）");
        }, isPrimary: false);
        
        AddButton("取消", () =>
        {
            Console.WriteLine("ℹ️ [SettingsDialog] 取消设置更改");
            Close(false);
        }, isPrimary: false);
        
        AddButton("保存", () =>
        {
            if (_appSettingsService.UpdateSettings(_tempSettings))
            {
                // 让 Data Plane 路由即时生效（无需重启应用）
                App.DataPlane.SetDefaults(
                    marketDataExchange: ExchangeIdParser.ParseOrDefault(_tempSettings.DefaultMarketDataExchange, ExchangeId.Binance),
                    tradingExchange: ExchangeIdParser.ParseOrDefault(_tempSettings.DefaultTradingExchange, ExchangeId.Binance));

                Console.WriteLine("✅ [SettingsDialog] 设置已保存并应用");
                Close(true);
            }
            else
            {
                Console.WriteLine("❌ [SettingsDialog] 保存设置失败");
            }
        }, isPrimary: true);
    }
    
    /// <summary>
    /// 深拷贝设置对象
    /// </summary>
    private AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            IsRedRiseGreenFall = source.IsRedRiseGreenFall,
            TimeZoneId = source.TimeZoneId,
            DateTimeFormat = source.DateTimeFormat,
            DateFormat = source.DateFormat,
            TimeFormat = source.TimeFormat,
            DecimalPlaces = source.DecimalPlaces,
            ShowGridLines = source.ShowGridLines,
            ShowCrosshair = source.ShowCrosshair,
            Language = source.Language,
            AutoRefreshIntervalMs = source.AutoRefreshIntervalMs,
            ChartDefaultVisibleCandles = source.ChartDefaultVisibleCandles,
            DefaultSymbol = source.DefaultSymbol,
            DefaultKlineLimit = source.DefaultKlineLimit,
            EnableProxy = source.EnableProxy,
            ProxyAddress = source.ProxyAddress,
            ProxyUsername = source.ProxyUsername,
            ProxyPassword = source.ProxyPassword,
            SkipSslCertificateValidation = source.SkipSslCertificateValidation,
            CurrentAiProvider = source.CurrentAiProvider,
            DeepSeekApiKey = source.DeepSeekApiKey,
            OpenAiApiKey = source.OpenAiApiKey,
            QwenApiKey = source.QwenApiKey,
            ChatGLMApiKey = source.ChatGLMApiKey,
            AiApiBaseUrl = source.AiApiBaseUrl,
            AiModel = source.AiModel,
            AiTemperature = source.AiTemperature,
            AiMaxTokens = source.AiMaxTokens,
            AiEnableStreaming = source.AiEnableStreaming,
            DefaultMarketDataExchange = source.DefaultMarketDataExchange,
            DefaultTradingExchange = source.DefaultTradingExchange,
            BinanceApiKey = source.BinanceApiKey,
            BinanceApiSecret = source.BinanceApiSecret,
            UseBinanceTestnet = source.UseBinanceTestnet,
            DefaultTradingSymbol = source.DefaultTradingSymbol,
            DefaultLeverage = source.DefaultLeverage
        };
    }

    private void InitializeUI()
    {
        // 颜色方案
        ColorSchemeRedFallGreenRise!.IsChecked = !_tempSettings.IsRedRiseGreenFall;
        ColorSchemeRedRiseGreenFall!.IsChecked = _tempSettings.IsRedRiseGreenFall;
        ColorSchemeRedFallGreenRise.IsCheckedChanged += (_, _) => 
        { 
            if (ColorSchemeRedFallGreenRise.IsChecked == true)
            {
                _tempSettings.IsRedRiseGreenFall = false; 
                UpdateColorPreview(); 
            }
        };
        ColorSchemeRedRiseGreenFall.IsCheckedChanged += (_, _) => 
        { 
            if (ColorSchemeRedRiseGreenFall.IsChecked == true)
            {
                _tempSettings.IsRedRiseGreenFall = true; 
                UpdateColorPreview(); 
            }
        };
        
        // 更新颜色预览
        UpdateColorPreview();
        
        // 时区下拉框
        InitializeTimeZoneComboBox();
        TimeZoneComboBox!.SelectionChanged += (_, e) =>
        {
            if (TimeZoneComboBox.SelectedItem is TimeZoneInfo selectedTimeZone)
            {
                _tempSettings.TimeZoneId = selectedTimeZone.Id;
                UpdateCurrentTime();
            }
        };
        
        // 显示设置
        ShowGridLinesCheckBox!.IsChecked = _tempSettings.ShowGridLines;
        ShowGridLinesCheckBox.IsCheckedChanged += (_, _) => 
            _tempSettings.ShowGridLines = ShowGridLinesCheckBox.IsChecked ?? false;
        
        ShowCrosshairCheckBox!.IsChecked = _tempSettings.ShowCrosshair;
        ShowCrosshairCheckBox.IsCheckedChanged += (_, _) => 
            _tempSettings.ShowCrosshair = ShowCrosshairCheckBox.IsChecked ?? false;
        
        // 数字格式
        DecimalPlacesNumericUpDown!.Value = _tempSettings.DecimalPlaces;
        DecimalPlacesNumericUpDown.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _tempSettings.DecimalPlaces = (int)e.NewValue.Value;
        };
        
        // UI设置
        AutoRefreshIntervalNumericUpDown!.Value = _tempSettings.AutoRefreshIntervalMs / 1000; // 转换为秒
        AutoRefreshIntervalNumericUpDown.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _tempSettings.AutoRefreshIntervalMs = (int)e.NewValue.Value * 1000;
        };
        
        ChartDefaultVisibleCandlesNumericUpDown!.Value = _tempSettings.ChartDefaultVisibleCandles;
        ChartDefaultVisibleCandlesNumericUpDown.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _tempSettings.ChartDefaultVisibleCandles = (int)e.NewValue.Value;
        };
        
        // 市场数据设置
        DefaultSymbolTextBox!.Text = _tempSettings.DefaultSymbol;
        DefaultSymbolTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.DefaultSymbol = DefaultSymbolTextBox.Text?.ToUpperInvariant() ?? "BTCUSDT";
        };
        
        DefaultKlineLimitNumericUpDown!.Value = _tempSettings.DefaultKlineLimit;
        DefaultKlineLimitNumericUpDown.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _tempSettings.DefaultKlineLimit = (int)e.NewValue.Value;
        };
        
        // 网络代理设置
        EnableProxyCheckBox!.IsChecked = _tempSettings.EnableProxy;
        EnableProxyCheckBox.IsCheckedChanged += (_, _) => 
            _tempSettings.EnableProxy = EnableProxyCheckBox.IsChecked ?? false;
        
        // 代理类型（根据旧配置推断）
        if (string.IsNullOrWhiteSpace(_tempSettings.ProxyAddress))
        {
            ProxyTypeComboBox!.SelectedIndex = 0; // 系统代理
        }
        else
        {
            // 根据地址判断类型（简单推断）
            ProxyTypeComboBox!.SelectedIndex = _tempSettings.ProxyAddress.Contains("socks") ? 2 : 1;
        }
        
        // 解析代理地址和端口
        var (address, port) = ParseProxyAddressAndPort(_tempSettings.ProxyAddress);
        ProxyAddressTextBox!.Text = address ?? "";
        ProxyPortNumericUpDown!.Value = port;
        
        ProxyUsernameTextBox!.Text = _tempSettings.ProxyUsername ?? "";
        
        // 解密密码显示（如果已加密）
        var encryptionService = new Services.Security.PasswordEncryptionService();
        var decryptedPassword = encryptionService.Decrypt(_tempSettings.ProxyPassword ?? "");
        ProxyPasswordTextBox!.Text = decryptedPassword;
        
        SkipSslCertificateValidationCheckBox!.IsChecked = _tempSettings.SkipSslCertificateValidation;
        SkipSslCertificateValidationCheckBox.IsCheckedChanged += (_, _) => 
            _tempSettings.SkipSslCertificateValidation = SkipSslCertificateValidationCheckBox.IsChecked ?? false;
        
        // 当代理设置改变时，更新临时设置
        ProxyTypeComboBox.SelectionChanged += (_, _) => UpdateProxySettings();
        ProxyAddressTextBox.TextChanged += (_, _) => UpdateProxySettings();
        ProxyPortNumericUpDown.ValueChanged += (_, _) => UpdateProxySettings();
        ProxyUsernameTextBox.TextChanged += (_, _) => UpdateProxySettings();
        ProxyPasswordTextBox.TextChanged += (_, _) => UpdateProxySettings();
        
        // AI 设置
        // 初始化 Provider 下拉框
        var providers = new List<string> { "DeepSeek", "OpenAI", "Qwen", "ChatGLM" };
        AiProviderComboBox!.ItemsSource = providers;
        AiProviderComboBox.SelectedItem = _tempSettings.CurrentAiProvider;
        AiProviderComboBox.SelectionChanged += (_, _) =>
        {
            if (AiProviderComboBox.SelectedItem is string provider)
            {
                _tempSettings.CurrentAiProvider = provider;
                UpdateAiProviderUI(provider);
            }
        };
        
        DeepSeekApiKeyTextBox!.Text = _tempSettings.DeepSeekApiKey;
        DeepSeekApiKeyTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.DeepSeekApiKey = DeepSeekApiKeyTextBox.Text ?? string.Empty;
        };
        
        OpenAiApiKeyTextBox!.Text = _tempSettings.OpenAiApiKey;
        OpenAiApiKeyTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.OpenAiApiKey = OpenAiApiKeyTextBox.Text ?? string.Empty;
        };
        
        QwenApiKeyTextBox!.Text = _tempSettings.QwenApiKey;
        QwenApiKeyTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.QwenApiKey = QwenApiKeyTextBox.Text ?? string.Empty;
        };
        
        ChatGLMApiKeyTextBox!.Text = _tempSettings.ChatGLMApiKey;
        ChatGLMApiKeyTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.ChatGLMApiKey = ChatGLMApiKeyTextBox.Text ?? string.Empty;
        };
        
        AiApiBaseUrlTextBox!.Text = _tempSettings.AiApiBaseUrl;
        AiApiBaseUrlTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.AiApiBaseUrl = AiApiBaseUrlTextBox.Text ?? "https://api.deepseek.com";
        };
        
        AiModelTextBox!.Text = _tempSettings.AiModel;
        AiModelTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.AiModel = AiModelTextBox.Text ?? "deepseek-chat";
        };
        
        AiTemperatureNumericUpDown!.Value = (decimal)_tempSettings.AiTemperature;
        AiTemperatureNumericUpDown.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _tempSettings.AiTemperature = (double)e.NewValue.Value;
        };
        
        AiMaxTokensNumericUpDown!.Value = _tempSettings.AiMaxTokens;
        AiMaxTokensNumericUpDown.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _tempSettings.AiMaxTokens = (int)e.NewValue.Value;
        };
        
        AiEnableStreamingCheckBox!.IsChecked = _tempSettings.AiEnableStreaming;
        AiEnableStreamingCheckBox.IsCheckedChanged += (_, _) => 
            _tempSettings.AiEnableStreaming = AiEnableStreamingCheckBox.IsChecked ?? false;
        
        // 初始化 Provider UI
        UpdateAiProviderUI(_tempSettings.CurrentAiProvider);
        
        // 交易所配置
        DefaultMarketDataExchangeComboBox!.SelectedItem = _tempSettings.DefaultMarketDataExchange;
        DefaultMarketDataExchangeComboBox.SelectionChanged += (_, _) =>
        {
            if (DefaultMarketDataExchangeComboBox.SelectedItem is string exchange)
            {
                _tempSettings.DefaultMarketDataExchange = exchange;
            }
        };
        
        DefaultTradingExchangeComboBox!.SelectedItem = _tempSettings.DefaultTradingExchange;
        DefaultTradingExchangeComboBox.SelectionChanged += (_, _) =>
        {
            if (DefaultTradingExchangeComboBox.SelectedItem is string exchange)
            {
                _tempSettings.DefaultTradingExchange = exchange;
            }
        };
        
        BinanceApiKeyTextBox!.Text = _tempSettings.BinanceApiKey;
        BinanceApiKeyTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.BinanceApiKey = BinanceApiKeyTextBox.Text ?? string.Empty;
        };
        
        BinanceApiSecretTextBox!.Text = _tempSettings.BinanceApiSecret;
        BinanceApiSecretTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.BinanceApiSecret = BinanceApiSecretTextBox.Text ?? string.Empty;
        };
        
        UseBinanceTestnetCheckBox!.IsChecked = _tempSettings.UseBinanceTestnet;
        UseBinanceTestnetCheckBox.IsCheckedChanged += (_, _) => 
            _tempSettings.UseBinanceTestnet = UseBinanceTestnetCheckBox.IsChecked ?? true;
        
        DefaultTradingSymbolTextBox!.Text = _tempSettings.DefaultTradingSymbol;
        DefaultTradingSymbolTextBox.TextChanged += (_, _) =>
        {
            _tempSettings.DefaultTradingSymbol = DefaultTradingSymbolTextBox.Text?.ToUpperInvariant() ?? "BTCUSDT";
        };
        
        DefaultLeverageNumericUpDown!.Value = _tempSettings.DefaultLeverage;
        DefaultLeverageNumericUpDown.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _tempSettings.DefaultLeverage = (int)e.NewValue.Value;
        };
        
        // 更新时间显示
        UpdateCurrentTime();
    }

    private void InitializeTimeZoneComboBox()
    {
        var timeZones = new List<TimeZoneInfo>();
        
        // 添加常用时区
        try
        {
            timeZones.Add(TimeZoneInfo.Utc);
            timeZones.Add(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"));
            timeZones.Add(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
            timeZones.Add(TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"));
            timeZones.Add(TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
            timeZones.Add(TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo"));
            timeZones.Add(TimeZoneInfo.FindSystemTimeZoneById("Asia/Hong_Kong"));
            timeZones.Add(TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore"));
        }
        catch
        {
            // 如果某些时区不存在，忽略
        }
        
        // 添加系统时区（如果不在列表中）
        if (!timeZones.Contains(TimeZoneInfo.Local))
        {
            timeZones.Insert(0, TimeZoneInfo.Local);
        }
        
        TimeZoneComboBox!.ItemsSource = timeZones;
        TimeZoneComboBox.SelectedItem = _tempSettings.GetTimeZone();
    }

    private void UpdateColorPreview()
    {
        // 更新颜色预览
        var risingColor = _tempSettings.GetRisingColor();
        var fallingColor = _tempSettings.GetFallingColor();
        
        if (RisingColorPreview != null)
        {
            RisingColorPreview.Background = new SolidColorBrush(Color.Parse(risingColor));
        }
        
        if (FallingColorPreview != null)
        {
            FallingColorPreview.Background = new SolidColorBrush(Color.Parse(fallingColor));
        }
    }

    private void UpdateCurrentTime()
    {
        try
        {
            var timeZone = _tempSettings.GetTimeZone();
            var currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, timeZone);
            CurrentTimeTextBlock!.Text = currentTime.ToString(_tempSettings.DateTimeFormat);
        }
        catch
        {
            CurrentTimeTextBlock!.Text = DateTime.Now.ToString(_tempSettings.DateTimeFormat);
        }
    }

    private void StartTimeUpdateTimer()
    {
        _timeUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timeUpdateTimer.Tick += (s, e) => UpdateCurrentTime();
        _timeUpdateTimer.Start();
    }

    /// <summary>
    /// 创建颜色方案设置区块
    /// </summary>
    private Border CreateColorSchemeSection()
    {
        var section = CreateSectionCard("颜色方案", "选择K线图表的颜色显示方案");
        _categorySectionMap["SectionColorScheme"] = section;
        
        var stack = new StackPanel { Spacing = 12 };
        
        ColorSchemeRedFallGreenRise = new RadioButton
        {
            Content = "红跌绿涨（国际习惯）",
            GroupName = "ColorScheme"
        };
        
        ColorSchemeRedRiseGreenFall = new RadioButton
        {
            Content = "红涨绿跌（中国习惯）",
            GroupName = "ColorScheme"
        };
        
        stack.Children.Add(ColorSchemeRedFallGreenRise);
        stack.Children.Add(ColorSchemeRedRiseGreenFall);
        
        // 颜色预览
        var previewGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            Margin = new Thickness(0, 8, 0, 0)
        };
        
        previewGrid.Children.Add(new TextBlock { Text = "涨：", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        
        RisingColorPreview = new Border
        {
            [Grid.ColumnProperty] = 1,
            Background = new SolidColorBrush(Color.Parse("#26A69A")),
            Height = 24,
            Width = 80,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 16, 0)
        };
        previewGrid.Children.Add(RisingColorPreview);
        
        var fallingLabel = new TextBlock
        {
            [Grid.ColumnProperty] = 2,
            Text = "跌：",
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        previewGrid.Children.Add(fallingLabel);
        
        FallingColorPreview = new Border
        {
            [Grid.ColumnProperty] = 3,
            Background = new SolidColorBrush(Color.Parse("#EF5350")),
            Height = 24,
            Width = 80,
            CornerRadius = new CornerRadius(2)
        };
        previewGrid.Children.Add(FallingColorPreview);
        
        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2B3139")),
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(4),
            Child = previewGrid
        };
        stack.Children.Add(previewBorder);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(stack);
        }
        
        return section;
    }
    
    /// <summary>
    /// 创建时区设置区块
    /// </summary>
    private Border CreateTimeZoneSection()
    {
        var section = CreateSectionCard("时区设置", "选择显示时间的时区");
        _categorySectionMap["SectionTimeZone"] = section;
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 12
        };
        
        grid.Children.Add(new TextBlock
        {
            Text = "时区：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        
        TimeZoneComboBox = new ComboBox
        {
            [Grid.ColumnProperty] = 1,
            MinWidth = 300
        };
        grid.Children.Add(TimeZoneComboBox);
        
        var currentTimeLabel = new TextBlock
        {
            [Grid.RowProperty] = 1,
            Text = "当前时间：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        grid.Children.Add(currentTimeLabel);
        
        CurrentTimeTextBlock = new TextBlock
        {
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(CurrentTimeTextBlock);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(grid);
        }
        
        return section;
    }
    
    /// <summary>
    /// 创建显示设置区块
    /// </summary>
    private Border CreateDisplaySection()
    {
        var section = CreateSectionCard("显示设置", null);
        _categorySectionMap["SectionDisplay"] = section;
        
        var stack = new StackPanel { Spacing = 12 };
        
        ShowGridLinesCheckBox = new CheckBox { Content = "显示网格线" };
        ShowCrosshairCheckBox = new CheckBox { Content = "显示十字线" };
        
        stack.Children.Add(ShowGridLinesCheckBox);
        stack.Children.Add(ShowCrosshairCheckBox);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(stack);
        }
        
        return section;
    }
    
    /// <summary>
    /// 创建配置区块卡片
    /// </summary>
    private Border CreateSectionCard(string title, string? description)
    {
        var stack = new StackPanel { Spacing = 16 };
        
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold
        });
        
        if (!string.IsNullOrEmpty(description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91"))
            });
        }
        
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E2329")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            Child = stack
        };
    }
    
    /// <summary>
    /// 创建数字格式设置区块
    /// </summary>
    private Border CreateNumberFormatSection()
    {
        var section = CreateSectionCard("数字格式", "设置价格和数值的小数位数");
        _categorySectionMap["SectionNumberFormat"] = section;
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };
        
        grid.Children.Add(new TextBlock
        {
            Text = "小数位数：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        
        DecimalPlacesNumericUpDown = new NumericUpDown
        {
            [Grid.ColumnProperty] = 1,
            Minimum = 0,
            Maximum = 8,
            Value = 2,
            MinWidth = 150
        };
        grid.Children.Add(DecimalPlacesNumericUpDown);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(grid);
        }
        
        return section;
    }
    
    /// <summary>
    /// 创建界面设置区块
    /// </summary>
    private Border CreateUISection()
    {
        var section = CreateSectionCard("界面设置", null);
        _categorySectionMap["SectionUI"] = section;
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 12
        };
        
        grid.Children.Add(new TextBlock
        {
            Text = "自动刷新间隔（秒）：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        
        AutoRefreshIntervalNumericUpDown = new NumericUpDown
        {
            [Grid.ColumnProperty] = 1,
            Minimum = 5,
            Maximum = 300,
            Value = 30,
            MinWidth = 150
        };
        grid.Children.Add(AutoRefreshIntervalNumericUpDown);
        
        var chartLabel = new TextBlock
        {
            [Grid.RowProperty] = 1,
            Text = "图表默认显示K线数量：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        grid.Children.Add(chartLabel);
        
        ChartDefaultVisibleCandlesNumericUpDown = new NumericUpDown
        {
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 1,
            Minimum = 50,
            Maximum = 1000,
            Value = 200,
            MinWidth = 150
        };
        grid.Children.Add(ChartDefaultVisibleCandlesNumericUpDown);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(grid);
        }
        
        return section;
    }
    
    /// <summary>
    /// 创建市场数据设置区块
    /// </summary>
    private Border CreateMarketDataSection()
    {
        var section = CreateSectionCard("市场数据设置", null);
        _categorySectionMap["SectionMarketData"] = section;
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 12
        };
        
        grid.Children.Add(new TextBlock
        {
            Text = "默认交易对：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        
        DefaultSymbolTextBox = new TextBox
        {
            [Grid.ColumnProperty] = 1,
            MinWidth = 200
        };
        grid.Children.Add(DefaultSymbolTextBox);
        
        var klineLabel = new TextBlock
        {
            [Grid.RowProperty] = 1,
            Text = "默认K线数量：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        grid.Children.Add(klineLabel);
        
        DefaultKlineLimitNumericUpDown = new NumericUpDown
        {
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 1,
            Minimum = 100,
            Maximum = 10000,
            Value = 1440,
            MinWidth = 150
        };
        grid.Children.Add(DefaultKlineLimitNumericUpDown);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(grid);
        }
        
        return section;
    }

    /// <summary>
    /// 创建网络设置区块
    /// </summary>
    private Border CreateNetworkSection()
    {
        var section = CreateSectionCard("网络代理设置", "配置HTTP代理以访问被限制的API（如币安、OpenAI等）");
        _categorySectionMap["SectionNetwork"] = section;
        
        var stack = new StackPanel { Spacing = 16 };
        
        // 启用代理复选框
        EnableProxyCheckBox = new CheckBox 
        { 
            Content = "启用代理",
            FontWeight = Avalonia.Media.FontWeight.Bold
        };
        stack.Children.Add(EnableProxyCheckBox);
        
        // 代理配置表单
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 12
        };
        
        // 行0: 代理类型
        var typeLabel = new TextBlock 
        { 
            Text = "代理类型：", 
            VerticalAlignment = VerticalAlignment.Center 
        };
        grid.Children.Add(typeLabel);
        
        ProxyTypeComboBox = new ComboBox
        {
            [Grid.ColumnProperty] = 1,
            [Grid.ColumnSpanProperty] = 2,
            MinWidth = 200
        };
        ProxyTypeComboBox.Items.Add(new ComboBoxItem { Content = "系统代理", Tag = "System" });
        ProxyTypeComboBox.Items.Add(new ComboBoxItem { Content = "HTTP/HTTPS", Tag = "Http" });
        ProxyTypeComboBox.Items.Add(new ComboBoxItem { Content = "SOCKS5", Tag = "Socks5" });
        ProxyTypeComboBox.SelectedIndex = 1; // 默认选择 HTTP
        grid.Children.Add(ProxyTypeComboBox);
        
        // 行1: 代理地址
        var addressLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 1,
            Text = "代理地址：", 
            VerticalAlignment = VerticalAlignment.Center 
        };
        grid.Children.Add(addressLabel);
        
        ProxyAddressTextBox = new TextBox 
        { 
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 1,
            Watermark = "127.0.0.1 或 proxy.example.com",
            MinWidth = 250
        };
        grid.Children.Add(ProxyAddressTextBox);
        
        // 行2: 端口
        var portLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 2,
            Text = "端口：", 
            VerticalAlignment = VerticalAlignment.Center 
        };
        grid.Children.Add(portLabel);
        
        ProxyPortNumericUpDown = new NumericUpDown
        {
            [Grid.RowProperty] = 2,
            [Grid.ColumnProperty] = 1,
            [Grid.ColumnSpanProperty] = 2,
            Minimum = 1,
            Maximum = 65535,
            Value = 7890,
            MinWidth = 150
        };
        grid.Children.Add(ProxyPortNumericUpDown);
        
        // 行3: 用户名
        var usernameLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 3,
            Text = "用户名：", 
            VerticalAlignment = VerticalAlignment.Center 
        };
        grid.Children.Add(usernameLabel);
        
        ProxyUsernameTextBox = new TextBox 
        { 
            [Grid.RowProperty] = 3,
            [Grid.ColumnProperty] = 1,
            [Grid.ColumnSpanProperty] = 2,
            Watermark = "如需认证请填写"
        };
        grid.Children.Add(ProxyUsernameTextBox);
        
        // 行4: 密码
        var passwordLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 4,
            Text = "密码：", 
            VerticalAlignment = VerticalAlignment.Center 
        };
        grid.Children.Add(passwordLabel);
        
        ProxyPasswordTextBox = new TextBox 
        { 
            [Grid.RowProperty] = 4,
            [Grid.ColumnProperty] = 1,
            [Grid.ColumnSpanProperty] = 2,
            PasswordChar = '●',
            Watermark = "如需认证请填写"
        };
        grid.Children.Add(ProxyPasswordTextBox);
        
        // 行5: SSL证书验证
        SkipSslCertificateValidationCheckBox = new CheckBox
        {
            [Grid.RowProperty] = 5,
            [Grid.ColumnProperty] = 1,
            [Grid.ColumnSpanProperty] = 2,
            Content = "跳过SSL证书验证（仅调试用，生产环境勿用）",
            Foreground = new SolidColorBrush(Color.Parse("#FFA500"))
        };
        grid.Children.Add(SkipSslCertificateValidationCheckBox);
        
        // 行6: 测试按钮和状态
        TestProxyButton = new Button
        {
            [Grid.RowProperty] = 6,
            [Grid.ColumnProperty] = 0,
            [Grid.ColumnSpanProperty] = 3,
            Content = "🔍 测试代理连接",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 8)
        };
        TestProxyButton.Click += TestProxyButton_Click;
        grid.Children.Add(TestProxyButton);
        
        stack.Children.Add(grid);
        
        // 状态显示区域 - 深色主题
        var statusBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#484C51")), // 深灰色背景
            BorderBrush = new SolidColorBrush(Color.Parse("#5A5E63")), // 稍亮的边框
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12),
            Margin = new Thickness(0, 12, 0, 0),
            BoxShadow = new BoxShadows(new BoxShadow 
            { 
                OffsetX = 0, 
                OffsetY = 1, 
                Blur = 3, 
                Color = Color.Parse("#20000000") // 稍深的阴影
            })
        };
        
        var statusStack = new StackPanel { Spacing = 6 };
        
        var statusHeader = new TextBlock
        {
            Text = "📊 代理状态",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#E9ECEF")), // 浅色文字
            Margin = new Thickness(0, 0, 0, 4)
        };
        statusStack.Children.Add(statusHeader);
        
        ProxyStatusTextBlock = new TextBlock
        {
            Text = "未测试",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#CED4DA")) // 浅灰色文字
        };
        statusStack.Children.Add(ProxyStatusTextBlock);
        
        ProxyLatencyTextBlock = new TextBlock
        {
            Text = "",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#CED4DA")) // 浅灰色文字
        };
        statusStack.Children.Add(ProxyLatencyTextBlock);
        
        statusBorder.Child = statusStack;
        stack.Children.Add(statusBorder);
        
        // 说明文字
        var hintText = new TextBlock
        {
            Text = "💡 提示：\n" +
                   "• 选择\"系统代理\"会自动使用系统的代理设置\n" +
                   "• 常见HTTP代理端口：7890（Clash）、1080（V2Ray）、8118（Privoxy）\n" +
                   "• SOCKS5 通常用于高级场景，如SSH隧道",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stack.Children.Add(hintText);
        
        // 根据代理启用状态控制表单的启用状态
        EnableProxyCheckBox!.IsCheckedChanged += (_, _) =>
        {
            var isEnabled = EnableProxyCheckBox.IsChecked ?? false;
            grid.IsEnabled = isEnabled;
            TestProxyButton!.IsEnabled = isEnabled;
            
            if (!isEnabled)
            {
                ProxyStatusTextBlock!.Text = "代理已禁用";
                ProxyStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#ADB5BD")); // 柔和的灰色
                ProxyLatencyTextBlock!.Text = "";
            }
        };
        grid.IsEnabled = false; // 默认禁用
        TestProxyButton.IsEnabled = false;
        
        // 代理类型切换时控制地址/端口的启用状态
        ProxyTypeComboBox.SelectionChanged += (_, _) =>
        {
            var isSystemProxy = (ProxyTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "System";
            ProxyAddressTextBox!.IsEnabled = !isSystemProxy;
            ProxyPortNumericUpDown!.IsEnabled = !isSystemProxy;
        };
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(stack);
        }
        
        return section;
    }
    
    /// <summary>
    /// 测试代理连接按钮点击事件
    /// </summary>
    private async void TestProxyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TestProxyButton == null || ProxyStatusTextBlock == null || ProxyLatencyTextBlock == null)
            return;
        
        TestProxyButton.IsEnabled = false;
        TestProxyButton.Content = "⏳ 测试中...";
        ProxyStatusTextBlock.Text = "正在测试代理连接...";
        ProxyStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#0D6EFD")); // 现代蓝色
        ProxyLatencyTextBlock.Text = "";
        
        try
        {
            // 构建临时代理配置用于测试
            var testConfig = new Services.Network.ProxyConfig
            {
                IsEnabled = true,
                Type = GetSelectedProxyType(),
                Address = ProxyAddressTextBox?.Text?.Trim(),
                Port = (int)(ProxyPortNumericUpDown?.Value ?? 7890),
                Username = ProxyUsernameTextBox?.Text?.Trim(),
                Password = ProxyPasswordTextBox?.Text,
                SkipSslValidation = SkipSslCertificateValidationCheckBox?.IsChecked ?? false,
                TimeoutSeconds = 10
            };
            
            // 创建验证器并测试
            var validator = new Services.Network.ProxyValidator();
            var result = await validator.ValidateProxyWithDetailsAsync(testConfig);
            
            // 显示结果
            if (result.IsSuccess)
            {
                ProxyStatusTextBlock.Text = "✅ 代理连接成功";
                ProxyStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#198754")); // Bootstrap绿色
                
                var latencyInfo = $"延迟: {result.LatencyMs}ms";
                if (!string.IsNullOrWhiteSpace(result.ExternalIp))
                {
                    latencyInfo += $" | 外网IP: {result.ExternalIp}";
                }
                ProxyLatencyTextBlock.Text = latencyInfo;
                ProxyLatencyTextBlock.Foreground = new SolidColorBrush(Color.Parse("#198754"));
            }
            else
            {
                ProxyStatusTextBlock.Text = $"❌ 代理连接失败";
                ProxyStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#DC3545")); // Bootstrap红色
                ProxyLatencyTextBlock.Text = result.ErrorMessage ?? "未知错误";
                ProxyLatencyTextBlock.Foreground = new SolidColorBrush(Color.Parse("#DC3545"));
            }
        }
        catch (Exception ex)
        {
            ProxyStatusTextBlock.Text = "❌ 测试异常";
            ProxyStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#DC3545")); // Bootstrap红色
            ProxyLatencyTextBlock.Text = ex.Message;
            ProxyLatencyTextBlock.Foreground = new SolidColorBrush(Color.Parse("#DC3545"));
            
            Console.WriteLine($"[SettingsDialog] 代理测试异常: {ex}");
        }
        finally
        {
            TestProxyButton.IsEnabled = true;
            TestProxyButton.Content = "🔍 测试代理连接";
        }
    }
    
    /// <summary>
    /// 获取选中的代理类型
    /// </summary>
    private Services.Network.ProxyType GetSelectedProxyType()
    {
        var tag = (ProxyTypeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return tag switch
        {
            "System" => Services.Network.ProxyType.System,
            "Socks5" => Services.Network.ProxyType.Socks5,
            _ => Services.Network.ProxyType.Http
        };
    }
    
    /// <summary>
    /// 解析代理地址和端口（从完整URI中提取）
    /// </summary>
    private (string? address, int port) ParseProxyAddressAndPort(string? proxyUri)
    {
        if (string.IsNullOrWhiteSpace(proxyUri))
            return (null, 7890);
        
        try
        {
            // 尝试作为完整URI解析
            if (Uri.TryCreate(proxyUri, UriKind.Absolute, out var uri))
            {
                return (uri.Host, uri.Port > 0 ? uri.Port : 7890);
            }
            
            // 尝试解析 host:port 格式
            var parts = proxyUri.Split(':');
            if (parts.Length >= 2)
            {
                // 可能是 http://host:port 或 host:port
                var host = parts[0].Replace("http://", "").Replace("https://", "").Replace("socks5://", "");
                if (int.TryParse(parts[^1], out var port))
                {
                    return (host, port);
                }
            }
            
            // 只有地址，没有端口
            return (proxyUri, 7890);
        }
        catch
        {
            return (proxyUri, 7890);
        }
    }
    
    /// <summary>
    /// 更新代理设置到临时配置
    /// </summary>
    private void UpdateProxySettings()
    {
        if (ProxyTypeComboBox == null || ProxyAddressTextBox == null || ProxyPortNumericUpDown == null)
            return;
        
        var proxyType = GetSelectedProxyType();
        
        // 如果是系统代理，清空地址
        if (proxyType == Services.Network.ProxyType.System)
        {
            _tempSettings.ProxyAddress = null;
        }
        else
        {
            // 构建代理地址
            var address = ProxyAddressTextBox.Text?.Trim();
            var port = (int)(ProxyPortNumericUpDown.Value ?? 7890);
            
            if (!string.IsNullOrWhiteSpace(address))
            {
                var protocol = proxyType switch
                {
                    Services.Network.ProxyType.Socks5 => "socks5",
                    _ => "http"
                };
                _tempSettings.ProxyAddress = $"{protocol}://{address}:{port}";
            }
            else
            {
                _tempSettings.ProxyAddress = null;
            }
        }
        
        _tempSettings.ProxyUsername = string.IsNullOrWhiteSpace(ProxyUsernameTextBox?.Text) 
            ? null 
            : ProxyUsernameTextBox.Text.Trim();
        
        // 加密密码存储
        var password = string.IsNullOrWhiteSpace(ProxyPasswordTextBox?.Text) 
            ? null 
            : ProxyPasswordTextBox.Text;
        
        if (!string.IsNullOrWhiteSpace(password))
        {
            var encryptionService = new Services.Security.PasswordEncryptionService();
            _tempSettings.ProxyPassword = encryptionService.EncryptIfNeeded(password);
        }
        else
        {
            _tempSettings.ProxyPassword = null;
        }
    }
    
    /// <summary>
    /// 创建AI设置区块
    /// </summary>
    private Border CreateAISection()
    {
        var section = CreateSectionCard("AI 设置", "配置 AI 服务提供商和相关参数");
        _categorySectionMap["SectionAI"] = section;
        
        var stack = new StackPanel { Spacing = 16 };
        
        // Provider 选择
        var providerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        providerGrid.Children.Add(new TextBlock 
        { 
            Text = "AI Provider：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0) 
        });
        
        AiProviderComboBox = new ComboBox
        {
            [Grid.ColumnProperty] = 1,
            MinWidth = 200
        };
        AiProviderComboBox.SelectionChanged += OnAiProviderChanged;
        providerGrid.Children.Add(AiProviderComboBox);
        stack.Children.Add(providerGrid);
        
        // API 密钥区域
        var apiKeyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 12
        };
        
        // DeepSeek API Key
        var deepseekLabel = new TextBlock 
        { 
            Text = "API Key：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0) 
        };
        apiKeyGrid.Children.Add(deepseekLabel);
        DeepSeekApiKeyTextBox = new TextBox 
        { 
            [Grid.ColumnProperty] = 1, 
            Watermark = "sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 
            PasswordChar = '*' 
        };
        apiKeyGrid.Children.Add(DeepSeekApiKeyTextBox);
        
        // OpenAI API Key
        var openaiLabel = new TextBlock 
        { 
            Text = "API Key：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0),
            IsVisible = false
        };
        apiKeyGrid.Children.Add(openaiLabel);
        OpenAiApiKeyTextBox = new TextBox 
        { 
            [Grid.ColumnProperty] = 1, 
            Watermark = "sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 
            PasswordChar = '*',
            IsVisible = false
        };
        apiKeyGrid.Children.Add(OpenAiApiKeyTextBox);
        
        // Qwen API Key
        var qwenLabel = new TextBlock 
        { 
            Text = "API Key：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0),
            IsVisible = false
        };
        apiKeyGrid.Children.Add(qwenLabel);
        QwenApiKeyTextBox = new TextBox 
        { 
            [Grid.ColumnProperty] = 1, 
            Watermark = "sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 
            PasswordChar = '*',
            IsVisible = false
        };
        apiKeyGrid.Children.Add(QwenApiKeyTextBox);
        
        // ChatGLM API Key
        var chatglmLabel = new TextBlock 
        { 
            Text = "API Key：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0),
            IsVisible = false
        };
        apiKeyGrid.Children.Add(chatglmLabel);
        ChatGLMApiKeyTextBox = new TextBox 
        { 
            [Grid.ColumnProperty] = 1, 
            Watermark = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 
            PasswordChar = '*',
            IsVisible = false
        };
        apiKeyGrid.Children.Add(ChatGLMApiKeyTextBox);
        
        // API 基础地址
        var apiUrlLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 1, 
            Text = "API地址：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0) 
        };
        apiKeyGrid.Children.Add(apiUrlLabel);
        AiApiBaseUrlTextBox = new TextBox 
        { 
            [Grid.RowProperty] = 1, 
            [Grid.ColumnProperty] = 1, 
            Watermark = "https://api.deepseek.com" 
        };
        apiKeyGrid.Children.Add(AiApiBaseUrlTextBox);
        
        // 模型名称
        var modelLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 2, 
            Text = "模型名称：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0) 
        };
        apiKeyGrid.Children.Add(modelLabel);
        AiModelTextBox = new TextBox 
        { 
            [Grid.RowProperty] = 2, 
            [Grid.ColumnProperty] = 1, 
            Watermark = "deepseek-chat" 
        };
        apiKeyGrid.Children.Add(AiModelTextBox);
        
        // 温度参数
        var tempLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 3, 
            Text = "温度参数：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0) 
        };
        apiKeyGrid.Children.Add(tempLabel);
        AiTemperatureNumericUpDown = new NumericUpDown 
        { 
            [Grid.RowProperty] = 3, 
            [Grid.ColumnProperty] = 1, 
            Minimum = 0m, 
            Maximum = 2m, 
            Increment = 0.1m, 
            Value = 0.7m 
        };
        apiKeyGrid.Children.Add(AiTemperatureNumericUpDown);
        
        // 最大Token数
        var tokensLabel = new TextBlock 
        { 
            [Grid.RowProperty] = 4, 
            Text = "最大Token数：", 
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 12, 0) 
        };
        apiKeyGrid.Children.Add(tokensLabel);
        AiMaxTokensNumericUpDown = new NumericUpDown 
        { 
            [Grid.RowProperty] = 4, 
            [Grid.ColumnProperty] = 1, 
            Minimum = 100, 
            Maximum = 10000, 
            Value = 4096 
        };
        apiKeyGrid.Children.Add(AiMaxTokensNumericUpDown);
        
        // 流式响应
        AiEnableStreamingCheckBox = new CheckBox
        {
            [Grid.RowProperty] = 5,
            [Grid.ColumnProperty] = 0,
            [Grid.ColumnSpanProperty] = 2,
            Content = "启用流式响应"
        };
        apiKeyGrid.Children.Add(AiEnableStreamingCheckBox);
        
        stack.Children.Add(apiKeyGrid);
        
        // 测试连接按钮
        TestAiConnectionButton = new Button
        {
            Content = "测试连接",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };
        TestAiConnectionButton.Click += TestAiConnection_OnClick;
        stack.Children.Add(TestAiConnectionButton);
        
        // 连接状态文本
        AiConnectionStatusTextBlock = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
            IsVisible = false,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        stack.Children.Add(AiConnectionStatusTextBlock);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(stack);
        }
        
        return section;
    }
    
    /// <summary>
    /// 创建交易所配置区块
    /// </summary>
    private Border CreateExchangeSection()
    {
        var section = CreateSectionCard("币安交易所配置", "配置币安合约交易API密钥（建议先在测试网验证）");
        _categorySectionMap["SectionExchange"] = section;
        
        var stack = new StackPanel { Spacing = 12 };
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 12
        };

        // 默认行情/交易交易所（Data Plane 路由）
        grid.Children.Add(new TextBlock
        {
            Text = "默认行情所：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });

        DefaultMarketDataExchangeComboBox = new ComboBox
        {
            [Grid.ColumnProperty] = 1,
            ItemsSource = new List<string> { "Binance", "Okx", "Bybit" },
            SelectedItem = "Binance"
        };
        grid.Children.Add(DefaultMarketDataExchangeComboBox);

        var defaultTradingExchangeLabel = new TextBlock
        {
            [Grid.RowProperty] = 1,
            Text = "默认实盘所：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        grid.Children.Add(defaultTradingExchangeLabel);

        DefaultTradingExchangeComboBox = new ComboBox
        {
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 1,
            ItemsSource = new List<string> { "Binance", "Okx", "Bybit" },
            SelectedItem = "Binance"
        };
        grid.Children.Add(DefaultTradingExchangeComboBox);
        
        grid.Children.Add(new TextBlock { [Grid.RowProperty] = 2, Text = "API Key：", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        BinanceApiKeyTextBox = new TextBox { [Grid.RowProperty] = 2, [Grid.ColumnProperty] = 1, Watermark = "请输入币安API Key" };
        grid.Children.Add(BinanceApiKeyTextBox);
        
        var secretLabel = new TextBlock { [Grid.RowProperty] = 3, Text = "API Secret：", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        grid.Children.Add(secretLabel);
        BinanceApiSecretTextBox = new TextBox { [Grid.RowProperty] = 3, [Grid.ColumnProperty] = 1, PasswordChar = '*', Watermark = "请输入币安API Secret" };
        grid.Children.Add(BinanceApiSecretTextBox);
        
        UseBinanceTestnetCheckBox = new CheckBox
        {
            [Grid.RowProperty] = 4,
            [Grid.ColumnProperty] = 0,
            [Grid.ColumnSpanProperty] = 2,
            Content = "使用测试网（推荐先在测试网验证策略）",
            IsChecked = true
        };
        grid.Children.Add(UseBinanceTestnetCheckBox);
        
        var symbolLabel = new TextBlock { [Grid.RowProperty] = 5, Text = "默认交易对：", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        grid.Children.Add(symbolLabel);
        DefaultTradingSymbolTextBox = new TextBox { [Grid.RowProperty] = 5, [Grid.ColumnProperty] = 1, Watermark = "BTCUSDT", Text = "BTCUSDT" };
        grid.Children.Add(DefaultTradingSymbolTextBox);
        
        var leverageLabel = new TextBlock { [Grid.RowProperty] = 6, Text = "默认杠杆：", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        grid.Children.Add(leverageLabel);
        DefaultLeverageNumericUpDown = new NumericUpDown { [Grid.RowProperty] = 6, [Grid.ColumnProperty] = 1, Minimum = 1, Maximum = 125, Value = 10 };
        grid.Children.Add(DefaultLeverageNumericUpDown);
        
        TestBinanceConnectionButton = new Button
        {
            [Grid.RowProperty] = 7,
            [Grid.ColumnProperty] = 1,
            Content = "测试连接",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        TestBinanceConnectionButton.Click += TestBinanceConnection_OnClick;
        grid.Children.Add(TestBinanceConnectionButton);
        
        stack.Children.Add(grid);
        
        BinanceConnectionStatusTextBlock = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
            IsVisible = false
        };
        stack.Children.Add(BinanceConnectionStatusTextBlock);
        
        if (section.Child is StackPanel sectionStack)
        {
            sectionStack.Children.Add(stack);
        }
        
        return section;
    }
    
    /// <summary>
    /// 测试币安连接
    /// </summary>
    private async void TestBinanceConnection_OnClick(object? sender, RoutedEventArgs e)
    {
        var apiKey = BinanceApiKeyTextBox!.Text ?? string.Empty;
        var apiSecret = BinanceApiSecretTextBox!.Text ?? string.Empty;
        var useTestnet = UseBinanceTestnetCheckBox!.IsChecked ?? true;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            BinanceConnectionStatusTextBlock!.Text = "❌ 请先输入API Key和API Secret";
            BinanceConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#EF5350"));
            BinanceConnectionStatusTextBlock.IsVisible = true;
            return;
        }

        // 禁用按钮，显示测试中状态
        TestBinanceConnectionButton!.IsEnabled = false;
        BinanceConnectionStatusTextBlock!.Text = "⏳ 正在测试连接...";
        BinanceConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#FFB74D"));
        BinanceConnectionStatusTextBlock.IsVisible = true;

        try
        {
            var exchange = App.DataPlane.Trading(Prophet.Client.Data.ExchangeId.Binance);
            var config = new ExchangeConfig
            {
                ExchangeName = "Binance",
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UseTestnet = useTestnet,
                EnableProxy = _appSettingsService.Settings.EnableProxy,
                ProxyAddress = _appSettingsService.Settings.ProxyAddress,
                ProxyUsername = _appSettingsService.Settings.ProxyUsername,
                ProxyPassword = _appSettingsService.Settings.ProxyPassword
            };

            var success = await exchange.InitializeAsync(config);
            
            if (success)
            {
                var account = await exchange.GetAccountInfoAsync();
                
                BinanceConnectionStatusTextBlock!.Text = $"✅ 连接成功！账户余额: {account.TotalBalance:F2} USDT ({(useTestnet ? "测试网" : "主网")})";
                BinanceConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#66BB6A"));
                
                Console.WriteLine($"✅ 币安API连接测试成功");
                Console.WriteLine($"   模式: {(useTestnet ? "测试网" : "主网")}");
                Console.WriteLine($"   总余额: {account.TotalBalance} USDT");
                Console.WriteLine($"   可用余额: {account.AvailableBalance} USDT");
            }
            else
            {
                BinanceConnectionStatusTextBlock!.Text = "❌ 连接失败，请检查API密钥是否正确";
                BinanceConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#EF5350"));
            }
        }
        catch (Exception ex)
        {
            BinanceConnectionStatusTextBlock!.Text = $"❌ 连接失败: {ex.Message}";
            BinanceConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#EF5350"));
            
            Console.WriteLine($"❌ 币安API连接测试失败: {ex.Message}");
        }
        finally
        {
            TestBinanceConnectionButton!.IsEnabled = true;
        }
    }
    
    /// <summary>
    /// AI Provider 切换事件
    /// </summary>
    private void OnAiProviderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AiProviderComboBox?.SelectedItem is string provider)
        {
            UpdateAiProviderUI(provider);
        }
    }
    
    /// <summary>
    /// 根据选中的 Provider 更新 UI
    /// </summary>
    private void UpdateAiProviderUI(string provider)
    {
        // 隐藏测试结果
        if (AiConnectionStatusTextBlock != null)
        {
            AiConnectionStatusTextBlock.IsVisible = false;
        }
        
        // 先隐藏所有 API Key 输入框
        if (DeepSeekApiKeyTextBox != null)
        {
            DeepSeekApiKeyTextBox.IsVisible = false;
            // 找到对应的 Label
            if (DeepSeekApiKeyTextBox.Parent is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is TextBlock tb && tb.GetValue(Grid.ColumnProperty) is int col && col == 0 && 
                        tb.GetValue(Grid.RowProperty) is int row && row == 0)
                    {
                        tb.IsVisible = false;
                    }
                }
            }
        }
        
        if (OpenAiApiKeyTextBox != null)
        {
            OpenAiApiKeyTextBox.IsVisible = false;
            if (OpenAiApiKeyTextBox.Parent is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is TextBlock tb && child != DeepSeekApiKeyTextBox?.Parent && 
                        tb.Text == "API Key：" && tb != ((DeepSeekApiKeyTextBox?.Parent as Grid)?.Children.OfType<TextBlock>().FirstOrDefault()))
                    {
                        var openaiLabel = grid.Children.OfType<TextBlock>().Skip(1).FirstOrDefault();
                        if (openaiLabel != null) openaiLabel.IsVisible = false;
                        break;
                    }
                }
            }
        }
        
        if (QwenApiKeyTextBox != null) QwenApiKeyTextBox.IsVisible = false;
        if (ChatGLMApiKeyTextBox != null) ChatGLMApiKeyTextBox.IsVisible = false;
        
        // 根据 Provider 显示对应的 API Key 输入框并设置默认值
        switch (provider)
        {
            case "DeepSeek":
                if (DeepSeekApiKeyTextBox != null)
                {
                    DeepSeekApiKeyTextBox.IsVisible = true;
                    // 显示对应的 Label
                    if (DeepSeekApiKeyTextBox.Parent is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is TextBlock tb && tb.GetValue(Grid.ColumnProperty) is int col && col == 0 && 
                                tb.GetValue(Grid.RowProperty) is int row && row == 0)
                            {
                                tb.IsVisible = true;
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(AiApiBaseUrlTextBox?.Text) || 
                    AiApiBaseUrlTextBox.Text != "https://api.deepseek.com")
                    AiApiBaseUrlTextBox!.Text = "https://api.deepseek.com";
                if (string.IsNullOrEmpty(AiModelTextBox?.Text) || 
                    AiModelTextBox.Text != "deepseek-chat")
                    AiModelTextBox!.Text = "deepseek-chat";
                break;
                
            case "OpenAI":
                if (OpenAiApiKeyTextBox != null)
                {
                    OpenAiApiKeyTextBox.IsVisible = true;
                    // 显示对应的 Label
                    if (OpenAiApiKeyTextBox.Parent is Grid grid)
                    {
                        var labels = grid.Children.OfType<TextBlock>().Where(tb => tb.Text == "API Key：").ToList();
                        if (labels.Count > 1) labels[1].IsVisible = true;
                    }
                }
                if (string.IsNullOrEmpty(AiApiBaseUrlTextBox?.Text) || 
                    AiApiBaseUrlTextBox.Text != "https://api.openai.com/v1")
                    AiApiBaseUrlTextBox!.Text = "https://api.openai.com/v1";
                if (string.IsNullOrEmpty(AiModelTextBox?.Text) || 
                    AiModelTextBox.Text != "gpt-3.5-turbo")
                    AiModelTextBox!.Text = "gpt-3.5-turbo";
                break;
                
            case "Qwen":
                if (QwenApiKeyTextBox != null)
                {
                    QwenApiKeyTextBox.IsVisible = true;
                    // 显示对应的 Label
                    if (QwenApiKeyTextBox.Parent is Grid grid)
                    {
                        var labels = grid.Children.OfType<TextBlock>().Where(tb => tb.Text == "API Key：").ToList();
                        if (labels.Count > 2) labels[2].IsVisible = true;
                    }
                }
                if (string.IsNullOrEmpty(AiApiBaseUrlTextBox?.Text) || 
                    AiApiBaseUrlTextBox.Text != "https://dashscope.aliyuncs.com/api/v1")
                    AiApiBaseUrlTextBox!.Text = "https://dashscope.aliyuncs.com/api/v1";
                if (string.IsNullOrEmpty(AiModelTextBox?.Text) || 
                    AiModelTextBox.Text != "qwen-plus")
                    AiModelTextBox!.Text = "qwen-plus";
                break;
                
            case "ChatGLM":
                if (ChatGLMApiKeyTextBox != null)
                {
                    ChatGLMApiKeyTextBox.IsVisible = true;
                    // 显示对应的 Label
                    if (ChatGLMApiKeyTextBox.Parent is Grid grid)
                    {
                        var labels = grid.Children.OfType<TextBlock>().Where(tb => tb.Text == "API Key：").ToList();
                        if (labels.Count > 3) labels[3].IsVisible = true;
                    }
                }
                if (string.IsNullOrEmpty(AiApiBaseUrlTextBox?.Text) || 
                    AiApiBaseUrlTextBox.Text != "https://open.bigmodel.cn/api/paas/v4")
                    AiApiBaseUrlTextBox!.Text = "https://open.bigmodel.cn/api/paas/v4";
                if (string.IsNullOrEmpty(AiModelTextBox?.Text) || 
                    AiModelTextBox.Text != "glm-4")
                    AiModelTextBox!.Text = "glm-4";
                break;
        }
    }
    
    /// <summary>
    /// 测试 AI 连接
    /// </summary>
    private async void TestAiConnection_OnClick(object? sender, RoutedEventArgs e)
    {
        var provider = AiProviderComboBox?.SelectedItem as string ?? "DeepSeek";
        var apiKey = provider switch
        {
            "DeepSeek" => DeepSeekApiKeyTextBox?.Text,
            "OpenAI" => OpenAiApiKeyTextBox?.Text,
            "Qwen" => QwenApiKeyTextBox?.Text,
            "ChatGLM" => ChatGLMApiKeyTextBox?.Text,
            _ => null
        };
        var apiBaseUrl = AiApiBaseUrlTextBox?.Text ?? string.Empty;
        var model = AiModelTextBox?.Text ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            AiConnectionStatusTextBlock!.Text = $"❌ 请先输入 {provider} API Key";
            AiConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#EF5350"));
            AiConnectionStatusTextBlock.IsVisible = true;
            return;
        }
        
        // 禁用按钮，显示测试中状态
        TestAiConnectionButton!.IsEnabled = false;
        AiConnectionStatusTextBlock!.Text = "⏳ 正在测试连接...";
        AiConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#FFB74D"));
        AiConnectionStatusTextBlock.IsVisible = true;
        
        try
        {
            var aiService = ServiceContainer.GetService<Prophet.Client.Services.AI.Core.AiServiceManager>();
            
            var config = new Prophet.Client.Services.AI.Configuration.AiProviderConfig
            {
                ProviderType = provider,
                ApiKey = apiKey,
                ApiBaseUrl = apiBaseUrl,
                Model = model,
                Temperature = _tempSettings.AiTemperature,
                MaxTokens = _tempSettings.AiMaxTokens
            };
            
            var success = await aiService.InitializeProviderAsync(config);
            
            if (success)
            {
                var validated = await aiService.ValidateConnectionAsync();
                
                if (validated)
                {
                    AiConnectionStatusTextBlock!.Text = $"✅ 连接成功！Provider: {provider}, Model: {model}";
                    AiConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#66BB6A"));
                    Console.WriteLine($"✅ AI API连接测试成功 ({provider})");
                }
                else
                {
                    AiConnectionStatusTextBlock!.Text = "❌ 连接验证失败，请检查API密钥是否正确";
                    AiConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#EF5350"));
                }
            }
            else
            {
                AiConnectionStatusTextBlock!.Text = "❌ 初始化失败，请检查配置";
                AiConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#EF5350"));
            }
        }
        catch (Exception ex)
        {
            AiConnectionStatusTextBlock!.Text = $"❌ 连接失败: {ex.Message}";
            AiConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#EF5350"));
            Console.WriteLine($"❌ AI API连接测试失败: {ex.Message}");
        }
        finally
        {
            TestAiConnectionButton!.IsEnabled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        // 停止定时器
        _timeUpdateTimer?.Stop();
        _timeUpdateTimer = null;
    }
}

