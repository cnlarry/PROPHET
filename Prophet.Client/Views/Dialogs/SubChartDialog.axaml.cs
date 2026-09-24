using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Prophet.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Views.Dialogs;

public partial class SubChartDialog : UserControl
{
    public event EventHandler<SubChartSettings>? SettingsApplied;
    public event EventHandler? CloseRequested;
    
    private SubChartSettings _settings = new();
    private Border? _currentSelectedBorder;
    
    public SubChartDialog()
    {
        InitializeComponent();
        
        // 默认选中Volume
        SelectIndicator(BtnVolume, VolumeConfigPanel);
    }
    
    /// <summary>
    /// 加载设置到UI
    /// </summary>
    public void LoadSettings(SubChartSettings settings)
    {
        _settings = settings ?? new SubChartSettings();
        
        // 加载开关状态
        ChkVolume.IsChecked = _settings.IsVolumeEnabled;
        ChkMACD.IsChecked = _settings.IsMACDEnabled;
        ChkRSI.IsChecked = _settings.IsRSIEnabled;
        ChkATR.IsChecked = _settings.IsATREnabled;
        ChkMFI.IsChecked = _settings.IsMFIEnabled;
        ChkOBV.IsChecked = _settings.IsOBVEnabled;
        ChkKDJ.IsChecked = _settings.IsKDJEnabled;
        ChkStochRSI.IsChecked = _settings.IsStochRSIEnabled;
        ChkCCI.IsChecked = _settings.IsCCIEnabled;
        ChkDMI.IsChecked = _settings.IsDMIEnabled;
        ChkWR.IsChecked = _settings.IsWREnabled;
        ChkCMF.IsChecked = _settings.IsCMFEnabled;
        ChkROC.IsChecked = _settings.IsROCEnabled;
        ChkEMV.IsChecked = _settings.IsEMVEnabled;
        ChkMTM.IsChecked = _settings.IsMTMEnabled;
        ChkCMO.IsChecked = _settings.IsCMOEnabled;
        ChkAroon.IsChecked = _settings.IsAroonEnabled;
        
        // 加载参数
        NumMACDFast.Value = _settings.MACDFastPeriod;
        NumMACDSlow.Value = _settings.MACDSlowPeriod;
        NumMACDSignal.Value = _settings.MACDSignalPeriod;
        // RSIPeriod 已改为多周期配置，在 RSIMultiPeriod 中加载
        NumATRPeriod.Value = _settings.ATRPeriod;
        
        // Volume MAVOL配置
        ChkShowVolumeMA.IsChecked = _settings.VolumeShowMA;
        if (_settings.VolumeMALines != null && _settings.VolumeMALines.Count >= 3)
        {
            ChkVolumeMA1.IsChecked = _settings.VolumeMALines[0].IsEnabled;
            NumVolumeMA1Period.Value = _settings.VolumeMALines[0].PERIOD;
            ColorVolumeMA1.Background = new SolidColorBrush(Color.Parse(_settings.VolumeMALines[0].Color));
            
            ChkVolumeMA2.IsChecked = _settings.VolumeMALines[1].IsEnabled;
            NumVolumeMA2Period.Value = _settings.VolumeMALines[1].PERIOD;
            ColorVolumeMA2.Background = new SolidColorBrush(Color.Parse(_settings.VolumeMALines[1].Color));
            
            ChkVolumeMA3.IsChecked = _settings.VolumeMALines[2].IsEnabled;
            NumVolumeMA3Period.Value = _settings.VolumeMALines[2].PERIOD;
            ColorVolumeMA3.Background = new SolidColorBrush(Color.Parse(_settings.VolumeMALines[2].Color));
        }
        
        // 新增指标参数
        // MFIPeriod 已改为多周期配置，在 MFIMultiPeriod 中加载
        NumKDJPeriod.Value = _settings.KDJPeriod;
        NumKDJKPeriod.Value = _settings.KDJKPeriod;
        NumKDJDPeriod.Value = _settings.KDJDPeriod;
        NumStochRSIPeriod.Value = _settings.StochRSIPeriod;
        NumStochRSIKPeriod.Value = _settings.StochRSIKPeriod;
        NumStochRSIDPeriod.Value = _settings.StochRSIDPeriod;
        // CCIPeriod和WRPeriod已改为多周期配置，在 CCIMultiPeriod/WRMultiPeriod 中加载
        NumDMIPeriod.Value = _settings.DMIPeriod;
        NumCMFPeriod.Value = _settings.CMFPeriod;
        NumROCPeriod.Value = _settings.ROCPeriod;
        NumEMVPeriod.Value = _settings.EMVPeriod;
        NumMTMPeriod.Value = _settings.MTMPeriod;
        NumCMOPeriod.Value = _settings.CMOPeriod;
        NumAroonPeriod.Value = _settings.AroonPeriod;
        
        // RSI、MFI、CCI、WR多周期配置加载
        LoadMultiPeriodConfig("RSI", _settings.RSIMultiPeriod, ChkRSIPeriod1, NumRSIPeriod1, ColorRSIPeriod1, ChkRSIPeriod2, NumRSIPeriod2, ColorRSIPeriod2, ChkRSIPeriod3, NumRSIPeriod3, ColorRSIPeriod3);
        LoadMultiPeriodConfig("MFI", _settings.MFIMultiPeriod, ChkMFIPeriod1, NumMFIPeriod1, ColorMFIPeriod1, ChkMFIPeriod2, NumMFIPeriod2, ColorMFIPeriod2, ChkMFIPeriod3, NumMFIPeriod3, ColorMFIPeriod3);
        LoadMultiPeriodConfig("CCI", _settings.CCIMultiPeriod, ChkCCIPeriod1, NumCCIPeriod1, ColorCCIPeriod1, ChkCCIPeriod2, NumCCIPeriod2, ColorCCIPeriod2, ChkCCIPeriod3, NumCCIPeriod3, ColorCCIPeriod3);
        LoadMultiPeriodConfig("WR", _settings.WRMultiPeriod, ChkWRPeriod1, NumWRPeriod1, ColorWRPeriod1, ChkWRPeriod2, NumWRPeriod2, ColorWRPeriod2, ChkWRPeriod3, NumWRPeriod3, ColorWRPeriod3);
        
        // 其他单线指标MA配置加载
        LoadMAConfig("ATR", _settings.ATRMA, ChkShowATRMA, ChkATRMA1, NumATRMA1Period, ColorATRMA1, ChkATRMA2, NumATRMA2Period, ColorATRMA2, ChkATRMA3, NumATRMA3Period, ColorATRMA3);
        LoadMAConfig("OBV", _settings.OBVMA, ChkShowOBVMA, ChkOBVMA1, NumOBVMA1Period, ColorOBVMA1, ChkOBVMA2, NumOBVMA2Period, ColorOBVMA2, ChkOBVMA3, NumOBVMA3Period, ColorOBVMA3);
        LoadMAConfig("CMF", _settings.CMFMA, ChkShowCMFMA, ChkCMFMA1, NumCMFMA1Period, ColorCMFMA1, ChkCMFMA2, NumCMFMA2Period, ColorCMFMA2, ChkCMFMA3, NumCMFMA3Period, ColorCMFMA3);
        LoadMAConfig("ROC", _settings.ROCMA, ChkShowROCMA, ChkROCMA1, NumROCMA1Period, ColorROCMA1, ChkROCMA2, NumROCMA2Period, ColorROCMA2, ChkROCMA3, NumROCMA3Period, ColorROCMA3);
        LoadMAConfig("EMV", _settings.EMVMA, ChkShowEMVMA, ChkEMVMA1, NumEMVMA1Period, ColorEMVMA1, ChkEMVMA2, NumEMVMA2Period, ColorEMVMA2, ChkEMVMA3, NumEMVMA3Period, ColorEMVMA3);
        LoadMAConfig("MTM", _settings.MTMMA, ChkShowMTMMA, ChkMTMMA1, NumMTMMA1Period, ColorMTMMA1, ChkMTMMA2, NumMTMMA2Period, ColorMTMMA2, ChkMTMMA3, NumMTMMA3Period, ColorMTMMA3);
        LoadMAConfig("CMO", _settings.CMOMA, ChkShowCMOMA, ChkCMOMA1, NumCMOMA1Period, ColorCMOMA1, ChkCMOMA2, NumCMOMA2Period, ColorCMOMA2, ChkCMOMA3, NumCMOMA3Period, ColorCMOMA3);
    }
    
