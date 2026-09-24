using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Core;
using Prophet.Client.Models;
using Prophet.Client.Services.Settings;
using Avalonia.Layout;

namespace Prophet.Client.Controls;

public partial class CandlestickChart : UserControl
{
    // ========== 副图配置 ==========
    
    /// <summary>
    /// 成交量副图是否启用
    /// </summary>
    public bool IsVolumeSubChartEnabled { get; set; } = true;
    
    /// <summary>
    /// MACD副图是否启用
    /// </summary>
    public bool IsMACDSubChartEnabled { get; set; } = true;
    
    /// <summary>
    /// RSI副图是否启用
    /// </summary>
    public bool IsRSISubChartEnabled { get; set; } = true;
    
    /// <summary>
    /// ATR副图是否启用
    /// </summary>
    public bool IsATRSubChartEnabled { get; set; } = true;
    
    /// <summary>
    /// MACD参数
    /// </summary>
    public (int Fast, int Slow, int Signal) MACDParams { get; set; } = (12, 26, 9);
    
    /// <summary>
    /// RSI参数
    /// </summary>
    public int RSIPeriod { get; set; } = 14;
    
    /// <summary>
    /// ATR参数
    /// </summary>
    public int ATRPeriod { get; set; } = 14;
    
    /// <summary>
    /// RSI超买阈值
    /// </summary>
    public double RSIOverboughtLevel { get; set; } = 70;
    
    /// <summary>
    /// RSI超卖阈值
    /// </summary>
    public double RSIOversoldLevel { get; set; } = 30;
    
    // ========== 私有字段 ==========
    
    private List<Candlestick> _data = new();
    private List<Candlestick> _allData = new(); // 存储所有数据
    private double _maxPrice;
    private double _minPrice;
    private double _priceRange;
    private double? _currentPrice; // 当前实时价格
    private string _currentSymbol = "BTCUSDT"; // 当前交易对符号
    
    // ========== 通用副图数据（新架构）==========
    private Dictionary<string, List<SubChartDataPoint>> _subChartIndicators = new();
    private SubChartSettings? _subChartSettings;
    
    // ========== 布局相关 ==========
    private double _chartBottomY = 0; // 图表实际底部Y坐标（包括所有副图）
    private double _mainChartBottomY = 0; // 主图底部Y坐标（不包括副图）
    
    // ========== 指标可见性控制==========
    public bool IsMAVisible { get; set; } = false;
    public bool IsMA5Visible { get; set; } = true;
    public bool IsMA10Visible { get; set; } = true;
    public bool IsMA20Visible { get; set; } = true;
    public bool IsMA60Visible { get; set; } = true;
    public bool IsBOLLVisible { get; set; } = false;
    public bool IsKeltnerVisible { get; set; } = false;
    public bool IsIchimokuVisible { get; set; } = false;
    public bool IsMACDVisible { get; set; } = false;
    public bool IsRSIVisible { get; set; } = false;
    
    // ========== 用户自定义指标配置==========
    public MATypeIndicatorConfig? MAConfig { get; set; }
    public MATypeIndicatorConfig? EMAConfig { get; set; }
    public MATypeIndicatorConfig? WMAConfig { get; set; }
    public MATypeIndicatorConfig? DEMAConfig { get; set; }
    public MATypeIndicatorConfig? TEMAConfig { get; set; }
    public bool IsVWAPVisible { get; set; } = false;
    public bool IsAVLVisible { get; set; } = false;
    public bool IsTRIXVisible { get; set; } = false;
    public bool IsSARVisible { get; set; } = false;
    
    // K线样式
    public CandleStyle CurrentCandleStyle { get; set; } = CandleStyle.Candlestick;
    
    // 缩放和滚动控制
    private int _startIndex = 0;  // 当前显示的起始索引
    private int _visibleCount = 100; // 当前显示的K线数量（默认100根）
    private const int MinVisibleCount = 20;   // 最小显示数量
    private const int MaxVisibleCount = 500;  // 最大显示数量
    
    // 拖拽滚动相关
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private int _dragStartIndex;
    
    // 绘图参数（用于鼠标定位）
    private double _currentSpacing = 0; // 当前每根K线的间距
    private double _currentEffectiveWidth = 0; // 当前有效绘图宽度（考虑右侧空白）
    
    // 十字准星相关
    private bool _isMouseOver = false;
    private Point _mousePosition;
    private int _hoverCandleIndex = -1;  // 当前鼠标悬停的K线索引
    
    private Line? _crosshairHLine;
    private Line? _crosshairVLine;
    private TextBlock? _crosshairPriceText;
    private Border? _crosshairPriceBorder;
    private TextBlock? _crosshairTimeText;
    private Border? _crosshairTimeBorder;
    
    // 副图十字准星相关
    private List<Line> _subChartCrosshairLines = new();
    private List<Border> _subChartCrosshairValueBorders = new();
    
    private readonly IBrush _crosshairLineColor = new SolidColorBrush(Color.Parse("#666666"));
    private readonly IBrush _crosshairTextColor = new SolidColorBrush(Color.Parse("#CCCCCC")); // #999999更亮
    private readonly IBrush _hoverCandleColor = new SolidColorBrush(Color.Parse("#F0B90B")); // 悬停高亮颜色（币安金黄色）
    
    // 光标K线信息
    private StackPanel? _cursorInfoPanel;
    private readonly IBrush _labelColor = new SolidColorBrush(Color.Parse("#999999")); // 标签颜色（灰色）
    
    // 自动加载更多数据相关
    private bool _isLoadingMore = false;
    private const int LoadMoreThreshold = 50; // 当距离起始位置小于50根K线时，触发加载更多数据
    
    /// <summary>
    /// 需要加载更多历史数据事件
    /// </summary>
    public event EventHandler<LoadMoreDataEventArgs>? LoadMoreDataRequested;
    
    // 存储K线的位置信息，用于点击检查
    private class CandlestickArea
    {
        public Candlestick Data { get; set; } = null!;
        public double X { get; set; }
        public double Width { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public int Index { get; set; }
    }
    
    private List<CandlestickArea> _candlestickAreas = new();
    // private int _selectedCandleIndex = -1; // 已移除K线点击选中功能
    
    // 颜色定义（从全局设置获取）
    private IBrush GetRisingColor() => new SolidColorBrush(Color.Parse(ServiceContainer.GetService<AppSettingsService>().GetRisingColor()));
    private IBrush GetFallingColor() => new SolidColorBrush(Color.Parse(ServiceContainer.GetService<AppSettingsService>().GetFallingColor()));
    
    private readonly IBrush _gridColor = new SolidColorBrush(Color.Parse("#2C2C2C"));
    private readonly IBrush _textColor = new SolidColorBrush(Color.Parse("#999999"));
    private readonly IBrush _highlightColor = new SolidColorBrush(Color.FromArgb(60, 255, 165, 0)); // 半透明橙色
    
    // 图表边距（公开以便副图对齐）
    public const double LeftMargin = 10;       // 左侧留小边距
    public const double RightMargin = 70;      // 右侧预留价格轴空格
    private const double TopMargin = 25;        // 顶部边距（为光标信息预留空间）
    private const double BottomMargin = 30;     // 底部边距（用于时间轴）
    
    public CandlestickChart()
    {
        InitializeComponent();
        
        // 加载副图设置
        // LoadSubChartSettings(); // 旧框架代码已废弃
        
        // 订阅全局设置变更事件
        ServiceContainer.GetService<AppSettingsService>().SettingsChanged += OnSettingsChanged;
        
        // 监听尺寸变化
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                DrawChart();
            }
        };
        
        // 添加鼠标滚轮事件监听（缩放功能）
        ChartCanvas.PointerWheelChanged += ChartCanvas_OnPointerWheelChanged;
        
        // 添加拖拽事件监听（左右滚动）
        ChartCanvas.PointerPressed += ChartCanvas_OnPointerPressed_Drag;
        ChartCanvas.PointerMoved += ChartCanvas_OnPointerMoved;
        ChartCanvas.PointerReleased += ChartCanvas_OnPointerReleased;
        ChartCanvas.PointerCaptureLost += ChartCanvas_OnPointerCaptureLost;
        
        // 添加键盘事件监听（方向键滚动）
        this.KeyDown += CandlestickChart_OnKeyDown;
        
        // 确保控件可以获得焦点以接收键盘事件
        this.Focusable = true;
        
