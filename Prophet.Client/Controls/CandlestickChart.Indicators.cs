using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Data;
using Prophet.Client.Indicators.Implementations;
using Prophet.Client.Indicators.Management;
using Prophet.Client.Indicators.Rendering;
// using Prophet.Client.Indicators.Utilities; // 已删除IndicatorDataConverter
using Prophet.Client.Indicators.Config;
using Prophet.Client.Models;
using IndicatorConfig = Prophet.Client.Indicators.Config;
using Implementations = Prophet.Client.Indicators.Implementations;

namespace Prophet.Client.Controls;

/// <summary>
/// CandlestickChart的统一指标系统（完全基于新框架）
/// 这个partial class完全接管所有指标的初始化、数据填充和渲染
/// </summary>
public partial class CandlestickChart
{
    // ========== 新架构：统一指标管理==========
    
    private IndicatorManager? _indicatorManager;
    
    // 副图区域信息（用于十字准星）
    private class SubChartArea
    {
        public string IndicatorId { get; set; } = "";
        public double TopY { get; set; }
        public double Height { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
    }
    
    private List<SubChartArea> _subChartAreas = new();
    
    // 副图指标摘要TextBlock字典（key: 指标ID）
    private Dictionary<string, TextBlock> _subChartTitleBlocks = new();
    
    // 🚀 性能优化：指标数据缓存
    private bool _indicatorDataDirty = true;  // 指标数据是否需要重新计算
    private int _lastDataCount = 0;  // 上次计算时的数据数量
    
    // 性能统计（可选）
    private static int _totalDrawCalls = 0;
    private static int _totalCalculations = 0;
    
    /// <summary>
    /// 初始化统一指标框架
    /// </summary>
    private void InitializeIndicators()
    {
        if (_indicatorManager != null) return; // 已初始化
        
        _indicatorManager = new IndicatorManager();
        RegisterAllIndicators();
    }
    
    /// <summary>
    /// 注册所有指标
    /// </summary>
    private void RegisterAllIndicators()
    {
        if (_indicatorManager == null)
        {
            return;
        }
        
        // MA - 移动平均
        if (MAConfig != null)
        {
            var maIndicator = new MAIndicator(new MAIndicatorConfig
            {
                IsEnabled = MAConfig.IsEnabled,
                Lines = MAConfig.Lines.Select(l => new IndicatorConfig.LineConfig
                {
                    IsEnabled = l.IsEnabled,
                    Key = $"ma{l.PERIOD}",
                    Label = $"MA{l.PERIOD}",
                    Color = l.Color,
                    PERIOD = l.PERIOD,
                    Style = l.Style,
                    Thickness = 1.2
                }).ToList()
            });
            _indicatorManager.RegisterIndicator(maIndicator);
        }
        
        // EMA - 指数移动平均
        if (EMAConfig != null)
        {
            var emaIndicator = new EMAIndicator(new EMAIndicatorConfig
            {
                IsEnabled = EMAConfig.IsEnabled,  // 使用配置的IsEnabled
                Lines = EMAConfig.Lines.Select(l => new LineConfig
                {
                    IsEnabled = l.IsEnabled,
                    Key = $"ema{l.PERIOD}",
                    Label = $"EMA{l.PERIOD}",
                    Color = l.Color,
                    PERIOD = l.PERIOD,
                    Style = l.Style,
                    Thickness = 1.2
                }).ToList()
            });
            _indicatorManager.RegisterIndicator(emaIndicator);
        }
        
        // WMA - 加权移动平均
        if (WMAConfig != null)
        {
            var wmaIndicator = new WMAIndicator(new WMAIndicatorConfig
            {
                IsEnabled = WMAConfig.IsEnabled,
                Lines = WMAConfig.Lines.Select(l => new LineConfig
                {
                    IsEnabled = l.IsEnabled,
                    Key = $"wma{l.PERIOD}",
                    Label = $"WMA{l.PERIOD}",
                    Color = l.Color,
                    PERIOD = l.PERIOD,
                    Style = l.Style,
                    Thickness = 1.2
                }).ToList()
            });
            _indicatorManager.RegisterIndicator(wmaIndicator);
        }
        
        // DEMA - 双重指数移动平均
        if (DEMAConfig != null)
        {
            var demaIndicator = new DEMAIndicator(new DEMAIndicatorConfig
            {
                IsEnabled = DEMAConfig.IsEnabled,
                Lines = DEMAConfig.Lines.Select(l => new LineConfig
                {
                    IsEnabled = l.IsEnabled,
                    Key = $"dema{l.PERIOD}",
                    Label = $"DEMA{l.PERIOD}",
                    Color = l.Color,
                    PERIOD = l.PERIOD,
                    Style = l.Style,
                    Thickness = 1.2
                }).ToList()
            });
            _indicatorManager.RegisterIndicator(demaIndicator);
        }
        
        // TEMA - 三重指数移动平均
        if (TEMAConfig != null)
        {
            var temaIndicator = new TEMAIndicator(new TEMAIndicatorConfig
            {
                IsEnabled = TEMAConfig.IsEnabled,
                Lines = TEMAConfig.Lines.Select(l => new LineConfig
                {
                    IsEnabled = l.IsEnabled,
                    Key = $"tema{l.PERIOD}",
                    Label = $"TEMA{l.PERIOD}",
                    Color = l.Color,
                    PERIOD = l.PERIOD,
                    Style = l.Style,
                    Thickness = 1.2
                }).ToList()
            });
            _indicatorManager.RegisterIndicator(temaIndicator);
        }
        
        // BOLL - 布林
        if (IsBOLLVisible)
        {
            var bollIndicator = new BOLLIndicator(new IndicatorConfig.BOLLIndicatorConfig
            {
                IsEnabled = true
            });
            _indicatorManager.RegisterIndicator(bollIndicator);
        }
        
        // Keltner - Keltner通道
        if (IsKeltnerVisible)
        {
            var keltnerIndicator = new KeltnerIndicator(new IndicatorConfig.KeltnerIndicatorConfig
            {
                IsEnabled = true
            });
            _indicatorManager.RegisterIndicator(keltnerIndicator);
        }
        
        // Ichimoku - 一目均衡表
        if (IsIchimokuVisible)
        {
            var ichimokuIndicator = new IchimokuIndicator(new Implementations.IchimokuIndicatorConfig
            {
                IsEnabled = true
            });
            _indicatorManager.RegisterIndicator(ichimokuIndicator);
        }
        
        // VWAP - 成交量加权平均价
        if (IsVWAPVisible)
        {
            var vwapIndicator = new VWAPIndicator(new Implementations.VWAPIndicatorConfig
            {
                IsEnabled = true
            });
            _indicatorManager.RegisterIndicator(vwapIndicator);
        }
        
        // SAR - 抛物线转
        if (IsSARVisible)
        {
            var sarIndicator = new SARIndicator(new Implementations.SARIndicatorConfig
            {
                IsEnabled = true
            });
            _indicatorManager.RegisterIndicator(sarIndicator);
        }
        
        // ========== 注册副图指标 ==========
        
        // Volume - 成交量
        if (IsVolumeSubChartEnabled)
        {
            var volumeIndicator = new VolumeIndicator(new IndicatorConfig.VolumeIndicatorConfig
            {
                IsEnabled = true
            });
            _indicatorManager.RegisterIndicator(volumeIndicator);
        }
        
        // MACD
        if (IsMACDSubChartEnabled)
        {
            var macdIndicator = new MACDIndicator(new MACDIndicatorConfig
            {
                IsEnabled = true,
                FastPeriod = MACDParams.Fast,
                SlowPeriod = MACDParams.Slow,
                SignalPeriod = MACDParams.Signal
            });
            _indicatorManager.RegisterIndicator(macdIndicator);
        }
        
        // RSI（支持多周期）
        if (IsRSISubChartEnabled)
        {
            var rsiConfig = new RSIIndicatorConfig
            {
                IsEnabled = true,
                PERIOD = RSIPeriod,
                OverboughtLevel = RSIOverboughtLevel,
                OversoldLevel = RSIOversoldLevel
            };
            
            // 检查是否启用多周期对比
            if (_subChartSettings?.RSIMultiPeriod != null && _subChartSettings.RSIMultiPeriod.ShowMA)
            {
                // 使用多周期配置
                rsiConfig.ShowMA = true;
                rsiConfig.MALines = _subChartSettings.RSIMultiPeriod.MALines?.Select((line, index) => new LineConfig
                {
                    IsEnabled = line.IsEnabled,
                    Key = $"PERIOD{index + 1}",
                    Label = $"RSI({line.PERIOD})",
                    Color = line.Color,
                    PERIOD = line.PERIOD,
                    Style = line.Style,
                    Thickness = 1.5
                }).ToList() ?? new List<LineConfig>();
            }
            
            var rsiIndicator = new RSIIndicator(rsiConfig);
            _indicatorManager.RegisterIndicator(rsiIndicator);
        }
        
        // ATR
        if (IsATRSubChartEnabled)
        {
            var atrIndicator = new ATRIndicator(new ATRIndicatorConfig
            {
                IsEnabled = true,
                PERIOD = ATRPeriod
            });
            _indicatorManager.RegisterIndicator(atrIndicator);
        }
        
        // ========== 注册新副图指标==========
        if (_subChartSettings != null)
        {
            if (_subChartSettings.IsMFIEnabled)
            {
                var mfiConfig = new MFIIndicatorConfig();
                
                // 检查是否启用多周期对比
                if (_subChartSettings.MFIMultiPeriod != null && _subChartSettings.MFIMultiPeriod.ShowMA)
                {
                    // 使用多周期配置
                    mfiConfig.ShowMA = true;
                    mfiConfig.MALines = _subChartSettings.MFIMultiPeriod.MALines?.Select((line, index) => new LineConfig
                    {
                        IsEnabled = line.IsEnabled,
                        Key = $"PERIOD{index + 1}",
                        Label = $"MFI({line.PERIOD})",
                        Color = line.Color,
                        PERIOD = line.PERIOD,
                        Style = line.Style,
                        Thickness = 1.5
                    }).ToList() ?? new List<LineConfig>();
                }
                
                _indicatorManager.RegisterIndicator(new MFIIndicator(mfiConfig));
            }
            
            if (_subChartSettings.IsOBVEnabled)
            {
                _indicatorManager.RegisterIndicator(new OBVIndicator(new OBVIndicatorConfig()));
            }
            
            if (_subChartSettings.IsKDJEnabled)
            {
                _indicatorManager.RegisterIndicator(new KDJIndicator(new KDJIndicatorConfig()));
            }
            
            if (_subChartSettings.IsStochRSIEnabled)
            {
                _indicatorManager.RegisterIndicator(new StochRSIIndicator(new StochRSIIndicatorConfig()));
            }
            
            if (_subChartSettings.IsCCIEnabled)
            {
                var cciConfig = new CCIIndicatorConfig();
                
                // 检查是否启用多周期对比
                if (_subChartSettings.CCIMultiPeriod != null && _subChartSettings.CCIMultiPeriod.ShowMA)
                {
                    // 使用多周期配置
                    cciConfig.ShowMA = true;
                    cciConfig.MALines = _subChartSettings.CCIMultiPeriod.MALines?.Select((line, index) => new LineConfig
                    {
                        IsEnabled = line.IsEnabled,
                        Key = $"PERIOD{index + 1}",
                        Label = $"CCI({line.PERIOD})",
                        Color = line.Color,
                        PERIOD = line.PERIOD,
                        Style = line.Style,
                        Thickness = 1.5
                    }).ToList() ?? new List<LineConfig>();
                }
                
                _indicatorManager.RegisterIndicator(new CCIIndicator(cciConfig));
            }
            
            if (_subChartSettings.IsDMIEnabled)
            {
                _indicatorManager.RegisterIndicator(new DMIIndicator(new DMIIndicatorConfig()));
            }
            
            if (_subChartSettings.IsWREnabled)
            {
                var wrConfig = new WRIndicatorConfig();
                
                // 检查是否启用多周期对比
                if (_subChartSettings.WRMultiPeriod != null && _subChartSettings.WRMultiPeriod.ShowMA)
                {
                    // 使用多周期配置
                    wrConfig.ShowMA = true;
                    wrConfig.MALines = _subChartSettings.WRMultiPeriod.MALines?.Select((line, index) => new LineConfig
                    {
                        IsEnabled = line.IsEnabled,
                        Key = $"PERIOD{index + 1}",
                        Label = $"WR({line.PERIOD})",
                        Color = line.Color,
                        PERIOD = line.PERIOD,
                        Style = line.Style,
                        Thickness = 1.5
                    }).ToList() ?? new List<LineConfig>();
                }
                
                _indicatorManager.RegisterIndicator(new WRIndicator(wrConfig));
            }
            
            if (_subChartSettings.IsCMFEnabled)
            {
                _indicatorManager.RegisterIndicator(new CMFIndicator(new CMFIndicatorConfig()));
            }
            
            if (_subChartSettings.IsROCEnabled)
            {
                _indicatorManager.RegisterIndicator(new ROCIndicator(new ROCIndicatorConfig()));
            }
            
            if (_subChartSettings.IsEMVEnabled)
            {
                _indicatorManager.RegisterIndicator(new EMVIndicator(new EMVIndicatorConfig()));
            }
            
            if (_subChartSettings.IsMTMEnabled)
            {
                _indicatorManager.RegisterIndicator(new MTMIndicator(new MTMIndicatorConfig()));
            }
            
            if (_subChartSettings.IsCMOEnabled)
            {
                _indicatorManager.RegisterIndicator(new CMOIndicator(new CMOIndicatorConfig()));
            }
            
            if (_subChartSettings.IsAroonEnabled)
            {
                _indicatorManager.RegisterIndicator(new AroonIndicator(new AroonIndicatorConfig()));
            }
        }
    }
    
