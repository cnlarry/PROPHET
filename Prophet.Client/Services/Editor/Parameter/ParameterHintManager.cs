using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit;
using Prophet.Client.Controls;
using Prophet.Client.Services.Editor.Shared;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services.Editor.Parameter;

/// <summary>
/// 参数提示管理器（版本 2.0 - 基于统一规则系统）
/// </summary>
/// <remarks>
/// <para>
/// 负责在用户输入函数或指标参数时显示参数提示信息。
/// 支持指标参数、数据函数字段参数、子字段参数等多种场景。
/// </para>
/// <para>
/// 支持的上下文：
/// <list type="bullet">
/// <item><description>指标参数: $(5m).MACD(</description></item>
/// <item><description>数据函数字段参数: KLINE(5m).high(</description></item>
/// <item><description>数据函数子字段参数: CHANGE(5m).close(10).value(</description></item>
/// </list>
/// </para>
/// <para>
/// 提示内容包括：
/// <list type="bullet">
/// <item><description>参数名称和类型</description></item>
/// <item><description>默认值</description></item>
/// <item><description>参数描述</description></item>
/// <item><description>参数范围（min/max）（新功能）</description></item>
/// <item><description>当前参数高亮显示</description></item>
/// </list>
/// </para>
/// </remarks>
public class ParameterHintManager
{
    private readonly TextEditor _editor;
    private readonly IntelliSenseService _intelliSense;
    private readonly DSLRulesService _rulesService;

    private Avalonia.Controls.Primitives.Popup? _parameterHintPopup;
    private ParameterHint? _parameterHintContent;
    private int _lastCheckedOffset = -1;

    /// <summary>
    /// 初始化 <see cref="ParameterHintManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    /// <param name="intelliSense">智能提示服务</param>
    /// <param name="rulesService">规则服务，用于获取参数范围等详细信息</param>
    public ParameterHintManager(TextEditor editor, IntelliSenseService intelliSense, DSLRulesService rulesService)
    {
        _editor = editor;
        _intelliSense = intelliSense;
        _rulesService = rulesService;
    }