    /// <summary>
    /// 加载MA配置的辅助方法
    /// </summary>
    private void LoadMAConfig(string name, SubChartMAConfig config, 
        CheckBox chkShow,
        CheckBox chk1, NumericUpDown num1, Border color1,
        CheckBox chk2, NumericUpDown num2, Border color2,
        CheckBox chk3, NumericUpDown num3, Border color3)
    {
        if (config == null) return;
        
        chkShow.IsChecked = config.ShowMA;
        if (config.MALines != null && config.MALines.Count >= 3)
        {
            chk1.IsChecked = config.MALines[0].IsEnabled;
            num1.Value = config.MALines[0].PERIOD;
            color1.Background = new SolidColorBrush(Color.Parse(config.MALines[0].Color));
            // Style和Thickness已保存在配置中，绘制时会自动使用
            
            chk2.IsChecked = config.MALines[1].IsEnabled;
            num2.Value = config.MALines[1].PERIOD;
            color2.Background = new SolidColorBrush(Color.Parse(config.MALines[1].Color));
            
            chk3.IsChecked = config.MALines[2].IsEnabled;
            num3.Value = config.MALines[2].PERIOD;
            color3.Background = new SolidColorBrush(Color.Parse(config.MALines[2].Color));
        }
    }
    
    /// <summary>
    /// 加载多周期配置的辅助方法（无ShowMA复选框）
    /// </summary>
    private void LoadMultiPeriodConfig(string name, SubChartMAConfig config, 
        CheckBox chk1, NumericUpDown num1, Border color1,
        CheckBox chk2, NumericUpDown num2, Border color2,
        CheckBox chk3, NumericUpDown num3, Border color3)
    {
        if (config == null) return;
        
        if (config.MALines != null && config.MALines.Count >= 3)
        {
            chk1.IsChecked = config.MALines[0].IsEnabled;
            num1.Value = config.MALines[0].PERIOD;
            color1.Background = new SolidColorBrush(Color.Parse(config.MALines[0].Color));
            
            chk2.IsChecked = config.MALines[1].IsEnabled;
            num2.Value = config.MALines[1].PERIOD;
            color2.Background = new SolidColorBrush(Color.Parse(config.MALines[1].Color));
            
            chk3.IsChecked = config.MALines[2].IsEnabled;
            num3.Value = config.MALines[2].PERIOD;
            color3.Background = new SolidColorBrush(Color.Parse(config.MALines[2].Color));
        }
    }
    
