using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Prophet.Client.Controls;
using Prophet.Client.Core;
using Prophet.Client.Data;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Views.Dialogs;
using Prophet.Client.Services.Data;
using Prophet.Client.Services.Indicators;
using Prophet.Client.Services.Settings;
using Prophet.Client.Services.Market;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Prophet.Client.Views.Pages;

public partial class MarketView : UserControl
{
    private Random _random = new Random();
    private TimeFrame _currentTimeFrame = TimeFrame.M15; // 默认15分钟
    private TimeFrame _previousTimeFrame = TimeFrame.M15;
    private string _currentSymbol;
    private readonly MarketDataContext _marketDataContext;
    private readonly MarketDataRepository _marketDataRepository;
    private readonly AppSettingsService _appSettingsService;
    private string? _currentStreamSymbol; // StreamHub订阅使用的symbol（目前仅Binance）
    private bool _isInitialized = false;
    
    // 本地持久化配置键
    private const string PREF_KEY_SYMBOL = "MarketView_Symbol";
    private const string PREF_KEY_TIMEFRAME = "MarketView_TimeFrame";
    
    // 加载动画相关
    private System.Timers.Timer? _loadingAnimationTimer;
    private double _currentRotationAngle = 0;
    
    // 自动刷新相关 ⭐
    private System.Timers.Timer? _autoRefreshTimer;
    private const int AUTO_REFRESH_INTERVAL_MS = 30000; // 30秒自动刷新一次    
    // 默认指标配置
    private IndicatorDialog.IndicatorSettings _defaultIndicatorSettings = new();
    
    // 当前市场数据（用于重新计算指标）
    private MarketDataPackage? _currentMarketData;
    private IDisposable? _streamSubscription;
    
    // 性能优化：防抖机制，限制重绘频率
    private System.Threading.Timer? _redrawDebounceTimer;
    private bool _pendingRedraw = false;
    private bool _pendingRedrawIsCurrentKline = false; // 保存最新的重绘类型
    private readonly object _redrawLock = new object();
    private const int REDRAW_DEBOUNCE_MS = 100; // 最多每100ms重绘一次
    
    public MarketView()
    {
        InitializeComponent();
        
        _marketDataContext = App.MarketDataContext ?? throw new InvalidOperationException("MarketDataContext 未初始化");
        _marketDataRepository = _marketDataContext.Repository;
        
        // 获取设置服务
        _appSettingsService = ServiceContainer.GetService<AppSettingsService>();
        _currentSymbol = _appSettingsService.Settings.DefaultSymbol;
        
        // 加载持久化配置
        LoadPersistedSettings();
        
        // 初始化默认指标配置
        InitializeDefaultIndicatorSettings();
        
        // 延迟初始化 - 使用多个事件确保能正确初始化
        Loaded += OnViewLoaded;
        AttachedToVisualTree += OnViewAttachedToVisualTree;
        
        // 视图卸载时清理资源
        Unloaded += OnViewUnloaded;
        
        // 添加键盘快捷键
        this.KeyDown += OnKeyDown;
        
    }

