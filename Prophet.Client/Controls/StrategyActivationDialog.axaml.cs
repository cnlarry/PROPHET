using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Prophet.Client.Services;

namespace Prophet.Client.Controls;

/// <summary>
/// 策略激活对话框 - 配置指标参数 + 显示回测数据 (🔥 P0-8)
/// </summary>
public partial class StrategyActivationDialog : UserControl
{
    private List<IndicatorUsage> _indicators = new();
    private Dictionary<string, Dictionary<string, IndicatorParameter>> _indicatorParameters = new();
    private Dictionary<string, Dictionary<string, TextBox>> _parameterInputs = new();
    private IndicatorUsage? _selectedIndicator;

    public StrategyActivationDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 初始化对话框，解析DSL代码并显示回测数据 (🔥 P0-8扩展)
    /// </summary>
    public void Initialize(string dslCode, string? backtestDataJson = null)
    {
        var parser = new DSLParser();
        
        // 提取指标使用情况
        _indicators = parser.ExtractIndicators(dslCode);
        
        // 获取每个指标的参数定义
        foreach (var indicator in _indicators)
        {
            var key = $"{indicator.Name}_{indicator.Timeframe}";
            _indicatorParameters[key] = parser.GetIndicatorParameters(indicator.Name);
            _parameterInputs[key] = new Dictionary<string, TextBox>();
        }
        
        // 填充指标列表
        PopulateIndicatorList();
        
        // 🔥 显示回测数据 (P0-8)
        if (!string.IsNullOrEmpty(backtestDataJson))
        {
            DisplayBacktestData(backtestDataJson);
        }
    }
    
    /// <summary>
    /// 显示回测数据 (🔥 P0-8 新增)
    /// </summary>
    private void DisplayBacktestData(string backtestDataJson)
    {
        try
        {
            var backtestPanel = this.FindControl<Border>("BacktestDataPanel");
            if (backtestPanel == null) return;
            
            // 解析JSON
            var data = JsonSerializer.Deserialize<JsonElement>(backtestDataJson);
            if (!data.TryGetProperty("performance", out var performance))
            {
                return;
            }
            
            // 提取关键指标
            var winRate = GetDecimalValue(performance, "win_rate");
            var totalReturn = GetDecimalValue(performance, "total_return");
            var maxDrawdown = GetDecimalValue(performance, "max_drawdown");
            var sharpeRatio = GetDecimalValue(performance, "sharpe_ratio");
            var profitFactor = GetDecimalValue(performance, "profit_factor");
            var totalTrades = GetIntValue(performance, "total_trades");
            
            // 计算质量评分 (客户端简化版)
            var qualityScore = CalculateQualityScore(winRate, totalReturn, maxDrawdown, sharpeRatio, profitFactor, totalTrades);
            
            // 更新UI
            var winRateText = this.FindControl<TextBlock>("WinRateText");
            var totalTradesText = this.FindControl<TextBlock>("TotalTradesText");
            var totalReturnText = this.FindControl<TextBlock>("TotalReturnText");
            var maxDrawdownText = this.FindControl<TextBlock>("MaxDrawdownText");
            var sharpeRatioText = this.FindControl<TextBlock>("SharpeRatioText");
            var profitFactorText = this.FindControl<TextBlock>("ProfitFactorText");
            var qualityScoreText = this.FindControl<TextBlock>("QualityScoreText");
            var qualityLevelText = this.FindControl<TextBlock>("QualityLevelText");
            
            if (winRateText != null)
                winRateText.Text = $"{winRate:P2}";
            
            if (totalTradesText != null)
                totalTradesText.Text = $"{totalTrades} 笔";
            
            if (totalReturnText != null)
            {
                totalReturnText.Text = $"{totalReturn:P2}";
                totalReturnText.Foreground = totalReturn >= 0 
                    ? new SolidColorBrush(Color.Parse("#4CAF50")) 
                    : new SolidColorBrush(Color.Parse("#F44336"));
            }
            
            if (maxDrawdownText != null)
                maxDrawdownText.Text = $"{Math.Abs(maxDrawdown):P2}";
            
            if (sharpeRatioText != null)
                sharpeRatioText.Text = sharpeRatio.ToString("F2");
            
            if (profitFactorText != null)
                profitFactorText.Text = profitFactor.ToString("F2");
            
            if (qualityScoreText != null)
                qualityScoreText.Text = qualityScore.ToString();
            
            if (qualityLevelText != null)
            {
                var (level, color) = GetQualityLevel(qualityScore);
                qualityLevelText.Text = level;
                if (qualityScoreText != null)
                    qualityScoreText.Foreground = new SolidColorBrush(Color.Parse(color));
            }
            
            // 显示面板
            backtestPanel.IsVisible = true;
            
            Console.WriteLine($"✅ 回测数据已显示: 胜率={winRate:P2}, 收益={totalReturn:P2}, 质量评分={qualityScore}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 显示回测数据失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从JSON元素中安全提取decimal值
    /// </summary>
    private decimal GetDecimalValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetDecimal();
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                if (decimal.TryParse(prop.GetString(), out var value))
                {
                    return value;
                }
            }
        }
        return 0;
    }
    
