using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Prophet.Client.Models;

namespace Prophet.Client.Views.Dialogs;

public partial class IndicatorDialog : UserControl
{
    // 指标配置结果
    public class IndicatorSettings
    {
        // MA 设置
        public MATypeIndicatorConfig MA { get; set; } = new();
        
        // EMA 设置
        public MATypeIndicatorConfig EMA { get; set; } = new();
        
        // WMA 设置
        public MATypeIndicatorConfig WMA { get; set; } = new();
        
        // BOLL 设置
        public bool IsBOLLEnabled { get; set; }
        public int BOLLPeriod { get; set; } = 20;
        public double BOLLStdDev { get; set; } = 2.0;
        
        // VWAP 设置
        public bool IsVWAPEnabled { get; set; }
        public int VWAPPeriod { get; set; } = 14;
        public string VWAPColor { get; set; } = "#2196F3";
        
        // AVL 设置
        public bool IsAVLEnabled { get; set; }
        public int AVLPeriod { get; set; } = 20;
        public string AVLColor { get; set; } = "#9C27B0";
        
        // TRIX 设置
        public bool IsTRIXEnabled { get; set; }
        public int TRIXPeriod { get; set; } = 12;
        public string TRIXColor { get; set; } = "#FF9800";
        
        // SAR 设置
        public bool IsSAREnabled { get; set; }
        public double SARAcceleration { get; set; } = 0.02;
        public double SARMaxAcceleration { get; set; } = 0.20;
        public string SARUpColor { get; set; } = "#26A69A";
        public string SARDownColor { get; set; } = "#EF5350";
        
        // DEMA 设置
        public MATypeIndicatorConfig DEMA { get; set; } = new();
        
        // TEMA 设置
        public MATypeIndicatorConfig TEMA { get; set; } = new();
        
        // Keltner Channel 设置
        public bool IsKeltnerEnabled { get; set; }
        public int KeltnerPeriod { get; set; } = 20;
        public double KeltnerMultiplier { get; set; } = 2.0;
        public string KeltnerUpperColor { get; set; } = "#26A69A";
        public string KeltnerMiddleColor { get; set; } = "#2196F3";
        public string KeltnerLowerColor { get; set; } = "#EF5350";
        
        // Ichimoku Cloud 设置
        public bool IsIchimokuEnabled { get; set; }
        public int IchimokuTenkanPeriod { get; set; } = 9;
        public int IchimokuKijunPeriod { get; set; } = 26;
        public int IchimokuSenkouBPeriod { get; set; } = 52;
        public string IchimokuTenkanColor { get; set; } = "#F44336";
        public string IchimokuKijunColor { get; set; } = "#2196F3";
        public string IchimokuCloudBullishColor { get; set; } = "#4CAF50";
        public string IchimokuCloudBearishColor { get; set; } = "#EF5350";
    }
    
    public IndicatorSettings Settings { get; private set; } = new();
    public bool IsConfirmed { get; private set; }
    
    // 关闭对话框的事件
    public event EventHandler<bool>? CloseRequested;
    
    // 当前选中的指标
    private Border? _selectedIndicatorBorder;
    
    // 预设颜色列表
    private static readonly string[] PresetColors = new[]
    {
        "#FFFFFF", "#FFD700", "#FF00FF", "#00FFFF", "#FFA500", "#00FF00", "#FF1493", "#1E90FF",
        "#FF0000", "#32CD32", "#0000FF", "#FFFF00", "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4",
        "#E74C3C", "#3498DB", "#2ECC71", "#F39C12", "#9B59B6", "#1ABC9C", "#34495E", "#95A5A6"
    };
    
    public IndicatorDialog()
    {
        InitializeComponent();
        
        // 绑定CheckBox事件
        ChkMA.IsCheckedChanged += OnMACheckedChanged;
        ChkEMA.IsCheckedChanged += OnEMACheckedChanged;
        ChkWMA.IsCheckedChanged += OnWMACheckedChanged;
        ChkBOLL.IsCheckedChanged += OnBOLLCheckedChanged;
        ChkVWAP.IsCheckedChanged += OnVWAPCheckedChanged;
        ChkSAR.IsCheckedChanged += OnSARCheckedChanged;
        ChkDEMA.IsCheckedChanged += OnDEMACheckedChanged;
        ChkTEMA.IsCheckedChanged += OnTEMACheckedChanged;
        ChkKeltner.IsCheckedChanged += OnKeltnerCheckedChanged;
        ChkIchimoku.IsCheckedChanged += OnIchimokuCheckedChanged;
        
        // 初始选中MA
        SelectIndicator(BtnMA);
    }
    
