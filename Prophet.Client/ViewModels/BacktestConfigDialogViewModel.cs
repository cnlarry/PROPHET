using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services;
using Prophet.Client.Services.Strategy;

namespace Prophet.Client.ViewModels;

public class BacktestConfigDialogViewModel : INotifyPropertyChanged
{
    private readonly LocalStrategyService _strategyService;
    private readonly InstrumentRepository _instrumentRepository;
    private StrategyInfo? _selectedStrategy;
    private VersionListItem? _selectedVersion;
    private DateTimeOffset _startDate = DateTimeOffset.Now.AddMonths(-1);
    private DateTimeOffset _endDate = DateTimeOffset.Now.AddDays(-1); // 默认昨天，不能选今天
    private decimal _initialCapital = 10000m;
    private decimal _leverage = 10m;  // 杠杆倍数，默认10倍
    private decimal _positionSizePercent = 0.05m;  // 仓位比例，默认5%
    private decimal _feeRate = 0.001m;
    private decimal _slippageRate = 0.0005m;
    private SignalIntervalOption? _selectedSignalInterval;
    private string _validationMessage = string.Empty;
    private bool _isLoading;

    private string? _selectedSymbolKey;
    
    // P0.1: 止盈止损默认百分比
    private decimal _defaultTakeProfitPercent = 0.05m;
    private decimal _defaultStopLossPercent = 0.02m;
    
    // P1.3: 滑点模式配置
    private string _slippageMode = "FixedBps";
    private decimal _fixedBps = 0.0005m;
    private decimal _fixedPrice = 0.5m;
    private decimal _pctOfSpread = 0.10m;
    private decimal _impactCoefficient = 0.1m;
    private decimal _impactExponent = 0.5m;
    private bool _slippageRandomness = true;
    private decimal _slippageRandomFactor = 0.2m;
    
    // P2.6: 仓位大小计算方法（UI显示为大写，存储时转换为小写）
    private string _positionSizeMethod = "Fixed";
    private int _atrPeriod = 14;
    private decimal _atrMultiplier = 2m;
    private decimal _riskPercentPerTrade = 0.01m;
    private decimal _maxKellyFraction = 0.25m;
    
    // P2.8: 移动止损/追踪止盈
    private bool _enableTrailingStop = false;
    private bool _enableTrailingTakeProfit = false;
    
    // 日期选择器的最大日期（昨天）
    public DateTimeOffset MaxEndDate => DateTimeOffset.Now.AddDays(-1).Date;

    public BacktestConfigDialogViewModel()
    {
        _strategyService = new LocalStrategyService();
        _instrumentRepository = new InstrumentRepository();
        
        // 🔧 使用 TimeFrameExtensions 获取所有标准时间框架选项（排除月线）
        SignalSamplingIntervals = new ObservableCollection<SignalIntervalOption>();
        var allIntervals = TimeFrameExtensions.GetBacktestSamplingIntervals();
        foreach (var interval in allIntervals)
        {
            SignalSamplingIntervals.Add(interval);
        }
        
        // 默认选择5分钟（如果存在），否则选择第一个
        // GetBacktestSamplingIntervals() 总是返回至少一个选项，所以这里不会为 null
        _selectedSignalInterval = SignalSamplingIntervals.FirstOrDefault(i => i.Value == "5m") 
                                  ?? SignalSamplingIntervals.FirstOrDefault() 
                                  ?? throw new InvalidOperationException("无法初始化采样频率选项：没有可用的时间框架");
        
        // 自动加载保存的配置
        LoadConfig();
    }
    