    /// <summary>
    /// 从JSON元素中安全提取int值
    /// </summary>
    private int GetIntValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                if (int.TryParse(prop.GetString(), out var value))
                {
                    return value;
                }
            }
        }
        return 0;
    }
    
    /// <summary>
    /// 计算质量评分 (客户端简化版)
    /// </summary>
    private int CalculateQualityScore(decimal winRate, decimal totalReturn, decimal maxDrawdown, 
        decimal sharpeRatio, decimal profitFactor, int totalTrades)
    {
        decimal score = 0;
        
        // 胜率 (30分)
        if (winRate >= 0.65m) score += 30;
        else if (winRate >= 0.60m) score += 25;
        else if (winRate >= 0.55m) score += 20;
        else if (winRate >= 0.50m) score += 15;
        else if (winRate >= 0.45m) score += 10;
        else if (winRate >= 0.40m) score += 5;
        
        // 收益率 (25分)
        if (totalReturn >= 2.0m) score += 25;
        else if (totalReturn >= 1.0m) score += 20;
        else if (totalReturn >= 0.5m) score += 15;
        else if (totalReturn >= 0.2m) score += 10;
        else if (totalReturn > 0) score += 5;
        
        // 最大回撤 (20分)
        var absDrawdown = Math.Abs(maxDrawdown);
        if (absDrawdown < 0.20m) score += 20;
        else if (absDrawdown < 0.30m) score += 15;
        else if (absDrawdown < 0.40m) score += 10;
        else if (absDrawdown < 0.50m) score += 5;
        
        // 夏普比率 (15分)
        if (sharpeRatio >= 3.0m) score += 15;
        else if (sharpeRatio >= 2.0m) score += 13;
        else if (sharpeRatio >= 1.5m) score += 10;
        else if (sharpeRatio >= 1.0m) score += 7;
        else if (sharpeRatio > 0) score += 3;
        
        // 盈亏比 (10分)
        if (profitFactor >= 3.0m) score += 10;
        else if (profitFactor >= 2.0m) score += 8;
        else if (profitFactor >= 1.5m) score += 6;
        else if (profitFactor >= 1.0m) score += 3;
        
        // 交易次数惩罚
        if (totalTrades < 10) score -= 10;
        else if (totalTrades < 30) score -= 5;
        else if (totalTrades > 1000) score -= 10;
        else if (totalTrades > 300) score -= 5;
        
        return (int)Math.Max(0, Math.Min(100, score));
    }
    
    /// <summary>
    /// 获取质量等级和颜色
    /// </summary>
    private (string level, string color) GetQualityLevel(int score)
    {
        if (score >= 80) return ("优秀", "#4CAF50");
        if (score >= 60) return ("良好", "#8BC34A");
        if (score >= 40) return ("一般", "#FFC107");
        return ("较差", "#F44336");
    }

    /// <summary>
    /// 填充左侧指标列表
    /// </summary>
    private void PopulateIndicatorList()
    {
        var listBox = this.FindControl<ListBox>("IndicatorListBox");
        if (listBox == null) return;

        listBox.Items.Clear();
        
        foreach (var ind in _indicators)
        {
            var item = new ListBoxItem
            {
                Content = CreateIndicatorListItem(ind),
                Tag = ind
            };
            listBox.Items.Add(item);
        }

        // 默认选择第一个指标
        if (_indicators.Count > 0)
        {
            listBox.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// 创建指标列表项UI
    /// </summary>
    private Control CreateIndicatorListItem(IndicatorUsage indicator)
    {
        var panel = new StackPanel { Spacing = 4 };
        
        panel.Children.Add(new TextBlock
        {
            Text = indicator.Name,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
        });
        
        panel.Children.Add(new TextBlock
        {
            Text = $"时间框架: {indicator.Timeframe}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#808080"))
        });
        
        return panel;
    }

    /// <summary>
    /// 指标选择变化事件
    /// </summary>
    private void OnIndicatorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        if (listBox?.SelectedItem is not ListBoxItem item || item.Tag is not IndicatorUsage indicator)
            return;

        _selectedIndicator = indicator;
        ShowParametersForIndicator(indicator);
    }

    /// <summary>
    /// 显示选中指标的参数
    /// </summary>
    private void ShowParametersForIndicator(IndicatorUsage indicator)
    {
        var parameterPanel = this.FindControl<StackPanel>("ParameterPanel");
        var emptyStateText = this.FindControl<TextBlock>("EmptyStateText");
        
        if (parameterPanel == null) return;

        // 清空面板
        parameterPanel.Children.Clear();

        var key = $"{indicator.Name}_{indicator.Timeframe}";
        if (!_indicatorParameters.TryGetValue(key, out var parameters) || parameters.Count == 0)
        {
            // 没有参数的指标
            parameterPanel.Children.Add(new TextBlock
            {
                Text = $"指标 '{indicator.Name}' 没有可配置的参数",
                Foreground = new SolidColorBrush(Color.Parse("#808080")),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        // 隐藏空状态提示
        if (emptyStateText != null)
        {
            emptyStateText.IsVisible = false;
        }

        // 添加指标标题
        parameterPanel.Children.Add(new TextBlock
        {
            Text = $"{indicator.Name} ({indicator.Timeframe})",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            Margin = new Thickness(0, 0, 0, 16)
        });

        // 为每个参数创建输入控件
        foreach (var param in parameters.Values.Where(p => p.Editable))
        {
            var paramControl = CreateParameterControl(indicator, param);
            parameterPanel.Children.Add(paramControl);
        }
    }

    /// <summary>
    /// 创建单个参数的输入控件
    /// </summary>
    private Control CreateParameterControl(IndicatorUsage indicator, IndicatorParameter param)
    {
        var container = new StackPanel { Spacing = 8 };

        // 参数名称和描述
        var headerPanel = new StackPanel { Spacing = 4 };
        
        headerPanel.Children.Add(new TextBlock
        {
            Text = param.DisplayName,
            Classes = { "param-name" }
        });

        if (!string.IsNullOrWhiteSpace(param.Description))
        {
            headerPanel.Children.Add(new TextBlock
            {
                Text = param.Description,
                Classes = { "param-label" }
            });
        }

        container.Children.Add(headerPanel);

        // 输入控件
        var inputControl = CreateInputControl(indicator, param);
        container.Children.Add(inputControl);

        // 显示取值范围
        if (param.Range != null && param.Range.Length >= 2)
        {
            container.Children.Add(new TextBlock
            {
                Text = $"取值范围: {param.Range[0]} - {param.Range[1]}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#666666"))
            });
        }

        return container;
    }

    /// <summary>
    /// 根据参数类型创建输入控件
    /// </summary>
    private Control CreateInputControl(IndicatorUsage indicator, IndicatorParameter param)
    {
        var key = $"{indicator.Name}_{indicator.Timeframe}";
        
        // 获取当前值（已设置的值或默认值）
        var currentValue = indicator.Parameters.ContainsKey(param.Name) 
            ? indicator.Parameters[param.Name]?.ToString() ?? ""
            : param.Default?.ToString() ?? "";

        var textBox = new TextBox
        {
            Text = currentValue,
            Classes = { "param-input" },
            Watermark = param.Default?.ToString() ?? $"输入{param.Type}类型的值"
        };

        // 保存输入框引用，便于后续获取值
        if (!_parameterInputs.ContainsKey(key))
        {
            _parameterInputs[key] = new Dictionary<string, TextBox>();
        }
        _parameterInputs[key][param.Name] = textBox;

        return textBox;
    }

    /// <summary>
    /// 取消按钮点击
    /// </summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        // 通过父窗口关闭对话框
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close(false);
    }

    /// <summary>
    /// 确认激活按钮点击
    /// </summary>
    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        // 收集所有参数值
        var allValid = ValidateAndCollectParameters();
        
        if (!allValid)
        {
            // 显示错误提示
            ShowErrorMessage("请检查参数值是否有效！");
            return;
        }

        // 通过父窗口关闭对话框，返回true表示确认
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close(true);
    }

    /// <summary>
    /// 验证并收集所有参数值
    /// </summary>
    private bool ValidateAndCollectParameters()
    {
        foreach (var indicator in _indicators)
        {
            var key = $"{indicator.Name}_{indicator.Timeframe}";
            
            if (!_parameterInputs.TryGetValue(key, out var inputs))
                continue;

            if (!_indicatorParameters.TryGetValue(key, out var paramDefs))
                continue;

            foreach (var kvp in inputs)
            {
                var paramName = kvp.Key;
                var textBox = kvp.Value;
                var paramDef = paramDefs[paramName];
                
                var value = textBox.Text?.Trim() ?? "";
                
                // 如果为空，使用默认值
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (paramDef.Default != null)
                    {
                        indicator.Parameters[paramName] = paramDef.Default;
                        continue;
                    }
                    else
                    {
                        // 必填参数为空
                        return false;
                    }
                }

                // 验证类型
                try
                {
                    object parsedValue;
                    
                    switch (paramDef.Type)
                    {
                        case "Integer":
                            if (!int.TryParse(value, out var intVal))
                                return false;
                            parsedValue = intVal;
                            
                            // 验证范围
                            if (paramDef.Range != null && paramDef.Range.Length >= 2)
                            {
                                var min = Convert.ToInt32(paramDef.Range[0]);
                                var max = Convert.ToInt32(paramDef.Range[1]);
                                if (intVal < min || intVal > max)
                                    return false;
                            }
                            break;
                            
                        case "Double":
                            if (!double.TryParse(value, out var doubleVal))
                                return false;
                            parsedValue = doubleVal;
                            
                            // 验证范围
                            if (paramDef.Range != null && paramDef.Range.Length >= 2)
                            {
                                var min = Convert.ToDouble(paramDef.Range[0]);
                                var max = Convert.ToDouble(paramDef.Range[1]);
                                if (doubleVal < min || doubleVal > max)
                                    return false;
                            }
                            break;
                            
                        case "Boolean":
                            if (!bool.TryParse(value, out var boolVal))
                                return false;
                            parsedValue = boolVal;
                            break;
                            
                        case "String":
                            parsedValue = value;
                            break;
                            
                        default:
                            parsedValue = value;
                            break;
                    }
                    
                    indicator.Parameters[paramName] = parsedValue;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        return true;
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    private void ShowErrorMessage(string message)
    {
        // TODO: 可以改为更美观的提示方式
        Console.WriteLine($"❌ 错误: {message}");
    }

    /// <summary>
    /// 获取配置好的指标列表
    /// </summary>
    public List<IndicatorUsage> GetConfiguredIndicators()
    {
        return _indicators;
    }
}