    /// <summary>
    /// 填充所有指标数据（直接从K线计算）
    /// 🚀 性能优化：使用缓存，避免重复计算
    /// </summary>
    private void PopulateAllIndicatorData()
    {
        if (_indicatorManager == null || _allData.Count == 0) return;
        
        // 🚀 性能优化：检查是否需要重新计算
        if (!_indicatorDataDirty && _lastDataCount == _allData.Count)
        {
            // 数据未变化，使用缓存
            // 性能统计：缓存命中
            _totalDrawCalls++;
            return;
        }
        
        // 性能统计：需要重新计算
        _totalCalculations++;
        _totalDrawCalls++;
        
        // 🎯 新架构：使用Native计算器直接从K线计算所有指标
        // 不再依赖旧的_maData、_bollData等变量
        // 统一使用SubChartCalculator + NativeCalculator
        
        try
        {
            PopulateMainChartIndicators();
            PopulateSubChartIndicators();
            
            // 标记数据已更新
            _indicatorDataDirty = false;
            _lastDataCount = _allData.Count;
            
            // 输出性能统计（每10次计算输出一次）
            if (_totalCalculations % 10 == 0)
            {
                var hitRate = (_totalDrawCalls - _totalCalculations) * 100.0 / _totalDrawCalls;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"更新K线图指标失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 标记指标数据为脏，需要重新计算
    /// </summary>
    private void InvalidateIndicatorData()
    {
        _indicatorDataDirty = true;
    }
    
    /// <summary>
    /// 填充主图指标（直接从K线计算）
    /// </summary>
    private void PopulateMainChartIndicators()
    {
        if (_indicatorManager == null || _allData.Count == 0) return;
        
        // MA - 使用配置中的周期
        if (MAConfig != null && MAConfig.IsEnabled)
        {
            var maIndicator = _indicatorManager.GetIndicator("MA");
            if (maIndicator != null)
            {
                var periods = MAConfig.Lines.Where(l => l.IsEnabled).Select(l => l.PERIOD).ToList();
                maIndicator.Data = CalculateMultiPeriodMA(_allData, periods, "ma");
            }
        }
        
        // EMA
        if (EMAConfig != null && EMAConfig.IsEnabled)
        {
            var emaIndicator = _indicatorManager.GetIndicator("EMA");
            if (emaIndicator != null)
            {
                var periods = EMAConfig.Lines.Where(l => l.IsEnabled).Select(l => l.PERIOD).ToList();
                emaIndicator.Data = CalculateMultiPeriodEMA(_allData, periods, "ema");
            }
        }
        
        // WMA
        if (WMAConfig != null && WMAConfig.IsEnabled)
        {
            var wmaIndicator = _indicatorManager.GetIndicator("WMA");
            if (wmaIndicator != null)
            {
                var periods = WMAConfig.Lines.Where(l => l.IsEnabled).Select(l => l.PERIOD).ToList();
                wmaIndicator.Data = CalculateMultiPeriodWMA(_allData, periods, "wma");
            }
        }
        
        // DEMA
        if (DEMAConfig != null && DEMAConfig.IsEnabled)
        {
            var demaIndicator = _indicatorManager.GetIndicator("DEMA");
            if (demaIndicator != null)
            {
                var periods = DEMAConfig.Lines.Where(l => l.IsEnabled).Select(l => l.PERIOD).ToList();
                demaIndicator.Data = CalculateMultiPeriodDEMA(_allData, periods, "dema");
            }
        }
        else
        {
        }
        
        // TEMA
        if (TEMAConfig != null && TEMAConfig.IsEnabled)
        {
            var temaIndicator = _indicatorManager.GetIndicator("TEMA");
            if (temaIndicator != null)
            {
                var periods = TEMAConfig.Lines.Where(l => l.IsEnabled).Select(l => l.PERIOD).ToList();
                temaIndicator.Data = CalculateMultiPeriodTEMA(_allData, periods, "tema");
            }
        }
        else
        {
        }
        
        // BOLL
        if (IsBOLLVisible)
        {
            var bollIndicator = _indicatorManager.GetIndicator("BOLL");
            if (bollIndicator != null)
            {
                bollIndicator.Data = CalculateBOLL(_allData, 20, 2.0);
            }
        }
        
        // Keltner
        if (IsKeltnerVisible)
        {
            var keltnerIndicator = _indicatorManager.GetIndicator("Keltner");
            if (keltnerIndicator != null)
            {
                var config = keltnerIndicator.Config as IndicatorConfig.KeltnerIndicatorConfig;
                int PERIOD = config?.PERIOD ?? 20;
                double multiplier = config?.Multiplier ?? 2.0;
                keltnerIndicator.Data = CalculateKeltnerBand(_allData, PERIOD, multiplier);
            }
        }
        else
        {
        }
        
        // Ichimoku
        if (IsIchimokuVisible)
        {
            var ichimokuIndicator = _indicatorManager.GetIndicator("Ichimoku");
            if (ichimokuIndicator != null)
            {
                var config = ichimokuIndicator.Config as Implementations.IchimokuIndicatorConfig;
                int tenkanPeriod = config?.TenkanPeriod ?? 9;
                int kijunPeriod = config?.KijunPeriod ?? 26;
                int senkouBPeriod = config?.SenkouBPeriod ?? 52;
                ichimokuIndicator.Data = CalculateIchimokuCloud(_allData, tenkanPeriod, kijunPeriod, senkouBPeriod);
            }
        }
        else
        {
        }
        
        // VWAP
        if (IsVWAPVisible)
        {
            var vwapIndicator = _indicatorManager.GetIndicator("VWAP");
            if (vwapIndicator != null)
            {
                // 从配置中获取period参数
                var config = vwapIndicator.Config as Implementations.VWAPIndicatorConfig;
                int PERIOD = config?.PERIOD ?? 14;
                
                vwapIndicator.Data = CalculateVWAPSingle(_allData, PERIOD);
                // 🔍 分析VWAP数据范围
                if (vwapIndicator.Data.Count > 0)
                {
                    var vwapValues = vwapIndicator.Data
                        .Select(d => d.GetValue("vwap"))
                        .Where(v => !double.IsNaN(v))
                        .ToList();
                    
                    if (vwapValues.Count > 0)
                    {
                        var vwapMin = vwapValues.Min();
                        var vwapMax = vwapValues.Max();
                        // 输出最后一个VWAP值用于对比
                        var lastValue = vwapValues.Last();
                    }
                }
            }
        }
        else
        {
        }
        
        // SAR
        if (IsSARVisible)
        {
            var sarIndicator = _indicatorManager.GetIndicator("SAR");
            if (sarIndicator != null)
            {
                // 从配置中获取SAR参数
                var config = sarIndicator.Config as Implementations.SARIndicatorConfig;
                double acceleration = config?.Acceleration ?? 0.02;
                double maxAcceleration = config?.MaxAcceleration ?? 0.20;
                
                sarIndicator.Data = CalculateSAR(_allData, acceleration, maxAcceleration);
            }
        }
        else
        {
        }
        
    }
    
    /// <summary>
    /// 填充副图指标（直接从K线计算）
    /// </summary>
    private void PopulateSubChartIndicators()
    {
        if (_indicatorManager == null || _allData.Count == 0) return;
        
        // Volume
        var volumeIndicator = _indicatorManager.GetIndicator("Volume");
        if (volumeIndicator != null)
        {
            volumeIndicator.Data = CalculateVolume(_allData);
        }
        
        // MACD
        if (IsMACDSubChartEnabled)
        {
            var macdIndicator = _indicatorManager.GetIndicator("MACD");
            if (macdIndicator != null)
            {
                macdIndicator.Data = CalculateMACD(_allData);
            }
        }
        
        // RSI（支持多周期）
        if (IsRSISubChartEnabled)
        {
            var rsiIndicator = _indicatorManager.GetIndicator("RSI");
            if (rsiIndicator != null)
            {
                var config = rsiIndicator.Config as RSIIndicatorConfig;
                
                // 检查是否启用多周期对比
                if (config?.ShowMA == true && config.MALines != null && config.MALines.Count > 0)
                {
                    // 使用多周期计算
                    var periods = config.MALines.Select(line => line.PERIOD).ToArray();
                    var enabled = config.MALines.Select(line => line.IsEnabled).ToArray();
                    var multiPeriodData = Services.Indicators.SubChartCalculator.CalculateRSIMultiPeriod(_allData, periods, enabled);
                    rsiIndicator.Data = ConvertSubChartData(multiPeriodData);
                }
                else
                {
                    // 使用单周期计算
                    rsiIndicator.Data = CalculateRSI(_allData, RSIPeriod);
                }
            }
        }
        
        // ATR
        if (IsATRSubChartEnabled)
        {
            var atrIndicator = _indicatorManager.GetIndicator("ATR");
            if (atrIndicator != null)
            {
                atrIndicator.Data = CalculateATR(_allData, ATRPeriod);
            }
        }
        
        // ========== 填充新增副图指标 ==========
        if (_subChartSettings != null)
        {
            // MFI（支持多周期）
            if (_subChartSettings.IsMFIEnabled)
            {
                var mfiIndicator = _indicatorManager.GetIndicator("MFI");
                if (mfiIndicator != null)
                {
                    var config = mfiIndicator.Config as MFIIndicatorConfig;
                    
                    // 检查是否启用多周期对比
                    if (config?.ShowMA == true && config.MALines != null && config.MALines.Count > 0)
                    {
                        // 使用多周期计算
                        var periods = config.MALines.Select(line => line.PERIOD).ToArray();
                        var enabled = config.MALines.Select(line => line.IsEnabled).ToArray();
                        var multiPeriodData = Services.Indicators.SubChartCalculator.CalculateMFIMultiPeriod(_allData, periods, enabled);
                        mfiIndicator.Data = ConvertSubChartData(multiPeriodData);
                    }
                    else
                    {
                        // 使用单周期计算
                        int PERIOD = config?.PERIOD ?? 14;
                        mfiIndicator.Data = ConvertSubChartData(
                            Services.Indicators.SubChartCalculator.CalculateMFI(_allData, PERIOD)
                        );
                    }
                }
            }
            
            // OBV
            if (_subChartSettings.IsOBVEnabled)
            {
                var obvIndicator = _indicatorManager.GetIndicator("OBV");
                if (obvIndicator != null)
                {
                    obvIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateOBV(_allData)
                    );
                }
            }
            
            // KDJ
            if (_subChartSettings.IsKDJEnabled)
            {
                var kdjIndicator = _indicatorManager.GetIndicator("KDJ");
                if (kdjIndicator != null)
                {
                    var config = kdjIndicator.Config as KDJIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 9;
                    int kPeriod = config?.KPeriod ?? 3;
                    int dPeriod = config?.DPeriod ?? 3;
                    kdjIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateKDJ(_allData, PERIOD, kPeriod, dPeriod)
                    );
                }
            }
            
            // StochRSI
            if (_subChartSettings.IsStochRSIEnabled)
            {
                var stochRSIIndicator = _indicatorManager.GetIndicator("StochRSI");
                if (stochRSIIndicator != null)
                {
                    var config = stochRSIIndicator.Config as StochRSIIndicatorConfig;
                    int rsiPeriod = config?.RSIPeriod ?? 14;
                    int stochPeriod = config?.StochPeriod ?? 14;
                    stochRSIIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateStochRSI(_allData, rsiPeriod, stochPeriod)
                    );
                }
            }
            
            // CCI（支持多周期）
            if (_subChartSettings.IsCCIEnabled)
            {
                var cciIndicator = _indicatorManager.GetIndicator("CCI");
                if (cciIndicator != null)
                {
                    var config = cciIndicator.Config as CCIIndicatorConfig;
                    
                    // 检查是否启用多周期对比
                    if (config?.ShowMA == true && config.MALines != null && config.MALines.Count > 0)
                    {
                        // 使用多周期计算
                        var periods = config.MALines.Select(line => line.PERIOD).ToArray();
                        var enabled = config.MALines.Select(line => line.IsEnabled).ToArray();
                        cciIndicator.Data = ConvertSubChartData(
                            Services.Indicators.SubChartCalculator.CalculateCCIMultiPeriod(_allData, periods, enabled)
                        );
                    }
                    else
                    {
                        // 使用单周期计算
                        int PERIOD = config?.PERIOD ?? 20;
                        cciIndicator.Data = ConvertSubChartData(
                            Services.Indicators.SubChartCalculator.CalculateCCI(_allData, PERIOD)
                        );
                    }
                }
            }
            
            // DMI
            if (_subChartSettings.IsDMIEnabled)
            {
                var dmiIndicator = _indicatorManager.GetIndicator("DMI");
                if (dmiIndicator != null)
                {
                    var config = dmiIndicator.Config as DMIIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 14;
                    dmiIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateDMI(_allData, PERIOD)
                    );
                }
            }
            
            // WR（支持多周期）
            if (_subChartSettings.IsWREnabled)
            {
                var wrIndicator = _indicatorManager.GetIndicator("WR");
                if (wrIndicator != null)
                {
                    var config = wrIndicator.Config as WRIndicatorConfig;
                    
                    // 检查是否启用多周期对比
                    if (config?.ShowMA == true && config.MALines != null && config.MALines.Count > 0)
                    {
                        // 使用多周期计算
                        var periods = config.MALines.Select(line => line.PERIOD).ToArray();
                        var enabled = config.MALines.Select(line => line.IsEnabled).ToArray();
                        wrIndicator.Data = ConvertSubChartData(
                            Services.Indicators.SubChartCalculator.CalculateWRMultiPeriod(_allData, periods, enabled)
                        );
                    }
                    else
                    {
                        // 使用单周期计算
                        int PERIOD = config?.PERIOD ?? 14;
                        wrIndicator.Data = ConvertSubChartData(
                            Services.Indicators.SubChartCalculator.CalculateWR(_allData, PERIOD)
                        );
                    }
                }
            }
            
            // CMF
            if (_subChartSettings.IsCMFEnabled)
            {
                var cmfIndicator = _indicatorManager.GetIndicator("CMF");
                if (cmfIndicator != null)
                {
                    var config = cmfIndicator.Config as CMFIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 20;
                    cmfIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateCMF(_allData, PERIOD)
                    );
                }
            }
            