    /// <summary>
    /// 加载保存的配置（自动调用）
    /// </summary>
    private void LoadConfig()
    {
        try
        {
            var config = BacktestConfigStorage.LoadConfig();
            if (config != null)
            {
                // 🔧 v4.0: 恢复配置到UI，确保正确处理UTC时间
                // config中的时间应该是UTC时间，需要转换为DateTimeOffset
                try 
                { 
                    var startDateUtc = config.StartDate.Kind == DateTimeKind.Utc 
                        ? config.StartDate 
                        : DateTime.SpecifyKind(config.StartDate, DateTimeKind.Utc);
                    StartDate = new DateTimeOffset(startDateUtc, TimeSpan.Zero);
                } 
                catch { }
                try 
                { 
                    var endDateUtc = config.EndDate.Kind == DateTimeKind.Utc 
                        ? config.EndDate 
                        : DateTime.SpecifyKind(config.EndDate, DateTimeKind.Utc);
                    EndDate = new DateTimeOffset(endDateUtc, TimeSpan.Zero);
                } 
                catch { }
                try { InitialCapital = config.InitialCapital; } catch { }
                try { Leverage = config.Leverage; } catch { }
                try { PositionSizePercent = config.PositionSizePercent; } catch { }
                try { FeeRate = config.TakerFeeRate; } catch { }
                try { SlippageRate = config.SlippageRate; } catch { }
                try { DefaultTakeProfitPercent = config.DefaultTakeProfitPercent; } catch { }
                try { DefaultStopLossPercent = config.DefaultStopLossPercent; } catch { }
                try 
                { 
                    // 直接使用枚举值转换为字符串，确保不为空
                    SlippageMode = config.SlippageMode.ToString();
                } 
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [BacktestConfigDialogViewModel] 加载 SlippageMode 失败: {ex.Message}");
                }
                try { FixedBps = config.FixedBps; } catch { }
                try { FixedPrice = config.FixedPrice; } catch { }
                try { PctOfSpread = config.PctOfSpread; } catch { }
                try { ImpactCoefficient = config.ImpactCoefficient; } catch { }
                try { ImpactExponent = config.ImpactExponent; } catch { }
                try { SlippageRandomness = config.SlippageRandomness; } catch { }
                try { SlippageRandomFactor = config.SlippageRandomFactor; } catch { }
                try 
                { 
                    // 将旧的小写值转换为新的大写显示值，确保UI显示正确
                    var method = config.PositionSizeMethod?.ToLower() ?? "fixed";
                    PositionSizeMethod = method switch
                    {
                        "atr" => "ATR",
                        "kelly" => "Kelly",
                        "fixed" => "Fixed",
                        _ => "Fixed"
                    };
                } 
                catch { }
                try { ATRPeriod = config.ATRPeriod; } catch { }
                try { ATRMultiplier = config.ATRMultiplier; } catch { }
                try { RiskPercentPerTrade = config.RiskPercentPerTrade; } catch { }
                try { MaxKellyFraction = config.MaxKellyFraction; } catch { }
                try { EnableTrailingStop = config.EnableTrailingStop; } catch { }
                try { EnableTrailingTakeProfit = config.EnableTrailingTakeProfit; } catch { }
                
                // 恢复信号采样间隔
                try
                {
                    var interval = SignalSamplingIntervals.FirstOrDefault(i => i.Value == config.SignalSamplingInterval);
                    if (interval != null)
                    {
                        SelectedSignalInterval = interval;
                    }
                }
                catch { }

                // 恢复标的（InstrumentKey）
                try { SelectedSymbolKey = config.Symbol; } catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [BacktestConfigDialogViewModel] 加载配置失败，将使用默认值: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存当前配置（在点击开始回测时调用）
    /// </summary>
    public bool SaveCurrentConfig()
    {
        if (!TryBuildRequest(out var request))
        {
            return false;
        }
        
        if (request?.Config != null)
        {
            var success = BacktestConfigStorage.SaveConfig(request.Config);
            if (!success)
            {
                Console.WriteLine("⚠️ [BacktestConfigDialogViewModel] 保存配置失败，但不影响回测执行");
            }
            return success;
        }
        
        return false;
    }

    public ObservableCollection<StrategyInfo> Strategies { get; } = new();
    public ObservableCollection<VersionListItem> StrategyVersions { get; } = new();
    public ObservableCollection<SignalIntervalOption> SignalSamplingIntervals { get; }
    public ObservableCollection<string> SymbolKeys { get; } = new();

    public StrategyInfo? SelectedStrategy
    {
        get => _selectedStrategy;
        set
        {
            if (_selectedStrategy != value)
            {
                _selectedStrategy = value;
                OnPropertyChanged();
                _ = LoadStrategyVersionsAsync();
            }
        }
    }

    public VersionListItem? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (_selectedVersion != value)
            {
                _selectedVersion = value;
                OnPropertyChanged();
            }
        }
    }