    /// <summary>
    /// 保存MA配置的辅助方法
    /// </summary>
    private SubChartMAConfig SaveMAConfig(CheckBox chkShow, 
        CheckBox chk1, NumericUpDown num1, Border color1,
        CheckBox chk2, NumericUpDown num2, Border color2,
        CheckBox chk3, NumericUpDown num3, Border color3)
    {
        return new SubChartMAConfig
        {
            ShowMA = chkShow.IsChecked ?? false,
            MALines = new List<SubChartMALineConfig>
            {
                new SubChartMALineConfig
                {
                    IsEnabled = chk1.IsChecked ?? true,
                    PERIOD = (int)(num1.Value ?? 5),
                    Color = (color1.Background as SolidColorBrush)?.Color.ToString() ?? "#FFD700",
                    Style = LineStyle.Solid
                },
                new SubChartMALineConfig
                {
                    IsEnabled = chk2.IsChecked ?? true,
                    PERIOD = (int)(num2.Value ?? 10),
                    Color = (color2.Background as SolidColorBrush)?.Color.ToString() ?? "#00FFFF",
                    Style = LineStyle.Solid
                },
                new SubChartMALineConfig
                {
                    IsEnabled = chk3.IsChecked ?? false,
                    PERIOD = (int)(num3.Value ?? 20),
                    Color = (color3.Background as SolidColorBrush)?.Color.ToString() ?? "#FF00FF",
                    Style = LineStyle.Solid
                }
            }
        };
    }
    