    /// <summary>
    /// 加载当前设置
    /// </summary>
    public void LoadSettings(IndicatorSettings settings)
    {
        Settings = settings;
        
        // 加载MA设置
        ChkMA.IsChecked = settings.MA.IsEnabled;
        LoadMATypeSettings(settings.MA, "MA");
        
        // 加载EMA设置
        ChkEMA.IsChecked = settings.EMA.IsEnabled;
        LoadMATypeSettings(settings.EMA, "EMA");
        
        // 加载WMA设置
        ChkWMA.IsChecked = settings.WMA.IsEnabled;
        LoadMATypeSettings(settings.WMA, "WMA");
        
        // 加载DEMA设置
        ChkDEMA.IsChecked = settings.DEMA.IsEnabled;
        LoadMATypeSettings(settings.DEMA, "DEMA");
        
        // 加载TEMA设置
        ChkTEMA.IsChecked = settings.TEMA.IsEnabled;
        LoadMATypeSettings(settings.TEMA, "TEMA");
        
        // 加载BOLL设置
        ChkBOLL.IsChecked = settings.IsBOLLEnabled;
        TxtBOLLPeriod.Text = settings.BOLLPeriod.ToString();
        TxtBOLLStdDev.Text = settings.BOLLStdDev.ToString("F1");
        
        // 加载VWAP设置
        ChkVWAP.IsChecked = settings.IsVWAPEnabled;
        TxtVWAPPeriod.Text = settings.VWAPPeriod.ToString();
        BtnVWAPColor.Background = new SolidColorBrush(Color.Parse(settings.VWAPColor));
        
        // AVL和TRIX已移至副图系统，不再在主图配置
        
        // 加载SAR设置
        ChkSAR.IsChecked = settings.IsSAREnabled;
        TxtSARAcceleration.Text = settings.SARAcceleration.ToString("F2");
        TxtSARMaxAcceleration.Text = settings.SARMaxAcceleration.ToString("F2");
        BtnSARUpColor.Background = new SolidColorBrush(Color.Parse(settings.SARUpColor));
        BtnSARDownColor.Background = new SolidColorBrush(Color.Parse(settings.SARDownColor));
        
        // 加载Keltner设置
        ChkKeltner.IsChecked = settings.IsKeltnerEnabled;
        TxtKeltnerPeriod.Text = settings.KeltnerPeriod.ToString();
        TxtKeltnerMultiplier.Text = settings.KeltnerMultiplier.ToString("F1");
        BtnKeltnerUpperColor.Background = new SolidColorBrush(Color.Parse(settings.KeltnerUpperColor));
        BtnKeltnerMiddleColor.Background = new SolidColorBrush(Color.Parse(settings.KeltnerMiddleColor));
        BtnKeltnerLowerColor.Background = new SolidColorBrush(Color.Parse(settings.KeltnerLowerColor));
        
        // 加载Ichimoku设置
        ChkIchimoku.IsChecked = settings.IsIchimokuEnabled;
        TxtIchimokuTenkan.Text = settings.IchimokuTenkanPeriod.ToString();
        TxtIchimokuKijun.Text = settings.IchimokuKijunPeriod.ToString();
        TxtIchimokuSenkouB.Text = settings.IchimokuSenkouBPeriod.ToString();
        BtnIchimokuTenkanColor.Background = new SolidColorBrush(Color.Parse(settings.IchimokuTenkanColor));
        BtnIchimokuKijunColor.Background = new SolidColorBrush(Color.Parse(settings.IchimokuKijunColor));
        BtnIchimokuCloudBullishColor.Background = new SolidColorBrush(Color.Parse(settings.IchimokuCloudBullishColor));
        BtnIchimokuCloudBearishColor.Background = new SolidColorBrush(Color.Parse(settings.IchimokuCloudBearishColor));
    }
    