    private (string BaseSymbol, ExchangeId ExchangeId) ResolveRouting()
    {
        if (InstrumentKey.TryParse(_currentSymbol, out var key))
        {
            var exchange = key.Exchange switch
            {
                "BINANCE" => ExchangeId.Binance,
                "OKX" => ExchangeId.Okx,
                "BYBIT" => ExchangeId.Bybit,
                _ => App.DataPlane.DefaultMarketDataExchange
            };

            return (key.BaseSymbol, exchange);
        }

        return (_currentSymbol, App.DataPlane.DefaultMarketDataExchange);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static List<Candlestick> ToCandles(IReadOnlyList<Prophet.Client.Data.Models.Kline> klines)
    {
        var list = new List<Candlestick>(klines.Count);
        foreach (var k in klines)
        {
            list.Add(new Candlestick
            {
                Time = EnsureUtc(k.OpenTime),
                CloseTime = k.CloseTime.HasValue ? EnsureUtc(k.CloseTime.Value) : null,
                Open = (double)k.Open,
                High = (double)k.High,
                Low = (double)k.Low,
                Close = (double)k.Close,
                Volume = (double)k.Volume,
                IsClosed = k.IsClosed
            });
        }
        return list;
    }

    private async Task<List<Candlestick>> FetchCandlesFromApiAsync(
        string interval,
        int limit,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        var (baseSymbol, exchangeId) = ResolveRouting();
        var provider = App.DataPlane.Market(exchangeId);
        var klines = await provider.FetchKlinesAsync(baseSymbol, interval, limit, startTime, endTime, CancellationToken.None);
        return ToCandles(klines);
    }
    
    /// <summary>
    /// 加载持久化的配置
    /// </summary>
    private void LoadPersistedSettings()
    {
        try
        {
            // 加载Symbol
            var savedSymbol = Avalonia.Application.Current?.Resources[PREF_KEY_SYMBOL] as string;
            if (!string.IsNullOrEmpty(savedSymbol))
            {
                _currentSymbol = savedSymbol;
            }
            
            // 加载TimeFrame
            var savedTimeFrame = Avalonia.Application.Current?.Resources[PREF_KEY_TIMEFRAME] as string;
            if (!string.IsNullOrEmpty(savedTimeFrame) && Enum.TryParse<TimeFrame>(savedTimeFrame, out var timeFrame))
            {
                _currentTimeFrame = timeFrame;
                _previousTimeFrame = timeFrame;
            }
            
            Console.WriteLine($"📂 [MarketView] 加载持久化配置: Symbol={_currentSymbol}, TimeFrame={_currentTimeFrame}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [MarketView] 加载持久化配置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存配置到本地
    /// </summary>
    private void SavePersistedSettings()
    {
        try
        {
            if (Avalonia.Application.Current != null)
            {
                Avalonia.Application.Current.Resources[PREF_KEY_SYMBOL] = _currentSymbol;
                Avalonia.Application.Current.Resources[PREF_KEY_TIMEFRAME] = _currentTimeFrame.ToString();
            }
            
            Console.WriteLine($"💾 [MarketView] 保存持久化配置: Symbol={_currentSymbol}, TimeFrame={_currentTimeFrame}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [MarketView] 保存持久化配置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 附加到可视树时触发
    /// </summary>
    private void OnViewAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (!_isInitialized && IsVisible)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await InitializeAsync();
            });
        }
    }
    
    /// <summary>
    /// 重写属性变化通知以监听IsVisible变化
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == IsVisibleProperty)
        {
            var isVisible = (bool)(change.NewValue ?? false);
            if (isVisible && !_isInitialized)
            {
                // 延迟执行以确保布局完成
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    await InitializeAsync();
                });
            }
        }
    }
    
    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        // Ctrl+Shift+S: 打开副图配置
        if (e.KeyModifiers == (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift) 
            && e.Key == Avalonia.Input.Key.S)
        {
            ShowSubChartDialogWindow();
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// 初始化默认指标配置（MA、EMA、WMA默认选中，各3根线�?    /// </summary>
    private void InitializeDefaultIndicatorSettings()
    {
        // 尝试从本地加载用户保存的设置
        var savedSettings = IndicatorSettingsStorage.LoadSettings();
        if (savedSettings != null)
        {
            _defaultIndicatorSettings = savedSettings;
            return;
        }
        
        // MA默认配置�?(�?, 25(�?, 99(�?
        _defaultIndicatorSettings.MA.IsEnabled = true;
        _defaultIndicatorSettings.MA.Lines = new List<MALineConfig>
        {
            new MALineConfig { IsEnabled = true, PERIOD = 7, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFFFFF" },
            new MALineConfig { IsEnabled = true, PERIOD = 25, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFD700" },
            new MALineConfig { IsEnabled = true, PERIOD = 99, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FF00FF" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#00FFFF" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFA500" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#00FF00" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FF1493" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#1E90FF" }
        };
        
        // EMA默认配置�?(�?, 25(�?, 99(�?
        _defaultIndicatorSettings.EMA.IsEnabled = true;
        _defaultIndicatorSettings.EMA.Lines = new List<MALineConfig>
        {
            new MALineConfig { IsEnabled = true, PERIOD = 7, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFFFFF" },
            new MALineConfig { IsEnabled = true, PERIOD = 25, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFD700" },
            new MALineConfig { IsEnabled = true, PERIOD = 99, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FF00FF" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#00FFFF" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFA500" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#00FF00" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FF1493" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#1E90FF" }
        };
        
        // WMA默认配置�?(�?, 25(�?, 99(�?
        _defaultIndicatorSettings.WMA.IsEnabled = true;
        _defaultIndicatorSettings.WMA.Lines = new List<MALineConfig>
        {
            new MALineConfig { IsEnabled = true, PERIOD = 7, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFFFFF" },
            new MALineConfig { IsEnabled = true, PERIOD = 25, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFD700" },
            new MALineConfig { IsEnabled = true, PERIOD = 99, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FF00FF" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#00FFFF" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FFA500" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#00FF00" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#FF1493" },
            new MALineConfig { IsEnabled = false, PERIOD = 0, Field = PriceField.Close, Style = LineStyle.Solid, Color = "#1E90FF" }
        };
    }
    
    /// <summary>
    /// 视图卸载时清理资源
    /// </summary>
    private void OnViewUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 停止自动刷新定时器
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer = null;
        
        // 清理防抖定时器
        lock (_redrawLock)
        {
            _redrawDebounceTimer?.Dispose();
            _redrawDebounceTimer = null;
            _pendingRedraw = false;
        }
        
        // 停止WebSocket订阅
        _streamSubscription?.Dispose();
        _streamSubscription = null;
        _currentStreamSymbol = null;
        
        // MarketDataRepository 不需要 Dispose（它使用 DBHelper 的静态方法）
    }
    
    /// <summary>
    /// 视图加载完成（只有在可见时才初始化）
    /// </summary>
    private async void OnViewLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 只有当视图可见时才初始化，避免程序启动时预加载
        if (IsVisible)
        {
            Console.WriteLine($"👀 [MarketView] 视图可见，开始初始化");
            await InitializeAsync();
        }
        else
        {
            Console.WriteLine($"👻 [MarketView] 视图已加载但不可见，跳过初始化");
        }
    }
    
    // 防止异步初始化时的竞态条件
    private bool _isInitializing = false;
    
    /// <summary>
    /// 统一的初始化方法
    /// </summary>
    private async Task InitializeAsync()
    {
        // 防止重复初始化（包括正在初始化中的情况）
        if (_isInitialized || _isInitializing)
        {
            Console.WriteLine($"⏭️ [MarketView] 跳过重复初始化 (已初始化: {_isInitialized}, 初始化中: {_isInitializing})");
            return;
        }
        
        _isInitializing = true;
        
        try
        {
            Console.WriteLine($"🚀 [MarketView] 开始初始化...");
            
            // 使用全局API客户端            
            // 离线版：不需要检查登录状态
            // MarketDataRepository 从本地 SQLite 加载数据
            
            // 初始化Symbol下拉框（从 instruments 表加载）
            await InitializeSymbolComboBoxAsync();
            
            // 同步UI控件的选中状态
            SyncUIWithPersistedSettings();
            
            // 订阅图表加载更多数据事件
            MainChart.LoadMoreDataRequested += OnLoadMoreDataRequested;
            
            // 加载本地数据
            await LoadRealDataAsync();
            
            _isInitialized = true;
            Console.WriteLine($"✅ [MarketView] 初始化完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketView] 初始化失败: {ex.Message}");
            ShowLoading($"初始化失败: {ex.Message}");
        }
        finally
        {
            _isInitializing = false;
        }
    }
    
    /// <summary>
    /// 初始化Symbol下拉框（优先从 instruments 表加载；兜底使用 AppConfig.Symbols）
    /// </summary>
    private async Task InitializeSymbolComboBoxAsync()
    {
        if (SymbolComboBox == null) return;
        
        try
        {
            SymbolComboBox.Items.Clear();

            var repo = new InstrumentRepository();
            var instruments = await repo.GetEnabledAsync();

            if (instruments.Count == 0)
            {
                foreach (var symbol in AppConfig.Trading.Symbols)
                {
                    var item = new ComboBoxItem { Content = symbol, Tag = symbol };
                    SymbolComboBox.Items.Add(item);
                }

                Console.WriteLine($"⚠️ [MarketView] instruments 为空，已回退到 AppConfig.Trading.Symbols（{SymbolComboBox.Items.Count}）");
                return;
            }

            foreach (var ins in instruments)
            {
                var item = new ComboBoxItem
                {
                    Content = BuildInstrumentDisplayText(ins),
                    Tag = ins.SymbolKey
                };
                SymbolComboBox.Items.Add(item);
            }
            
            Console.WriteLine($"✅ [MarketView] Symbol下拉框已初始化，共 {SymbolComboBox.Items.Count} 个交易对");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketView] 初始化Symbol下拉框失败: {ex.Message}");
        }
    }

    private static string BuildInstrumentDisplayText(InstrumentDefinition ins)
    {
        var exchangeName = ins.Exchange.ToUpperInvariant() switch
        {
            "BINANCE" => "币安",
            "OKX" => "欧意",
            _ => ins.Exchange.ToUpperInvariant()
        };

        var marketName = ins.MarketType.ToUpperInvariant() switch
        {
            "SWAP" => "永续",
            "SPOT" => "现货",
            _ => ins.MarketType.ToUpperInvariant()
        };

        return $"{ins.BaseSymbol}-{exchangeName}-{marketName}";
    }
    
    /// <summary>
    /// 同步UI控件与持久化配置
    /// </summary>
    private void SyncUIWithPersistedSettings()
    {
        try
        {
            // 同步Symbol下拉框（暂时取消事件订阅，避免触发SelectionChanged）
            if (SymbolComboBox != null)
            {
                SymbolComboBox.SelectionChanged -= OnSymbolChanged;
                
                foreach (var obj in SymbolComboBox.Items)
                {
                    if (obj is ComboBoxItem item && item.Tag?.ToString() == _currentSymbol)
                    {
                        SymbolComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                // 如果没找到匹配项，选择第一个
                if (SymbolComboBox.SelectedItem == null && SymbolComboBox.Items.Count > 0)
                {
                    SymbolComboBox.SelectedItem = SymbolComboBox.Items[0];
                    if (SymbolComboBox.Items[0] is ComboBoxItem firstItem)
                    {
                        _currentSymbol = firstItem.Tag?.ToString() ?? AppConfig.Trading.Symbols[0];
                    }
                }
                
                SymbolComboBox.SelectionChanged += OnSymbolChanged;
            }
            
            // 同步时间框架按钮
            var timeframeButtons = new[] { Btn1m, Btn3m, Btn5m, Btn15m, Btn30m, Btn1h, Btn2h, Btn4h, Btn6h, Btn8h, Btn12h, Btn1d };
            foreach (var btn in timeframeButtons)
            {
                if (btn != null)
                {
                    var btnTimeFrame = btn.Name switch
                    {
                        "Btn1m" => TimeFrame.M1,
                        "Btn3m" => TimeFrame.M3,
                        "Btn5m" => TimeFrame.M5,
                        "Btn15m" => TimeFrame.M15,
                        "Btn30m" => TimeFrame.M30,
                        "Btn1h" => TimeFrame.H1,
                        "Btn2h" => TimeFrame.H2,
                        "Btn4h" => TimeFrame.H4,
                        "Btn6h" => TimeFrame.H6,
                        "Btn8h" => TimeFrame.H8,
                        "Btn12h" => TimeFrame.H12,
                        "Btn1d" => TimeFrame.D1,
                        _ => TimeFrame.M15
                    };
                    
                    btn.IsChecked = (btnTimeFrame == _currentTimeFrame);
                }
            }
            
            Console.WriteLine($"🔄 [MarketView] UI同步完成: Symbol={_currentSymbol}, TimeFrame={_currentTimeFrame}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [MarketView] UI同步失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Symbol切换事件
    /// </summary>
    private async void OnSymbolChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (comboBox.SelectedItem is not ComboBoxItem selectedItem) return;
        
        var newSymbol = selectedItem.Tag?.ToString() ?? selectedItem.Content?.ToString();
        if (string.IsNullOrEmpty(newSymbol)) return;
        
        // 如果Symbol没有改变，不重新加载
        if (newSymbol == _currentSymbol) return;
        
        _currentSymbol = newSymbol;
        
        Console.WriteLine($"📊 [MarketView] Symbol切换: {_currentSymbol}");
        
        // 保存配置
        SavePersistedSettings();
        
        // 停止旧的WebSocket订阅
        _streamSubscription?.Dispose();
        _streamSubscription = null;
        
        // 重新加载数据
        await LoadRealDataAsync();
    }
    
    /// <summary>
    /// 时间周期切换事件
    /// </summary>
    private async void OnTimeFrameChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        
        // 如果点击的是已选中的按钮，保持选中状态（不允许取消选中）        
        if (button.IsChecked != true)
        {
            button.IsChecked = true;
            return;
        }
        
        // 取消其他按钮的选中状态（实现单选效果）
        var buttons = new[] { Btn1m, Btn3m, Btn5m, Btn15m, Btn30m, Btn1h, Btn2h, Btn4h, Btn6h, Btn8h, Btn12h, Btn1d };
        foreach (var btn in buttons)
        {
            if (btn != button && btn != null)
                btn.IsChecked = false;
        }
        
        // 保存旧的时间框架
        _previousTimeFrame = _currentTimeFrame;
        
        // 根据按钮确定时间周期
        _currentTimeFrame = button.Name switch
        {
            "Btn1m" => TimeFrame.M1,
            "Btn3m" => TimeFrame.M3,
            "Btn5m" => TimeFrame.M5,
            "Btn15m" => TimeFrame.M15,
            "Btn30m" => TimeFrame.M30,
            "Btn1h" => TimeFrame.H1,
            "Btn2h" => TimeFrame.H2,
            "Btn4h" => TimeFrame.H4,
            "Btn6h" => TimeFrame.H6,
            "Btn8h" => TimeFrame.H8,
            "Btn12h" => TimeFrame.H12,
            "Btn1d" => TimeFrame.D1,
            _ => TimeFrame.M15
        };
        
        // 保存配置
        SavePersistedSettings();
        
        // 重新加载数据
        await LoadRealDataAsync();
    }
    
    /// <summary>
    /// K线样式切换事件
    /// </summary>
    private void OnCandleStyleChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        
        // 如果点击的是已选中的按钮，保持选中状态（不允许取消选中）        
        if (button.IsChecked != true)
        {
            button.IsChecked = true;
            return;
        }
        
        // 取消其他按钮的选中状态（实现单选效果）
        var buttons = new[] { BtnCandleStyle, BtnLineStyle, BtnLineChartStyle, BtnAreaStyle };
        foreach (var btn in buttons)
        {
            if (btn != button && btn != null)
                btn.IsChecked = false;
        }
        
        // 根据按钮确定K线样式        
        var selectedStyle = button.Name switch
        {
            "BtnCandleStyle" => CandleStyle.Candlestick,
            "BtnLineStyle" => CandleStyle.OHLC,
            "BtnLineChartStyle" => CandleStyle.Line,
            "BtnAreaStyle" => CandleStyle.Area,
            _ => CandleStyle.Candlestick
        };
        
        // 应用K线样式并重绘
        MainChart.CurrentCandleStyle = selectedStyle;
        MainChart.RedrawChart();
    }
    
    /// <summary>
    /// 主图指标按钮点击事件（弹出指标管理窗口）
    /// </summary>
    private void OnIndicatorClick(object? sender, RoutedEventArgs e)
    {
        ShowIndicatorDialog();
    }
    
    /// <summary>
    /// 副图按钮点击事件（弹出副图管理窗口）
    /// </summary>
    private void OnSubChartClick(object? sender, RoutedEventArgs e)
    {
        ShowSubChartDialogWindow();
    }
    
    /// <summary>
    /// 加载真实K线数据（首次直接调用API，不补缺口）    
    /// </summary>
    private async Task LoadRealDataAsync()
    {
        try
        {
            var interval = TimeFrameToInterval(_currentTimeFrame);
            ShowLoading($"正在获取 {_currentSymbol} {interval} 最新行情数据...");
            
            List<Candlestick>? candles = null;
            
            // 首先尝试从REST API获取最新数据
            try
            {
                Console.WriteLine($"📥 [MarketView] 通过REST API获取最新1000条数据...");
                candles = await FetchCandlesFromApiAsync(interval, 1000);
                
                if (candles != null && candles.Count > 0)
                {
                    Console.WriteLine($"✅ [MarketView] 从API获取到 {candles.Count} 条K线数据");
                    // ✅ 保存到数据库（DB key 使用 symbol_key，避免多交易所同名冲突）
                    await _marketDataRepository.BulkInsertKlinesAsync(_currentSymbol, interval, candles);
                }
            }
            catch (Exception apiEx)
            {
                Console.WriteLine($"⚠️ [MarketView] API获取失败: {apiEx.Message}");
                Console.WriteLine($"   尝试从本地数据库读取数据...");
            }
            
            // 如果API失败或返回空数据，尝试从本地数据库读取
            if (candles == null || candles.Count == 0)
            {
                Console.WriteLine($"📥 [MarketView] 从本地数据库读取数据...");
                candles = await App.DataQuery.GetKlinesAsync(
                    _currentSymbol,
                    interval,
                    limit: 1000
                );

                // ✅ 兼容迁移：旧版本缓存使用 baseSymbol（如 BTCUSDT）。
                // 仅对 Binance 生效，避免把 Binance 历史数据错误套用到 OKX 等交易所。
                if ((candles == null || candles.Count == 0) &&
                    InstrumentKey.TryParse(_currentSymbol, out var key) &&
                    key.Exchange == "BINANCE")
                {
                    Console.WriteLine($"🔁 [MarketView] 未找到新Key缓存，尝试从旧Key迁移: {key.BaseSymbol} -> {_currentSymbol}");
                    var legacyCandles = await App.DataQuery.GetKlinesAsync(
                        key.BaseSymbol,
                        interval,
                        limit: 1000
                    );

                    if (legacyCandles != null && legacyCandles.Count > 0)
                    {
                        await _marketDataRepository.BulkInsertKlinesAsync(_currentSymbol, interval, legacyCandles);
                        candles = legacyCandles;
                        Console.WriteLine($"✅ [MarketView] 已迁移旧缓存: {legacyCandles.Count} 条 ({_currentSymbol} {interval})");
                    }
                }
                
                if (candles != null && candles.Count > 0)
                {
                    Console.WriteLine($"✅ [MarketView] 从本地数据库获取到 {candles.Count} 条K线数据");
                    ShowLoading($"已加载本地数据: {candles.Count} 条\n（网络连接失败，使用本地缓存）");
                }
                else
                {
                    Console.WriteLine($"❌ [MarketView] 本地数据库也没有数据: {_currentSymbol} {interval}");
                    ShowLoading($"无法获取数据: {_currentSymbol} {interval}\n" +
                               $"请检查：\n" +
                               $"1. 网络连接是否正常\n" +
                               $"2. 是否需要配置代理（在设置页面配置）\n" +
                               $"3. 对应交易所API是否可访问");
                    return;
                }
            }
            
            // 生成完整的市场数据包
            var marketData = GenerateMarketDataFromCandles(candles);
            
            // 保存当前数据引用
            _currentMarketData = marketData;
            
            // 设置到图表
            MainChart.SetMarketData(marketData);
            
            // 应用默认指标配置（仅在第一次加载时）            
            if (!_isInitialized)
            {
                ApplyIndicatorSettings(_defaultIndicatorSettings);
                
                // 应用副图设置（初始化时）
                var subChartSettings = Services.Settings.SubChartSettingsStorage.LoadSettings();
                if (subChartSettings != null)
                {
                    ApplySubChartSettings(subChartSettings);
                }
            }
            else
            {
                // 切换时间框架时，重新计算并应用指标                
                RecalculateIndicators(_defaultIndicatorSettings);
                MainChart.SetMarketData(_currentMarketData);
                
                // 重新应用副图设置
                var subChartSettings = Services.Settings.SubChartSettingsStorage.LoadSettings();
                if (subChartSettings != null)
                {
                    ApplySubChartSettings(subChartSettings);
                }
            }
            
            // 启动WebSocket接收实时数据（不执行补缺口逻辑）
            await StartWebSocketForCurrentSymbol(interval);
            
            // 隐藏加载提示
            HideLoading();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketView] 加载失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            ShowLoading($"加载失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 启动WebSocket接收当前Symbol的实时数据（后台静默处理，不阻塞）
    /// </summary>
    private Task StartWebSocketForCurrentSymbol(string interval)
    {
        // 在后台线程异步处理，不阻塞UI
        _ = Task.Run(async () =>
        {
            try
            {
                // ✅ 目前 StreamHub 仅支持 Binance。非 Binance instrument 则跳过订阅。
                var (baseSymbol, exchangeId) = ResolveRouting();
                if (exchangeId != ExchangeId.Binance)
                {
                    if (_streamSubscription != null)
                    {
                        _streamSubscription.Dispose();
                        _streamSubscription = null;
                    }
                    _currentStreamSymbol = null;
                    return;
                }

                // 先完全清理旧的订阅，确保不会重复订阅
                if (_streamSubscription != null)
                {
                    _streamSubscription.Dispose();
                    _streamSubscription = null;
                    // 等待一小段时间确保清理完成
                    await Task.Delay(50);
                }
                
                // 创建新的订阅（在UI线程上执行，因为订阅可能涉及UI更新）
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _currentStreamSymbol = baseSymbol;
                    _streamSubscription = _marketDataContext.StreamHub.Subscribe(
                        baseSymbol,
                        interval,
                        OnStreamHubKlineReceived,
                        enableGapFill: false);
                    
                    Console.WriteLine($"✅ [MarketView] 已通过StreamHub订阅: {_currentStreamSymbol} {interval}");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MarketView] 订阅实时数据失败: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }
    
    /// <summary>
    /// StreamHub推送新K线（性能优化：使用防抖机制）
    /// </summary>
    private void OnStreamHubKlineReceived(Services.Data.Collectors.KlineReceivedEventArgs e)
    {
        // 在UI线程上更新图表
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 只处理当前Symbol和TimeFrame的数据
                if (_currentStreamSymbol == null ||
                    e.Symbol.ToUpperInvariant() != _currentStreamSymbol.ToUpperInvariant() || 
                    e.Interval != TimeFrameToInterval(_currentTimeFrame))
                {
                    return;
                }
                
                // 只在K线闭合时打印日志，减少未闭合K线更新的日志噪音
                if (e.Candlestick.IsClosed)
                {
                    Console.WriteLine($"📊 [MarketView] StreamHub收到K线: {e.Candlestick.Time:yyyy-MM-dd HH:mm:ss} C:{e.Candlestick.Close} (已闭合)");
                }
                
                // 立即更新数据（不阻塞）
                UpdateChartData(e.Candlestick);
                
                // 使用防抖机制延迟重绘，避免频繁重绘导致卡顿
                ScheduleDebouncedRedraw(e.Candlestick);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MarketView] 处理实时K线失败: {ex.Message}");
            }
        });
    }
    
    /// <summary>
    /// 立即更新图表数据（不触发重绘）
    /// </summary>
    private void UpdateChartData(Candlestick newKline)
    {
        if (_currentMarketData == null || _currentMarketData.Candles == null)
            return;
            
        var candles = _currentMarketData.Candles;
        
        if (candles.Count == 0)
        {
            candles.Add(newKline);
        }
        else
        {
            var lastCandle = candles[candles.Count - 1];
            
            if (newKline.Time == lastCandle.Time)
            {
                // 更新最后一根K线
                candles[candles.Count - 1] = newKline;
            }
            else if (newKline.Time > lastCandle.Time)
            {
                // 追加新K线
                if (!lastCandle.IsClosed)
                {
                    lastCandle.IsClosed = true;
                }
                candles.Add(newKline);
            }
        }
        
        // 更新当前价格
        if (candles.Count > 0)
        {
            var lastCandle = candles[candles.Count - 1];
            MainChart.UpdateCurrentPrice(lastCandle.Close);
        }
    }
    
    /// <summary>
    /// 使用防抖机制调度重绘（最多每100ms重绘一次）
    /// </summary>
    private void ScheduleDebouncedRedraw(Candlestick newKline)
    {
        lock (_redrawLock)
        {
            _pendingRedraw = true;
            _pendingRedrawIsCurrentKline = !newKline.IsClosed; // 保存最新的重绘类型
            
            // 重置定时器
            _redrawDebounceTimer?.Dispose();
            _redrawDebounceTimer = new System.Threading.Timer(_ =>
            {
                lock (_redrawLock)
                {
                    if (_pendingRedraw)
                    {
                        var isCurrentKlineUpdate = _pendingRedrawIsCurrentKline;
                        _pendingRedraw = false;
                        
                        // 在UI线程上执行重绘
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                if (isCurrentKlineUpdate)
                                {
                                    // 实时更新：只重绘图表，不重新计算指标（提高性能）
                                    MainChart.RedrawChart();
                                }
                                else
                                {
                                    // K线闭合或新增：重新计算指标并更新图表
                                    MainChart.SetMarketData(_currentMarketData!);
                                    RecalculateIndicators(_defaultIndicatorSettings);
                                    MainChart.SetMarketData(_currentMarketData!);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ [MarketView] 防抖重绘失败: {ex.Message}");
                            }
                        });
                    }
                }
            }, null, REDRAW_DEBOUNCE_MS, Timeout.Infinite);
        }
    }
    
    
    /// <summary>
    /// 将TimeFrame枚举转换为API interval字符串    
    /// </summary>
    private string TimeFrameToInterval(TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            TimeFrame.M1 => "1m",
            TimeFrame.M3 => "3m",
            TimeFrame.M5 => "5m",
            TimeFrame.M15 => "15m",
            TimeFrame.M30 => "30m",
            TimeFrame.H1 => "1h",
            TimeFrame.H2 => "2h",
            TimeFrame.H4 => "4h",
            TimeFrame.H6 => "6h",
            TimeFrame.H8 => "8h",
            TimeFrame.H12 => "12h",
            TimeFrame.D1 => "1d",
            _ => "5m" // 默认5m而不使用小时
        };
    }
    
    /// <summary>
    /// 从K线数据生成市场数据包（新框架 - 简化版）    
    /// </summary>
    private MarketDataPackage GenerateMarketDataFromCandles(List<Candlestick> candles, SubChartSettings? subChartSettings = null)
    {
        // 🎯 新框架：只返回K线数据，所有指标由CandlestickChart内部计算
        var package = new MarketDataPackage
        {
            Symbol = _currentSymbol,
            TimeFrame = _currentTimeFrame,
            Candles = candles
        };
        
        return package;
    }
    
    /// <summary>
    /// 应用副图配置并重新计算指标（新框架 - 简化版）    
    /// </summary>
    public void ApplySubChartSettings(SubChartSettings settings)
    {
        if (_currentMarketData == null || _currentMarketData.Candles == null || _currentMarketData.Candles.Count == 0)
        {
            return;
        }
        
        // 🔑 关键：更新副图设置并重新注册指标
        MainChart.UpdateSubChartSettings(settings);
        
        // 保存设置
        Services.Settings.SubChartSettingsStorage.SaveSettings(settings);
    }
    
    /// <summary>
    /// 显示副图配置对话框（Window模式，参考主图指标）
    /// </summary>
    private async void ShowSubChartDialogWindow()
    {
        var dialog = new SubChartDialog();
        
        // 加载当前设置
        var currentSettings = Services.Settings.SubChartSettingsStorage.LoadSettings() ?? new SubChartSettings();
        dialog.LoadSettings(currentSettings);
        
        // 使用 ModernDialog 包装自定义控件        
        var window = Controls.ModernDialogPresets.CustomContent(dialog, 660, 550);
        
        // 监听对话框事件        
        dialog.SettingsApplied += (s, settings) =>
        {
            ApplySubChartSettings(settings);
        };
        
        dialog.CloseRequested += (s, e) =>
        {
            window.Close();
        };
        
        // 显示模态对话框
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        
        if (parentWindow != null)
        {
            await window.ShowDialog(parentWindow);
        }
        else
        {
            window.Show();
        }
    }
    
    /// <summary>
    /// 计算MACD数据（简化版）    
    /// </summary>
    private List<MACDData> CalculateMACD(List<Candlestick> candles)
    {
        var result = new List<MACDData>();
        
        // 简化处理：使用固定值模拟        
        foreach (var candle in candles)
        {
            result.Add(new MACDData
            {
                Time = candle.Time,
                DIF = _random.NextDouble() * 20 - 10,
                DEA = _random.NextDouble() * 20 - 10,
                Histogram = _random.NextDouble() * 10 - 5
            });
        }
        
        return result;
    }
    
    /// <summary>
    /// 计算RSI数据（简化版）    
    /// </summary>
    private List<RSIData> CalculateRSI(List<Candlestick> candles, int PERIOD)
    {
        var result = new List<RSIData>();
        
        // 简化处理：使用固定值模拟        
        foreach (var candle in candles)
        {
            result.Add(new RSIData
            {
                Time = candle.Time,
                Value = _random.NextDouble() * 100
            });
        }
        
        return result;
    }
    
    /// <summary>
    /// 显示指标管理弹窗
    /// </summary>
    private async void ShowIndicatorDialog()
    {
        var dialog = new IndicatorDialog();
        
        var currentSettings = new IndicatorDialog.IndicatorSettings
        {
            MA = MainChart.MAConfig ?? new MATypeIndicatorConfig(),
            EMA = MainChart.EMAConfig ?? new MATypeIndicatorConfig(),
            WMA = MainChart.WMAConfig ?? new MATypeIndicatorConfig(),
            DEMA = MainChart.DEMAConfig ?? new MATypeIndicatorConfig(),
            TEMA = MainChart.TEMAConfig ?? new MATypeIndicatorConfig(),
            IsBOLLEnabled = MainChart.IsBOLLVisible,
            IsKeltnerEnabled = MainChart.IsKeltnerVisible,
            IsIchimokuEnabled = MainChart.IsIchimokuVisible,
            IsVWAPEnabled = MainChart.IsVWAPVisible,
            IsAVLEnabled = MainChart.IsAVLVisible,
            IsTRIXEnabled = MainChart.IsTRIXVisible,
            IsSAREnabled = MainChart.IsSARVisible
        };
        
        dialog.LoadSettings(currentSettings);
        
        // 使用 ModernDialog 包装自定义控件
        var window = Controls.ModernDialogPresets.CustomContent(dialog, 660, 550);
        
        dialog.CloseRequested += (s, confirmed) =>
        {
            window.Close();
            
            if (confirmed && dialog.Settings != null)
            {
                ApplyIndicatorSettings(dialog.Settings);
            }
        };
        
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        
        if (parentWindow != null)
        {
            await window.ShowDialog(parentWindow);
        }
        else
        {
            window.Show();
        }
    }
    
    /// <summary>
    /// 应用指标设置到图表    
    /// </summary>
    private void ApplyIndicatorSettings(IndicatorDialog.IndicatorSettings settings)
    {
        MainChart.IsMAVisible = settings.MA.IsEnabled;
        MainChart.MAConfig = settings.MA;
        MainChart.EMAConfig = settings.EMA;
        MainChart.WMAConfig = settings.WMA;
        MainChart.DEMAConfig = settings.DEMA;
        MainChart.TEMAConfig = settings.TEMA;
        MainChart.IsBOLLVisible = settings.IsBOLLEnabled;
        MainChart.IsKeltnerVisible = settings.IsKeltnerEnabled;
        MainChart.IsIchimokuVisible = settings.IsIchimokuEnabled;
        MainChart.IsVWAPVisible = settings.IsVWAPEnabled;
        MainChart.IsAVLVisible = settings.IsAVLEnabled;
        MainChart.IsTRIXVisible = settings.IsTRIXEnabled;
        MainChart.IsSARVisible = settings.IsSAREnabled;
        
        // 🔑 关键：配置更新后，强制重新初始化指标系统
        MainChart.RefreshIndicators();  // 重新注册所有指标        
        // 重新计算指标数据（根据用户配置）
        RecalculateIndicators(settings);
        
        MainChart.RedrawChart();
        
        // 保存设置到本地        
        _defaultIndicatorSettings = settings;
        IndicatorSettingsStorage.SaveSettings(settings);
    }
    
    /// <summary>
    /// 根据用户配置重新计算指标（新框架 - 简化版）    
    /// </summary>
    private void RecalculateIndicators(IndicatorDialog.IndicatorSettings settings)
    {
        if (_currentMarketData == null || _currentMarketData.Candles.Count == 0) return;
        
        // 新框架：刷新图表即可，指标由CandlestickChart内部计算
        MainChart.SetMarketData(_currentMarketData);
    }
    
    /// <summary>
    /// 显示加载提示
    /// </summary>
    private void ShowLoading(string message = "加载中..")
    {
        if (LoadingOverlay != null)
        {
            LoadingOverlay.IsVisible = true;
        }
        
        if (LoadingText != null)
        {
            LoadingText.Text = message;
        }
        
        // 启动旋转动画
        StartLoadingAnimation();
    }
    
    /// <summary>
    /// 隐藏加载提示
    /// </summary>
    private void HideLoading()
    {
        if (LoadingOverlay != null)
        {
            LoadingOverlay.IsVisible = false;
        }
        
        // 停止旋转动画
        StopLoadingAnimation();
    }
    
    /// <summary>
    /// 启动加载图标旋转动画
    /// </summary>
    private void StartLoadingAnimation()
    {
        // 停止已有的动画        
        StopLoadingAnimation();
        
        // 重置角度
        _currentRotationAngle = 0;
        
        // 创建定时器（16ms更新一次，30fps）        
        _loadingAnimationTimer = new System.Timers.Timer(16);
        _loadingAnimationTimer.Elapsed += (s, e) =>
        {
            // 在UI线程上更新旋转角度            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (LoadingIcon?.RenderTransform is RotateTransform rotateTransform)
                {
                    _currentRotationAngle += 4; // 每帧旋转4度(360° / 1.5秒 / 60fps = 4°)
                    if (_currentRotationAngle >= 360)
                    {
                        _currentRotationAngle -= 360;
                    }
                    rotateTransform.Angle = _currentRotationAngle;
                }
            });
        };
        _loadingAnimationTimer.Start();
    }
    
    /// <summary>
    /// 停止加载图标旋转动画
    /// </summary>
    private void StopLoadingAnimation()
    {
        // 停止定时器        
        if (_loadingAnimationTimer != null)
        {
            _loadingAnimationTimer.Stop();
            _loadingAnimationTimer.Dispose();
            _loadingAnimationTimer = null;
        }
        
        // 重置角度
        if (LoadingIcon?.RenderTransform is RotateTransform rotateTransform)
        {
            rotateTransform.Angle = 0;
        }
        _currentRotationAngle = 0;
    }
    
    /// <summary>
    /// 启动自动刷新定时器    
    /// </summary>
    private void StartAutoRefreshTimer()
    {
        // 停止已有的定时器
        StopAutoRefreshTimer();
        
        
        _autoRefreshTimer = new System.Timers.Timer(AUTO_REFRESH_INTERVAL_MS);
        _autoRefreshTimer.Elapsed += async (s, e) =>
        {
            try
            {
                // 在UI线程上执行刷新            
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await RefreshCurrentDataAsync();
                });
            }
            catch (Exception)
            {
                // 静默处理定时器异常
            }
        };
        _autoRefreshTimer.Start();
    }
    
    /// <summary>
    /// 停止自动刷新定时器    
    /// </summary>
    private void StopAutoRefreshTimer()
    {
        if (_autoRefreshTimer != null)
        {
            _autoRefreshTimer.Stop();
            _autoRefreshTimer.Dispose();
            _autoRefreshTimer = null;
        }
    }
    
    /// <summary>
    /// 后台检查并刷新数据（异步，不阻塞UI）⭐
    /// 离线版：暂不支持自动更新
    /// </summary>
    private async Task CheckAndRefreshDataAsync(string interval)
    {
        // 离线版：暂不支持自动更新，直接返回
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 刷新当前显示的数据（定时器调用）    
    /// </summary>
    private async Task RefreshCurrentDataAsync()
    {
        // 离线版：从本地 SQLite 重新加载数据
        try
        {
            var interval = TimeFrameToInterval(_currentTimeFrame);
            
            var candles = await App.DataQuery.GetKlinesAsync(
                _currentSymbol,
                interval,
                null,
                null,
                _appSettingsService.Settings.DefaultKlineLimit
            );
            
            if (candles != null && candles.Count > 0)
            {
                // 检查数据是否有变化（比较最后一条的时间）                
                var currentLatest = _currentMarketData?.Candles.LastOrDefault()?.Time;
                var newLatest = candles.LastOrDefault()?.Time;
                
                if (newLatest.HasValue && (!currentLatest.HasValue || newLatest > currentLatest))
                {
                    // 更新数据
                    var marketData = GenerateMarketDataFromCandles(candles);
                    _currentMarketData = marketData;
                    MainChart.SetMarketData(marketData);
                    
                    // 重新计算指标
                    if (_isInitialized)
                    {
                        RecalculateIndicators(_defaultIndicatorSettings);
                        MainChart.SetMarketData(_currentMarketData);
                    }
                }
            }
        }
        catch (Exception)
        {
        }
    }
    
    /// <summary>
    /// 处理"加载更多数据"请求（优先使用数据库，检查连续性后决定是否调用API）
    /// </summary>
    private async void OnLoadMoreDataRequested(object? sender, LoadMoreDataEventArgs e)
    {
        try
        {
            // 将TimeFrame枚举转换为interval字符串        
            var interval = TimeFrameToInterval(_currentTimeFrame);
            var intervalMinutes = GetIntervalMinutes(interval);
            
            // 结束时间：当前最早的K线时间
            var endTime = e.EarliestTime;
            
            Console.WriteLine($"📥 [MarketView] 请求加载更多数据，当前最早时间: {endTime:yyyy-MM-dd HH:mm:ss}");
            
            // 步骤1：先从数据库查询历史数据
            ShowLoading("正在从本地数据库查询历史数据...");
            
            var dbCandles = await App.DataQuery.GetKlinesAsync(
                _currentSymbol,
                interval,
                null, // startTime - 获取所有更早的数据
                endTime, // endTime - 当前最早的时间之前
                1000  // 获取1000条
            );
            
            List<Candlestick> moreCandles;
            
            // 步骤2：检查数据库数据是否足够且连续
            if (dbCandles != null && dbCandles.Count > 0)
            {
                Console.WriteLine($"📊 [MarketView] 从数据库获取到 {dbCandles.Count} 条历史数据");
                
                // 检查数据连续性：最新一条的时间应该紧邻当前最早时间
                var dbLatestTime = dbCandles[dbCandles.Count - 1].Time;
                var expectedTime = endTime.AddMinutes(-intervalMinutes);
                var timeDiff = Math.Abs((dbLatestTime - expectedTime).TotalMinutes);
                
                // 如果时间差在容差范围内（1.5倍interval），认为数据连续
                if (timeDiff <= intervalMinutes * 1.5 && dbCandles.Count >= 800)
                {
                    Console.WriteLine($"✅ [MarketView] 数据库数据连续且充足，无需调用API");
                    moreCandles = dbCandles;
                }
                else
                {
                    Console.WriteLine($"⚠️ [MarketView] 数据库数据不连续（时间差: {timeDiff:F1}分钟）或不足（{dbCandles.Count} 条），尝试从API获取...");
                    ShowLoading("正在通过API获取历史数据...");
                    
                    var startTime = endTime.AddMinutes(-1000 * intervalMinutes);
                    moreCandles = await FetchCandlesFromApiAsync(interval, 1000, startTime, endTime);
                    
                    if (moreCandles != null && moreCandles.Count > 0)
                    {
                        Console.WriteLine($"✅ [MarketView] 从API获取到 {moreCandles.Count} 条历史数据");
                        await _marketDataRepository.BulkInsertKlinesAsync(_currentSymbol, interval, moreCandles);
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ [MarketView] API获取失败，使用数据库数据");
                        moreCandles = dbCandles;
                    }
                }
            }
            else
            {
                // 数据库没有数据，调用API
                Console.WriteLine($"⚠️ [MarketView] 数据库无历史数据，尝试从API获取...");
                ShowLoading("正在通过API获取历史数据...");
                
                var startTime = endTime.AddMinutes(-1000 * intervalMinutes);
                moreCandles = await FetchCandlesFromApiAsync(interval, 1000, startTime, endTime);
                
                if (moreCandles != null && moreCandles.Count > 0)
                {
                    Console.WriteLine($"✅ [MarketView] 从API获取到 {moreCandles.Count} 条历史数据");
                    await _marketDataRepository.BulkInsertKlinesAsync(_currentSymbol, interval, moreCandles);
                }
                else
                {
                    Console.WriteLine($"❌ [MarketView] API也无法获取数据");
                    moreCandles = new List<Candlestick>();
                }
            }
            
            // 步骤3：如果获取到数据，检查是否真的扩展了时间范围
            if (moreCandles.Count > 0)
            {
                // 获取当前所有K线数据                
                var allCandles = MainChart.GetAllCandles();
                
                // 🔑 关键检查：验证新数据是否真的扩展了时间范围
                var newEarliestTime = moreCandles[0].Time; // 新数据的最早时间
                var currentEarliestTime = allCandles.Count > 0 ? allCandles[0].Time : endTime;
                
                // 如果新数据的最早时间不早于当前最早时间，说明没有扩展范围
                if (newEarliestTime >= currentEarliestTime)
                {
                    Console.WriteLine($"⚠️ [MarketView] 获取的数据未扩展时间范围（新最早: {newEarliestTime:yyyy-MM-dd HH:mm:ss}, 当前最早: {currentEarliestTime:yyyy-MM-dd HH:mm:ss}），停止加载");
                    // 重置加载状态，避免死循环
                    MainChart.PrependMoreData(new List<Candlestick>());
                    HideLoading();
                    return;
                }
                
                // 将新K线插入到前面
                var mergedCandles = new List<Candlestick>(moreCandles);
                mergedCandles.AddRange(allCandles);
                
                // 重新生成市场数据包（包含所有指标）
                var marketData = GenerateMarketDataFromCandles(mergedCandles);
                
                // 保存当前数据引用
                _currentMarketData = marketData;
                
                // 设置到图表（这会触发重绘）                
                MainChart.SetMarketData(marketData);
                
                // 重新计算MA类指标（根据用户配置）                
                RecalculateIndicators(_defaultIndicatorSettings);
                
                // 重新设置数据以应用新计算的指标                
                MainChart.SetMarketData(_currentMarketData);
                
                // 恢复视图位置（考虑新增的K线数量）
                MainChart.SetViewPosition(moreCandles.Count + e.CurrentStartIndex, e.CurrentVisibleCount);
                
                Console.WriteLine($"✅ [MarketView] 已加载 {moreCandles.Count} 条历史数据（时间范围: {newEarliestTime:yyyy-MM-dd HH:mm:ss} -> {currentEarliestTime:yyyy-MM-dd HH:mm:ss}）");
            }
            else
            {
                Console.WriteLine($"⚠️ [MarketView] 没有获取到更多数据，可能已到达数据边界");
                // 重置加载状态，停止继续请求
                MainChart.PrependMoreData(new List<Candlestick>());
            }
            
            // 隐藏加载提示
            HideLoading();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketView] 加载更多数据失败: {ex.Message}");
            HideLoading();
            
            // 重置加载状态          
            MainChart.PrependMoreData(new List<Candlestick>());
        }
    }
    
    /// <summary>
    /// 获取时间框架对应的分钟数
    /// </summary>
    private int GetIntervalMinutes(string interval)
    {
        return interval switch
        {
            "1m" => 1,
            "3m" => 3,
            "5m" => 5,
            "15m" => 15,
            "30m" => 30,
            "1h" => 60,
            "2h" => 120,
            "4h" => 240,
            "6h" => 360,
            "8h" => 480,
            "12h" => 720,
            "1d" => 1440,
            _ => 1
        };
    }
}