    /// <summary>
    /// 保存多周期配置的辅助方法（无ShowMA复选框，只要有任何周期启用就表示启用多周期）
    /// </summary>
    private SubChartMAConfig SaveMultiPeriodConfig(
        CheckBox chk1, NumericUpDown num1, Border color1,
        CheckBox chk2, NumericUpDown num2, Border color2,
        CheckBox chk3, NumericUpDown num3, Border color3)
    {
        var lines = new List<SubChartMALineConfig>
        {
            new SubChartMALineConfig
            {
                IsEnabled = chk1.IsChecked ?? true,
                PERIOD = (int)(num1.Value ?? 7),
                Color = (color1.Background as SolidColorBrush)?.Color.ToString() ?? "#FFD700",
                Style = LineStyle.Solid,
                Thickness = 1.5
            },
            new SubChartMALineConfig
            {
                IsEnabled = chk2.IsChecked ?? true,
                PERIOD = (int)(num2.Value ?? 14),
                Color = (color2.Background as SolidColorBrush)?.Color.ToString() ?? "#00FFFF",
                Style = LineStyle.Solid,
                Thickness = 1.5
            },
            new SubChartMALineConfig
            {
                IsEnabled = chk3.IsChecked ?? true,
                PERIOD = (int)(num3.Value ?? 21),
                Color = (color3.Background as SolidColorBrush)?.Color.ToString() ?? "#FF00FF",
                Style = LineStyle.Solid,
                Thickness = 1.5
            }
        };
        
        // ShowMA自动设置为true，只要有任何一个周期被启用
        bool hasAnyEnabled = lines.Any(line => line.IsEnabled);
        
        return new SubChartMAConfig
        {
            ShowMA = hasAnyEnabled,
            MALines = lines
        };
    }
    