    public string? SelectedSymbolKey
    {
        get => _selectedSymbolKey;
        set
        {
            if (_selectedSymbolKey != value)
            {
                _selectedSymbolKey = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTimeOffset StartDate
    {
        get => _startDate;
        set
        {
            // 🔧 v4.0: 使用UTC时间的日期部分，避免时区转换问题
            // 创建UTC时间的DateTimeOffset（日期部分，时间部分为00:00:00）
            var selectedDateUtc = value.UtcDateTime.Date;  // 使用UTC时间的日期部分
            var newStartDate = new DateTimeOffset(selectedDateUtc, TimeSpan.Zero);
            
            if (_startDate != newStartDate)
            {
                _startDate = newStartDate;
                OnPropertyChanged();
            }
        }
    }

    public DateTimeOffset EndDate
    {
        get => _endDate;
        set
        {
            // 🔧 v4.0: 使用UTC时间的日期部分，避免时区转换问题
            // 限制不能选择今天或未来日期
            var maxDate = DateTime.UtcNow.Date.AddDays(-1);
            var selectedDateUtc = value.UtcDateTime.Date;  // 使用UTC时间的日期部分
            
            if (selectedDateUtc > maxDate)
            {
                selectedDateUtc = maxDate;
            }
            
            // 创建UTC时间的DateTimeOffset（日期部分，时间部分为00:00:00）
            var newEndDate = new DateTimeOffset(selectedDateUtc, TimeSpan.Zero);
            
            if (_endDate != newEndDate)
            {
                _endDate = newEndDate;
                OnPropertyChanged();
            }
        }
    }

    public decimal InitialCapital
    {
        get => _initialCapital;
        set
        {
            if (_initialCapital != value)
            {
                _initialCapital = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal Leverage
    {
        get => _leverage;
        set
        {
            if (_leverage != value)
            {
                _leverage = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal PositionSizePercent
    {
        get => _positionSizePercent;
        set
        {
            if (_positionSizePercent != value)
            {
                _positionSizePercent = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal FeeRate
    {
        get => _feeRate;
        set
        {
            if (_feeRate != value)
            {
                _feeRate = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal SlippageRate
    {
        get => _slippageRate;
        set
        {
            if (_slippageRate != value)
            {
                _slippageRate = value;
                OnPropertyChanged();
            }
        }
    }

    public SignalIntervalOption? SelectedSignalInterval
    {
        get => _selectedSignalInterval;
        set
        {
            if (_selectedSignalInterval != value)
            {
                _selectedSignalInterval = value;
                OnPropertyChanged();
            }
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            if (_validationMessage != value)
            {
                _validationMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(_validationMessage);

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }
    
    // ========== P0.1: 止盈止损默认百分比 ==========
    public decimal DefaultTakeProfitPercent
    {
        get => _defaultTakeProfitPercent;
        set
        {
            if (_defaultTakeProfitPercent != value)
            {
                _defaultTakeProfitPercent = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal DefaultStopLossPercent
    {
        get => _defaultStopLossPercent;
        set
        {
            if (_defaultStopLossPercent != value)
            {
                _defaultStopLossPercent = value;
                OnPropertyChanged();
            }
        }
    }
    
    // ========== P1.3: 滑点模式配置 ==========
    public string SlippageMode
    {
        get => _slippageMode;
        set
        {
            if (_slippageMode != value)
            {
                _slippageMode = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal FixedBps
    {
        get => _fixedBps;
        set
        {
            if (_fixedBps != value)
            {
                _fixedBps = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal FixedPrice
    {
        get => _fixedPrice;
        set
        {
            if (_fixedPrice != value)
            {
                _fixedPrice = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal PctOfSpread
    {
        get => _pctOfSpread;
        set
        {
            if (_pctOfSpread != value)
            {
                _pctOfSpread = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal ImpactCoefficient
    {
        get => _impactCoefficient;
        set
        {
            if (_impactCoefficient != value)
            {
                _impactCoefficient = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal ImpactExponent
    {
        get => _impactExponent;
        set
        {
            if (_impactExponent != value)
            {
                _impactExponent = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool SlippageRandomness
    {
        get => _slippageRandomness;
        set
        {
            if (_slippageRandomness != value)
            {
                _slippageRandomness = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal SlippageRandomFactor
    {
        get => _slippageRandomFactor;
        set
        {
            if (_slippageRandomFactor != value)
            {
                _slippageRandomFactor = value;
                OnPropertyChanged();
            }
        }
    }
    
    // ========== P2.6: 仓位大小计算方法 ==========
    public string PositionSizeMethod
    {
        get => _positionSizeMethod;
        set
        {
            if (_positionSizeMethod != value)
            {
                _positionSizeMethod = value;
                OnPropertyChanged();
            }
        }
    }
    
    public int ATRPeriod
    {
        get => _atrPeriod;
        set
        {
            if (_atrPeriod != value)
            {
                _atrPeriod = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal ATRMultiplier
    {
        get => _atrMultiplier;
        set
        {
            if (_atrMultiplier != value)
            {
                _atrMultiplier = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal RiskPercentPerTrade
    {
        get => _riskPercentPerTrade;
        set
        {
            if (_riskPercentPerTrade != value)
            {
                _riskPercentPerTrade = value;
                OnPropertyChanged();
            }
        }
    }
    
    public decimal MaxKellyFraction
    {
        get => _maxKellyFraction;
        set
        {
            if (_maxKellyFraction != value)
            {
                _maxKellyFraction = value;
                OnPropertyChanged();
            }
        }
    }
    
    // ========== P2.8: 移动止损/追踪止盈 ==========
    public bool EnableTrailingStop
    {
        get => _enableTrailingStop;
        set
        {
            if (_enableTrailingStop != value)
            {
                _enableTrailingStop = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool EnableTrailingTakeProfit
    {
        get => _enableTrailingTakeProfit;
        set
        {
            if (_enableTrailingTakeProfit != value)
            {
                _enableTrailingTakeProfit = value;
                OnPropertyChanged();
            }
        }
    }

    public async Task InitializeAsync()
    {
        await LoadInstrumentsAsync();
        await LoadStrategiesAsync();
    }

    private async Task LoadInstrumentsAsync()
    {
        try
        {
            var instruments = await _instrumentRepository.GetEnabledAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SymbolKeys.Clear();
                foreach (var item in instruments)
                {
                    if (!string.IsNullOrWhiteSpace(item.SymbolKey))
                    {
                        SymbolKeys.Add(item.SymbolKey);
                    }
                }

                if (string.IsNullOrWhiteSpace(SelectedSymbolKey))
                {
                    SelectedSymbolKey = SymbolKeys.FirstOrDefault() ?? "BTCUSDT-BINANCE-SWAP";
                }
                else if (!SymbolKeys.Contains(SelectedSymbolKey))
                {
                    // 若配置中已存的标的已经被禁用或不存在，回退到第一个可用标的
                    SelectedSymbolKey = SymbolKeys.FirstOrDefault() ?? SelectedSymbolKey;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [BacktestConfigDialogViewModel] 加载 instruments 失败: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SymbolKeys.Count == 0)
                {
                    SymbolKeys.Add("BTCUSDT-BINANCE-SWAP");
                    SymbolKeys.Add("BTCUSDT-OKX-SWAP");
                }

                SelectedSymbolKey ??= SymbolKeys.FirstOrDefault();
            });
        }
    }

    private async Task LoadStrategiesAsync()
    {
        try
        {
            IsLoading = true;
            var strategies = await _strategyService.GetMyStrategiesAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Strategies.Clear();
                foreach (var strategy in strategies)
                {
                    Strategies.Add(strategy);
                }

                SelectedStrategy = Strategies.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            ValidationMessage = $"加载策略失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadStrategyVersionsAsync()
    {
        if (SelectedStrategy == null)
        {
            StrategyVersions.Clear();
            return;
        }

        try
        {
            IsLoading = true;
            var versions = await _strategyService.GetStrategyVersionsAsync(SelectedStrategy.Id);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StrategyVersions.Clear();
                foreach (var version in versions)
                {
                    StrategyVersions.Add(version);
                }

                SelectedVersion = StrategyVersions.FirstOrDefault(v => v.Status == "active")
                                  ?? StrategyVersions.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            ValidationMessage = $"加载策略版本失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public bool TryBuildRequest(out BacktestConfigRequest? request)
    {
        request = null;

        // 1. 验证策略和版本
        if (SelectedStrategy == null)
        {
            ValidationMessage = "请选择策略";
            return false;
        }

        if (SelectedVersion == null)
        {
            ValidationMessage = "请选择策略版本";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedSymbolKey))
        {
            ValidationMessage = "请选择标的";
            return false;
        }

        // 2. 验证日期范围
        if (EndDate <= StartDate)
        {
            ValidationMessage = "结束日期必须晚于开始日期";
            return false;
        }
        
        // 验证结束日期不能是今天或未来
        if (EndDate.DateTime.Date >= DateTime.UtcNow.Date)
        {
            ValidationMessage = "结束日期不能选择今天或未来日期（币安历史数据未生成）";
            return false;
        }
        
        // 验证日期跨度（至少1天）
        var dateSpan = (EndDate.DateTime - StartDate.DateTime).TotalDays;
        if (dateSpan < 1)
        {
            ValidationMessage = "回测时间跨度至少需要1天";
            return false;
        }
        
        // 验证日期跨度不要太长（避免性能问题）
        if (dateSpan > 365)
        {
            ValidationMessage = "回测时间跨度不能超过1年（365天）";
            return false;
        }
        
        // 3. 验证初始资金
        if (InitialCapital < 100)
        {
            ValidationMessage = "初始资金不能低于100 USDT";
            return false;
        }
        
        if (InitialCapital > 10000000)
        {
            ValidationMessage = "初始资金不能超过1000万 USDT";
            return false;
        }
        
        // 4. 验证杠杆倍数
        if (Leverage < 1 || Leverage > 125)
        {
            ValidationMessage = "杠杆倍数必须在1-125倍之间";
            return false;
        }
        
        // 5. 验证仓位比例
        if (PositionSizePercent <= 0 || PositionSizePercent > 1)
        {
            ValidationMessage = "仓位比例必须在0-100%之间";
            return false;
        }
        
        // 6. 验证手续费率
        if (FeeRate < 0 || FeeRate > 0.01m)
        {
            ValidationMessage = "手续费率必须在0-1%之间";
            return false;
        }
        
        // 7. 验证滑点率
        if (SlippageRate < 0 || SlippageRate > 0.01m)
        {
            ValidationMessage = "滑点率必须在0-1%之间";
            return false;
        }
        
        // 8. 验证止盈止损比例
        if (DefaultTakeProfitPercent <= 0 || DefaultTakeProfitPercent > 1)
        {
            ValidationMessage = "默认止盈比例必须在0-100%之间";
            return false;
        }
        
        if (DefaultStopLossPercent <= 0 || DefaultStopLossPercent > 1)
        {
            ValidationMessage = "默认止损比例必须在0-100%之间";
            return false;
        }
        
        // 止盈必须大于止损
        if (DefaultTakeProfitPercent <= DefaultStopLossPercent)
        {
            ValidationMessage = "默认止盈比例必须大于止损比例";
            return false;
        }
        
        // 9. 验证滑点模式参数
        if (FixedBps < 0 || FixedBps > 0.01m)
        {
            ValidationMessage = "固定基点滑点必须在0-100bps之间";
            return false;
        }
        
        if (FixedPrice < 0 || FixedPrice > 100)
        {
            ValidationMessage = "固定价格滑点必须在0-100 USDT之间";
            return false;
        }
        
        if (PctOfSpread < 0 || PctOfSpread > 1)
        {
            ValidationMessage = "价差百分比必须在0-100%之间";
            return false;
        }
        
        // 10. 验证ATR参数
        if (ATRPeriod < 5 || ATRPeriod > 100)
        {
            ValidationMessage = "ATR周期必须在5-100之间";
            return false;
        }
        
        if (ATRMultiplier < 0.1m || ATRMultiplier > 10)
        {
            ValidationMessage = "ATR止损倍数必须在0.1-10之间";
            return false;
        }
        
        if (RiskPercentPerTrade < 0.001m || RiskPercentPerTrade > 0.1m)
        {
            ValidationMessage = "每笔交易风险必须在0.1%-10%之间";
            return false;
        }
        
        // 11. 验证Kelly参数
        if (MaxKellyFraction < 0.05m || MaxKellyFraction > 1)
        {
            ValidationMessage = "最大Kelly比例必须在5%-100%之间";
            return false;
        }

        // 🔧 v4.0: 正确处理日期，不进行时区转换
        // 开始日期：使用日期的 00:00:00 UTC
        var startDateUtc = StartDate.UtcDateTime;
        var utcStartDate = DateTime.SpecifyKind(
            new DateTime(startDateUtc.Year, startDateUtc.Month, startDateUtc.Day, 0, 0, 0),
            DateTimeKind.Utc
        );
        
        // 结束日期：使用日期的 23:59:59 UTC（当天的最后一秒）
        var endDateUtc = EndDate.UtcDateTime;
        var utcEndDate = DateTime.SpecifyKind(
            new DateTime(endDateUtc.Year, endDateUtc.Month, endDateUtc.Day, 23, 59, 59),
            DateTimeKind.Utc
        );
        
        var config = new BacktestConfig
        {
            // 语义升级：Symbol 存储 InstrumentKey（symbol_key）
            Symbol = SelectedSymbolKey!,
            Interval = "1m",
            StartDate = utcStartDate,
            EndDate = utcEndDate,
            InitialCapital = InitialCapital,
            Leverage = Leverage,  // 杠杆倍数
            PositionSizePercent = PositionSizePercent,  // 仓位比例
            TakerFeeRate = FeeRate,
            SlippageRate = SlippageRate,
            SignalSamplingInterval = SelectedSignalInterval?.Value ?? "5m",  // 如果未选择，默认使用5m
            Parameters = new Dictionary<string, object>(),
            
            // P0.1: 止盈止损默认百分比
            DefaultTakeProfitPercent = DefaultTakeProfitPercent,
            DefaultStopLossPercent = DefaultStopLossPercent,
            
            // P1.3: 滑点模式配置
            SlippageMode = string.IsNullOrWhiteSpace(SlippageMode) 
                ? Backtest.Models.SlippageMode.FixedBps 
                : Enum.Parse<Backtest.Models.SlippageMode>(SlippageMode, ignoreCase: true),
            FixedBps = FixedBps,
            FixedPrice = FixedPrice,
            PctOfSpread = PctOfSpread,
            ImpactCoefficient = ImpactCoefficient,
            ImpactExponent = ImpactExponent,
            SlippageRandomness = SlippageRandomness,
            SlippageRandomFactor = SlippageRandomFactor,
            MarketOrderMultiplier = 1.0m,
            StopOrderMultiplier = 1.5m,
            
            // P2.6: 仓位大小计算方法（存储时转换为小写，确保向后兼容）
            PositionSizeMethod = PositionSizeMethod?.ToLower() ?? "fixed",
            ATRPeriod = ATRPeriod,
            ATRMultiplier = ATRMultiplier,
            RiskPercentPerTrade = RiskPercentPerTrade,
            MaxKellyFraction = MaxKellyFraction,
            
            // P2.8: 移动止损/追踪止盈
            EnableTrailingStop = EnableTrailingStop,
            EnableTrailingTakeProfit = EnableTrailingTakeProfit
        };

        request = new BacktestConfigRequest
        {
            Strategy = SelectedStrategy,
            Version = SelectedVersion,
            Config = config
        };

        ValidationMessage = string.Empty;
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