    /// <summary>
    /// 初始化参数提示功能
    /// </summary>
    /// <remarks>
    /// 注册文本输入和光标移动事件，在用户输入时自动显示参数提示。
    /// </remarks>
    public void Initialize()
    {
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _editor.TextArea.TextEntered += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var offset = _editor.CaretOffset;
                // 防抖：如果偏移量没有变化，跳过检查
                if (offset == _lastCheckedOffset)
                    return;
                
                _lastCheckedOffset = offset;
                CheckAndShowParameterHint(offset);
            }, Avalonia.Threading.DispatcherPriority.Input);
        };

        _editor.TextArea.TextView.ScrollOffsetChanged += OnScrollOffsetChanged;
    }

    /// <summary>
    /// 关闭参数提示（供外部调用）
    /// </summary>
    public void Close()
    {
        CloseParameterHint();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        try
        {
            var offset = _editor.CaretOffset;
            // 防抖：如果偏移量没有变化，跳过检查
            if (offset == _lastCheckedOffset)
                return;
            
            _lastCheckedOffset = offset;
            CheckAndShowParameterHint(offset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ParameterHintManager] Caret position error: {ex.Message}");
        }
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_parameterHintPopup != null && _parameterHintPopup.IsOpen)
                {
                    var caretPos = _editor.TextArea.Caret.Position;
                    var visualPos = _editor.TextArea.TextView.GetVisualPosition(caretPos, AvaloniaEdit.Rendering.VisualYPosition.LineBottom);

                    if (!double.IsNaN(visualPos.X) && !double.IsNaN(visualPos.Y))
                    {
                        var scrollOffset = _editor.TextArea.TextView.ScrollOffset;
                        var relativeX = visualPos.X;
                        var relativeY = visualPos.Y - scrollOffset.Y;
                        var placementRect = new Avalonia.Rect(relativeX, relativeY, 1, 1);
                        _parameterHintPopup.PlacementRect = placementRect;
                    }
                    else
                    {
                        CloseParameterHint();
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ParameterHintManager] Scroll error: {ex.Message}");
        }
    }

    private void CheckAndShowParameterHint(int offset)
    {
        var document = _editor.Document;
        if (offset < 0 || offset >= document.TextLength)
        {
            CloseParameterHint();
            return;
        }

        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, offset - line.Offset);

        var parameterInfo = ParseParameterContext(lineText);
        if (parameterInfo.HasValue)
        {
            var (type, name, fieldName, isSubField, currentParamIndex, description, parentFieldName) = parameterInfo.Value;
            ShowParameterHint(type, name, fieldName, isSubField, currentParamIndex, description, parentFieldName, offset);
        }
        else
        {
            CloseParameterHint();
        }
    }

    private (ParameterContextType type, string name, string? fieldName, bool isSubField, int paramIndex, string? description, string? parentFieldName)? ParseParameterContext(string lineText)
    {
        // 1. 指标参数: $(5m).MACD(
        var indicatorMatch = Regex.Match(lineText, @"\$\(([^)]+)\)\.(\w+)\(([^)]*)$");
        if (indicatorMatch.Success)
        {
            var indicatorName = indicatorMatch.Groups[2].Value;
            var paramsStr = indicatorMatch.Groups[3].Value;
            var currentParamIndex = CountIndicatorParameterIndex(paramsStr);

            return (ParameterContextType.Indicator, indicatorName, null, false, currentParamIndex, null, null);
        }

        // 2. 时间序列函数方法参数: FEARGREED().method(
        var timeSeriesMethodMatch = Regex.Match(lineText,
            @"\b([A-Z][A-Z0-9_]*)\(\)\.(\w+)\(([^)]*)$");
        if (timeSeriesMethodMatch.Success && _rulesService.IsValidTimeSeriesFunction(timeSeriesMethodMatch.Groups[1].Value))
        {
            var functionName = timeSeriesMethodMatch.Groups[1].Value;
            var methodName = timeSeriesMethodMatch.Groups[2].Value;
            var paramsStr = timeSeriesMethodMatch.Groups[3].Value;
            var currentParamIndex = CountCommas(paramsStr);
            
            // 获取方法的参数信息
            var function = _rulesService.GetTimeSeriesFunction(functionName);
            var method = function?.Methods?.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            var description = method?.Description;

            return (ParameterContextType.DataFunctionField, functionName, methodName, false, currentParamIndex, description, null);
        }

        // 3. 数据函数方法参数: Func(5m).method(
        var dataFuncMethodMatch = Regex.Match(lineText,
            @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)\(([^)]*)$");
        if (dataFuncMethodMatch.Success && _rulesService.IsValidDataFunction(dataFuncMethodMatch.Groups[1].Value))
        {
            var functionName = dataFuncMethodMatch.Groups[1].Value;
            var methodName = dataFuncMethodMatch.Groups[2].Value;
            var paramsStr = dataFuncMethodMatch.Groups[3].Value;
            var currentParamIndex = CountCommas(paramsStr);
            var description = GetFieldParameterDescription(functionName, methodName, false);

            return (ParameterContextType.DataFunctionField, functionName, methodName, false, currentParamIndex, description, null);
        }

        // 4. 数据函数子方法参数: Func(5m).method(10).submethod(
        var subMethodMatch = Regex.Match(lineText,
            @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)\([^)]*\)\.(\w+)\(([^)]*)$");
        if (subMethodMatch.Success && _rulesService.IsValidDataFunction(subMethodMatch.Groups[1].Value))
        {
            var functionName = subMethodMatch.Groups[1].Value;
            var parentMethodName = subMethodMatch.Groups[2].Value; // 父方法名
            var subMethodName = subMethodMatch.Groups[3].Value; // 子方法名
            var paramsStr = subMethodMatch.Groups[4].Value;
            var currentParamIndex = CountCommas(paramsStr);
            
            // 获取子方法的参数信息
            var function = _rulesService.GetDataFunction(functionName);
            var parentMethod = function?.Methods?.FirstOrDefault(m => m.Name.Equals(parentMethodName, StringComparison.OrdinalIgnoreCase));
            var subMethod = parentMethod?.SubMethods?.FirstOrDefault(m => m.Name.Equals(subMethodName, StringComparison.OrdinalIgnoreCase));
            var description = subMethod?.Description;

            return (ParameterContextType.DataFunctionSubField, functionName, subMethodName, true, currentParamIndex, description, parentMethodName);
        }

        return null;
    }

    private void ShowParameterHint(ParameterContextType type, string name, string? fieldName, bool isSubField, int currentParamIndex, string? description, string? parentFieldName, int offset)
    {
        try
        {
            List<ParameterInfo>? parameters = null;
            string displayName;

            if (type == ParameterContextType.Indicator)
            {
                parameters = GetIndicatorParameters(name);
                displayName = $"{name}(...)";
            }
            else
            {
                parameters = GetFieldParameters(name, fieldName!, isSubField, parentFieldName);
                displayName = isSubField && !string.IsNullOrEmpty(parentFieldName)
                    ? $"{name}(...).{parentFieldName}(...).{fieldName}"
                    : $"{name}(...).{fieldName}";
            }

            if (parameters == null || parameters.Count == 0)
            {
                CloseParameterHint();
                return;
            }

            currentParamIndex = Math.Max(0, Math.Min(currentParamIndex, parameters.Count - 1));

            if (_parameterHintContent == null)
            {
                _parameterHintContent = new ParameterHint();
            }

            _parameterHintContent.SetParameters(displayName, parameters, currentParamIndex, description);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var caretPos = _editor.TextArea.Caret.Position;
                    var visualPos = _editor.TextArea.TextView.GetVisualPosition(caretPos, AvaloniaEdit.Rendering.VisualYPosition.LineBottom);

                    if (double.IsNaN(visualPos.X) || double.IsNaN(visualPos.Y))
                        return;

                    var scrollOffset = _editor.TextArea.TextView.ScrollOffset;
                    var relativeX = visualPos.X;
                    var relativeY = visualPos.Y - scrollOffset.Y;
                    var placementRect = new Avalonia.Rect(relativeX, relativeY, 1, 1);

                    if (_parameterHintPopup == null)
                    {
                        _parameterHintPopup = new Avalonia.Controls.Primitives.Popup
                        {
                            Child = _parameterHintContent,
                            PlacementTarget = _editor.TextArea.TextView,
                            PlacementRect = placementRect,
                            Placement = Avalonia.Controls.PlacementMode.BottomEdgeAlignedLeft,
                            IsLightDismissEnabled = true,
                            Focusable = false
                        };
                    }
                    else
                    {
                        _parameterHintPopup.PlacementRect = placementRect;
                    }

                    if (!_parameterHintPopup.IsOpen)
                    {
                        _parameterHintPopup.IsOpen = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ParameterHintManager] Show error: {ex.Message}");
                }
            }, Avalonia.Threading.DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ParameterHintManager] Show error: {ex.Message}");
            CloseParameterHint();
        }
    }

    private void CloseParameterHint()
    {
        if (_parameterHintPopup != null)
        {
            _parameterHintPopup.IsOpen = false;
        }
    }

    private List<ParameterInfo>? GetIndicatorParameters(string indicatorName)
    {
        var indicator = _rulesService.GetIndicator(indicatorName);
        if (indicator == null || indicator.Parameters == null || indicator.Parameters.Count == 0)
            return null;

        var parameters = new List<ParameterInfo>();

        foreach (var param in indicator.Parameters)
        {
            var paramName = param.Name;
            var prefix = $"{indicatorName}_";
            if (paramName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                paramName = paramName.Substring(prefix.Length);
            }

            parameters.Add(new ParameterInfo
            {
                Name = paramName,
                Type = param.Type,
                DefaultValue = param.Default?.ToString(),
                Description = param.Description
            });
        }

        return parameters;
    }

    private List<ParameterInfo>? GetFieldParameters(string functionName, string fieldName, bool isSubField, string? parentFieldName = null)
    {
        // 检查是否是时间序列函数
        if (_rulesService.IsValidTimeSeriesFunction(functionName))
        {
            var timeSeriesFunction = _rulesService.GetTimeSeriesFunction(functionName);
            if (timeSeriesFunction == null)
                return null;

            // 时间序列函数方法参数：FEARGREED().method(
            var method = timeSeriesFunction.Methods?.FirstOrDefault(m => m.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (method?.Parameters != null && method.Parameters.Any())
            {
                var parameters = new List<ParameterInfo>();
                foreach (var param in method.Parameters)
                {
                    var description = param.Description ?? "";
                    if (param.Min.HasValue || param.Max.HasValue)
                    {
                        var range = $" [{param.Min ?? 0}..{param.Max ?? int.MaxValue}]";
                        description = description + range;
                    }
                    if (param.Default != null)
                    {
                        description = (string.IsNullOrEmpty(description) ? "" : description + " ") + $"默认值: {param.Default}";
                    }
                    
                    parameters.Add(new ParameterInfo
                    {
                        Name = param.Name,
                        Type = param.Type,
                        DefaultValue = param.Default?.ToString(),
                        Description = description
                    });
                }
                return parameters;
            }
            return null;
        }
        
        var dataFunction = _rulesService.GetDataFunction(functionName);
        if (dataFunction == null)
            return null;

        if (isSubField && !string.IsNullOrEmpty(parentFieldName))
        {
            // 子方法参数：Func(5m).method(10).submethod(
            var parentMethod = dataFunction.Methods?.FirstOrDefault(m => m.Name.Equals(parentFieldName, StringComparison.OrdinalIgnoreCase));
            var subMethod = parentMethod?.SubMethods?.FirstOrDefault(m => m.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            
            if (subMethod?.Parameters != null && subMethod.Parameters.Any())
            {
                var parameters = new List<ParameterInfo>();
                foreach (var param in subMethod.Parameters)
                {
                    var description = param.Description ?? "";
                    if (param.Min.HasValue || param.Max.HasValue)
                    {
                        var range = $" [{param.Min ?? 0}..{param.Max ?? int.MaxValue}]";
                        description = description + range;
                    }
                    if (param.Default != null)
                    {
                        description = (string.IsNullOrEmpty(description) ? "" : description + " ") + $"默认值: {param.Default}";
                    }
                    
                    parameters.Add(new ParameterInfo
                    {
                        Name = param.Name,
                        Type = param.Type,
                        DefaultValue = param.Default?.ToString(),
                        Description = description
                    });
                }
                return parameters;
            }
        }
        else
        {
            // 方法参数：Func(5m).method(
            var method = dataFunction.Methods?.FirstOrDefault(m => m.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (method?.Parameters != null && method.Parameters.Any())
            {
                var parameters = new List<ParameterInfo>();
                foreach (var param in method.Parameters)
                {
                    var description = param.Description ?? "";
                    if (param.Min.HasValue || param.Max.HasValue)
                    {
                        var range = $" [{param.Min ?? 0}..{param.Max ?? int.MaxValue}]";
                        description = description + range;
                    }
                    if (param.Default != null)
                    {
                        description = (string.IsNullOrEmpty(description) ? "" : description + " ") + $"默认值: {param.Default}";
                    }
                    
                    parameters.Add(new ParameterInfo
                    {
                        Name = param.Name,
                        Type = param.Type,
                        DefaultValue = param.Default?.ToString(),
                        Description = description
                    });
                }
                return parameters;
            }
        }

        return null;
    }

    private List<ParameterInfo> ParseParameterTemplate(string? template)
    {
        var parameters = new List<ParameterInfo>();

        if (string.IsNullOrEmpty(template))
            return parameters;

        var trimmed = template.Trim();
        if (!trimmed.StartsWith("(") || !trimmed.EndsWith(")"))
            return parameters;

        var content = trimmed.Substring(1, trimmed.Length - 2).Trim();
        var placeholderPattern = @"\$\{(\d+):([^}]+)\}";
        var matches = Regex.Matches(content, placeholderPattern);

        if (matches.Count > 0)
        {
            foreach (Match match in matches)
            {
                var paramIndex = int.Parse(match.Groups[1].Value);
                var defaultValue = match.Groups[2].Value.Trim();

                parameters.Add(new ParameterInfo
                {
                    Name = $"param{paramIndex}",
                    Type = "Integer",
                    DefaultValue = defaultValue == "0" ? null : defaultValue,
                    Description = GetParameterDescription(paramIndex)
                });
            }
        }

        return parameters;
    }

    private string? GetFieldParameterDescription(string functionName, string fieldName, bool isSubField)
    {
        // 数据函数字段参数描述暂不支持（需要在规则文件中定义）
        return null;
    }

    private string GetParameterDescription(int paramIndex)
    {
        return paramIndex switch
        {
            1 => "偏移量(0=当前，-1=上一根，范围[-100,0])",
            2 => "周期数",
            _ => "参数"
        };
    }

    private int CountIndicatorParameterIndex(string paramsStr)
    {
        if (string.IsNullOrWhiteSpace(paramsStr))
            return 0;

        return CountCommas(paramsStr.Trim());
    }

    private int CountCommas(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        int commaCount = 0;
        bool inString = false;
        foreach (var ch in text.Trim())
        {
            if (ch == '"')
                inString = !inString;
            else if (ch == ',' && !inString)
                commaCount++;
        }

        return commaCount;
    }
}