    /// <summary>
    /// 从UI保存设置
    /// </summary>
    private SubChartSettings SaveSettings()
    {
        var settings = new SubChartSettings
        {
            IsVolumeEnabled = ChkVolume.IsChecked ?? true,
            IsMACDEnabled = ChkMACD.IsChecked ?? true,
            IsRSIEnabled = ChkRSI.IsChecked ?? true,
            IsATREnabled = ChkATR.IsChecked ?? true,
            IsMFIEnabled = ChkMFI.IsChecked ?? false,
            IsOBVEnabled = ChkOBV.IsChecked ?? false,
            IsKDJEnabled = ChkKDJ.IsChecked ?? false,
            IsStochRSIEnabled = ChkStochRSI.IsChecked ?? false,
            IsCCIEnabled = ChkCCI.IsChecked ?? false,
            IsDMIEnabled = ChkDMI.IsChecked ?? false,
            IsWREnabled = ChkWR.IsChecked ?? false,
            IsCMFEnabled = ChkCMF.IsChecked ?? false,
            IsROCEnabled = ChkROC.IsChecked ?? false,
            IsEMVEnabled = ChkEMV.IsChecked ?? false,
            IsMTMEnabled = ChkMTM.IsChecked ?? false,
            IsCMOEnabled = ChkCMO.IsChecked ?? false,
            IsAroonEnabled = ChkAroon.IsChecked ?? false,
            
            MACDFastPeriod = (int)(NumMACDFast.Value ?? 12),
            MACDSlowPeriod = (int)(NumMACDSlow.Value ?? 26),
            MACDSignalPeriod = (int)(NumMACDSignal.Value ?? 9),
            // RSIPeriod 使用第一个多周期配置的周期
            RSIPeriod = (int)(NumRSIPeriod1.Value ?? 14),
            ATRPeriod = (int)(NumATRPeriod.Value ?? 14),
            
            // Volume MAVOL配置
            VolumeShowMA = ChkShowVolumeMA.IsChecked ?? true,
            VolumeMALines = new List<VolumeMALineConfig>
            {
                new VolumeMALineConfig
                {
                    IsEnabled = ChkVolumeMA1.IsChecked ?? true,
                    PERIOD = (int)(NumVolumeMA1Period.Value ?? 5),
                    Color = (ColorVolumeMA1.Background as SolidColorBrush)?.Color.ToString() ?? "#FFD700",
                    Style = LineStyle.Solid
                },
                new VolumeMALineConfig
                {
                    IsEnabled = ChkVolumeMA2.IsChecked ?? true,
                    PERIOD = (int)(NumVolumeMA2Period.Value ?? 10),
                    Color = (ColorVolumeMA2.Background as SolidColorBrush)?.Color.ToString() ?? "#00FFFF",
                    Style = LineStyle.Solid
                },
                new VolumeMALineConfig
                {
                    IsEnabled = ChkVolumeMA3.IsChecked ?? false,
                    PERIOD = (int)(NumVolumeMA3Period.Value ?? 20),
                    Color = (ColorVolumeMA3.Background as SolidColorBrush)?.Color.ToString() ?? "#FF00FF",
                    Style = LineStyle.Solid
                }
            },
            
            // 新增指标参数
            // MFIPeriod 使用第一个多周期配置的周期
            MFIPeriod = (int)(NumMFIPeriod1.Value ?? 14),
            KDJPeriod = (int)(NumKDJPeriod.Value ?? 9),
            KDJKPeriod = (int)(NumKDJKPeriod.Value ?? 3),
            KDJDPeriod = (int)(NumKDJDPeriod.Value ?? 3),
            StochRSIPeriod = (int)(NumStochRSIPeriod.Value ?? 14),
            StochRSIKPeriod = (int)(NumStochRSIKPeriod.Value ?? 3),
            StochRSIDPeriod = (int)(NumStochRSIDPeriod.Value ?? 3),
            // CCIPeriod和WRPeriod使用第一个多周期配置的周期
            CCIPeriod = (int)(NumCCIPeriod1.Value ?? 20),
            DMIPeriod = (int)(NumDMIPeriod.Value ?? 14),
            WRPeriod = (int)(NumWRPeriod1.Value ?? 14),
            CMFPeriod = (int)(NumCMFPeriod.Value ?? 20),
            ROCPeriod = (int)(NumROCPeriod.Value ?? 12),
            EMVPeriod = (int)(NumEMVPeriod.Value ?? 14),
            MTMPeriod = (int)(NumMTMPeriod.Value ?? 12),
            CMOPeriod = (int)(NumCMOPeriod.Value ?? 14),
            AroonPeriod = (int)(NumAroonPeriod.Value ?? 25),
            
            // RSI、MFI、CCI、WR多周期配置保存
            RSIMultiPeriod = SaveMultiPeriodConfig(ChkRSIPeriod1, NumRSIPeriod1, ColorRSIPeriod1, ChkRSIPeriod2, NumRSIPeriod2, ColorRSIPeriod2, ChkRSIPeriod3, NumRSIPeriod3, ColorRSIPeriod3),
            MFIMultiPeriod = SaveMultiPeriodConfig(ChkMFIPeriod1, NumMFIPeriod1, ColorMFIPeriod1, ChkMFIPeriod2, NumMFIPeriod2, ColorMFIPeriod2, ChkMFIPeriod3, NumMFIPeriod3, ColorMFIPeriod3),
            CCIMultiPeriod = SaveMultiPeriodConfig(ChkCCIPeriod1, NumCCIPeriod1, ColorCCIPeriod1, ChkCCIPeriod2, NumCCIPeriod2, ColorCCIPeriod2, ChkCCIPeriod3, NumCCIPeriod3, ColorCCIPeriod3),
            WRMultiPeriod = SaveMultiPeriodConfig(ChkWRPeriod1, NumWRPeriod1, ColorWRPeriod1, ChkWRPeriod2, NumWRPeriod2, ColorWRPeriod2, ChkWRPeriod3, NumWRPeriod3, ColorWRPeriod3),
            
            // 其他单线指标MA配置保存
            ATRMA = SaveMAConfig(ChkShowATRMA, ChkATRMA1, NumATRMA1Period, ColorATRMA1, ChkATRMA2, NumATRMA2Period, ColorATRMA2, ChkATRMA3, NumATRMA3Period, ColorATRMA3),
            OBVMA = SaveMAConfig(ChkShowOBVMA, ChkOBVMA1, NumOBVMA1Period, ColorOBVMA1, ChkOBVMA2, NumOBVMA2Period, ColorOBVMA2, ChkOBVMA3, NumOBVMA3Period, ColorOBVMA3),
            CMFMA = SaveMAConfig(ChkShowCMFMA, ChkCMFMA1, NumCMFMA1Period, ColorCMFMA1, ChkCMFMA2, NumCMFMA2Period, ColorCMFMA2, ChkCMFMA3, NumCMFMA3Period, ColorCMFMA3),
            ROCMA = SaveMAConfig(ChkShowROCMA, ChkROCMA1, NumROCMA1Period, ColorROCMA1, ChkROCMA2, NumROCMA2Period, ColorROCMA2, ChkROCMA3, NumROCMA3Period, ColorROCMA3),
            EMVMA = SaveMAConfig(ChkShowEMVMA, ChkEMVMA1, NumEMVMA1Period, ColorEMVMA1, ChkEMVMA2, NumEMVMA2Period, ColorEMVMA2, ChkEMVMA3, NumEMVMA3Period, ColorEMVMA3),
            MTMMA = SaveMAConfig(ChkShowMTMMA, ChkMTMMA1, NumMTMMA1Period, ColorMTMMA1, ChkMTMMA2, NumMTMMA2Period, ColorMTMMA2, ChkMTMMA3, NumMTMMA3Period, ColorMTMMA3),
            CMOMA = SaveMAConfig(ChkShowCMOMA, ChkCMOMA1, NumCMOMA1Period, ColorCMOMA1, ChkCMOMA2, NumCMOMA2Period, ColorCMOMA2, ChkCMOMA3, NumCMOMA3Period, ColorCMOMA3)
        };
        
        return settings;
    }
    