    /// <summary>
    /// 加载MA类型指标的8条线配置
    /// </summary>
    private void LoadMATypeSettings(MATypeIndicatorConfig config, string prefix)
    {
        // 根据指标类型获取默认颜色
        var defaultColors = GetDefaultColorsForIndicator(prefix);
        
        for (int i = 0; i < 8; i++)
        {
            int lineNum = i + 1;
            
            // 获取控件
            var chk = this.FindControl<CheckBox>($"Chk{prefix}{lineNum}");
            var txtPeriod = this.FindControl<TextBox>($"Txt{prefix}{lineNum}PERIOD");
            var cmbField = this.FindControl<ComboBox>($"Cmb{prefix}{lineNum}Field");
            var cmbStyle = this.FindControl<ComboBox>($"Cmb{prefix}{lineNum}Style");
            var btnColor = this.FindControl<Button>($"Btn{prefix}{lineNum}Color");
            
            // 如果config有足够的Lines，使用config中的值；否则使用默认值
            if (i < config.Lines.Count)
            {
                var line = config.Lines[i];
                if (chk != null) chk.IsChecked = line.IsEnabled;
                if (txtPeriod != null) txtPeriod.Text = line.PERIOD.ToString();
                if (cmbField != null) cmbField.SelectedIndex = (int)line.Field;
                if (cmbStyle != null) cmbStyle.SelectedIndex = (int)line.Style;
                if (btnColor != null) btnColor.Background = new SolidColorBrush(Color.Parse(line.Color));
            }
            else
            {
                // 使用默认值
                if (chk != null) chk.IsChecked = false;
                if (txtPeriod != null) txtPeriod.Text = (i == 0 ? "7" : i == 1 ? "25" : "99");
                if (cmbField != null) cmbField.SelectedIndex = 3; // Close
                if (cmbStyle != null) cmbStyle.SelectedIndex = 0; // Solid
                if (btnColor != null) btnColor.Background = new SolidColorBrush(Color.Parse(defaultColors[i]));
            }
        }
    }
    
    /// <summary>
    /// 获取指标的默认颜色列表
    /// </summary>
    private List<string> GetDefaultColorsForIndicator(string prefix)
    {
        switch (prefix)
        {
            case "DEMA":
                return new List<string> { "#FFD700", "#FF1493", "#00BFFF", "#00FFFF", "#FFA500", "#00FF00", "#FF1493", "#1E90FF" };
            case "TEMA":
                return new List<string> { "#FF0000", "#FFD700", "#FF00FF", "#00FFFF", "#FFA500", "#00FF00", "#FF1493", "#1E90FF" };
            default: // MA, EMA, WMA
                return new List<string> { "#FFFFFF", "#FFD700", "#FF00FF", "#00FFFF", "#FFA500", "#00FF00", "#FF1493", "#1E90FF" };
        }
    }
    