            // ROC
            if (_subChartSettings.IsROCEnabled)
            {
                var rocIndicator = _indicatorManager.GetIndicator("ROC");
                if (rocIndicator != null)
                {
                    var config = rocIndicator.Config as ROCIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 12;
                    rocIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateROC(_allData, PERIOD)
                    );
                }
            }
            
            // EMV
            if (_subChartSettings.IsEMVEnabled)
            {
                var emvIndicator = _indicatorManager.GetIndicator("EMV");
                if (emvIndicator != null)
                {
                    var config = emvIndicator.Config as EMVIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 14;
                    emvIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateEMV(_allData, PERIOD)
                    );
                }
            }
            
            // MTM
            if (_subChartSettings.IsMTMEnabled)
            {
                var mtmIndicator = _indicatorManager.GetIndicator("MTM");
                if (mtmIndicator != null)
                {
                    var config = mtmIndicator.Config as MTMIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 12;
                    mtmIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateMTM(_allData, PERIOD)
                    );
                }
            }
            
            // CMO
            if (_subChartSettings.IsCMOEnabled)
            {
                var cmoIndicator = _indicatorManager.GetIndicator("CMO");
                if (cmoIndicator != null)
                {
                    var config = cmoIndicator.Config as CMOIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 14;
                    cmoIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateCMO(_allData, PERIOD)
                    );
                }
            }
            
            // Aroon
            if (_subChartSettings.IsAroonEnabled)
            {
                var aroonIndicator = _indicatorManager.GetIndicator("Aroon");
                if (aroonIndicator != null)
                {
                    var config = aroonIndicator.Config as AroonIndicatorConfig;
                    int PERIOD = config?.PERIOD ?? 25;
                    aroonIndicator.Data = ConvertSubChartData(
                        Services.Indicators.SubChartCalculator.CalculateAroon(_allData, PERIOD)
                    );
                }
            }
        }
    }
    
    /// <summary>
    /// 统一绘制所有主图指标（替换所有旧的DrawXXX调用）
    /// </summary>
    private void DrawAllMainChartIndicators(double effectiveWidth, double mainChartHeight, double spacing)
    {
        if (_data.Count == 0) return;
        
        // 🚀 性能优化：只在需要时初始化
        if (_indicatorManager == null)
        {
            InitializeIndicators();
        }
        
        // 🚀 性能优化：使用缓存，只在数据变化时才重新计算
        PopulateAllIndicatorData();
        
        // 创建主图渲染上下文
        var mainContext = new IndicatorRenderContext
        {
            Canvas = ChartCanvas,
            Candles = _data,
            LeftMargin = LeftMargin,
            TopMargin = TopMargin,
            Width = effectiveWidth,
            Height = mainChartHeight,
            Spacing = spacing,
            MinValue = _minPrice,
            MaxValue = _maxPrice,
            StartIndex = _startIndex,
            VisibleCount = _visibleCount,
            HoverIndex = _hoverCandleIndex,
            MousePosition = _mousePosition,
            LabelColor = _labelColor,
            GridColor = _gridColor
        };
        
        // 渲染所有主图指标（只渲染线条，不渲染标题）
        var mainIndicators = _indicatorManager?.GetAllIndicators()
            .Where(i => i.Config.IsEnabled && i.Location == Indicators.Core.IndicatorLocation.MainChart);
        
        if (mainIndicators != null)
        {
            foreach (var indicator in mainIndicators)
            {
                indicator.Render(mainContext);
            }
        }
    }
    
    /// <summary>
    /// 统一绘制所有副图指标（替换所有旧的DrawXXXSubChart调用）
    /// </summary>
    private void DrawAllSubChartIndicators(double effectiveWidth, double realChartWidth, double spacing, ref double currentY, double subChartHeight)
    {
        if (_indicatorManager == null || _data.Count == 0) return;
        
        var subChartIndicators = _indicatorManager.GetAllIndicators()
            .Where(i => i.Config.IsEnabled && i.Location == Indicators.Core.IndicatorLocation.SubChart);
        if (!subChartIndicators.Any()) return;
        
        // 清空副图区域信息（用于十字准星）
        _subChartAreas.Clear();
        
        foreach (var indicator in subChartIndicators)
        {
            if (!indicator.Config.IsEnabled) continue;
            
            // 绘制副图顶部分隔线
            var separatorLine = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(LeftMargin, currentY),
                EndPoint = new Avalonia.Point(LeftMargin + realChartWidth, currentY),
                Stroke = _gridColor,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(separatorLine);
            
            currentY += 10; // 分隔线高度
            
            // 计算该指标在可见范围内的最小最大值
            var (minVal, maxVal) = CalculateIndicatorRange(indicator, _data);
            
            // 创建副图渲染上下文
            var subContext = new IndicatorRenderContext
            {
                Canvas = ChartCanvas,
                Candles = _data,
                LeftMargin = LeftMargin,
                TopMargin = currentY,  // 关键：设置副图起始Y坐标
                Width = effectiveWidth,
                Spacing = spacing,
                Height = subChartHeight,
                MinValue = minVal,  // 根据实际数据计算
                MaxValue = maxVal,  // 根据实际数据计算
                StartIndex = _startIndex,
                VisibleCount = _visibleCount,
                HoverIndex = _hoverCandleIndex,
                MousePosition = _mousePosition,
                LabelColor = _labelColor,
                GridColor = _gridColor
            };
            
            // 创建或获取该副图的标题TextBlock
            if (!_subChartTitleBlocks.ContainsKey(indicator.Id))
            {
                _subChartTitleBlocks[indicator.Id] = new TextBlock { FontSize = 10 };
            }
            
            var titleBlock = _subChartTitleBlocks[indicator.Id];
            UpdateSubChartIndicatorTitle(indicator, titleBlock);
            
            Canvas.SetLeft(titleBlock, LeftMargin + 5);
            Canvas.SetTop(titleBlock, currentY + 2);
            ChartCanvas.Children.Add(titleBlock);
            
            // 渲染单个副图指标
            indicator.Render(subContext);
            
            // 🔑 绘制副图右侧Y轴刻度
            DrawSubChartYAxis(realChartWidth, currentY, subChartHeight, minVal, maxVal);
            
            // 🔑 记录副图区域信息（用于十字准星）
            _subChartAreas.Add(new SubChartArea
            {
                IndicatorId = indicator.Id,
                TopY = currentY,
                Height = subChartHeight,
                MinValue = minVal,
                MaxValue = maxVal
            });
            
            currentY += subChartHeight;
        }
    }
    
    /// <summary>
    /// 绘制副图指标的右侧Y轴刻度
    /// </summary>
    private void DrawSubChartYAxis(double chartWidth, double topY, double height, double minValue, double maxValue)
    {
        const int segments = 3; // 将Y轴划分为3个刻度（4个刻度点）
        var range = maxValue - minValue;
        
        // 如果范围太小，不绘制刻度
        if (range < 1e-10)
        {
            return;
        }
        
        for (int i = 0; i <= segments; i++)
        {
            var ratio = (double)i / segments;
            var value = maxValue - range * ratio;  // 从上到下递减
            var y = topY + height * ratio;
            
            // 刻度标签
            var text = new TextBlock
            {
                Text = FormatAxisValue(value, range),
                Foreground = _textColor,
                FontSize = 10,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            
            // 放置在右侧（与主图价格轴对齐）
            var xPosition = LeftMargin + chartWidth + 8; // 图表右侧 + 8px间距
            Canvas.SetLeft(text, xPosition);
            Canvas.SetTop(text, y - 7); // 垂直居中对齐刻度
            ChartCanvas.Children.Add(text);
            
            // 绘制刻度短线
            var tickLine = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(LeftMargin + chartWidth, y),
                EndPoint = new Avalonia.Point(LeftMargin + chartWidth + 4, y),
                Stroke = _textColor,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(tickLine);
            
            // 绘制水平参考线（可选，使用虚线）
            if (i > 0 && i < segments) // 不在顶部和底部绘制
            {
                var gridLine = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = new Avalonia.Point(LeftMargin, y),
                    EndPoint = new Avalonia.Point(LeftMargin + chartWidth, y),
                    Stroke = _gridColor,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 2 },
                    Opacity = 0.3
                };
                ChartCanvas.Children.Add(gridLine);
            }
        }
    }
    
    /// <summary>
    /// 格式化Y轴数值显示
    /// </summary>
    private string FormatAxisValue(double value, double range)
    {
        // 根据数值范围选择合适的格式
        if (Math.Abs(value) < 0.01 || range < 0.1)
        {
            // 非常小的值，使用科学计数法或更多小数
            return value.ToString("F4");
        }
        else if (Math.Abs(value) >= 1000000)
        {
            // 大数值，使用K/M后缀
            if (Math.Abs(value) >= 1000000000)
                return $"{value / 1000000000:N1}B";
            else if (Math.Abs(value) >= 1000000)
                return $"{value / 1000000:N1}M";
            else
                return $"{value / 1000:N1}K";
        }
        else if (Math.Abs(value) >= 100)
        {
            // 中等数值，保留1位小数
            return value.ToString("N1");
        }
        else
        {
            // 小数值，保留2位小数
            return value.ToString("N2");
        }
    }
    
    /// <summary>
    /// 更新副图指标摘要标题
    /// </summary>
    private void UpdateSubChartIndicatorTitle(IIndicator indicator, TextBlock titleBlock)
    {
        // 确定要显示的数据索引：如果鼠标悬停，显示悬停的值；否则显示最新值
        int dataIndex = _hoverCandleIndex >= 0 ? _hoverCandleIndex : _data.Count - 1;
        if (dataIndex < 0 || dataIndex >= _data.Count)
        {
            titleBlock.Inlines = new Avalonia.Controls.Documents.InlineCollection();
            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
            { 
                Text = indicator.Id,  // 🚀 使用英文ID而不是中文Name
                Foreground = _textColor 
            });
            return;
        }
        
        var candle = _data[dataIndex];
        var dataPoint = indicator.Data.FindByTime(candle.Time);
        
        // 🚀 获取指标参数描述（使用英文ID）
        string paramDesc = GetIndicatorParameterDescription(indicator);
        string indicatorLabel = string.IsNullOrEmpty(paramDesc) ? indicator.Id : $"{indicator.Id}({paramDesc})";
        
        titleBlock.Inlines = new Avalonia.Controls.Documents.InlineCollection();
        
        // 🚀 特殊处理Volume指标（显示成交量）
        if (indicator.Id == "Volume")
        {
            // 添加指标名：Volume
            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
            { 
                Text = "Volume: ", 
                Foreground = _labelColor 
            });
            
            if (dataPoint != null)
            {
                var volume = dataPoint.GetValue("volume");
                if (!double.IsNaN(volume) && !double.IsInfinity(volume))
                {
                    // 判断涨跌来选择颜色
                    var volumeColor = _labelColor; // 默认颜色
                    if (dataIndex < _data.Count)
                    {
                        var currentCandle = _data[dataIndex];
                        // 根据K线涨跌选择颜色
                        volumeColor = currentCandle.Close >= currentCandle.Open 
                            ? new SolidColorBrush(Color.Parse("#26A69A"))  // 涨：绿色
                            : new SolidColorBrush(Color.Parse("#EF5350")); // 跌：红色
                    }
                    
                    // 格式化成交量值（大数值使用K/M后缀）
                    string volumeText;
                    if (volume >= 1_000_000_000)
                        volumeText = $"{volume / 1_000_000_000:N2}B";
                    else if (volume >= 1_000_000)
                        volumeText = $"{volume / 1_000_000:N2}M";
                    else if (volume >= 1_000)
                        volumeText = $"{volume / 1_000:N2}K";
                    else
                        volumeText = $"{volume:N0}";
                    
                    titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                    {
                        Text = volumeText,
                        Foreground = volumeColor
                    });
                }
            }
        }
        // 🚀 特殊处理RSI多周期指标
        else if (indicator.Id == "RSI")
        {
            var rsiConfig = indicator.Config as RSIIndicatorConfig;
            
            // 检查是否为多周期模式
            if (rsiConfig?.ShowMA == true && rsiConfig.MALines != null && rsiConfig.MALines.Count > 0)
            {
                // 多周期模式：显示每个周期的值
                if (dataPoint != null)
                {
                    bool first = true;
                    foreach (var lineConfig in rsiConfig.MALines.Where(l => l.IsEnabled))
                    {
                        var value = dataPoint.GetValue(lineConfig.Key);
                        
                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                        {
                            if (!first)
                            {
                                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                                { 
                                    Text = ", ",
                                    Foreground = _labelColor 
                                });
                            }
                            
                            // 显示 "RSI(周期)"
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"RSI({lineConfig.PERIOD}): ",
                                Foreground = _labelColor 
                            });
                            
                            // 显示值（使用线条颜色）
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"{value:N2}",
                                Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                            });
                            
                            first = false;
                        }
                    }
                }
            }
            else
            {
                // 单周期模式：传统显示
                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                { 
                    Text = $"{indicatorLabel}: ", 
                    Foreground = _labelColor 
                });
                
                if (dataPoint != null)
                {
                    var value = dataPoint.GetValue("value");
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                        { 
                            Text = $"{value:N2}",
                            Foreground = new SolidColorBrush(Color.Parse("#FFD700"))
                        });
                    }
                }
            }
        }
        // 🚀 特殊处理MFI多周期指标
        else if (indicator.Id == "MFI")
        {
            var mfiConfig = indicator.Config as MFIIndicatorConfig;
            
            // 检查是否为多周期模式
            if (mfiConfig?.ShowMA == true && mfiConfig.MALines != null && mfiConfig.MALines.Count > 0)
            {
                // 多周期模式：显示每个周期的值
                if (dataPoint != null)
                {
                    bool first = true;
                    foreach (var lineConfig in mfiConfig.MALines.Where(l => l.IsEnabled))
                    {
                        var value = dataPoint.GetValue(lineConfig.Key);
                        
                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                        {
                            if (!first)
                            {
                                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                                { 
                                    Text = ", ",
                                    Foreground = _labelColor 
                                });
                            }
                            
                            // 显示 "MFI(周期)"
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"MFI({lineConfig.PERIOD}): ",
                                Foreground = _labelColor 
                            });
                            
                            // 显示值（使用线条颜色）
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"{value:N2}",
                                Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                            });
                            
                            first = false;
                        }
                    }
                }
            }
            else
            {
                // 单周期模式：传统显示
                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                { 
                    Text = $"{indicatorLabel}: ", 
                    Foreground = _labelColor 
                });
                
                if (dataPoint != null)
                {
                    var value = dataPoint.GetValue("value");
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                        { 
                            Text = $"{value:N2}",
                            Foreground = new SolidColorBrush(Color.Parse("#00FFFF"))
                        });
                    }
                }
            }
        }
        // 🚀 特殊处理CCI多周期指标
        else if (indicator.Id == "CCI")
        {
            var cciConfig = indicator.Config as CCIIndicatorConfig;
            
            // 检查是否为多周期模式
            if (cciConfig?.ShowMA == true && cciConfig.MALines != null && cciConfig.MALines.Count > 0)
            {
                // 多周期模式：显示每个周期的值
                if (dataPoint != null)
                {
                    bool first = true;
                    foreach (var lineConfig in cciConfig.MALines.Where(l => l.IsEnabled))
                    {
                        var value = dataPoint.GetValue(lineConfig.Key);
                        
                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                        {
                            if (!first)
                            {
                                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                                { 
                                    Text = ", ",
                                    Foreground = _labelColor 
                                });
                            }
                            
                            // 显示 "CCI(周期)"
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"CCI({lineConfig.PERIOD}): ",
                                Foreground = _labelColor 
                            });
                            
                            // 显示值（使用线条颜色）
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"{value:N2}",
                                Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                            });
                            
                            first = false;
                        }
                    }
                }
            }
            else
            {
                // 单周期模式：传统显示
                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                { 
                    Text = $"{indicatorLabel}: ", 
                    Foreground = _labelColor 
                });
                
                if (dataPoint != null)
                {
                    var value = dataPoint.GetValue("value");
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                        { 
                            Text = $"{value:N2}",
                            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
                        });
                    }
                }
            }
        }
        // 🚀 特殊处理WR多周期指标
        else if (indicator.Id == "WR")
        {
            var wrConfig = indicator.Config as WRIndicatorConfig;
            
            // 检查是否为多周期模式
            if (wrConfig?.ShowMA == true && wrConfig.MALines != null && wrConfig.MALines.Count > 0)
            {
                // 多周期模式：显示每个周期的值
                if (dataPoint != null)
                {
                    bool first = true;
                    foreach (var lineConfig in wrConfig.MALines.Where(l => l.IsEnabled))
                    {
                        var value = dataPoint.GetValue(lineConfig.Key);
                        
                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                        {
                            if (!first)
                            {
                                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                                { 
                                    Text = ", ",
                                    Foreground = _labelColor 
                                });
                            }
                            
                            // 显示 "WR(周期)"
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"WR({lineConfig.PERIOD}): ",
                                Foreground = _labelColor 
                            });
                            
                            // 显示值（使用线条颜色）
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = $"{value:N2}",
                                Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                            });
                            
                            first = false;
                        }
                    }
                }
            }
            else
            {
                // 单周期模式：传统显示
                titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                { 
                    Text = $"{indicatorLabel}: ", 
                    Foreground = _labelColor 
                });
                
                if (dataPoint != null)
                {
                    var value = dataPoint.GetValue("value");
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                        { 
                            Text = $"{value:N2}",
                            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
                        });
                    }
                }
            }
        }
        // 🚀 特殊处理MACD指标（统一格式显示）
        else if (indicator.Id == "MACD")
        {
            // 添加指标名和参数：MACD(12,26,9)
            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
            { 
                Text = $"{indicatorLabel} ", 
                Foreground = _labelColor 
            });
            
            if (dataPoint != null)
            {
                bool first = true;
                
                // 显示DIF (从Lines获取)
                var difLine = indicator.Config.Lines?.FirstOrDefault(l => l.Key.ToLower() == "dif");
                if (difLine != null && difLine.IsEnabled)
                {
                    var difValue = dataPoint.GetValue("dif");
                    if (!double.IsNaN(difValue) && !double.IsInfinity(difValue))
                    {
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                        {
                            Text = "DIF: ",
                            Foreground = _labelColor
                        });
                        
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                        {
                            Text = $"{difValue:N2}",
                            Foreground = new SolidColorBrush(Color.Parse(difLine.Color))
                        });
                        
                        first = false;
                    }
                }
                
                // 显示DEA (从Lines获取)
                var deaLine = indicator.Config.Lines?.FirstOrDefault(l => l.Key.ToLower() == "dea");
                if (deaLine != null && deaLine.IsEnabled)
                {
                    var deaValue = dataPoint.GetValue("dea");
                    if (!double.IsNaN(deaValue) && !double.IsInfinity(deaValue))
                    {
                        if (!first)
                        {
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                            {
                                Text = ", ",
                                Foreground = _labelColor
                            });
                        }
                        
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                        {
                            Text = "DEA: ",
                            Foreground = _labelColor
                        });
                        
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                        {
                            Text = $"{deaValue:N2}",
                            Foreground = new SolidColorBrush(Color.Parse(deaLine.Color))
                        });
                        
                        first = false;
                    }
                }
                
                // 🚀 显示HIST (直接从数据获取，不在Lines中)
                var histValue = dataPoint.GetValue("histogram");
                if (!double.IsNaN(histValue) && !double.IsInfinity(histValue))
                {
                    if (!first)
                    {
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                        {
                            Text = ", ",
                            Foreground = _labelColor
                        });
                    }
                    
                    titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                    {
                        Text = "HIST: ",
                        Foreground = _labelColor
                    });
                    
                    // 🚀 HIST根据正负值使用不同颜色
                    var macdConfig = indicator.Config as MACDIndicatorConfig;
                    var histColor = histValue >= 0 
                        ? (macdConfig?.HistogramPositiveColor ?? "#26A69A")
                        : (macdConfig?.HistogramNegativeColor ?? "#EF5350");
                    
                    titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                    {
                        Text = $"{histValue:N2}",
                        Foreground = new SolidColorBrush(Color.Parse(histColor))
                    });
                }
            }
        }
        else
        {
            // 其他指标：原有逻辑
            // 添加指标名称和参数，并在冒号后添加空格
            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
            { 
                Text = $"{indicatorLabel}: ", 
                Foreground = _labelColor 
            });
            
            // 🚀 添加指标值（使用各条线的颜色）
            if (dataPoint != null && indicator.Config.Lines != null)
            {
                bool first = true;
                foreach (var lineConfig in indicator.Config.Lines.Where(l => l.IsEnabled))
                {
                    var value = dataPoint.GetValue(lineConfig.Key);
                    
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        if (!first)
                        {
                            titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                            { 
                                Text = "  ",  // 值之间的间隔
                                Foreground = _labelColor 
                            });
                        }
                        
                        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run 
                        { 
                            Text = $"{value:N2}",  // 🚀 使用N2格式（千分位）
                            Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))  // 🚀 使用线条颜色
                        });
                        
                        first = false;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 获取指标参数描述（用于摘要显示）
    /// </summary>
    private string GetIndicatorParameterDescription(IIndicator indicator)
    {
        // 根据不同指标类型返回参数描述
        switch (indicator.Id)
        {
            // === 主图指标 ===
            case "BOLL":
                if (indicator.Config is Indicators.Config.BOLLIndicatorConfig bollConfig)
                    return $"{bollConfig.PERIOD},{bollConfig.StdDevMultiplier}";
                break;
                
            case "Keltner":
                if (indicator.Config is Indicators.Config.KeltnerIndicatorConfig keltnerConfig)
                    return $"{keltnerConfig.PERIOD},{keltnerConfig.Multiplier}";
                break;
                
            case "Ichimoku":
                if (indicator.Config is Indicators.Implementations.IchimokuIndicatorConfig ichimokuConfig)
                    return $"{ichimokuConfig.TenkanPeriod},{ichimokuConfig.KijunPeriod},{ichimokuConfig.SenkouBPeriod}";
                break;
                
            case "SAR":
                if (indicator.Config is Indicators.Implementations.SARIndicatorConfig sarConfig)
                    return $"{sarConfig.Acceleration},{sarConfig.MaxAcceleration}";
                break;
                
            case "VWAP":
                if (indicator.Config is Indicators.Implementations.VWAPIndicatorConfig vwapConfig)
                    return $"{vwapConfig.PERIOD}";
                break;
            
            // === 副图指标 ===
            case "MACD":
                if (indicator.Config is MACDIndicatorConfig macdConfig)
                    return $"{macdConfig.FastPeriod},{macdConfig.SlowPeriod},{macdConfig.SignalPeriod}";
                break;
                
            case "RSI":
                if (indicator.Config is RSIIndicatorConfig rsiConfig)
                    return $"{rsiConfig.PERIOD}";
                break;
                
            case "ATR":
                if (indicator.Config is ATRIndicatorConfig atrConfig)
                    return $"{atrConfig.PERIOD}";
                break;
                
            case "KDJ":
                if (indicator.Config is KDJIndicatorConfig kdjConfig)
                    return $"{kdjConfig.PERIOD},{kdjConfig.KPeriod},{kdjConfig.DPeriod}";
                break;
                
            case "MFI":
                if (indicator.Config is MFIIndicatorConfig mfiConfig)
                    return $"{mfiConfig.PERIOD}";
                break;
                
            case "CCI":
                if (indicator.Config is CCIIndicatorConfig cciConfig)
                    return $"{cciConfig.PERIOD}";
                break;
                
            case "StochRSI":
                if (indicator.Config is StochRSIIndicatorConfig stochConfig)
                    return $"{stochConfig.RSIPeriod},{stochConfig.StochPeriod}";
                break;
                
            case "DMI":
                if (indicator.Config is DMIIndicatorConfig dmiConfig)
                    return $"{dmiConfig.PERIOD}";
                break;
                
            case "WR":
                if (indicator.Config is WRIndicatorConfig wrConfig)
                    return $"{wrConfig.PERIOD}";
                break;
                
            case "CMF":
                if (indicator.Config is CMFIndicatorConfig cmfConfig)
                    return $"{cmfConfig.PERIOD}";
                break;
                
            case "ROC":
                if (indicator.Config is ROCIndicatorConfig rocConfig)
                    return $"{rocConfig.PERIOD}";
                break;
                
            case "EMV":
                if (indicator.Config is EMVIndicatorConfig emvConfig)
                    return $"{emvConfig.PERIOD}";
                break;
                
            case "MTM":
                if (indicator.Config is MTMIndicatorConfig mtmConfig)
                    return $"{mtmConfig.PERIOD}";
                break;
                
            case "CMO":
                if (indicator.Config is CMOIndicatorConfig cmoConfig)
                    return $"{cmoConfig.PERIOD}";
                break;
                
            case "Aroon":
                if (indicator.Config is AroonIndicatorConfig aroonConfig)
                    return $"{aroonConfig.PERIOD}";
                break;
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// 更新所有副图指标摘要（在鼠标移动时调用）
    /// </summary>
    public void UpdateNewFrameworkSubChartTitles()
    {
        if (_indicatorManager == null) return;
        
        var subChartIndicators = _indicatorManager.GetAllIndicators()
            .Where(i => i.Config.IsEnabled && i.Location == Indicators.Core.IndicatorLocation.SubChart);
        
        foreach (var indicator in subChartIndicators)
        {
            if (_subChartTitleBlocks.TryGetValue(indicator.Id, out var titleBlock))
            {
                UpdateSubChartIndicatorTitle(indicator, titleBlock);
            }
        }
    }
    
    /// <summary>
    /// 计算指标在可见范围内的最小最大值
    /// </summary>
    private (double min, double max) CalculateIndicatorRange(IIndicator indicator, List<Candlestick> candles)
    {
        double min = double.MaxValue;
        double max = double.MinValue;
        bool hasValue = false;
        
        // 遍历可见K线对应的指标数据
        foreach (var candle in candles)
        {
            var dataPoint = indicator.Data.FindByTime(candle.Time);
            if (dataPoint != null)
            {
                // 获取该数据点的所有值
                var values = dataPoint.GetAllValues();
                foreach (var kvp in values)
                {
                    var value = kvp.Value;
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                        hasValue = true;
                    }
                }
            }
        }
        
        // 如果没有有效值，返回默认范围
        if (!hasValue)
        {
            return (0, 100);
        }
        
        // 添加一些边距（上下各留5%空间）
        var range = max - min;
        if (range < 1e-10) // 避免除零
        {
            range = Math.Abs(max) * 0.1;
            if (range < 1e-10) range = 1.0;
        }
        
        min -= range * 0.05;
        max += range * 0.05;
        
        // 对于Momentum类型指标，确保包含零点（MACD、RSI等需要零线）
        if (indicator.Type == IndicatorType.Momentum && indicator.Id != "RSI" && indicator.Id != "KDJ")
        {
            if (min > 0) min = 0;
            if (max < 0) max = 0;
        }
        
        // 成交量类确保开盘价
        if (indicator.Type == IndicatorType.Volume && min > 0)
        {
            min = 0;
        }
        
        return (min, max);
    }
    
    /// <summary>
    /// 获取指标管理器（供外部访问）
    /// </summary>
    public IndicatorManager? GetIndicatorManager()
    {
        InitializeIndicators();
        return _indicatorManager;
    }
    
    /// <summary>
    /// 重新初始化所有指标（当配置改变时调用）
    /// </summary>
    public void RefreshIndicators()
    {
        _indicatorManager = null;
        _subChartTitleBlocks.Clear();
        InvalidateIndicatorData();  // 标记需要重新计算
        InitializeIndicators();
    }
    
    /// <summary>
    /// 同步指标配置（当用户改变配置时调用）
    /// </summary>
    private void SyncIndicatorConfigs()
    {
        if (_indicatorManager == null) return;
        
        // 同步MA配置
        var maIndicator = _indicatorManager.GetIndicator("MA");
        if (maIndicator != null && MAConfig != null)
        {
            maIndicator.Config.IsEnabled = MAConfig.IsEnabled;
        }
        
        // 同步EMA配置
        var emaIndicator = _indicatorManager.GetIndicator("EMA");
        if (emaIndicator != null && EMAConfig != null)
        {
            emaIndicator.Config.IsEnabled = EMAConfig.IsEnabled;
        }
        
        // 同步WMA配置
        var wmaIndicator = _indicatorManager.GetIndicator("WMA");
        if (wmaIndicator != null && WMAConfig != null)
        {
            wmaIndicator.Config.IsEnabled = WMAConfig.IsEnabled;
        }
        
        // 其他指标配置同步...
    }
    
    // ========================================
    // 指标计算辅助方法（直接从K线计算）
    // ========================================
    
    /// <summary>
    /// 计算多周期MA（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateMultiPeriodMA(List<Candlestick> candles, List<int> periods, string keyPrefix)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0 || periods == null) return series;
        
        foreach (var PERIOD in periods)
        {
            var maData = Services.Indicators.IndicatorCalculatorNative.CalculateSMA(candles, PERIOD);
            var key = $"{keyPrefix}{PERIOD}";
            
            foreach (var ma in maData)
            {
                var dp = series.FindByTime(ma.Time);
                if (dp == null)
                {
                    dp = new IndicatorDataPoint { Time = ma.Time };
                    series.Add(dp);
                }
                dp.SetValue(key, ma.Value);
            }
        }
        
        series.SortByTime();
        return series;
    }
    
    /// <summary>
    /// 计算BOLL（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateBOLL(List<Candlestick> candles, int PERIOD, double stdDev)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        var bollData = Services.Indicators.IndicatorCalculatorNative.CalculateBOLL(candles, PERIOD, stdDev);
        
        foreach (var boll in bollData)
        {
            var dp = new IndicatorDataPoint { Time = boll.Time };
            dp.SetValue("upper", boll.Upper);
            dp.SetValue("middle", boll.Middle);
            dp.SetValue("lower", boll.Lower);
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算Volume数据（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateVolume(List<Candlestick> candles)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        foreach (var candle in candles)
        {
            var dp = new IndicatorDataPoint { Time = candle.Time };
            dp.SetValue("volume", candle.Volume);
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算多周期EMA（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateMultiPeriodEMA(List<Candlestick> candles, List<int> periods, string keyPrefix)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0 || periods == null) return series;
        
        foreach (var PERIOD in periods)
        {
            var emaData = Services.Indicators.IndicatorCalculatorNative.CalculateEMA(candles, PERIOD);
            var key = $"{keyPrefix}{PERIOD}";
            
            foreach (var ema in emaData)
            {
                var dp = series.FindByTime(ema.Time);
                if (dp == null)
                {
                    dp = new IndicatorDataPoint { Time = ema.Time };
                    series.Add(dp);
                }
                dp.SetValue(key, ema.Value);
            }
        }
        
        series.SortByTime();
        return series;
    }
    
    /// <summary>
    /// 计算多周期WMA（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateMultiPeriodWMA(List<Candlestick> candles, List<int> periods, string keyPrefix)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0 || periods == null) return series;
        
        foreach (var PERIOD in periods)
        {
            var wmaData = Services.Indicators.IndicatorCalculatorNative.CalculateWMA(candles, PERIOD);
            var key = $"{keyPrefix}{PERIOD}";
            
            foreach (var wma in wmaData)
            {
                var dp = series.FindByTime(wma.Time);
                if (dp == null)
                {
                    dp = new IndicatorDataPoint { Time = wma.Time };
                    series.Add(dp);
                }
                dp.SetValue(key, wma.Value);
            }
        }
        
        series.SortByTime();
        return series;
    }
    
    /// <summary>
    /// 计算多周期DEMA（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateMultiPeriodDEMA(List<Candlestick> candles, List<int> periods, string keyPrefix)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0 || periods == null) return series;
        
        foreach (var PERIOD in periods)
        {
            var demaData = Services.Indicators.IndicatorCalculatorNative.CalculateDEMA(candles, PERIOD);
            var key = $"{keyPrefix}{PERIOD}";
            
            foreach (var dema in demaData)
            {
                var dp = series.FindByTime(dema.Time);
                if (dp == null)
                {
                    dp = new IndicatorDataPoint { Time = dema.Time };
                    series.Add(dp);
                }
                dp.SetValue(key, dema.Value);
            }
        }
        
        series.SortByTime();
        return series;
    }
    
    /// <summary>
    /// 计算多周期TEMA（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateMultiPeriodTEMA(List<Candlestick> candles, List<int> periods, string keyPrefix)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0 || periods == null) return series;
        
        foreach (var PERIOD in periods)
        {
            var temaData = Services.Indicators.IndicatorCalculatorNative.CalculateTEMA(candles, PERIOD);
            var key = $"{keyPrefix}{PERIOD}";
            
            foreach (var tema in temaData)
            {
                var dp = series.FindByTime(tema.Time);
                if (dp == null)
                {
                    dp = new IndicatorDataPoint { Time = tema.Time };
                    series.Add(dp);
                }
                dp.SetValue(key, tema.Value);
            }
        }
        
        series.SortByTime();
        return series;
    }
    
    /// <summary>
    /// 计算VWAP（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateVWAPSingle(List<Candlestick> candles, int PERIOD)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        // 使用Prophet.Core的Native实现，传入正确的period参数
        var vwapData = Services.Indicators.IndicatorCalculatorNative.CalculateVWAP(candles, PERIOD);
        
        foreach (var vwap in vwapData)
        {
            var dp = new IndicatorDataPoint { Time = vwap.Time };
            dp.SetValue("vwap", vwap.Value);
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算Keltner通道（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateKeltnerBand(List<Candlestick> candles, int PERIOD, double multiplier)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        var keltnerData = Services.Indicators.IndicatorCalculatorNative.CalculateKeltner(candles, PERIOD, multiplier);
        
        foreach (var keltner in keltnerData)
        {
            var dp = new IndicatorDataPoint { Time = keltner.Time };
            dp.SetValue("upper", keltner.Upper);
            dp.SetValue("middle", keltner.Middle);
            dp.SetValue("lower", keltner.Lower);
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算Ichimoku一目均衡表（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateIchimokuCloud(List<Candlestick> candles, int tenkanPeriod, int kijunPeriod, int senkouBPeriod)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        var ichimokuData = Services.Indicators.IndicatorCalculatorNative.CalculateIchimoku(candles, tenkanPeriod, kijunPeriod, senkouBPeriod);
        
        foreach (var ichimoku in ichimokuData)
        {
            var dp = new IndicatorDataPoint { Time = ichimoku.Time };
            dp.SetValue("tenkan", ichimoku.Tenkan);
            dp.SetValue("kijun", ichimoku.Kijun);
            dp.SetValue("senkou_a", ichimoku.SenkouA);
            dp.SetValue("senkou_b", ichimoku.SenkouB);
            dp.SetValue("chikou", ichimoku.Chikou);
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算SAR（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateSAR(List<Candlestick> candles, double acceleration, double maxAcceleration)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        // 使用Prophet.Core的Native实现，传入正确的acceleration参数
        var sarData = Services.Indicators.IndicatorCalculatorNative.CalculateSAR(candles, acceleration, maxAcceleration);
        
        foreach (var sar in sarData)
        {
            var dp = new IndicatorDataPoint { Time = sar.Time };
            dp.SetValue("sar", sar.Value);
            dp.SetValue("trend", sar.IsUpTrend ? 1.0 : -1.0);
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算MACD（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateMACD(List<Candlestick> candles)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        var (fastPeriod, slowPeriod, signalPeriod) = MACDParams;
        var macdPoints = Services.Indicators.SubChartCalculator.CalculateMACD(candles, fastPeriod, slowPeriod, signalPeriod);
        
        foreach (var point in macdPoints)
        {
            var dp = new IndicatorDataPoint { Time = point.Time };
            foreach (var kv in point.Values)
            {
                dp.SetValue(kv.Key, kv.Value);
            }
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算RSI（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateRSI(List<Candlestick> candles, int PERIOD)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        var rsiPoints = Services.Indicators.SubChartCalculator.CalculateRSI(candles, PERIOD);
        
        foreach (var point in rsiPoints)
        {
            var dp = new IndicatorDataPoint { Time = point.Time };
            foreach (var kv in point.Values)
            {
                dp.SetValue(kv.Key, kv.Value);
            }
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 计算ATR（返回新格式）
    /// </summary>
    private IndicatorDataSeries CalculateATR(List<Candlestick> candles, int PERIOD)
    {
        var series = new IndicatorDataSeries();
        if (candles == null || candles.Count == 0) return series;
        
        var atrPoints = Services.Indicators.SubChartCalculator.CalculateATR(candles, PERIOD);
        
        foreach (var point in atrPoints)
        {
            var dp = new IndicatorDataPoint { Time = point.Time };
            foreach (var kv in point.Values)
            {
                dp.SetValue(kv.Key, kv.Value);
            }
            series.Add(dp);
        }
        
        return series;
    }
    
    /// <summary>
    /// 转换SubChartDataPoint列表到IndicatorDataSeries
    /// </summary>
    private IndicatorDataSeries ConvertSubChartData(List<Models.SubChartDataPoint> points)
    {
        var series = new IndicatorDataSeries();
        if (points == null) return series;
        
        foreach (var point in points)
        {
            var dp = new IndicatorDataPoint { Time = point.Time };
            foreach (var kv in point.Values)
            {
                dp.SetValue(kv.Key, kv.Value);
            }
            series.Add(dp);
        }
        
        return series;
    }
}

