using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Prophet.Client.Core;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services;

/// <summary>
/// DSL代码解析器 - 用于提取指标调用和时间框架
/// </summary>
public class DSLParser
{
    private readonly IntelliSenseService _intelliSense;

    public DSLParser()
    {
        _intelliSense = ServiceContainer.GetService<IntelliSenseService>();
    }

    /// <summary>
    /// 解析DSL代码，提取所有指标调用
    /// </summary>
    public List<IndicatorUsage> ExtractIndicators(string dslCode)
    {
        var indicators = new Dictionary<string, IndicatorUsage>();

        // 匹配新语法：$(timeframe).INDICATOR(params).field 或 $(timeframe).INDICATOR.field
        var pattern = @"\$\(([^)]+)\)\.(\w+)(?:\(([^)]*)\))?\.(\w+)";
        var matches = Regex.Matches(dslCode, pattern);

        foreach (Match match in matches)
        {
            var timeframe = match.Groups[1].Value.Trim();
            var indicatorName = match.Groups[2].Value;
            var paramsStr = match.Groups[3].Value.Trim();
            var field = match.Groups[4].Value;

            // 验证是否是有效的指标
            if (!_intelliSense.IsValidIndicator(indicatorName))
                continue;

            // 验证是否是有效的时间框架
            if (!_intelliSense.IsValidTimeframe(timeframe))
                continue;

            // 创建唯一键：指标名称+时间框架
            var key = $"{indicatorName}_{timeframe}";

            if (!indicators.ContainsKey(key))
            {
                indicators[key] = new IndicatorUsage
                {
                    Name = indicatorName,
                    Timeframe = timeframe,
                    Parameters = new Dictionary<string, object>(),
                    Fields = new HashSet<string>()
                };
            }

            // 添加访问的字段
            indicators[key].Fields.Add(field);

            // 解析参数（如果有）
            if (!string.IsNullOrWhiteSpace(paramsStr))
            {
                ParsePositionalParameters(paramsStr, indicatorName, indicators[key]);
            }
        }

        // 查找参数赋值语句：$(timeframe).INDICATOR.PARAM_NAME = value
        var paramAssignmentPattern = @"\$\(([^)]+)\)\.(\w+)\.([A-Z][A-Z0-9_]*)\s*=\s*([^;,\n]+)";
        var paramMatches = Regex.Matches(dslCode, paramAssignmentPattern);

        foreach (Match match in paramMatches)
        {
            var timeframe = match.Groups[1].Value.Trim();
            var indicatorName = match.Groups[2].Value;
            var paramName = match.Groups[3].Value;
            var paramValue = match.Groups[4].Value.Trim();

            if (!_intelliSense.IsValidIndicator(indicatorName))
                continue;

            if (!_intelliSense.IsValidTimeframe(timeframe))
                continue;

            var key = $"{indicatorName}_{timeframe}";

            if (!indicators.ContainsKey(key))
            {
                indicators[key] = new IndicatorUsage
                {
                    Name = indicatorName,
                    Timeframe = timeframe,
                    Parameters = new Dictionary<string, object>(),
                    Fields = new HashSet<string>()
                };
            }

            // 尝试解析参数值
            object parsedValue = paramValue;
            if (double.TryParse(paramValue, out var doubleVal))
            {
                parsedValue = doubleVal;
            }
            else if (int.TryParse(paramValue, out var intVal))
            {
                parsedValue = intVal;
            }
            else if (bool.TryParse(paramValue, out var boolVal))
            {
                parsedValue = boolVal;
            }
            else if (paramValue.StartsWith("\"") && paramValue.EndsWith("\""))
            {
                parsedValue = paramValue.Trim('"');
            }

            indicators[key].Parameters[paramName] = parsedValue;
        }

        return indicators.Values.ToList();
    }

    /// <summary>
    /// 解析位置参数（如 MACD(12,26,9)）
    /// </summary>
    private void ParsePositionalParameters(string paramsStr, string indicatorName, IndicatorUsage usage)
    {
        var paramValues = paramsStr.Split(',').Select(p => p.Trim()).ToList();
        
        // 从DSLRulesService获取指标定义
        var rulesService = ServiceContainer.GetService<DSLRulesService>();
        var indicatorDef = rulesService.GetIndicator(indicatorName);
        if (indicatorDef?.Parameters == null)
            return;

        // 按顺序匹配参数
        for (int i = 0; i < Math.Min(paramValues.Count, indicatorDef.Parameters.Count); i++)
        {
            var paramDef = indicatorDef.Parameters[i];
            var paramValue = paramValues[i];

            object parsedValue = paramValue;
            
            // 根据参数类型解析
            if (paramDef.Type == "Integer" && int.TryParse(paramValue, out var intVal))
            {
                parsedValue = intVal;
            }
            else if (paramDef.Type == "Double" && double.TryParse(paramValue, out var doubleVal))
            {
                parsedValue = doubleVal;
            }
            else if (paramDef.Type == "Boolean" && bool.TryParse(paramValue, out var boolVal))
            {
                parsedValue = boolVal;
            }

            usage.Parameters[paramDef.Name] = parsedValue;
        }
    }

    /// <summary>
    /// 提取所有使用的时间框架
    /// </summary>
    public List<string> ExtractTimeframes(string dslCode)
    {
        var timeframes = new HashSet<string>();
        
        // 匹配 $(timeframe) 模式
        var pattern = @"\$\(([^)]+)\)";
        var matches = Regex.Matches(dslCode, pattern);

        foreach (Match match in matches)
        {
            var timeframe = match.Groups[1].Value.Trim();
            if (_intelliSense.IsValidTimeframe(timeframe))
            {
                timeframes.Add(timeframe);
            }
        }

        return timeframes.ToList();
    }

    /// <summary>
    /// 获取指标的完整参数信息（包括默认值）- 使用统一规则系统
    /// </summary>
    public Dictionary<string, IndicatorParameter> GetIndicatorParameters(string indicatorName)
    {
        var result = new Dictionary<string, IndicatorParameter>();
        
        var rulesService = ServiceContainer.GetService<DSLRulesService>();
        var indicatorDef = rulesService.GetIndicator(indicatorName);
        if (indicatorDef?.Parameters == null)
            return result;

        foreach (var param in indicatorDef.Parameters)
        {
            // 将新的 ParameterDefinition 转换为旧的 IndicatorParameter
            result[param.Name] = new IndicatorParameter
            {
                Name = param.Name,
                DisplayName = param.Name, // 使用参数名作为显示名
                Type = param.Type,
                Default = param.Default,
                Description = param.Description,
                Range = param.Min.HasValue && param.Max.HasValue 
                    ? new[] { (double)param.Min.Value, (double)param.Max.Value }
                    : null,
                Editable = true
            };
        }

        return result;
    }
}

/// <summary>
/// 指标使用情况
/// </summary>
public class IndicatorUsage
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 时间框架
    /// </summary>
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>
    /// 已设置的参数（参数名 -> 参数值）
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 访问的字段列表
    /// </summary>
    public HashSet<string> Fields { get; set; } = new();

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName => $"{Name} ({Timeframe})";
}

/// <summary>
/// 指标参数信息（用于UI显示和编辑）
/// </summary>
public class IndicatorParameter
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 参数类型（integer, double, boolean, enum:xxx）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 默认值
    /// </summary>
    public object? Default { get; set; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 取值范围 [min, max]（仅适用于数值类型）
    /// </summary>
    public double[]? Range { get; set; }

    /// <summary>
    /// 是否可编辑
    /// </summary>
    public bool Editable { get; set; } = true;
}