    /// <summary>
    /// 获取当前所有设置
    /// </summary>
    public IndicatorSettings GetSettings()
    {
        var settings = new IndicatorSettings();
        
        // 获取MA设置
        settings.MA.IsEnabled = ChkMA.IsChecked == true;
        SaveMATypeSettings(settings.MA, "MA");
        
        // 获取EMA设置
        settings.EMA.IsEnabled = ChkEMA.IsChecked == true;
        SaveMATypeSettings(settings.EMA, "EMA");
        
        // 获取WMA设置
        settings.WMA.IsEnabled = ChkWMA.IsChecked == true;
        SaveMATypeSettings(settings.WMA, "WMA");
        
        // 获取DEMA设置
        settings.DEMA.IsEnabled = ChkDEMA.IsChecked == true;
        SaveMATypeSettings(settings.DEMA, "DEMA");
        
        // 获取TEMA设置
        settings.TEMA.IsEnabled = ChkTEMA.IsChecked == true;
        SaveMATypeSettings(settings.TEMA, "TEMA");
        
        // 获取BOLL设置
        settings.IsBOLLEnabled = ChkBOLL.IsChecked == true;
        
        if (int.TryParse(TxtBOLLPeriod.Text, out var bollPeriod))
        {
            settings.BOLLPeriod = Math.Max(2, Math.Min(100, bollPeriod));
        }
        
        if (double.TryParse(TxtBOLLStdDev.Text, out var stdDev))
        {
            settings.BOLLStdDev = Math.Max(0.5, Math.Min(5.0, stdDev));
        }
        
        // 获取VWAP设置
        settings.IsVWAPEnabled = ChkVWAP.IsChecked == true;
        
        if (int.TryParse(TxtVWAPPeriod.Text, out var vwapPeriod))
        {
            settings.VWAPPeriod = Math.Max(1, Math.Min(500, vwapPeriod));
        }
        
        if (BtnVWAPColor.Background is SolidColorBrush vwapBrush)
        {
            settings.VWAPColor = vwapBrush.Color.ToString();
        }
        
        // AVL和TRIX已移至副图系统，不再在主图配置
        
        // 获取SAR设置
        settings.IsSAREnabled = ChkSAR.IsChecked == true;
        
        if (double.TryParse(TxtSARAcceleration.Text, out var sarAccel))
        {
            settings.SARAcceleration = Math.Max(0.001, Math.Min(0.5, sarAccel));
        }
        
        if (double.TryParse(TxtSARMaxAcceleration.Text, out var sarMaxAccel))
        {
            settings.SARMaxAcceleration = Math.Max(0.01, Math.Min(1.0, sarMaxAccel));
        }
        
        if (BtnSARUpColor.Background is SolidColorBrush sarUpBrush)
        {
            settings.SARUpColor = sarUpBrush.Color.ToString();
        }
        
        if (BtnSARDownColor.Background is SolidColorBrush sarDownBrush)
        {
            settings.SARDownColor = sarDownBrush.Color.ToString();
        }
        
        // 获取Keltner设置
        settings.IsKeltnerEnabled = ChkKeltner.IsChecked == true;
        
        if (int.TryParse(TxtKeltnerPeriod.Text, out var keltnerPeriod))
        {
            settings.KeltnerPeriod = Math.Max(2, Math.Min(200, keltnerPeriod));
        }
        
        if (double.TryParse(TxtKeltnerMultiplier.Text, out var keltnerMultiplier))
        {
            settings.KeltnerMultiplier = Math.Max(0.5, Math.Min(10.0, keltnerMultiplier));
        }
        
        if (BtnKeltnerUpperColor.Background is SolidColorBrush keltnerUpperBrush)
        {
            settings.KeltnerUpperColor = keltnerUpperBrush.Color.ToString();
        }
        
        if (BtnKeltnerMiddleColor.Background is SolidColorBrush keltnerMiddleBrush)
        {
            settings.KeltnerMiddleColor = keltnerMiddleBrush.Color.ToString();
        }
        
        if (BtnKeltnerLowerColor.Background is SolidColorBrush keltnerLowerBrush)
        {
            settings.KeltnerLowerColor = keltnerLowerBrush.Color.ToString();
        }
        
        // 获取Ichimoku设置
        settings.IsIchimokuEnabled = ChkIchimoku.IsChecked == true;
        
        if (int.TryParse(TxtIchimokuTenkan.Text, out var ichimokuTenkan))
        {
            settings.IchimokuTenkanPeriod = Math.Max(1, Math.Min(100, ichimokuTenkan));
        }
        
        if (int.TryParse(TxtIchimokuKijun.Text, out var ichimokuKijun))
        {
            settings.IchimokuKijunPeriod = Math.Max(1, Math.Min(100, ichimokuKijun));
        }
        
        if (int.TryParse(TxtIchimokuSenkouB.Text, out var ichimokuSenkouB))
        {
            settings.IchimokuSenkouBPeriod = Math.Max(1, Math.Min(200, ichimokuSenkouB));
        }
        
        if (BtnIchimokuTenkanColor.Background is SolidColorBrush tenkanBrush)
        {
            settings.IchimokuTenkanColor = tenkanBrush.Color.ToString();
        }
        
        if (BtnIchimokuKijunColor.Background is SolidColorBrush kijunBrush)
        {
            settings.IchimokuKijunColor = kijunBrush.Color.ToString();
        }
        
        if (BtnIchimokuCloudBullishColor.Background is SolidColorBrush bullishBrush)
        {
            settings.IchimokuCloudBullishColor = bullishBrush.Color.ToString();
        }
        
        if (BtnIchimokuCloudBearishColor.Background is SolidColorBrush bearishBrush)
        {
            settings.IchimokuCloudBearishColor = bearishBrush.Color.ToString();
        }
        
        return settings;
    }
    