        // 添加鼠标悬停事件监听（十字准星）
        ChartCanvas.PointerEntered += ChartCanvas_OnPointerEntered;
        ChartCanvas.PointerExited += ChartCanvas_OnPointerExited;
    }
    
    /// <summary>
    /// 设置K线数据（旧方法，保留向后兼容）
    /// </summary>
    public void SetData(List<Candlestick> data)
    {
        _allData = data ?? new List<Candlestick>();
        
        // 🚀 性能优化：标记指标数据需要重新计算
        InvalidateIndicatorData();
        
        // 初始显示最后的visibleCount根K线
        if (_allData.Count > 0)
        {
            _startIndex = Math.Max(0, _allData.Count - _visibleCount);
            UpdateVisibleData();
        }
        
        DrawChart();
    }
    
    /// <summary>
    /// 设置完整市场数据（包含K线和指标）
    /// </summary>
    public void SetMarketData(MarketDataPackage data)
    {
        if (data == null)
        {
            Console.WriteLine($"⚠️ [CandlestickChart] data 为 null，跳过");
            return;
        }
        
        // 存储当前symbol
        _currentSymbol = data.Symbol ?? "BTCUSDT";
        
        // 新框架：只接收K线数据，所有指标由框架自己计算
        _allData = data.Candles ?? new List<Candlestick>();
        
        // 🚀 性能优化：标记指标数据需要重新计算
        InvalidateIndicatorData();
        
        // 清空MA计算缓存（数据已更新）
        Services.Cache.MACalculationCache.Instance.ClearAll();
        
        // 初始显示最后的visibleCount根K线
        if (_allData.Count > 0)
        {
            _startIndex = Math.Max(0, _allData.Count - _visibleCount);
            UpdateVisibleData();
            
            // 更新当前价格（使用最后一根K线的收盘价）
            var lastCandle = _allData[_allData.Count - 1];
            _currentPrice = lastCandle.Close;
            
            // 如果鼠标没有悬停，显示最新蜡烛图数据
            if (!_isMouseOver || _hoverCandleIndex < 0)
            {
                ShowLatestCandleInfo();
            }
        }
        else
        {
            Console.WriteLine($"⚠️ [CandlestickChart] _allData 为空，无法更新可见数据");
            _currentPrice = null;
        }
        
        DrawChart();
    }
    
    /// <summary>
    /// 设置通用副图指标数据（新架构）
    /// </summary>
    public void SetSubChartIndicators(Dictionary<string, List<SubChartDataPoint>> indicators, SubChartSettings settings)
    {
        _subChartIndicators = indicators ?? new Dictionary<string, List<SubChartDataPoint>>();
        _subChartSettings = settings;
        // 重新绘制图表以应用新的副图
        DrawChart();
    }
    
    /// <summary>
    /// 更新副图指标设置并重新注册指标（新架构）
    /// </summary>
    public void UpdateSubChartSettings(SubChartSettings settings)
    {
        // 更新设置
        _subChartSettings = settings;
        
        // 🔑 关键：同步独立属性的值（从settings读取）
        IsVolumeSubChartEnabled = settings.IsVolumeEnabled;
        IsMACDSubChartEnabled = settings.IsMACDEnabled;
        IsRSISubChartEnabled = settings.IsRSIEnabled;
        IsATRSubChartEnabled = settings.IsATREnabled;
        
        // 同步MACD参数（使用元组语法）
        MACDParams = (settings.MACDFastPeriod, settings.MACDSlowPeriod, settings.MACDSignalPeriod);
        
        // 同步RSI参数
        RSIPeriod = settings.RSIPeriod;
        
        // 同步ATR参数
        ATRPeriod = settings.ATRPeriod;
        
        // 🔑 关键：重新初始化指标系统以应用新的启用状态
        RefreshIndicators();
        
        // 标记数据需要重新计算
        InvalidateIndicatorData();
        
        // 重新绘制图表
        DrawChart();
        
    }
    
    /// <summary>
    /// 在前面插入更多历史数据（用于增量加载）
    /// </summary>
    public void PrependMoreData(List<Candlestick> moreCandles)
    {
        if (moreCandles == null || moreCandles.Count == 0)
        {
            _isLoadingMore = false;
            return;
        }
        
        // 保存当前的起始索引位置
        var oldStartIndex = _startIndex;
        
        // 将新数据插入到前面
        _allData.InsertRange(0, moreCandles);
        
        // 🚀 性能优化：标记指标数据需要重新计算
        InvalidateIndicatorData();
        
        // 调整起始索引，保持用户当前查看的位置不变
        _startIndex = oldStartIndex + moreCandles.Count;
        
        // 更新可见数据
        UpdateVisibleData();
        DrawChart();
        
        _isLoadingMore = false;
        
    }
    
    /// <summary>
    /// 重绘图表（用于切换指标可见性）
    /// 注意：Canvas控件通过Children管理，需要直接调用DrawChart
    /// </summary>
    public void RedrawChart()
    {
        DrawChart();
    }
    
    /// <summary>
    /// 更新当前价格（实时显示）
    /// 注意：价格更新会通过防抖机制统一处理，这里只更新数据
    /// </summary>
    public void UpdateCurrentPrice(double price)
    {
        _currentPrice = price;
        // 不立即重绘，由防抖机制统一处理
    }
    
    /// <summary>
    /// 获取所有K线数量
    /// </summary>
    public List<Candlestick> GetAllCandles()
    {
        return new List<Candlestick>(_allData);
    }
    
    /// <summary>
    /// 设置视图位置
    /// </summary>
    /// <param name="startIndex">起始索引</param>
    /// <param name="visibleCount">可见数量</param>
    public void SetViewPosition(int startIndex, int visibleCount)
    {
        _startIndex = Math.Max(0, Math.Min(_allData.Count - visibleCount, startIndex));
        _visibleCount = visibleCount;
        UpdateVisibleData();
        DrawChart();
        _isLoadingMore = false;
    }
    
    /// <summary>
    /// 更新当前可见的数据
    /// </summary>
    private void UpdateVisibleData()
    {
        if (_allData.Count == 0)
        {
            _data = new List<Candlestick>();
            return;
        }
        
        // 确保索引在有效范围内
        _startIndex = Math.Max(0, Math.Min(_startIndex, _allData.Count - 1));
        var endIndex = Math.Min(_startIndex + _visibleCount, _allData.Count);
        
        // 获取可见数据
        _data = _allData.Skip(_startIndex).Take(endIndex - _startIndex).ToList();
        
        if (_data.Count > 0)
        {
            _maxPrice = _data.Max(c => c.High);
            _minPrice = _data.Min(c => c.Low);
            _priceRange = _maxPrice - _minPrice;
        }
    }
    
    /// <summary>
    /// 绘制图表
    /// </summary>
    private void DrawChart()
    {
        if (ChartCanvas == null || _data.Count == 0)
        {
            Console.WriteLine($"⚠️ [CandlestickChart] DrawChart 提前返回 - ChartCanvas={ChartCanvas != null}, _data.Count={_data.Count}");
            return;
        }
        
        ChartCanvas.Children.Clear();
        _candlestickAreas.Clear();
        
        // ========== 新框架：初始化和填充指标数据 ==========
        InitializeIndicators();
        PopulateAllIndicatorData();
        
        // 重新计算价格范围，包含主图指标的值，确保指标线不会超出绘图区域
        CalculatePriceRangeWithIndicators();
        
        var width = ChartCanvas.Bounds.Width;
        var height = ChartCanvas.Bounds.Height;
        
        if (width <= 0 || height <= 0) return;
        
        // 计算绘图区域
        var chartWidth = width - LeftMargin - RightMargin;
        
        // 动态计算布局（根据启用的副图）
        var enabledSubChartsCount = 0;
        if (IsVolumeSubChartEnabled) enabledSubChartsCount++;
        if (IsMACDSubChartEnabled) enabledSubChartsCount++;
        if (IsRSISubChartEnabled) enabledSubChartsCount++;
        if (IsATRSubChartEnabled) enabledSubChartsCount++;
        
        // 统计新副图指标（通用架构）
        if (_subChartSettings != null)
        {
            if (_subChartSettings.IsMFIEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsOBVEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsKDJEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsStochRSIEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsCCIEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsDMIEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsWREnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsCMFEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsROCEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsEMVEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsMTMEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsCMOEnabled) enabledSubChartsCount++;
            if (_subChartSettings.IsAroonEnabled) enabledSubChartsCount++;
        }
        
        // 副图固定高度策略
        var subChartSeparatorHeight = 10.0; // 副图之间的间隙高度
        var fixedSubChartHeight = 80.0; // 每个副图的固定高度
        var totalSeparatorHeight = enabledSubChartsCount > 0 ? subChartSeparatorHeight * enabledSubChartsCount : 0;
        var totalSubChartsHeight = enabledSubChartsCount * fixedSubChartHeight;
        
        // 主图高度 = 总高度 - 顶部留白 - 底部留白 - 副图总高度 - 分隔线总高度
        var mainChartHeight = height - TopMargin - BottomMargin - totalSubChartsHeight - totalSeparatorHeight;
        var subChartHeight = fixedSubChartHeight;
        
        // 如果主图高度太小，调整副图高度
        if (mainChartHeight < 200 && enabledSubChartsCount > 0)
        {
            mainChartHeight = 200;
            var remainingHeight = height - TopMargin - BottomMargin - mainChartHeight - totalSeparatorHeight;
            subChartHeight = remainingHeight / enabledSubChartsCount;
        }
        
        if (chartWidth <= 0 || mainChartHeight <= 0) return;
        
        // 绘制主图网格
        DrawGrid(chartWidth, mainChartHeight);
        
        // 计算每根K线的宽度和间距
        var candleCount = _data.Count;
        
        // 计算右侧空白：如果显示范围超出实际数据，动态计算空白比例
        var rightPaddingRatio = 0.0;
        if (_startIndex + _visibleCount > _allData.Count)
        {
            // 空白K线数 / 可见K线数 = 空白比例
            var emptyCount = _startIndex + _visibleCount - _allData.Count;
            rightPaddingRatio = (double)emptyCount / _visibleCount;
        }
        var effectiveWidth = chartWidth * (1.0 - rightPaddingRatio);
        
        var candleWidth = Math.Max(2, effectiveWidth / candleCount * 0.7); // K线实体宽度
        var spacing = effectiveWidth / candleCount; // 每根K线占用的总宽度
        
        // 保存绘图参数（用于鼠标定位）
        _currentSpacing = spacing;
        _currentEffectiveWidth = effectiveWidth;
        
        // 根据样式绘制K线
        switch (CurrentCandleStyle)
        {
            case CandleStyle.Candlestick:
                for (int i = 0; i < _data.Count; i++)
                {
                    var candle = _data[i];
                    var x = LeftMargin + i * spacing + spacing / 2;
                    DrawCandlestick(candle, x, mainChartHeight, candleWidth, i);
                }
                break;
                
            case CandleStyle.OHLC:
                for (int i = 0; i < _data.Count; i++)
                {
                    var candle = _data[i];
                    var x = LeftMargin + i * spacing + spacing / 2;
                    DrawOHLC(candle, x, mainChartHeight, candleWidth, i);
                }
                break;
                
            case CandleStyle.Line:
                DrawLine(mainChartHeight, spacing);
                break;
                
            case CandleStyle.Area:
                DrawArea(mainChartHeight, spacing);
                break;
        }
        
        // ========== 新框架：统一绘制所有主图指标==========
        DrawAllMainChartIndicators(effectiveWidth, mainChartHeight, spacing);
        
        // 绘制价格轴（右侧）
        DrawPriceAxis(chartWidth, mainChartHeight);
        
        // 保存主图底部Y坐标（用于十字准星判断）
        _mainChartBottomY = TopMargin + mainChartHeight;
        
        // ========== 新框架：统一绘制所有副图指标==========
        var currentY = _mainChartBottomY;
        DrawAllSubChartIndicators(effectiveWidth, chartWidth, spacing, ref currentY, subChartHeight);
        
        // ==================== 旧框架副图绘制已全部移除 ====================
        // 所有副图指标现已由新框架的DrawAllSubChartIndicators统一处理
        
        // 绘制时间轴（最底部）
        DrawTimeAxis(chartWidth, currentY);
        
        // 保存图表实际底部Y坐标（用于十字准星贯穿所有副图）
        _chartBottomY = currentY;
        
        // 绘制视图内最高价和最低价标记
        DrawHighLowPriceMarkers(effectiveWidth, mainChartHeight, spacing);
        
        // 如果鼠标在图表内，重新绘制十字准星（确保缩放、滚动时十字准星持续显示）
        if (_isMouseOver && _hoverCandleIndex >= 0)
        {
            DrawCrosshair(_mousePosition);
        }
        else if (_allData.Count > 0)
        {
            // 如果鼠标不在图表内或没有悬停在K线上，显示最新一根K线的信息
            ShowLatestCandleInfo();
        }
    }
    
    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGrid(double chartWidth, double chartHeight)
    {
        const int gridLines = 5;
        
        // 水平网格
        for (int i = 0; i <= gridLines; i++)
        {
            var y = TopMargin + (chartHeight / gridLines) * i;
            var line = new Line
            {
                StartPoint = new Point(LeftMargin, y),
                EndPoint = new Point(LeftMargin + chartWidth, y),
                Stroke = _gridColor,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(line);
        }
        
        // 垂直网格
        var verticalLines = Math.Min(10, _data.Count);
        for (int i = 0; i <= verticalLines; i++)
        {
            var x = LeftMargin + (chartWidth / verticalLines) * i;
            var line = new Line
            {
                StartPoint = new Point(x, TopMargin),
                EndPoint = new Point(x, TopMargin + chartHeight),
                Stroke = _gridColor,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(line);
        }
    }
    
    /// <summary>
    /// 绘制单根K线
    /// </summary>
    private void DrawCandlestick(Candlestick candle, double x, double chartHeight, double width, int index)
    {
        // 未闭合的K线使用#F0B90B颜色，其他情况正常显示
        // 检查是否是最后一根可见K线且未闭合
        var isCurrentKline = !candle.IsClosed && index == _data.Count - 1;
        
        IBrush color;
        if (isCurrentKline)
        {
            // 未闭合的K线使用金黄色
            color = new SolidColorBrush(Color.Parse("#F0B90B"));
        }
        else if (index == _hoverCandleIndex)
        {
            // 悬停的K线使用金黄色高亮颜色
            color = _hoverCandleColor;
        }
        else
        {
            // 普通K线根据涨跌显示颜色
            color = candle.IsRising ? GetRisingColor() : GetFallingColor();
        }
        
        // 价格转Y坐标
        double PriceToY(double price)
        {
            var ratio = (_maxPrice - price) / _priceRange;
            return TopMargin + chartHeight * ratio;
        }
        
        var openY = PriceToY(candle.Open);
        var closeY = PriceToY(candle.Close);
        var highY = PriceToY(candle.High);
        var lowY = PriceToY(candle.Low);
        
        // 存储K线区域信息，用于点击检查
        _candlestickAreas.Add(new CandlestickArea
        {
            Data = candle,
            X = x,
            Width = width * 1.5, // 扩大点击区域
            Top = highY,
            Bottom = lowY,
            Index = index
        });
        
        // 绘制上下影线（细线）
        var shadowLine = new Line
        {
            StartPoint = new Point(x, highY),
            EndPoint = new Point(x, lowY),
            Stroke = color,
            StrokeThickness = 1
        };
        ChartCanvas.Children.Add(shadowLine);
        
        // 绘制实体（矩形）
        var bodyTop = Math.Min(openY, closeY);
        var bodyHeight = Math.Max(1, Math.Abs(closeY - openY)); // 至少1像素
        
        var body = new Rectangle
        {
            Width = width,
            Height = bodyHeight,
            Fill = color,
            Stroke = color,
            StrokeThickness = 1
        };
        
        Canvas.SetLeft(body, x - width / 2);
        Canvas.SetTop(body, bodyTop);
        ChartCanvas.Children.Add(body);
    }
    
    /// <summary>
    /// 绘制美国线（OHLC线）
    /// </summary>
    private void DrawOHLC(Candlestick candle, double x, double chartHeight, double width, int index)
    {
        // 未闭合的K线使用#F0B90B颜色，其他情况正常显示
        // 检查是否是最后一根可见K线且未闭合
        var isCurrentKline = !candle.IsClosed && index == _data.Count - 1;
        
        IBrush color;
        if (isCurrentKline)
        {
            // 未闭合的K线使用金黄色
            color = new SolidColorBrush(Color.Parse("#F0B90B"));
        }
        else if (index == _hoverCandleIndex)
        {
            // 悬停的K线使用金黄色高亮颜色
            color = _hoverCandleColor;
        }
        else
        {
            // 普通K线根据涨跌显示颜色
            color = candle.IsRising ? GetRisingColor() : GetFallingColor();
        }
        
        // 价格转Y坐标
        double PriceToY(double price)
        {
            var ratio = (_maxPrice - price) / _priceRange;
            return TopMargin + chartHeight * ratio;
        }
        
        var openY = PriceToY(candle.Open);
        var closeY = PriceToY(candle.Close);
        var highY = PriceToY(candle.High);
        var lowY = PriceToY(candle.Low);
        
        // 存储K线区域信息，用于点击检查
        _candlestickAreas.Add(new CandlestickArea
        {
            Data = candle,
            X = x,
            Width = width * 1.5,
            Top = highY,
            Bottom = lowY,
            Index = index
        });
        
        // 绘制竖线（High到Low）
        var verticalLine = new Line
        {
            StartPoint = new Point(x, highY),
            EndPoint = new Point(x, lowY),
            Stroke = color,
            StrokeThickness = 1.5
        };
        ChartCanvas.Children.Add(verticalLine);
        
        // 绘制开盘价横线（向左延伸）
        var tickWidth = width * 0.5;
        var openLine = new Line
        {
            StartPoint = new Point(x - tickWidth, openY),
            EndPoint = new Point(x, openY),
            Stroke = color,
            StrokeThickness = 1.5
        };
        ChartCanvas.Children.Add(openLine);
        
        // 绘制收盘价横线（向右延伸）
        var closeLine = new Line
        {
            StartPoint = new Point(x, closeY),
            EndPoint = new Point(x + tickWidth, closeY),
            Stroke = color,
            StrokeThickness = 1.5
        };
        ChartCanvas.Children.Add(closeLine);
    }
    
    /// <summary>
    /// 绘制折线图（Line Chart）
    /// </summary>
    private void DrawLine(double chartHeight, double spacing)
    {
        if (_data.Count < 2) return;
        
        // 价格转Y坐标
        double PriceToY(double price)
        {
            var ratio = (_maxPrice - price) / _priceRange;
            return TopMargin + chartHeight * ratio;
        }
        
        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.Parse("#2196F3")), // 蓝色
            StrokeThickness = 2.0 // 比收盘线稍粗
        };
        
        var points = new List<Point>();
        
        for (int i = 0; i < _data.Count; i++)
        {
            var candle = _data[i];
            var x = LeftMargin + i * spacing + spacing / 2;
            var y = PriceToY(candle.Close);
            
            points.Add(new Point(x, y));
            
            // 存储K线区域信息，用于点击检查
            _candlestickAreas.Add(new CandlestickArea
            {
                Data = candle,
                X = x,
                Width = spacing,
                Top = y - 5,
                Bottom = y + 5,
                Index = i
            });
        }
        
        polyline.Points = points;
        ChartCanvas.Children.Add(polyline);
        
        // 如果有悬停的K线，在该点绘制一个圆点高亮
        if (_hoverCandleIndex >= 0 && _hoverCandleIndex < _data.Count)
        {
            var hoverCandle = _data[_hoverCandleIndex];
            var hoverX = LeftMargin + _hoverCandleIndex * spacing + spacing / 2;
            var hoverY = PriceToY(hoverCandle.Close);
            
            var hoverDot = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = _hoverCandleColor,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            
            Canvas.SetLeft(hoverDot, hoverX - 5);
            Canvas.SetTop(hoverDot, hoverY - 5);
            ChartCanvas.Children.Add(hoverDot);
        }
    }
    
    /// <summary>
    /// 绘制面积图（Area Chart）
    /// </summary>
    private void DrawArea(double chartHeight, double spacing)
    {
        if (_data.Count < 2) return;
        
        // 价格转Y坐标
        double PriceToY(double price)
        {
            var ratio = (_maxPrice - price) / _priceRange;
            return TopMargin + chartHeight * ratio;
        }
        
        // 创建面积多边形
        var polygon = new Avalonia.Controls.Shapes.Polygon
        {
            Fill = new SolidColorBrush(Color.Parse("#4D2196F3")), // 半透明蓝色
            Stroke = new SolidColorBrush(Color.Parse("#2196F3")), // 蓝色边框
            StrokeThickness = 1.5
        };
        
        var points = new List<Point>();
        
        // 添加顶部轮廓点（收盘价）
        for (int i = 0; i < _data.Count; i++)
        {
            var candle = _data[i];
            var x = LeftMargin + i * spacing + spacing / 2;
            var y = PriceToY(candle.Close);
            
            points.Add(new Point(x, y));
            
            // 存储K线区域信息，用于点击检查
            _candlestickAreas.Add(new CandlestickArea
            {
                Data = candle,
                X = x,
                Width = spacing,
                Top = y - 5,
                Bottom = y + 5,
                Index = i
            });
        }
        
        // 添加底部轮廓点（从右下角到左下角）
        var bottomY = TopMargin + chartHeight;
        points.Add(new Point(LeftMargin + (_data.Count - 1) * spacing + spacing / 2, bottomY));
        points.Add(new Point(LeftMargin + spacing / 2, bottomY));
        
        polygon.Points = points;
        ChartCanvas.Children.Add(polygon);
        
        // 如果有悬停的K线，在该点绘制一个圆点高亮和垂直辅助线
        if (_hoverCandleIndex >= 0 && _hoverCandleIndex < _data.Count)
        {
            var hoverCandle = _data[_hoverCandleIndex];
            var hoverX = LeftMargin + _hoverCandleIndex * spacing + spacing / 2;
            var hoverY = PriceToY(hoverCandle.Close);
            
            // 绘制垂直辅助线
            var verticalLine = new Line
            {
                StartPoint = new Point(hoverX, hoverY),
                EndPoint = new Point(hoverX, bottomY),
                Stroke = new SolidColorBrush(Color.Parse("#88FFFFFF")),
                StrokeThickness = 1,
                StrokeDashArray = new AvaloniaList<double> { 4, 4 }
            };
            ChartCanvas.Children.Add(verticalLine);
            
            // 绘制高亮圆点
            var hoverDot = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = _hoverCandleColor,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            
            Canvas.SetLeft(hoverDot, hoverX - 5);
            Canvas.SetTop(hoverDot, hoverY - 5);
            ChartCanvas.Children.Add(hoverDot);
        }
    }
    
    /// <summary>
    /// 绘制价格轴（右侧）
    /// </summary>
    private void DrawPriceAxis(double chartWidth, double chartHeight)
    {
        const int priceSegments = 10; // 将价格轴划分10个价格区间
        
        for (int i = 0; i <= priceSegments; i++)
        {
            var ratio = (double)i / priceSegments;
            var price = _maxPrice - _priceRange * ratio;
            var y = TopMargin + chartHeight * ratio;
            
            // 价格标签（使用千分符格式）
            var text = new TextBlock
            {
                Text = price.ToString("N2"),
                Foreground = _textColor,
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            
            // 放置在右侧
            var xPosition = LeftMargin + chartWidth + 8; // 图表右侧 + 8px间距
            Canvas.SetLeft(text, xPosition);
            Canvas.SetTop(text, y - 7);
            ChartCanvas.Children.Add(text);
            
            // 绘制价格线（连接到右侧刻度）
            var priceLine = new Line
            {
                StartPoint = new Point(LeftMargin + chartWidth, y),
                EndPoint = new Point(LeftMargin + chartWidth + 4, y),
                Stroke = _textColor,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(priceLine);
        }
        
        // 绘制当前价格（实时显示）
        if (_currentPrice.HasValue && _currentPrice.Value >= _minPrice && _currentPrice.Value <= _maxPrice)
        {
            var currentPriceRatio = (_maxPrice - _currentPrice.Value) / _priceRange;
            var currentPriceY = TopMargin + chartHeight * currentPriceRatio;
            
            // 绘制当前价格水平线（贯穿整个图表）
            var currentPriceLine = new Line
            {
                StartPoint = new Point(LeftMargin, currentPriceY),
                EndPoint = new Point(LeftMargin + chartWidth, currentPriceY),
                Stroke = new SolidColorBrush(Color.Parse("#F0B90B")), // 币安金黄色
                StrokeThickness = 1,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 3 } // 虚线
            };
            ChartCanvas.Children.Add(currentPriceLine);
            
            // 绘制当前价格标签（右侧，紧贴K线视图边缘，与价格轴刻度线左对齐）
            var currentPriceBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#F0B90B")), // 币安金黄色背景
                CornerRadius = new CornerRadius(4, 4, 4, 4), // 4个角都圆角
                Padding = new Thickness(4, 2, 4, 2),
                Child = new TextBlock
                {
                    Text = _currentPrice.Value.ToString("N2"),
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 11, // 与价格轴字体大小一致
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                }
            };
            
            // 紧贴K线视图边缘（与价格轴刻度线左对齐）
            var currentPriceXPosition = LeftMargin + chartWidth;
            Canvas.SetLeft(currentPriceBorder, currentPriceXPosition);
            Canvas.SetTop(currentPriceBorder, currentPriceY - 10);
            ChartCanvas.Children.Add(currentPriceBorder);
        }
    }
    
    /// <summary>
    /// 绘制视图内最高价和最低价标记（用三角形标记在K线绘图区域外边缘）
    /// </summary>
    private void DrawHighLowPriceMarkers(double effectiveWidth, double chartHeight, double spacing)
    {
        if (_data.Count == 0)
            return;
        
        // 计算视图内可见K线的最高价和最低价
        var visibleHigh = _data.Max(c => c.High);
        var visibleLow = _data.Min(c => c.Low);
        
        // 找到最高价和最低价对应的K线索引
        int highIndex = -1, lowIndex = -1;
        for (int i = 0; i < _data.Count; i++)
        {
            if (_data[i].High == visibleHigh && highIndex == -1)
                highIndex = i;
            if (_data[i].Low == visibleLow && lowIndex == -1)
                lowIndex = i;
        }
        
        // 价格转Y坐标
        double PriceToY(double price)
        {
            var ratio = (_maxPrice - price) / _priceRange;
            return TopMargin + chartHeight * ratio;
        }
        
        var highY = PriceToY(visibleHigh);
        var lowY = PriceToY(visibleLow);
        var chartWidth = effectiveWidth;
        var chartRightEdge = LeftMargin + chartWidth;
        const double triangleSize = 8; // 三角形大小
        const double labelSpacing = 4; // 标签与三角形的间距
        
        // 绘制最高价标记（根据对应K线颜色显示）
        if (highIndex >= 0)
        {
            var highCandle = _data[highIndex];
            var highX = LeftMargin + highIndex * spacing + spacing / 2;
            
            // 根据K线颜色确定三角形和标签颜色
            var highColor = highCandle.IsRising ? GetRisingColor() : GetFallingColor();
            Color highColorValue;
            if (highColor is SolidColorBrush highSolidBrush)
            {
                highColorValue = highSolidBrush.Color;
            }
            else
            {
                highColorValue = highCandle.IsRising ? Color.Parse("#26A69A") : Color.Parse("#EF5350");
            }
            
            // 绘制倒三角（在K线绘图区域上方外边缘）
            var highTriangle = new Avalonia.Controls.Shapes.Polygon
            {
                Points = new Avalonia.Collections.AvaloniaList<Point>
                {
                    new Point(highX, TopMargin - triangleSize), // 顶点（上方）
                    new Point(highX - triangleSize / 2, TopMargin), // 左下
                    new Point(highX + triangleSize / 2, TopMargin)  // 右下
                },
                Fill = new SolidColorBrush(highColorValue),
                Stroke = new SolidColorBrush(highColorValue),
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(highTriangle);
            
            // 绘制最高价标签（三角形右边，与三角形水平对齐）
            var highLabel = new Border
            {
                Background = new SolidColorBrush(highColorValue), // 使用K线颜色作为背景
                CornerRadius = new CornerRadius(3, 3, 3, 3), // 圆角
                Padding = new Thickness(4, 2, 4, 2),
                Child = new TextBlock
                {
                    Text = visibleHigh.ToString("N2"),
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                }
            };
            // 价格标签与三角形水平对齐（三角形中心在 TopMargin - triangleSize/2）
            var labelHeight = 20.0; // 标签高度（Padding 2*2 + FontSize 10 + 一些额外空间）
            var triangleCenterY = TopMargin - triangleSize / 2; // 三角形中心Y坐标
            Canvas.SetLeft(highLabel, highX + triangleSize / 2 + labelSpacing);
            Canvas.SetTop(highLabel, triangleCenterY - labelHeight / 2); // 标签中心与三角形中心对齐
            ChartCanvas.Children.Add(highLabel);
        }
        
        // 绘制最低价标记（根据对应K线颜色显示）
        if (lowIndex >= 0)
        {
            var lowCandle = _data[lowIndex];
            var lowX = LeftMargin + lowIndex * spacing + spacing / 2;
            var chartBottom = TopMargin + chartHeight;
            
            // 根据K线颜色确定三角形和标签颜色
            var lowColor = lowCandle.IsRising ? GetRisingColor() : GetFallingColor();
            Color lowColorValue;
            if (lowColor is SolidColorBrush lowSolidBrush)
            {
                lowColorValue = lowSolidBrush.Color;
            }
            else
            {
                lowColorValue = lowCandle.IsRising ? Color.Parse("#26A69A") : Color.Parse("#EF5350");
            }
            
            // 绘制正三角（在K线绘图区域下方外边缘）
            var lowTriangle = new Avalonia.Controls.Shapes.Polygon
            {
                Points = new Avalonia.Collections.AvaloniaList<Point>
                {
                    new Point(lowX, chartBottom + triangleSize), // 顶点（下方）
                    new Point(lowX - triangleSize / 2, chartBottom), // 左上
                    new Point(lowX + triangleSize / 2, chartBottom)  // 右上
                },
                Fill = new SolidColorBrush(lowColorValue),
                Stroke = new SolidColorBrush(lowColorValue),
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(lowTriangle);
            
            // 绘制最低价标签（三角形右边，与三角形水平对齐）
            var lowLabel = new Border
            {
                Background = new SolidColorBrush(lowColorValue), // 使用K线颜色作为背景
                CornerRadius = new CornerRadius(3, 3, 3, 3), // 圆角
                Padding = new Thickness(4, 2, 4, 2),
                Child = new TextBlock
                {
                    Text = visibleLow.ToString("N2"),
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                }
            };
            // 价格标签与三角形水平对齐（三角形中心在 chartBottom + triangleSize/2）
            var labelHeight = 20.0; // 标签高度
            var triangleCenterY = chartBottom + triangleSize / 2; // 三角形中心Y坐标
            Canvas.SetLeft(lowLabel, lowX + triangleSize / 2 + labelSpacing);
            Canvas.SetTop(lowLabel, triangleCenterY - labelHeight / 2); // 标签中心与三角形中心对齐
            ChartCanvas.Children.Add(lowLabel);
        }
    }
    
    /// <summary>
    /// 格式化成交量显示
    /// </summary>
    private string FormatVolume(double volume)
    {
        if (volume >= 1000000)
            return $"{volume / 1000000:F2}M";
        else if (volume >= 1000)
            return $"{volume / 1000:F2}K";
        else
            return $"{volume:F2}";
    }
    
    /// <summary>
    /// 绘制副图刻度线
    /// </summary>
    /// <param name="chartWidth">图表宽度</param>
    /// <param name="startY">副图起始Y坐标</param>
    /// <param name="height">副图高度</param>
    /// <param name="minValue">最小值</param>
    /// <param name="maxValue">最大值</param>
    /// <param name="formatter">值格式化函数</param>
    private void DrawSubChartAxis(double chartWidth, double startY, double height, double minValue, double maxValue, Func<double, string> formatter)
    {
        const int segments = 3; // 副图刻度分段数（顶部、中间、底部）
        var valueRange = maxValue - minValue;
        
        if (valueRange <= 0) return;
        
        for (int i = 0; i <= segments; i++)
        {
            var ratio = (double)i / segments;
            var value = maxValue - valueRange * ratio;
            var y = startY + height * ratio;
            
            // 刻度标签
            var text = new TextBlock
            {
                Text = formatter(value),
                Foreground = _textColor,
                FontSize = 9,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            
            // 放置在右侧
            var xPosition = LeftMargin + chartWidth + 8;
            Canvas.SetLeft(text, xPosition);
            Canvas.SetTop(text, y - 6);
            ChartCanvas.Children.Add(text);
            
            // 绘制刻度线（连接到右侧）
            var tickLine = new Line
            {
                StartPoint = new Point(LeftMargin + chartWidth, y),
                EndPoint = new Point(LeftMargin + chartWidth + 4, y),
                Stroke = _textColor,
                StrokeThickness = 1,
                Opacity = 0.5
            };
            ChartCanvas.Children.Add(tickLine);
        }
    }
    
    /// <summary>
    /// 绘制RSI专用刻度轴（显示70, 50, 30）
    /// </summary>
    private void DrawRSIAxis(double chartWidth, double startY, double height)
    {
        // RSI刻度值：超买线、中线、超卖线
        var levels = new[] 
        { 
            (Value: RSIOverboughtLevel, Label: $"{RSIOverboughtLevel:F0}"),
            (Value: 50.0, Label: "50"),
            (Value: RSIOversoldLevel, Label: $"{RSIOversoldLevel:F0}")
        };
        
        foreach (var level in levels)
        {
            // 计算Y坐标（RSI范围0-100）
            var ratio = (100 - level.Value) / 100.0;
            var y = startY + height * ratio;
            
            // 刻度标签
            var text = new TextBlock
            {
                Text = level.Label,
                Foreground = _textColor,
                FontSize = 9,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            
            // 放置在右侧
            var xPosition = LeftMargin + chartWidth + 8;
            Canvas.SetLeft(text, xPosition);
            Canvas.SetTop(text, y - 6);
            ChartCanvas.Children.Add(text);
            
            // 绘制刻度线（连接到右侧）
            var tickLine = new Line
            {
                StartPoint = new Point(LeftMargin + chartWidth, y),
                EndPoint = new Point(LeftMargin + chartWidth + 4, y),
                Stroke = _textColor,
                StrokeThickness = 1,
                Opacity = 0.5
            };
            ChartCanvas.Children.Add(tickLine);
        }
    }
    
    /// <summary>
    /// 绘制时间轴（在成交量图下方）
    /// </summary>
    private void DrawTimeAxis(double chartWidth, double volumeBottomY)
    {
        if (_data.Count == 0) return;
        
        const int timeSegments = 10; // 将时间轴划分10个时间段
        
        // 绘制时间段分界点（共11个点，跳过第0个和第10个的刻度线和标签）
        for (int i = 0; i <= timeSegments; i++)
        {
            var ratio = (double)i / timeSegments;
            var x = LeftMargin + chartWidth * ratio;
            
            // 只绘制中间的刻度线和标签（跳过首尾）
            if (i > 0 && i < timeSegments)
            {
                // 绘制刻度线
                var tickLine = new Line
                {
                    StartPoint = new Point(x, volumeBottomY),
                    EndPoint = new Point(x, volumeBottomY + 4),
                    Stroke = _textColor,
                    StrokeThickness = 1
                };
                ChartCanvas.Children.Add(tickLine);
            }
            
            // 只在中间的时间点显示标签（跳过首尾，避免溢出）
            if (i > 0 && i < timeSegments)
            {
                // 计算对应的K线索引
                var dataIndex = (int)((_data.Count - 1) * ratio);
                dataIndex = Math.Min(dataIndex, _data.Count - 1); // 确保不越界
                
                var candle = _data[dataIndex];
                
                // 时间标签
                var timeText = new TextBlock
                {
                    Text = FormatTimeLabel(candle.Time),
                    Foreground = _textColor,
                    FontSize = 9,  // 缩小字体以适应更长的时间格线
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    Padding = new Thickness(0),  // 确保没有内边距
                    Margin = new Thickness(0)    // 确保没有外边距
                };
                
                // 测量文本实际宽度以精确居中
                timeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textWidth = timeText.DesiredSize.Width;
                
                // 精确居中：刻度线x位置 - 文本宽度的一半
                Canvas.SetLeft(timeText, x - textWidth / 2);
                Canvas.SetTop(timeText, volumeBottomY + 8);
                ChartCanvas.Children.Add(timeText);
            }
        }
    }
    
    /// <summary>
    /// 格式化时间标签
    /// </summary>
    private string FormatTimeLabel(DateTime time)
    {
        // 统一使用标准格式：yyyy-MM-dd HH:mm:ss
        return time.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 绘制高亮
    /// </summary>
    private void DrawHighlight(CandlestickArea area)
    {
        var highlight = new Rectangle
        {
            Width = area.Width,
            Height = area.Bottom - area.Top + 10,
            Fill = _highlightColor,
            Stroke = new SolidColorBrush(Color.Parse("#FFA500")),
            StrokeThickness = 2,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 2 }
        };
        
        Canvas.SetLeft(highlight, area.X - area.Width / 2);
        Canvas.SetTop(highlight, area.Top - 5);
        ChartCanvas.Children.Add(highlight);
    }
    
    /// <summary>
    /// 查找指定位置的K线
    /// </summary>
    private CandlestickArea? FindCandleAtPosition(Point position)
    {
        foreach (var area in _candlestickAreas)
        {
            var left = area.X - area.Width / 2;
            var right = area.X + area.Width / 2;
            
            if (position.X >= left && position.X <= right &&
                position.Y >= area.Top - 5 && position.Y <= area.Bottom + 5)
            {
                return area;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 显示详细信息面板
    /// </summary>
    private void ShowDetailPanel(Candlestick candle)
    {
        if (DetailPanel == null) return;
        
        DetailPanel.IsVisible = true;
        
        // 计算涨跌额和涨跌百分比
        var change = candle.Close - candle.Open;
        var changePercent = (change / candle.Open) * 100;
        var isRising = change >= 0;
        var changeColor = isRising ? GetRisingColor() : GetFallingColor();
        
        // 更新详细信息
        if (DetailTimeText != null) 
            DetailTimeText.Text = candle.Time.ToString("yyyy-MM-dd HH:mm:ss");
        
        if (DetailOpenText != null) 
            DetailOpenText.Text = candle.Open.ToString("F2");
        
        if (DetailHighText != null) 
            DetailHighText.Text = candle.High.ToString("F2");
        
        if (DetailLowText != null) 
            DetailLowText.Text = candle.Low.ToString("F2");
        
        if (DetailCloseText != null)
        {
            DetailCloseText.Text = candle.Close.ToString("F2");
            DetailCloseText.Foreground = changeColor;
        }
        
        if (DetailChangeText != null)
        {
            DetailChangeText.Text = $"{(change >= 0 ? "+" : "")}{change:F2}";
            DetailChangeText.Foreground = changeColor;
        }
        
        if (DetailChangePercentText != null)
        {
            DetailChangePercentText.Text = $"{(changePercent >= 0 ? "+" : "")}{changePercent:F2}%";
            DetailChangePercentText.Foreground = changeColor;
        }
        
        if (DetailVolumeText != null)
        {
            DetailVolumeText.Text = candle.Volume.ToString("F2");
        }
    }
    
    /// <summary>
    /// 鼠标滚轮事件（缩放功能）
    /// </summary>
    private void ChartCanvas_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // 滚轮向上（delta.Y > 0）：缩小（显示更多K线）
        // 滚轮向下（delta.Y < 0）：放大（显示更少K线）
        var delta = e.Delta.Y;
        var zoomFactor = delta > 0 ? 0.9 : 1.1; // 缩放系数
        
        // 计算新的可见数量
        var newVisibleCount = (int)(_visibleCount * zoomFactor);
        newVisibleCount = Math.Max(MinVisibleCount, Math.Min(MaxVisibleCount, newVisibleCount));
        
        if (newVisibleCount != _visibleCount)
        {
            // 获取鼠标在图表中的相对位置（0-1）
            var mousePos = e.GetPosition(ChartCanvas);
            var chartWidth = ChartCanvas.Bounds.Width - LeftMargin - RightMargin;
            var relativePos = (mousePos.X - LeftMargin) / chartWidth;
            relativePos = Math.Max(0, Math.Min(1, relativePos));
            
            // 计算缩放中心点对应的数据索引
            var centerDataIndex = _startIndex + (int)(_visibleCount * relativePos);
            
            // 更新可见数量
            _visibleCount = newVisibleCount;
            
            // 调整起始索引，使缩放中心保持在鼠标位置
            // 允许将最后一根K线拖拽到3/4屏位�?
            var maxStartIndex = _allData.Count - (int)(_visibleCount * 0.75);
            _startIndex = centerDataIndex - (int)(_visibleCount * relativePos);
            _startIndex = Math.Max(0, Math.Min(maxStartIndex, _startIndex));
            
            // 更新数据并重绘
            UpdateVisibleData();
            DrawChart();
        }
        
        e.Handled = true;
    }
    
    /// <summary>
    /// 键盘事件（方向键滚动）
    /// </summary>
    private void CandlestickChart_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_allData.Count == 0) return;
        
        var scrollStep = Math.Max(1, _visibleCount / 10); // 每次滚动10%的可见数量
        var oldIndex = _startIndex;
        
        switch (e.Key)
        {
            case Key.Left:
                // 向左滚动（显示更早的数据）
                _startIndex = Math.Max(0, _startIndex - scrollStep);
                e.Handled = true;
                break;
                
            case Key.Right:
                // 向右滚动（显示更新的数据）
                // 允许将最后一根K线拖拽到3/4屏位�?
                var maxStartIndex = _allData.Count - (int)(_visibleCount * 0.75);
                _startIndex = Math.Min(maxStartIndex, _startIndex + scrollStep);
                e.Handled = true;
                break;
                
            case Key.Home:
                // 跳转到最早的数据
                _startIndex = 0;
                e.Handled = true;
                break;
                
            case Key.End:
                // 跳转到最末尾（允许右侧留白）
                _startIndex = Math.Max(0, _allData.Count - (int)(_visibleCount * 0.75));
                e.Handled = true;
                break;
        }
        
        // 如果索引改变了，更新显示
        if (_startIndex != oldIndex)
        {
            UpdateVisibleData();
            DrawChart();
        }
    }
    
    /// <summary>
    /// 鼠标按下事件（开始拖拽）
    /// </summary>
    private void ChartCanvas_OnPointerPressed_Drag(object? sender, PointerPressedEventArgs e)
    {
        // 只处理左键拖拽
        if (!e.GetCurrentPoint(ChartCanvas).Properties.IsLeftButtonPressed)
            return;
        
        var position = e.GetPosition(ChartCanvas);
        
        // 开始拖拽滚动
        _isDragging = true;
        _dragStartPoint = position;
        _dragStartIndex = _startIndex;
        
        // 捕获鼠标，确保即使鼠标移出控件也能继续拖拽
        e.Pointer.Capture(ChartCanvas);
        
        // 改变鼠标光标为手型
        ChartCanvas.Cursor = new Cursor(StandardCursorType.Hand);
    }
    
    /// <summary>
    /// 鼠标移动事件（拖拽滚动 + 十字准星）
    /// </summary>
    private void ChartCanvas_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var currentPos = e.GetPosition(ChartCanvas);
        
        // 更新光标样式：在主图区域显示十字光标
        UpdateCursorStyle(currentPos);
        
        // 处理拖拽滚动
        if (_isDragging)
        {
            var deltaX = currentPos.X - _dragStartPoint.X;
            
            // 计算拖拽的距离对应的K线数量
            var chartWidth = ChartCanvas.Bounds.Width - LeftMargin - RightMargin;
            if (chartWidth <= 0) return;
            
            var draggedCandleCount = (int)(-deltaX / chartWidth * _visibleCount);
            
            // 更新起始索引
            // 允许将最后一根K线拖拽到3/4屏位置（右侧预留1/4屏空白，即_visibleCount/4根K线的位置）
            var maxStartIndex = _allData.Count - (int)(_visibleCount * 0.75);
            var newStartIndex = _dragStartIndex + draggedCandleCount;
            newStartIndex = Math.Max(0, Math.Min(maxStartIndex, newStartIndex));
            
            if (newStartIndex != _startIndex)
            {
                _startIndex = newStartIndex;
                UpdateVisibleData();
                DrawChart();
                
                // 检查是否需要加载更多历史数据
                CheckAndRequestMoreData();
            }
        }
        // 处理十字准星
        else if (_isMouseOver)
        {
            _mousePosition = currentPos;
            DrawCrosshair(currentPos);
        }
    }
    
    /// <summary>
    /// 根据鼠标位置更新光标样式
    /// </summary>
    private void UpdateCursorStyle(Point position)
    {
        if (ChartCanvas == null) return;
        
        // 如果正在拖拽，保持手型光标
        if (_isDragging)
        {
            ChartCanvas.Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }
        
        var chartWidth = ChartCanvas.Bounds.Width - LeftMargin - RightMargin;
        var bottomY = _chartBottomY > 0 ? _chartBottomY : (ChartCanvas.Bounds.Height - BottomMargin);
        
        // 判断是否在图表区域（主图+副图区域）
        bool isInChartArea = position.X >= LeftMargin && 
                            position.X <= LeftMargin + chartWidth &&
                            position.Y >= TopMargin && 
                            position.Y <= bottomY;
        
        // 在图表区域显示十字光标，其他区域显示默认光标
        ChartCanvas.Cursor = isInChartArea ? new Cursor(StandardCursorType.Cross) : new Cursor(StandardCursorType.Arrow);
    }
    
    /// <summary>
    /// 鼠标释放事件（结束拖拽）
    /// </summary>
    private void ChartCanvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            
            // 恢复鼠标光标
            ChartCanvas.Cursor = new Cursor(StandardCursorType.Arrow);
            
            // 释放鼠标捕获
            e.Pointer.Capture(null);
        }
    }
    
    /// <summary>
    /// 鼠标捕获丢失事件（取消拖拽）
    /// </summary>
    private void ChartCanvas_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ChartCanvas.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }
    
    /// <summary>
    /// 检查是否需要加载更多历史数据
    /// </summary>
    private void CheckAndRequestMoreData()
    {
        // 如果正在加载中，不重复触发
        if (_isLoadingMore) return;
        
        // 如果距离起始位置小于阈值，触发加载更多
        if (_startIndex < LoadMoreThreshold && _allData.Count > 0)
        {
            _isLoadingMore = true;
            
            // 获取当前最早的K线时间
            var earliestTime = _allData.FirstOrDefault()?.Time;
            
            if (earliestTime.HasValue)
            {
                // 触发事件，通知外部需要加载更多数据
                LoadMoreDataRequested?.Invoke(this, new LoadMoreDataEventArgs
                {
                    EarliestTime = earliestTime.Value,
                    RequestCount = 500, // 每次请求500根K线
                    CurrentStartIndex = _startIndex,
                    CurrentVisibleCount = _visibleCount
                });
            }
        }
    }
    
    /// <summary>
    /// 鼠标进入事件（显示十字准星）
    /// </summary>
    private void ChartCanvas_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isMouseOver = true;
    }
    
    /// <summary>
    /// 鼠标离开事件（隐藏十字准星）
    /// </summary>
    private void ChartCanvas_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isMouseOver = false;
        _hoverCandleIndex = -1;  // 清除悬停索引
        
        // 恢复默认光标
        if (ChartCanvas != null)
        {
            ChartCanvas.Cursor = new Cursor(StandardCursorType.Arrow);
        }
        
        HideCrosshair();
        
        // 显示最新蜡烛图数据
        ShowLatestCandleInfo();
        
        DrawChart();  // 重新绘制以移除高亮
    }
    
    /// <summary>
    /// 隐藏十字准星
    /// </summary>
    private void HideCrosshair()
    {
        if (_crosshairHLine != null)
        {
            ChartCanvas.Children.Remove(_crosshairHLine);
            _crosshairHLine = null;
        }
        if (_crosshairVLine != null)
        {
            ChartCanvas.Children.Remove(_crosshairVLine);
            _crosshairVLine = null;
        }
        if (_crosshairPriceBorder != null)
        {
            ChartCanvas.Children.Remove(_crosshairPriceBorder);
            _crosshairPriceBorder = null;
        }
        if (_crosshairTimeBorder != null)
        {
            ChartCanvas.Children.Remove(_crosshairTimeBorder);
            _crosshairTimeBorder = null;
        }
        
        // 清除副图十字准星元素
        foreach (var line in _subChartCrosshairLines)
        {
            ChartCanvas.Children.Remove(line);
        }
        _subChartCrosshairLines.Clear();
        
        foreach (var border in _subChartCrosshairValueBorders)
        {
            ChartCanvas.Children.Remove(border);
        }
        _subChartCrosshairValueBorders.Clear();
    }
    
    /// <summary>
    /// 格式化副图指标值（用于十字准星显示）
    /// </summary>
    private string FormatSubChartValue(double value, double range)
    {
        // 根据数值范围选择合适的格式
        if (Math.Abs(value) < 0.01 || range < 0.1)
        {
            return value.ToString("F4");
        }
        else if (Math.Abs(value) >= 1000000)
        {
            if (Math.Abs(value) >= 1000000000)
                return $"{value / 1000000000:N1}B";
            else if (Math.Abs(value) >= 1000000)
                return $"{value / 1000000:N1}M";
            else
                return $"{value / 1000:N1}K";
        }
        else if (Math.Abs(value) >= 100)
        {
            return value.ToString("N1");
        }
        else
        {
            return value.ToString("N2");
        }
    }
    
    /// <summary>
    /// 绘制十字准星
    /// </summary>
    private void DrawCrosshair(Point position)
    {
        if (_data.Count == 0 || ChartCanvas == null) return;
        
        var width = ChartCanvas.Bounds.Width;
        var height = ChartCanvas.Bounds.Height;
        if (width <= 0 || height <= 0) return;
        
        var chartWidth = width - LeftMargin - RightMargin;
        var bottomY = _chartBottomY > 0 ? _chartBottomY : (height - BottomMargin);
        
        // 限制鼠标位置在有效区域内
        var mouseX = Math.Max(LeftMargin, Math.Min(position.X, LeftMargin + chartWidth));
        var y = Math.Max(TopMargin, Math.Min(position.Y, bottomY));
        
        // 先移除旧的十字准星
        HideCrosshair();
        
        // 使用保存的有效宽度和间距来计算K线索引（考虑右侧空白）
        var effectiveWidth = _currentEffectiveWidth > 0 ? _currentEffectiveWidth : chartWidth;
        var spacing = _currentSpacing > 0 ? _currentSpacing : (chartWidth / _data.Count);
        
        // 计算最近的K线索引和该K线的中心X坐标
        var relativeX = (mouseX - LeftMargin) / effectiveWidth;
        var dataIndex = (int)((_data.Count - 1) * relativeX + 0.5); // +0.5 实现四舍五入到最近的K线
        dataIndex = Math.Max(0, Math.Min(dataIndex, _data.Count - 1));
        
        // 计算该K线的中心X坐标（吸附位置）
        var snapX = LeftMargin + dataIndex * spacing + spacing / 2;
        
        // 判断鼠标是否在主图区域（不包括副图）
        var mainChartBottom = _mainChartBottomY > 0 ? _mainChartBottomY : (height - BottomMargin);
        var isInMainChart = y >= TopMargin && y < mainChartBottom;
        
        // 只在主图区域时绘制横向虚线（价格线）
        if (isInMainChart)
        {
            _crosshairHLine = new Line
            {
                StartPoint = new Point(LeftMargin, y),
                EndPoint = new Point(LeftMargin + chartWidth, y),
                Stroke = _crosshairLineColor,
                StrokeThickness = 1,
                StrokeDashArray = new AvaloniaList<double> { 4, 4 } // 虚线样式
            };
            ChartCanvas.Children.Add(_crosshairHLine);
        }
        
        // 绘制竖向虚线（时间线） 贯穿主图和所有副图，吸附到K线中
        _crosshairVLine = new Line
        {
            StartPoint = new Point(snapX, TopMargin),
            EndPoint = new Point(snapX, bottomY),
            Stroke = _crosshairLineColor,
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 4, 4 } // 虚线样式
        };
        ChartCanvas.Children.Add(_crosshairVLine);
        
        // 只在主图区域时在价格轴上显示价格
        if (isInMainChart && _priceRange > 0)
        {
            var actualMainChartHeight = mainChartBottom - TopMargin;
            var priceRatio = (actualMainChartHeight - (y - TopMargin)) / actualMainChartHeight;
            var price = _minPrice + _priceRange * priceRatio;
            
            _crosshairPriceText = new TextBlock
            {
                Text = price.ToString("N2"),
                Foreground = _crosshairTextColor, // 使用更亮的颜色
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Background = new SolidColorBrush(Color.Parse("#2C2C2C")),
                Padding = new Thickness(4, 2)
            };
            
            _crosshairPriceBorder = new Border
            {
                Child = _crosshairPriceText,
                Background = new SolidColorBrush(Color.Parse("#2C2C2C")),
                BorderBrush = _crosshairLineColor,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };
            
            Canvas.SetLeft(_crosshairPriceBorder, LeftMargin + chartWidth + 5);
            Canvas.SetTop(_crosshairPriceBorder, y - 10);
            ChartCanvas.Children.Add(_crosshairPriceBorder);
        }
        
        // 🔑 绘制副图的横向虚线和动态值显示
        foreach (var subArea in _subChartAreas)
        {
            // 检查鼠标是否在该副图区域内
            if (y >= subArea.TopY && y < subArea.TopY + subArea.Height)
            {
                // 绘制副图横向虚线
                var subLine = new Line
                {
                    StartPoint = new Point(LeftMargin, y),
                    EndPoint = new Point(LeftMargin + chartWidth, y),
                    Stroke = _crosshairLineColor,
                    StrokeThickness = 1,
                    StrokeDashArray = new AvaloniaList<double> { 4, 4 }
                };
                _subChartCrosshairLines.Add(subLine);
                ChartCanvas.Children.Add(subLine);
                
                // 计算该位置对应的指标值
                var subChartRatio = (y - subArea.TopY) / subArea.Height;
                var indicatorValue = subArea.MaxValue - (subArea.MaxValue - subArea.MinValue) * subChartRatio;
                
                // 在右侧显示指标值
                var valueText = new TextBlock
                {
                    Text = FormatSubChartValue(indicatorValue, subArea.MaxValue - subArea.MinValue),
                    Foreground = _crosshairTextColor,
                    FontSize = 10,
                    FontWeight = FontWeight.SemiBold,
                    Background = new SolidColorBrush(Color.Parse("#2C2C2C")),
                    Padding = new Thickness(4, 2)
                };
                
                var valueBorder = new Border
                {
                    Child = valueText,
                    Background = new SolidColorBrush(Color.Parse("#2C2C2C")),
                    BorderBrush = _crosshairLineColor,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3)
                };
                
                Canvas.SetLeft(valueBorder, LeftMargin + chartWidth + 5);
                Canvas.SetTop(valueBorder, y - 10);
                _subChartCrosshairValueBorders.Add(valueBorder);
                ChartCanvas.Children.Add(valueBorder);
                
                // 只显示第一个匹配的副图（鼠标只能在一个副图中）
                break;
            }
        }
        
        // 在时间轴上显示时间（使用吸附后的K线数据）
        var candle = _data[dataIndex];
        
        _crosshairTimeText = new TextBlock
        {
            Text = FormatTimeLabel(candle.Time),
            Foreground = _crosshairTextColor, // 使用更亮的颜色
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            Background = new SolidColorBrush(Color.Parse("#2C2C2C")),
            Padding = new Thickness(4, 2),
            TextAlignment = Avalonia.Media.TextAlignment.Center
        };
        
        _crosshairTimeBorder = new Border
        {
            Child = _crosshairTimeText,
            Background = new SolidColorBrush(Color.Parse("#2C2C2C")),
            BorderBrush = _crosshairLineColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3)
        };
        
        // 测量文本宽度以居中
        _crosshairTimeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = _crosshairTimeText.DesiredSize.Width;
        
        // 使用吸附后的X坐标，时间标签放在图表底部（包括所有副图）
        Canvas.SetLeft(_crosshairTimeBorder, snapX - textWidth / 2 - 4);
        Canvas.SetTop(_crosshairTimeBorder, bottomY + 8);
        ChartCanvas.Children.Add(_crosshairTimeBorder);
        
        // 如果悬停的K线索引改变了，需要重新绘制整个图表
        if (_hoverCandleIndex != dataIndex)
        {
            _hoverCandleIndex = dataIndex;
            DrawChart();  // 重新绘制以高亮新的K线
        }
        else
        {
            // 更新左上角的光标K线信息
            UpdateCursorInfo(candle, dataIndex);
        }
    }
    
    /// <summary>
    /// 显示最新蜡烛图信息（当鼠标未悬停时）
    /// </summary>
    private void ShowLatestCandleInfo()
    {
        if (ChartCanvas == null || _allData.Count == 0) return;
        
        // 获取最新的蜡烛图
        var latestCandle = _allData[_allData.Count - 1];
        var latestIndex = _allData.Count - 1;
        
        // 移除旧的信息面板
        if (_cursorInfoPanel != null)
        {
            ChartCanvas.Children.Remove(_cursorInfoPanel);
            _cursorInfoPanel = null;
        }
        
        // 创建一个StackPanel来容纳多行信息
        _cursorInfoPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 3
        };
        
        // ===== 第一行：K线基础数据 =====
        var candleInfoBlock = CreateCandleInfoLine(latestCandle);
        _cursorInfoPanel.Children.Add(candleInfoBlock);
        
        // ===== 新框架：添加主图指标摘要 =====
        if (_indicatorManager != null)
        {
            var mainIndicators = _indicatorManager.GetAllIndicators()
                .Where(i => i.Config.IsEnabled && i.Location == Indicators.Core.IndicatorLocation.MainChart);
            
            foreach (var indicator in mainIndicators)
            {
                var indicatorInfoBlock = CreateIndicatorInfoLine(indicator, latestCandle);
                if (indicatorInfoBlock != null)
                {
                    _cursorInfoPanel.Children.Add(indicatorInfoBlock);
                }
            }
        }
        
        // 设置位置（在Canvas内左上角）
        Canvas.SetLeft(_cursorInfoPanel, 8);
        Canvas.SetTop(_cursorInfoPanel, 8);
        
        // 添加到Canvas（最后添加，确保在上层）
        ChartCanvas.Children.Add(_cursorInfoPanel);
        
        // 更新副图标题（动态显示最新K线的副图指标值）
        UpdateSubChartTitles();
    }
    
    /// <summary>
    /// 更新光标K线信息
    /// </summary>
    private void UpdateCursorInfo(Candlestick candle, int dataIndex)
    {
        if (ChartCanvas == null) return;
        
        // 设置当前悬停的K线索引（用于副图摘要）
        _hoverCandleIndex = dataIndex;
        
        // 移除旧的信息面板
        if (_cursorInfoPanel != null)
        {
            ChartCanvas.Children.Remove(_cursorInfoPanel);
            _cursorInfoPanel = null;
        }
        
        // 创建一个StackPanel来容纳多行信息
        _cursorInfoPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 3
        };
        
        // ===== 第一行：K线基础数据 =====
        var candleInfoBlock = CreateCandleInfoLine(candle);
        _cursorInfoPanel.Children.Add(candleInfoBlock);
        
        // 计算全局索引（基于_allData）
        var globalIndex = _startIndex + dataIndex;
        
        // ===== 新框架：添加主图指标摘要 =====
        if (_indicatorManager != null)
        {
            var mainIndicators = _indicatorManager.GetAllIndicators()
                .Where(i => i.Config.IsEnabled && i.Location == Indicators.Core.IndicatorLocation.MainChart);
            
            foreach (var indicator in mainIndicators)
            {
                var indicatorInfoBlock = CreateIndicatorInfoLine(indicator, candle);
                if (indicatorInfoBlock != null)
                {
                    _cursorInfoPanel.Children.Add(indicatorInfoBlock);
                }
            }
        }
        
        // 设置位置（在Canvas内左上角）
        Canvas.SetLeft(_cursorInfoPanel, 8);
        Canvas.SetTop(_cursorInfoPanel, 8);
        
        // 添加到Canvas（最后添加，确保在上层）
        ChartCanvas.Children.Add(_cursorInfoPanel);
        
        // 更新副图标题（动态显示当前K线的副图指标值）
        UpdateSubChartTitles();
    }
    
    /// <summary>
    /// 更新副图标题（显示当前悬停K线的指标摘要）
    /// </summary>
    private void UpdateSubChartTitles()
    {
        // 更新新框架的副图指标摘要
        UpdateNewFrameworkSubChartTitles();
    }
    
    /// <summary>
    /// 创建指标信息行（新框架）
    /// </summary>
    private TextBlock? CreateIndicatorInfoLine(Indicators.Core.IIndicator indicator, Candlestick candle)
    {
        var dataPoint = indicator.Data.FindByTime(candle.Time);
        if (dataPoint == null || indicator.Config.Lines == null) return null;
        
        // 创建TextBlock，使用Run来实现多色文本
        var textBlock = new TextBlock
        {
            Inlines = new Avalonia.Controls.Documents.InlineCollection(),
            FontSize = 11  // 与K线摘要相同
        };
        
        // 🚀 特殊处理带状指标（BOLL、Keltner）
        if (indicator.Id == "BOLL" || indicator.Id == "Keltner")
        {
            // 获取参数描述
            string paramDesc = GetIndicatorParameterDescription(indicator);
            
            // 添加指标名和参数
            textBlock.Inlines!.Add(new Run
            {
                Text = $"{indicator.Id}({paramDesc}) ",
                Foreground = _labelColor
            });
            
            // 按顺序显示：UP, MB, DN
            var lineOrder = new[] { ("upper", "UP"), ("middle", "MB"), ("lower", "DN") };
            bool first = true;
            
            foreach (var (key, shortName) in lineOrder)
            {
                var lineConfig = indicator.Config.Lines.FirstOrDefault(l => l.Key.ToLower() == key && l.IsEnabled);
                if (lineConfig != null)
                {
                    var value = dataPoint.GetValue(lineConfig.Key);
                    if (!double.IsNaN(value))
                    {
                        if (!first)
                        {
                            textBlock.Inlines!.Add(new Run
                            {
                                Text = ", ",
                                Foreground = _labelColor
                            });
                        }
                        
                        textBlock.Inlines!.Add(new Run
                        {
                            Text = $"{shortName}: ",
                            Foreground = _labelColor
                        });
                        
                        textBlock.Inlines!.Add(new Run
                        {
                            Text = $"{value:N2}",
                            Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                        });
                        
                        first = false;
                    }
                }
            }
        }
        // 🚀 特殊处理Ichimoku指标
        else if (indicator.Id == "Ichimoku")
        {
            // 获取参数描述
            string paramDesc = GetIndicatorParameterDescription(indicator);
            
            // 添加指标名和参数
            textBlock.Inlines!.Add(new Run
            {
                Text = $"Ichimoku({paramDesc}) ",
                Foreground = _labelColor
            });
            
            // 按顺序显示：Tenkan, Kijun, Senkou A, Senkou B, Chikou
            var lineOrder = new[] 
            { 
                ("tenkan", "Tenkan"), 
                ("kijun", "Kijun"), 
                ("senkou_a", "Senkou A"), 
                ("senkou_b", "Senkou B"),
                ("chikou", "Chikou")
            };
            bool first = true;
            
            foreach (var (key, shortName) in lineOrder)
            {
                var lineConfig = indicator.Config.Lines.FirstOrDefault(l => l.Key.ToLower() == key && l.IsEnabled);
                if (lineConfig != null)
                {
                    var value = dataPoint.GetValue(lineConfig.Key);
                    if (!double.IsNaN(value))
                    {
                        if (!first)
                        {
                            textBlock.Inlines!.Add(new Run
                            {
                                Text = ", ",
                                Foreground = _labelColor
                            });
                        }
                        
                        textBlock.Inlines!.Add(new Run
                        {
                            Text = $"{shortName}: ",
                            Foreground = _labelColor
                        });
                        
                        textBlock.Inlines!.Add(new Run
                        {
                            Text = $"{value:N2}",
                            Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                        });
                        
                        first = false;
                    }
                }
            }
        }
        // 🚀 特殊处理SAR指标
        else if (indicator.Id == "SAR")
        {
            // 获取参数描述
            string paramDesc = GetIndicatorParameterDescription(indicator);
            
            // 获取SAR值
            var lineConfig = indicator.Config.Lines.FirstOrDefault(l => l.IsEnabled);
            if (lineConfig != null)
            {
                var value = dataPoint.GetValue(lineConfig.Key);
                if (!double.IsNaN(value))
                {
                    textBlock.Inlines!.Add(new Run
                    {
                        Text = $"SAR({paramDesc}): ",
                        Foreground = _labelColor
                    });
                    
                    textBlock.Inlines!.Add(new Run
                    {
                        Text = $"{value:N2}",
                        Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                    });
                }
            }
        }
        // 🚀 特殊处理VWAP指标
        else if (indicator.Id == "VWAP")
        {
            // VWAP通常没有周期参数，但如果配置中有，则显示
            string paramDesc = GetIndicatorParameterDescription(indicator);
            
            // 获取VWAP值
            var lineConfig = indicator.Config.Lines.FirstOrDefault(l => l.IsEnabled);
            if (lineConfig != null)
            {
                var value = dataPoint.GetValue(lineConfig.Key);
                if (!double.IsNaN(value))
                {
                    string label = string.IsNullOrEmpty(paramDesc) ? "VWAP" : $"VWAP({paramDesc})";
                    
                    textBlock.Inlines!.Add(new Run
                    {
                        Text = $"{label}: ",
                        Foreground = _labelColor
                    });
                    
                    textBlock.Inlines!.Add(new Run
                    {
                        Text = $"{value:N2}",
                        Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                    });
                }
            }
        }
        else
        {
            // 其他指标：各条线的值（格式：指标名(参数): 值）
            foreach (var lineConfig in indicator.Config.Lines.Where(l => l.IsEnabled))
            {
                var value = dataPoint.GetValue(lineConfig.Key);
                
                if (!double.IsNaN(value))
                {
                    // 🚀 格式化标签：从LineConfig提取参数
                    string label;
                    if (lineConfig.PERIOD > 0)
                    {
                        // 多周期指标：MA(7), EMA(25)
                        label = $"{indicator.Id}({lineConfig.PERIOD})";
                    }
                    else
                    {
                        // 无周期参数的指标：使用原始Label
                        label = lineConfig.Label ?? indicator.Id;
                    }
                    
                    textBlock.Inlines!.Add(new Run
                    {
                        Text = $"{label}: ",  // 🚀 冒号后面加空格
                        Foreground = _labelColor
                    });
                    
                    textBlock.Inlines!.Add(new Run
                    {
                        Text = $"{value:N2} ",  // 🚀 使用N2格式（千分位+2位小数）
                        Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                    });
                }
            }
        }
        
        return textBlock;
    }
    
    /// <summary>
    /// 创建K线信息行
    /// </summary>
    private TextBlock CreateCandleInfoLine(Candlestick candle)
    {
        // 计算涨幅
        var change = candle.Close - candle.Open;
        var changePercent = candle.Open > 0 ? (change / candle.Open * 100) : 0;
        var changeSign = change >= 0 ? "+" : "";
        
        // 计算振幅
        var amplitude = candle.High - candle.Low;
        var amplitudePercent = candle.Low > 0 ? (amplitude / candle.Low * 100) : 0;
        
        // 格式化时间
        var timeStr = candle.Time.ToString("yyyy-MM-dd HH:mm:ss");
        
        // 格式化成交量
        var volumeStr = FormatVolume(candle.Volume);
        
        // 设置数值颜色（涨绿跌红）
        var valueColor = candle.IsRising ? GetRisingColor() : GetFallingColor();
        
        // 创建TextBlock，使用Run来实现多色文本
        var textBlock = new TextBlock
        {
            Inlines = new Avalonia.Controls.Documents.InlineCollection(),
            FontSize = 11
        };
        
        // 添加各个部分的文本
        textBlock.Inlines!.Add(new Run { Text = $"{_currentSymbol} ", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = timeStr, Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = " Open:", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = $"{candle.Open:N2}", Foreground = valueColor });
        textBlock.Inlines!.Add(new Run { Text = ", High:", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = $"{candle.High:N2}", Foreground = valueColor });
        textBlock.Inlines!.Add(new Run { Text = ", Low:", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = $"{candle.Low:N2}", Foreground = valueColor });
        textBlock.Inlines!.Add(new Run { Text = ", Close:", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = $"{candle.Close:N2}", Foreground = valueColor });
        textBlock.Inlines!.Add(new Run { Text = ", Volume:", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = volumeStr, Foreground = valueColor });
        textBlock.Inlines!.Add(new Run { Text = ", Change:", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = $"{changeSign}{changePercent:F2}%", Foreground = valueColor });
        textBlock.Inlines!.Add(new Run { Text = ", Amplitude:", Foreground = _labelColor });
        textBlock.Inlines!.Add(new Run { Text = $"{amplitudePercent:F2}%", Foreground = valueColor });
        
        return textBlock;
    }
    
    /// <summary>
    /// 计算价格范围，包含主图指标的值，确保指标线不会超出绘图区域
    /// </summary>
    private void CalculatePriceRangeWithIndicators()
    {
        if (_data.Count == 0) return;
        
        // 从K线数据获取初始价格范围
        double minPrice = _data.Min(c => c.Low);
        double maxPrice = _data.Max(c => c.High);
        
        // 如果指标管理器已初始化，检查主图指标的值
        if (_indicatorManager != null)
        {
            var mainIndicators = _indicatorManager.GetAllIndicators()
                .Where(i => i.Config.IsEnabled && i.Location == Indicators.Core.IndicatorLocation.MainChart);
            
            foreach (var indicator in mainIndicators)
            {
                // 遍历可见K线对应的指标数据
                foreach (var candle in _data)
                {
                    var dataPoint = indicator.Data.FindByTime(candle.Time);
                    if (dataPoint != null)
                    {
                        // 获取该数据点的所有值
                        var values = dataPoint.GetAllValues();
                        foreach (var kvp in values)
                        {
                            var value = kvp.Value;
                            // 只考虑有效的数值（不是NaN或Infinity）
                            if (!double.IsNaN(value) && !double.IsInfinity(value))
                            {
                                minPrice = Math.Min(minPrice, value);
                                maxPrice = Math.Max(maxPrice, value);
                            }
                        }
                    }
                }
            }
        }
        
        // 添加一些边距（上下各留2%空间），使图表更美观
        var range = maxPrice - minPrice;
        if (range > 0)
        {
            var margin = range * 0.02;
            minPrice -= margin;
            maxPrice += margin;
        }
        else
        {
            // 如果范围为零，添加一个小的默认范围
            var defaultRange = Math.Abs(maxPrice) * 0.1;
            if (defaultRange < 1e-10) defaultRange = 1.0;
            minPrice -= defaultRange * 0.02;
            maxPrice += defaultRange * 0.02;
        }
        
        // 更新价格范围
        _minPrice = minPrice;
        _maxPrice = maxPrice;
        _priceRange = maxPrice - minPrice;
    }
    
    /// <summary>
    /// 设置变更事件处理
    /// </summary>
    private void OnSettingsChanged(object? sender, AppSettings newSettings)
    {
        // 当设置改变时，重新绘制图表以应用新的颜色方案
        Dispatcher.UIThread.Post(() =>
        {
            DrawChart();
        });
    }
}

/// <summary>
/// 加载更多数据事件参数
/// </summary>
public class LoadMoreDataEventArgs : EventArgs
{
    /// <summary>
    /// 当前最早的K线时间
    /// </summary>
    public DateTime EarliestTime { get; set; }
    
    /// <summary>
    /// 请求加载的数据数量
    /// </summary>
    public int RequestCount { get; set; }
    
    /// <summary>
    /// 当前起始索引
    /// </summary>
    public int CurrentStartIndex { get; set; }
    
    /// <summary>
    /// 当前可见数量
    /// </summary>
    public int CurrentVisibleCount { get; set; }
}