    /// <summary>
    /// 左侧指标选择事件
    /// </summary>
    private void OnIndicatorSelected(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        
        // 确定显示哪个配置面板
        StackPanel? configPanel = border.Name switch
        {
            "BtnVolume" => VolumeConfigPanel,
            "BtnMACD" => MACDConfigPanel,
            "BtnRSI" => RSIConfigPanel,
            "BtnATR" => ATRConfigPanel,
            "BtnMFI" => MFIConfigPanel,
            "BtnOBV" => OBVConfigPanel,
            "BtnKDJ" => KDJConfigPanel,
            "BtnStochRSI" => StochRSIConfigPanel,
            "BtnCCI" => CCIConfigPanel,
            "BtnDMI" => DMIConfigPanel,
            "BtnWR" => WRConfigPanel,
            "BtnCMF" => CMFConfigPanel,
            "BtnROC" => ROCConfigPanel,
            "BtnEMV" => EMVConfigPanel,
            "BtnMTM" => MTMConfigPanel,
            "BtnCMO" => CMOConfigPanel,
            "BtnAroon" => AroonConfigPanel,
            _ => null
        };
        
        if (configPanel != null)
        {
            SelectIndicator(border, configPanel);
        }
    }
    
    /// <summary>
    /// 选中指标并显示配置面板
    /// </summary>
    private void SelectIndicator(Border border, StackPanel configPanel)
    {
        // 取消之前选中项的背景色
        if (_currentSelectedBorder != null)
        {
            _currentSelectedBorder.Background = Brushes.Transparent;
        }
        
        // 设置当前选中项的背景色
        border.Background = new SolidColorBrush(Color.Parse("#2C2E33"));
        _currentSelectedBorder = border;
        
        // 隐藏所有配置面板
        VolumeConfigPanel.IsVisible = false;
        MACDConfigPanel.IsVisible = false;
        RSIConfigPanel.IsVisible = false;
        ATRConfigPanel.IsVisible = false;
        MFIConfigPanel.IsVisible = false;
        OBVConfigPanel.IsVisible = false;
        KDJConfigPanel.IsVisible = false;
        StochRSIConfigPanel.IsVisible = false;
        CCIConfigPanel.IsVisible = false;
        DMIConfigPanel.IsVisible = false;
        WRConfigPanel.IsVisible = false;
        CMFConfigPanel.IsVisible = false;
        ROCConfigPanel.IsVisible = false;
        EMVConfigPanel.IsVisible = false;
        MTMConfigPanel.IsVisible = false;
        CMOConfigPanel.IsVisible = false;
        AroonConfigPanel.IsVisible = false;
        NoSelectionText.IsVisible = false;
        
        // 显示选中的配置面板
        configPanel.IsVisible = true;
    }
    
    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = SaveSettings();
        SettingsApplied?.Invoke(this, settings);
    }
    
    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = SaveSettings();
        SettingsApplied?.Invoke(this, settings);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    
    // ==================== 快速配置功能 ====================
    
    /// <summary>
    /// 快速配置：保守型 (5,10,20)
    /// </summary>
    private void OnQuickConfig_Conservative(object? sender, RoutedEventArgs e)
    {
        ApplyQuickConfig(5, 10, 20, LineStyle.Solid, 1.5);
    }
    
    /// <summary>
    /// 快速配置：激进型 (3,7,14)
    /// </summary>
    private void OnQuickConfig_Aggressive(object? sender, RoutedEventArgs e)
    {
        ApplyQuickConfig(3, 7, 14, LineStyle.Dashed, 1.2);
    }
    
    /// <summary>
    /// 快速配置：长线型 (10,20,30)
    /// </summary>
    private void OnQuickConfig_LongTerm(object? sender, RoutedEventArgs e)
    {
        ApplyQuickConfig(10, 20, 30, LineStyle.Solid, 1.8);
    }
    
    /// <summary>
    /// 快速配置：自定义
    /// </summary>
    private void OnQuickConfig_Custom(object? sender, RoutedEventArgs e)
    {
        // TODO: 打开自定义配置对话框
        // 暂时使用默认配置
        ApplyQuickConfig(6, 12, 24, LineStyle.Dotted, 1.3);
    }
    
    /// <summary>
    /// 应用快速配置到所有指标的MA线
    /// </summary>
    private void ApplyQuickConfig(int PERIOD1, int PERIOD2, int PERIOD3, LineStyle style, double thickness)
    {
        // 应用到Volume
        if (ChkShowVolumeMA.IsChecked == true)
        {
            NumVolumeMA1Period.Value = PERIOD1;
            NumVolumeMA2Period.Value = PERIOD2;
            NumVolumeMA3Period.Value = PERIOD3;
        }
        
        // 应用到所有单线指标（RSI、MFI、CCI、WR不在这里，因为它们使用多周期配置）
        var indicatorPeriodControls = new List<(NumericUpDown ma1, NumericUpDown ma2, NumericUpDown ma3, CheckBox showMA)>
        {
            (NumATRMA1Period, NumATRMA2Period, NumATRMA3Period, ChkShowATRMA),
            (NumOBVMA1Period, NumOBVMA2Period, NumOBVMA3Period, ChkShowOBVMA),
            (NumCMFMA1Period, NumCMFMA2Period, NumCMFMA3Period, ChkShowCMFMA),
            (NumROCMA1Period, NumROCMA2Period, NumROCMA3Period, ChkShowROCMA),
            (NumEMVMA1Period, NumEMVMA2Period, NumEMVMA3Period, ChkShowEMVMA),
            (NumMTMMA1Period, NumMTMMA2Period, NumMTMMA3Period, ChkShowMTMMA),
            (NumCMOMA1Period, NumCMOMA2Period, NumCMOMA3Period, ChkShowCMOMA)
        };
        
        foreach (var (ma1, ma2, ma3, showMA) in indicatorPeriodControls)
        {
            if (showMA.IsChecked == true)
            {
                ma1.Value = PERIOD1;
                ma2.Value = PERIOD2;
                ma3.Value = PERIOD3;
            }
        }
        
        // 应用到RSI、MFI、CCI、WR的多周期配置
        NumRSIPeriod1.Value = PERIOD1;
        NumRSIPeriod2.Value = PERIOD2;
        NumRSIPeriod3.Value = PERIOD3;
        NumMFIPeriod1.Value = PERIOD1;
        NumMFIPeriod2.Value = PERIOD2;
        NumMFIPeriod3.Value = PERIOD3;
        NumCCIPeriod1.Value = PERIOD1;
        NumCCIPeriod2.Value = PERIOD2;
        NumCCIPeriod3.Value = PERIOD3;
        NumWRPeriod1.Value = PERIOD1;
        NumWRPeriod2.Value = PERIOD2;
        NumWRPeriod3.Value = PERIOD3;
        
    }
}