    /// <summary>
    /// 保存MA类型指标的8条线配置
    /// </summary>
    private void SaveMATypeSettings(MATypeIndicatorConfig config, string prefix)
    {
        // 获取指标的默认颜色
        var defaultColors = GetDefaultColorsForIndicator(prefix);
        
        config.Lines.Clear();
        
        for (int i = 0; i < 8; i++)
        {
            int lineNum = i + 1;
            
            // 获取控件
            var chk = this.FindControl<CheckBox>($"Chk{prefix}{lineNum}");
            var txtPeriod = this.FindControl<TextBox>($"Txt{prefix}{lineNum}PERIOD");
            var cmbField = this.FindControl<ComboBox>($"Cmb{prefix}{lineNum}Field");
            var cmbStyle = this.FindControl<ComboBox>($"Cmb{prefix}{lineNum}Style");
            
            // 创建线配置
            var lineConfig = new MALineConfig
            {
                IsEnabled = chk?.IsChecked == true,
                Color = defaultColors[i]
            };
            
            // 解析周期
            if (txtPeriod != null && int.TryParse(txtPeriod.Text, out var PERIOD))
            {
                lineConfig.PERIOD = Math.Max(0, Math.Min(500, PERIOD));
            }
            
            // 获取计算字段
            if (cmbField != null)
            {
                lineConfig.Field = (PriceField)(cmbField.SelectedIndex >= 0 ? cmbField.SelectedIndex : 3);
            }
            
            // 获取线形样式
            if (cmbStyle != null)
            {
                lineConfig.Style = (LineStyle)(cmbStyle.SelectedIndex >= 0 ? cmbStyle.SelectedIndex : 0);
            }
            
            config.Lines.Add(lineConfig);
        }
    }
    
