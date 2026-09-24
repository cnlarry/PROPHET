using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Prophet.Client.Core;
using Prophet.Client.Data.Models;
using Prophet.Client.Services.Settings;

namespace Prophet.Client.Views.Pages;

public partial class HomePageView : UserControl
{
    private System.Timers.Timer? _refreshTimer;
    private bool _isRefreshing = false;
    private bool _isDisposed = false; // 防止重复清理
    private readonly AppSettingsService _appSettingsService;

    public HomePageView()
    {
        InitializeComponent();
        
        // 获取设置服务
        _appSettingsService = ServiceContainer.GetService<AppSettingsService>();
        
        SetupGridColumnAlignment();
        InitializeDataServices();
        InitializeData();
    }
    
    /// <summary>
    /// 设置表格列对齐（使用 SharedSizeGroup）
    /// </summary>
    private void SetupGridColumnAlignment()
    {
        // 等待布局完成后再设置
        this.Loaded += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SetupSharedSizeGroups();
            }, DispatcherPriority.Loaded);
        };
    }
    
    /// <summary>
    /// 为表格设置 SharedSizeGroup 以确保列对齐
    /// </summary>
    private void SetupSharedSizeGroups()
    {
        // 涨幅榜和跌幅榜使用相同的列定义
        SetupGridSharedSizeGroups(TopGainersItems, new[] { "RankCol", "SymbolCol", "PriceCol", "ChangeCol" });
        SetupGridSharedSizeGroups(TopLosersItems, new[] { "RankCol", "SymbolCol", "PriceCol", "ChangeCol" });
        SetupGridSharedSizeGroups(IndexPairsItems, new[] { "SymbolCol3", "PriceCol3", "ChangeCol3", "VolumeCol3" });
    }
    
    /// <summary>
    /// 为指定的 ItemsControl 设置 SharedSizeGroup
    /// </summary>
    private void SetupGridSharedSizeGroups(ItemsControl itemsControl, string[] sharedSizeGroups)
    {
        if (itemsControl == null) return;
        
        // 监听 ItemsControl 的布局更新
        itemsControl.LayoutUpdated += (s, e) =>
        {
            // 使用 VisualTree 遍历所有子元素
            var visualChildren = itemsControl.GetVisualChildren().OfType<ContentPresenter>();
            foreach (var presenter in visualChildren)
            {
                var grid = presenter.Content as Grid;
                if (grid != null && grid.ColumnDefinitions.Count == sharedSizeGroups.Length)
                {
                    for (int i = 0; i < grid.ColumnDefinitions.Count; i++)
                    {
                        grid.ColumnDefinitions[i].SharedSizeGroup = sharedSizeGroups[i];
                    }
                }
            }
        };
    }
    
    /// <summary>
    /// 初始化数据服务
    /// </summary>
    private void InitializeDataServices()
    {
        try
        {
            _ = Task.Run(async () =>
            {
                // 启动后立即刷新一次数据
                await RefreshAllDataAsync();
            });
            
            // 启动定时刷新（每30秒刷新一次）
            var intervalMs = _appSettingsService.Settings.AutoRefreshIntervalMs;
            if (intervalMs < 1000)
            {
                intervalMs = 1000;
            }

            _refreshTimer = new System.Timers.Timer(intervalMs);
            _refreshTimer.Elapsed += async (s, e) => await RefreshAllDataAsync();
            _refreshTimer.AutoReset = true;
            _refreshTimer.Start();
            
            Console.WriteLine($"✅ [HomePageView] 数据服务初始化完成（DataPlane），刷新间隔={intervalMs}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 数据服务初始化失败: {ex.Message}");
        }
    }
    
    private void InitializeData()
    {
        // 初始化资金费率列表（默认值）
        var defaultFundingRates = new ObservableCollection<FundingRateItem>
        {
            new FundingRateItem { Symbol = "BTCUSDT", Rate = "--", RateColor = "#848E9C", Change24h = "--", ChangeColor = "#848E9C" },
            new FundingRateItem { Symbol = "ETHUSDT", Rate = "--", RateColor = "#848E9C", Change24h = "--", ChangeColor = "#848E9C" },
            new FundingRateItem { Symbol = "SOLUSDT", Rate = "--", RateColor = "#848E9C", Change24h = "--", ChangeColor = "#848E9C" }
        };
        FundingRateItems.ItemsSource = defaultFundingRates;
        
        // 初始化综合指数交易对列表（默认值）
        var defaultIndexPairs = new ObservableCollection<IndexPairItem>
        {
            new IndexPairItem { Symbol = "BTCUSDT", Price = "--", Change24h = "--", ChangeColor = "#848E9C", Volume24h = "--" },
            new IndexPairItem { Symbol = "ETHUSDT", Price = "--", Change24h = "--", ChangeColor = "#848E9C", Volume24h = "--" },
            new IndexPairItem { Symbol = "BNBUSDT", Price = "--", Change24h = "--", ChangeColor = "#848E9C", Volume24h = "--" }
        };
        IndexPairsItems.ItemsSource = defaultIndexPairs;
        
        // 初始化涨幅榜（默认值）
        var defaultTopGainers = new ObservableCollection<TopGainerItem>
        {
            new TopGainerItem { Rank = "1", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopGainerItem { Rank = "2", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopGainerItem { Rank = "3", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopGainerItem { Rank = "4", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopGainerItem { Rank = "5", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" }
        };
        TopGainersItems.ItemsSource = defaultTopGainers;
        
        // 初始化跌幅榜（默认值）
        var defaultTopLosers = new ObservableCollection<TopLoserItem>
        {
            new TopLoserItem { Rank = "1", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopLoserItem { Rank = "2", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopLoserItem { Rank = "3", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopLoserItem { Rank = "4", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" },
            new TopLoserItem { Rank = "5", Symbol = "--", Price = "--", ChangePercent = "--", ChangeColor = "#848E9C" }
        };
        TopLosersItems.ItemsSource = defaultTopLosers;
    }
    
    /// <summary>
    /// 刷新所有数据
    /// </summary>
    private async Task RefreshAllDataAsync()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;
        try
        {
            Console.WriteLine("🔄 [HomePageView] 开始刷新数据...");
            
            // 并行刷新所有数据
            await Task.WhenAll(
                RefreshFearGreedIndexAsync(),
                RefreshMainCoinPricesAsync(),
                RefreshFundingRatesAsync(),
                RefreshLongShortRatiosAsync(),
                RefreshBuySellVolumeAsync(),
                RefreshIndexPairsAsync(),
                RefreshTopGainersLosersAsync(),
                RefreshOpenInterestAsync(),
                RefreshLiquidationDataAsync()
            );
            
            Console.WriteLine("✅ [HomePageView] 数据刷新完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新数据失败: {ex.Message}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// 刷新恐惧与贪婪指数
    /// </summary>
    private async Task RefreshFearGreedIndexAsync()
    {
        try
        {
            var data = await Prophet.Client.App.DataQuery.GetFearGreedIndexAsync();
            if (data != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // 更新仪表盘
                    var gauge = this.FindControl<Controls.FearGreedGauge>("FearGreedGauge");
                    if (gauge != null)
                    {
                        gauge.Value = data.Value;
                        gauge.Classification = data.Classification;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新恐惧与贪婪指数失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新主流币种价格
    /// </summary>
    private async Task RefreshMainCoinPricesAsync()
    {
        try
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "XRPUSDT", "DOGEUSDT" };
            var tickers = await Prophet.Client.App.DataQuery.GetTickersAsync(symbols.ToList());
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var ticker in tickers)
                {
                    var priceText = ticker.LastPrice.ToString("N2");
                    var changeText = $"{ticker.PriceChangePercent24h:F2}%";
                    var changeColor = ticker.PriceChangePercent24h >= 0 
                        ? Avalonia.Media.Color.Parse("#0ECB81") 
                        : Avalonia.Media.Color.Parse("#F6465D");
                    var changeBrush = new Avalonia.Media.SolidColorBrush(changeColor);
                    
                    switch (ticker.Symbol)
                    {
                        case "BTCUSDT":
                            BTCPrice.Text = priceText;
                            BTCChange.Text = changeText;
                            BTCChange.Foreground = changeBrush;
                            break;
                        case "ETHUSDT":
                            ETHPrice.Text = priceText;
                            ETHChange.Text = changeText;
                            ETHChange.Foreground = changeBrush;
                            break;
                        case "BNBUSDT":
                            BNBPrice.Text = priceText;
                            BNBChange.Text = changeText;
                            BNBChange.Foreground = changeBrush;
                            break;
                        case "SOLUSDT":
                            SOLPrice.Text = priceText;
                            SOLChange.Text = changeText;
                            SOLChange.Foreground = changeBrush;
                            break;
                        case "XRPUSDT":
                            XRPPrice.Text = priceText;
                            XRPChange.Text = changeText;
                            XRPChange.Foreground = changeBrush;
                            break;
                        case "DOGEUSDT":
                            DOGEPrice.Text = priceText;
                            DOGEChange.Text = changeText;
                            DOGEChange.Foreground = changeBrush;
                            break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新主流币种价格失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新资金费率
    /// </summary>
    private async Task RefreshFundingRatesAsync()
    {
        try
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT" };
            var rates = await Prophet.Client.App.DataQuery.GetFundingRateSnapshotsAsync(symbols.ToList());
            var items = new ObservableCollection<FundingRateItem>();
            
            foreach (var rate in rates)
            {
                items.Add(new FundingRateItem
                {
                    Symbol = rate.Symbol,
                    Rate = $"{rate.Rate:P4}",
                    RateColor = rate.Rate >= 0 ? "#0ECB81" : "#F6465D",
                    Change24h = rate.Change24hPercent.HasValue ? $"{rate.Change24hPercent.Value:+#0.0000;-#0.0000}%" : "--",
                    ChangeColor = !rate.Change24hPercent.HasValue || rate.Change24hPercent.Value >= 0 ? "#0ECB81" : "#F6465D"
                });
            }
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                FundingRateItems.ItemsSource = items;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新资金费率失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新多空比
    /// </summary>
    private async Task RefreshLongShortRatiosAsync()
    {
        try
        {
            var ratio = await Prophet.Client.App.DataQuery.GetLongShortRatioAsync("BTCUSDT", "5m");
            if (ratio != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // 大户持仓量多空比（LongPositionRatio）
                    LongShortRatioValue.Text = ratio.LongPositionRatio.ToString("F2");
                    
                    // 大户账户数多空比（LongAccountRatio）
                    AccountRatioValue.Text = ratio.LongAccountRatio.ToString("F2");
                    
                    // 多空持仓人数比（LongShortRatio）
                    TraderRatioValue.Text = ratio.LongShortRatioValue.ToString("F2");
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新多空比失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新合约主动买卖量
    /// </summary>
    private async Task RefreshBuySellVolumeAsync()
    {
        try
        {
            // 优先使用主动买卖量API获取数据
            var takerRatio = await Prophet.Client.App.DataQuery.GetTakerLongShortRatioAsync("BTCUSDT", "5m");
            if (takerRatio != null)
            {
                var totalVolume = takerRatio.BuyVol + takerRatio.SellVol;
                var buyPercent = totalVolume > 0 ? (takerRatio.BuyVol / totalVolume * 100) : 0m;
                var sellPercent = totalVolume > 0 ? (takerRatio.SellVol / totalVolume * 100) : 0m;
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    BuyVolumeValue.Text = takerRatio.BuyVol.ToString("N2");
                    BuyVolumePercent.Text = $"{buyPercent:F1}%";
                    SellVolumeValue.Text = takerRatio.SellVol.ToString("N2");
                    SellVolumePercent.Text = $"{sellPercent:F1}%";
                    BuySellRatioValue.Text = takerRatio.BuySellRatio.ToString("F2");
                });
            }
            else
            {
                // 如果API失败，尝试使用Ticker24h数据作为降级方案
                var ticker = await Prophet.Client.App.DataQuery.GetTickerAsync("BTCUSDT");
                if (ticker != null)
                {
                    var totalVolume = ticker.BuyVolume24h + ticker.SellVolume24h;
                    var buyPercent = totalVolume > 0 ? (ticker.BuyVolume24h / totalVolume * 100) : 0m;
                    var sellPercent = totalVolume > 0 ? (ticker.SellVolume24h / totalVolume * 100) : 0m;
                    var ratio = ticker.SellVolume24h > 0 ? (ticker.BuyVolume24h / ticker.SellVolume24h) : 0m;
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        BuyVolumeValue.Text = ticker.BuyVolume24h.ToString("N2");
                        BuyVolumePercent.Text = $"{buyPercent:F1}%";
                        SellVolumeValue.Text = ticker.SellVolume24h.ToString("N2");
                        SellVolumePercent.Text = $"{sellPercent:F1}%";
                        BuySellRatioValue.Text = ratio.ToString("F2");
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新买卖量失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新综合指数交易对
    /// </summary>
    private async Task RefreshIndexPairsAsync()
    {
        try
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT" };
            var tickers = await Prophet.Client.App.DataQuery.GetTickersAsync(symbols.ToList());
            
            var items = tickers.Select(t => new IndexPairItem
            {
                Symbol = t.Symbol,
                Price = t.LastPrice.ToString("N2"),
                Change24h = $"{t.PriceChangePercent24h:F2}%",
                ChangeColor = t.PriceChangePercent24h >= 0 ? "#0ECB81" : "#F6465D",
                Volume24h = t.Volume24h.ToString("N2")
            }).ToList();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IndexPairsItems.ItemsSource = items;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新综合指数交易对失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新涨跌幅排行榜
    /// </summary>
    private async Task RefreshTopGainersLosersAsync()
    {
        try
        {
            var gainers = await Prophet.Client.App.DataQuery.GetTopGainersAsync(5);
            var losers = await Prophet.Client.App.DataQuery.GetTopLosersAsync(5);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var gainerItems = gainers.Select((g, i) => new TopGainerItem
                {
                    Rank = (i + 1).ToString(),
                    Symbol = g.Symbol,
                    Price = g.LastPrice.ToString("N2"),
                    ChangePercent = $"+{g.PriceChangePercent24h:F2}%",
                    ChangeColor = "#0ECB81"
                }).ToList();
                TopGainersItems.ItemsSource = gainerItems;
                
                var loserItems = losers.Select((l, i) => new TopLoserItem
                {
                    Rank = (i + 1).ToString(),
                    Symbol = l.Symbol,
                    Price = l.LastPrice.ToString("N2"),
                    ChangePercent = $"{l.PriceChangePercent24h:F2}%",
                    ChangeColor = "#F6465D"
                }).ToList();
                TopLosersItems.ItemsSource = loserItems;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新涨跌幅排行榜失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新持仓量
    /// </summary>
    private async Task RefreshOpenInterestAsync()
    {
        try
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT" };
            foreach (var symbol in symbols)
            {
                var oi = await Prophet.Client.App.DataQuery.GetOpenInterestAsync(symbol);
                if (oi != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var oiText = oi.Value.ToString("N2");
                        switch (symbol)
                        {
                            case "BTCUSDT":
                                var btcOi = this.FindControl<TextBlock>("BTCOpenInterest");
                                if (btcOi != null) btcOi.Text = oiText;
                                break;
                            case "ETHUSDT":
                                var ethOi = this.FindControl<TextBlock>("ETHOpenInterest");
                                if (ethOi != null) ethOi.Text = oiText;
                                break;
                            case "SOLUSDT":
                                var solOi = this.FindControl<TextBlock>("SOLOpenInterest");
                                if (solOi != null) solOi.Text = oiText;
                                break;
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新持仓量失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新爆仓数据
    /// </summary>
    private async Task RefreshLiquidationDataAsync()
    {
        try
        {
            var liquidation = await Prophet.Client.App.DataQuery.GetLiquidationAsync("BTCUSDT");
            if (liquidation != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var totalLiq = this.FindControl<TextBlock>("TotalLiquidation");
                    var longLiq = this.FindControl<TextBlock>("LongLiquidation");
                    var shortLiq = this.FindControl<TextBlock>("ShortLiquidation");
                    
                    if (totalLiq != null) totalLiq.Text = liquidation.TotalLiquidation.ToString("N2");
                    if (longLiq != null) longLiq.Text = liquidation.LongLiquidation.ToString("N2");
                    if (shortLiq != null) shortLiq.Text = liquidation.ShortLiquidation.ToString("N2");
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [HomePageView] 刷新爆仓数据失败: {ex.Message}");
        }
    }
    
    private async void RefreshData_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshAllDataAsync();
    }
    
    private void EditFundingRate_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: 打开编辑对话框，允许用户自定义资金费率列表
        Console.WriteLine("编辑资金费率列表...");
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // 防止重复清理
        if (_isDisposed)
        {
            base.OnDetachedFromVisualTree(e);
            return;
        }
        
        _isDisposed = true;
        
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        
        base.OnDetachedFromVisualTree(e);
    }
}

// 资金费率数据模型
public class FundingRateItem
{
    public string Symbol { get; set; } = string.Empty;
    public string Rate { get; set; } = string.Empty;
    public string RateColor { get; set; } = "#848E9C";
    public string Change24h { get; set; } = string.Empty;
    public string ChangeColor { get; set; } = "#848E9C";
}

// 综合指数交易对数据模型
public class IndexPairItem
{
    public string Symbol { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Change24h { get; set; } = string.Empty;
    public string ChangeColor { get; set; } = "#848E9C";
    public string Volume24h { get; set; } = string.Empty;
}

// 涨幅榜数据模型
public class TopGainerItem
{
    public string Rank { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string ChangePercent { get; set; } = string.Empty;
    public string ChangeColor { get; set; } = "#0ECB81"; // 默认绿色
}

// 跌幅榜数据模型
public class TopLoserItem
{
    public string Rank { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string ChangePercent { get; set; } = string.Empty;
    public string ChangeColor { get; set; } = "#F6465D"; // 默认红色
}