    /// <summary>
    /// 指标选择事件
    /// </summary>
    private void OnIndicatorSelected(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border)
        {
            // 只高亮选中该指标项，不影响CheckBox的勾选状态
            // CheckBox点击时会自己处理勾选逻辑
            SelectIndicator(border);
        }
    }
    
    /// <summary>
    /// 选中指标
    /// </summary>
    private void SelectIndicator(Border border)
    {
        // 取消之前选中项的高亮
        if (_selectedIndicatorBorder != null)
        {
            _selectedIndicatorBorder.Background = Brushes.Transparent;
        }
        
        // 高亮当前选中项
        border.Background = new SolidColorBrush(Color.Parse("#4B4F54"));
        _selectedIndicatorBorder = border;
        
        // 显示对应的配置面板
        NoSelectionText.IsVisible = false;
        MAConfigPanel.IsVisible = false;
        EMAConfigPanel.IsVisible = false;
        WMAConfigPanel.IsVisible = false;
        BOLLConfigPanel.IsVisible = false;
        VWAPConfigPanel.IsVisible = false;
        AVLConfigPanel.IsVisible = false;
        TRIXConfigPanel.IsVisible = false;
        SARConfigPanel.IsVisible = false;
        DEMAConfigPanel.IsVisible = false;
        TEMAConfigPanel.IsVisible = false;
        KeltnerConfigPanel.IsVisible = false;
        IchimokuConfigPanel.IsVisible = false;
        
        switch (border.Name)
        {
            case "BtnMA":
                MAConfigPanel.IsVisible = true;
                break;
            case "BtnEMA":
                EMAConfigPanel.IsVisible = true;
                break;
            case "BtnWMA":
                WMAConfigPanel.IsVisible = true;
                break;
            case "BtnBOLL":
                BOLLConfigPanel.IsVisible = true;
                break;
            case "BtnVWAP":
                VWAPConfigPanel.IsVisible = true;
                break;
            case "BtnAVL":
                AVLConfigPanel.IsVisible = true;
                break;
            case "BtnTRIX":
                TRIXConfigPanel.IsVisible = true;
                break;
            case "BtnSAR":
                SARConfigPanel.IsVisible = true;
                break;
            case "BtnDEMA":
                DEMAConfigPanel.IsVisible = true;
                break;
            case "BtnTEMA":
                TEMAConfigPanel.IsVisible = true;
                break;
            case "BtnKeltner":
                KeltnerConfigPanel.IsVisible = true;
                break;
            case "BtnIchimoku":
                IchimokuConfigPanel.IsVisible = true;
                break;
        }
    }
    
    /// <summary>
    /// MA主CheckBox状态变化
    /// </summary>
    private void OnMACheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// EMA主CheckBox状态变化
    /// </summary>
    private void OnEMACheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// WMA主CheckBox状态变化
    /// </summary>
    private void OnWMACheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// BOLL主CheckBox状态变化
    /// </summary>
    private void OnBOLLCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// VWAP主CheckBox状态变化
    /// </summary>
    private void OnVWAPCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// AVL主CheckBox状态变化
    /// </summary>
    private void OnAVLCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// TRIX主CheckBox状态变化
    /// </summary>
    private void OnTRIXCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// SAR主CheckBox状态变化
    /// </summary>
    private void OnSARCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// DEMA主CheckBox状态变化
    /// </summary>
    private void OnDEMACheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// TEMA主CheckBox状态变化
    /// </summary>
    private void OnTEMACheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// Keltner主CheckBox状态变化
    /// </summary>
    private void OnKeltnerCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// Ichimoku主CheckBox状态变化
    /// </summary>
    private void OnIchimokuCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // 可以在这里添加联动逻辑
    }
    
    /// <summary>
    /// 确定按钮点击
    /// </summary>
    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Settings = GetSettings();
        IsConfirmed = true;
        CloseRequested?.Invoke(this, true);
    }
    
    /// <summary>
    /// 取消按钮点击
    /// </summary>
    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        CloseRequested?.Invoke(this, false);
    }
    
    /// <summary>
    /// 重置按钮点击
    /// </summary>
    private void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // 重置为默认设置
        var defaultSettings = new IndicatorSettings();
        LoadSettings(defaultSettings);
    }
    
    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        CloseRequested?.Invoke(this, false);
    }
    
    /// <summary>
    /// 颜色按钮点击 - 动态创建颜色选择器Flyout
    /// </summary>
    private void OnColorButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button targetButton) return;
        
        // 创建Flyout内容
        var flyoutPanel = new StackPanel 
        { 
            Width = 250, 
            Spacing = 12,
            Margin = new Avalonia.Thickness(15)
        };
        
        // 标题
        flyoutPanel.Children.Add(new TextBlock
        {
            Text = "选择颜色",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E6E6E6"))
        });
        
        // 颜色网格
        var colorGrid = new UniformGrid 
        { 
            Columns = 8, 
            Rows = 3 
        };
        
        foreach (var colorHex in PresetColors)
        {
            var colorBtn = new Button
            {
                Width = 26,
                Height = 26,
                Padding = new Avalonia.Thickness(0),
                Background = new SolidColorBrush(Color.Parse(colorHex)),
                BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(3),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = colorHex,
                Margin = new Avalonia.Thickness(2)
            };
            
            // 绑定点击事件
            colorBtn.Click += (s, args) =>
            {
                if (s is Button btn && btn.Tag is string hex)
                {
                    targetButton.Background = new SolidColorBrush(Color.Parse(hex));
                    targetButton.Flyout?.Hide();
                }
            };
            
            colorGrid.Children.Add(colorBtn);
        }
        
        flyoutPanel.Children.Add(colorGrid);
        
        // 十六进制输入部分
        var hexPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };
        
        hexPanel.Children.Add(new TextBlock
        {
            Text = "HEX:",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#999999")),
            VerticalAlignment = VerticalAlignment.Center
        });
        
        var hexTextBox = new TextBox
        {
            Width = 100,
            Height = 28,
            FontSize = 11,
            Text = (targetButton.Background as SolidColorBrush)?.Color.ToString() ?? "#FFFFFF",
            Padding = new Avalonia.Thickness(8, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        
        var confirmBtn = new Button
        {
            Content = "确定",
            Width = 60,
            Height = 28,
            FontSize = 11,
            Background = new SolidColorBrush(Color.Parse("#F0B90B")),
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        
        confirmBtn.Click += (s, args) =>
        {
            try
            {
                var color = Color.Parse(hexTextBox.Text ?? "#FFFFFF");
                targetButton.Background = new SolidColorBrush(color);
                targetButton.Flyout?.Hide();
            }
            catch
            {
                // 颜色格式无效，忽略
            }
        };
        
        hexPanel.Children.Add(hexTextBox);
        hexPanel.Children.Add(confirmBtn);
        
        flyoutPanel.Children.Add(hexPanel);
        
        // 创建Flyout
        var flyout = new Flyout
        {
            Content = flyoutPanel,
            Placement = PlacementMode.Bottom,
            ShowMode = FlyoutShowMode.Standard
        };
        
        // 设置并显示Flyout
        targetButton.Flyout = flyout;
        flyout.ShowAt(targetButton);
    }
}
