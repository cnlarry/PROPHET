using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Services.Editor.Shared;
using Prophet.Client.Services.Editor.Snippets;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services.Editor.Completion;

/// <summary>
/// 补全项提供器
/// </summary>
/// <remarks>
/// <para>
/// 根据上下文分析结果提供相应的补全项。是补全系统的数据源。
/// </para>
/// <para>
/// 提供的补全项类型：
/// <list type="bullet">
/// <item><description>指标补全：MACD, RSI, BOLL 等技术指标</description></item>
/// <item><description>时间框架补全：1m, 5m, 15m, 1h, 4h, 1d</description></item>
/// <item><description>字段补全：open, close, high, low, volume</description></item>
/// <item><description>函数补全：KLINE, CHANGE, CROSSOVER 等数据函数</description></item>
/// <item><description>环境变量补全：@symbol, @timeframe, @position_size</description></item>
/// <item><description>运算符补全：+, -, *, /, ==, !=, &gt;, &lt;</description></item>
/// <item><description>代码片段：预定义的代码模板</description></item>
/// </list>
/// </para>
/// </remarks>
public class CompletionItemProvider
{
    public const string RulesNotReadyTag = "__RULES_NOT_READY__";

    private readonly IntelliSenseService _intelliSense;
    private readonly SnippetProvider _snippetProvider;
    private readonly DSLRulesService _rulesService;
    private string _currentCode = string.Empty;

    /// <summary>
    /// 初始化 <see cref="CompletionItemProvider"/> 类的新实例
    /// </summary>
    /// <param name="intelliSense">智能提示服务</param>
    /// <param name="snippetProvider">代码片段提供器</param>
    /// <param name="rulesService">DSL 规则服务</param>
    public CompletionItemProvider(IntelliSenseService intelliSense, SnippetProvider snippetProvider, DSLRulesService rulesService)
    {
        _intelliSense = intelliSense;
        _snippetProvider = snippetProvider;
        _rulesService = rulesService;
    }
    
    /// <summary>
    /// 设置当前代码（用于提取自定义函数）
    /// </summary>
    public void SetCurrentCode(string code)
    {
        _currentCode = code;
    }

    /// <summary>
    /// 根据上下文获取补全项
    /// </summary>
    /// <param name="context">补全上下文信息</param>
    /// <param name="includeSnippets">是否包含代码片段，默认为 true</param>
    /// <returns>返回与上下文匹配的补全项列表</returns>
    /// <remarks>
    /// 根据 <see cref="CompletionContext.Type"/> 选择相应的补全策略：
    /// <list type="bullet">
    /// <item><description>Indicator: 技术指标</description></item>
    /// <item><description>Timeframe: 时间框架</description></item>
    /// <item><description>IndicatorField: 指标字段</description></item>
    /// <item><description>IndicatorParameter: 指标参数</description></item>
    /// <item><description>FieldValue: 字段值</description></item>
    /// <item><description>DataFunction: 数据函数</description></item>
    /// <item><description>Environment: 环境变量</description></item>
    /// <item><description>Operator: 运算符</description></item>
    /// </list>
    /// </remarks>
    public List<CompletionItem> GetCompletionsForContext(CompletionContext context, bool includeSnippets = true)
    {
        var items = new List<CompletionItem>();

        if (!_rulesService.IsReady)
        {
            items.Add(new CompletionItem
            {
                Label = "⚠ DSL规则未加载",
                DisplayText = "⚠ DSL规则未加载（补全已降级）",
                Kind = CompletionKind.Keyword,
                Detail = "补全已降级",
                Documentation = _rulesService.LoadError ?? "未知原因",
                Description = _rulesService.RulesSource != null ? $"来源: {_rulesService.RulesSource}" : null,
                InsertText = string.Empty,
                Category = "系统",
                Tag = RulesNotReadyTag
            });
        }

        switch (context.Type)
        {
            case CompletionContextType.Indicator:
                items.AddRange(GetIndicatorCompletions(context));
                break;

            case CompletionContextType.Timeframe:
                items.AddRange(GetTimeframeCompletions(context));
                break;

            case CompletionContextType.IndicatorField:
                items.AddRange(GetIndicatorFieldCompletions(context));
                break;

            case CompletionContextType.IndicatorParameter:
                items.AddRange(GetIndicatorParameterCompletions(context));
                break;

            case CompletionContextType.FieldValue:
                items.AddRange(GetFieldValueCompletions(context));
                break;

            case CompletionContextType.DataFunctionFieldValue:
                items.AddRange(GetDataFunctionFieldValueCompletions(context));
                break;

            case CompletionContextType.ThreeLevelFieldValue:
                items.AddRange(GetThreeLevelFieldValueCompletions(context));
                break;

            case CompletionContextType.TimeSeriesFunctionMethodValue:
                items.AddRange(GetTimeSeriesFunctionMethodValueCompletions(context));
                break;

            case CompletionContextType.EnvVariable:
                items.AddRange(_intelliSense.GetEnvVariableCompletions());
                break;

            case CompletionContextType.VariableTypeDeclaration:
                items.AddRange(_intelliSense.GetVariableTypeCompletions());
                break;

            case CompletionContextType.DataFunctionField:
                // 检查是否是时间序列函数
                if (!string.IsNullOrEmpty(context.FunctionName) && 
                    _intelliSense.IsValidTimeSeriesFunction(context.FunctionName))
                {
                    items.AddRange(GetTimeSeriesFunctionMethodCompletions(context));
                }
                else
                {
                items.AddRange(GetDataFunctionFieldCompletions(context));
                }
                break;

            case CompletionContextType.SignalFunctionOrCustom:
                items.AddRange(GetSignalFunctionOrCustomCompletions(context));
                break;

            case CompletionContextType.InsideSignalFunction:
                // 信号函数内部：只显示数据函数、数学函数、时间序列函数，不显示信号函数（不支持嵌套）
                items.AddRange(GetFunctionCompletionsForExpression(context));
                break;

            case CompletionContextType.ChainedMethodCall:
                items.AddRange(GetChainedMethodCompletions(context));
                break;

            case CompletionContextType.ThreeLevelChainedCall:
                items.AddRange(GetThreeLevelChainedCompletions(context));
                break;

            case CompletionContextType.ChainedMethodParam:
                // 检查是否是时间序列函数
                if (!string.IsNullOrEmpty(context.FunctionName) && 
                    _intelliSense.IsValidTimeSeriesFunction(context.FunctionName))
                {
                    items.AddRange(GetTimeSeriesFunctionMethodParameterCompletions(context));
                }
                else
                {
                    items.AddRange(GetDataFunctionMethodParameterCompletions(context));
                }
                break;

            case CompletionContextType.SignalValue:
                items.AddRange(_intelliSense.GetSignalCompletions());
                break;

            case CompletionContextType.Operator:
                items.AddRange(GetOperatorCompletions());
                break;

            case CompletionContextType.General:
                // 通用上下文：只显示数据函数、数学函数、时间序列函数
                items.AddRange(GetFunctionCompletionsForExpression(context));
                break;
        }

        return items;
    }

    #region 私有获取方法

    /// <summary>
    /// 获取指标补全
    /// </summary>
    private IEnumerable<CompletionItem> GetIndicatorCompletions(CompletionContext context)
    {
        return _intelliSense.GetIndicatorCompletions()
            .Select(item => new CompletionItem
            {
                Label = item.Label,
                DisplayText = item.DisplayText,
                Description = item.Description,
                InsertText = item.InsertText,
                Kind = item.Kind,
                Category = item.Category
            });
    }

    /// <summary>
    /// 获取时间框架补全
    /// </summary>
    private IEnumerable<CompletionItem> GetTimeframeCompletions(CompletionContext context)
    {
        return _intelliSense.GetTimeframeCompletions();
    }

    /// <summary>
    /// 获取指标字段补全
    /// </summary>
    private IEnumerable<CompletionItem> GetIndicatorFieldCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.IndicatorName))
            return Enumerable.Empty<CompletionItem>();

        var allFields = _intelliSense.GetIndicatorFieldCompletions(context.IndicatorName).ToList();
        
        // 如果在函数体外，只显示可赋值的参数字段（过滤掉只读字段）
        if (!context.IsInsideFunctionBody)
        {
            allFields = allFields.Where(field => !field.IsReadOnly).ToList();
        }
        
        return allFields;
    }

    /// <summary>
    /// 获取指标参数补全
    /// </summary>
    private IEnumerable<CompletionItem> GetIndicatorParameterCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.IndicatorName))
            return Enumerable.Empty<CompletionItem>();

        var allParams = _intelliSense.GetIndicatorFieldCompletions(context.IndicatorName)
            .Where(item => item.Kind == CompletionKind.Parameter)
            .ToList();
        
        // 过滤已输入的参数
        if (context.Tag is HashSet<string> enteredParams && enteredParams.Count > 0)
        {
            allParams = allParams.Where(param => !enteredParams.Contains(param.Label)).ToList();
        }
        
        return allParams;
    }

    /// <summary>
    /// 获取字段值补全
    /// </summary>
    private IEnumerable<CompletionItem> GetFieldValueCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.IndicatorName) || string.IsNullOrEmpty(context.FieldName))
        {
            return Enumerable.Empty<CompletionItem>();
        }

        var items = _intelliSense.GetFieldValueCompletions(context.IndicatorName, context.FieldName).ToList();
        return items;
    }

    /// <summary>
    /// 获取数据函数字段值补全
    /// </summary>
    private IEnumerable<CompletionItem> GetDataFunctionFieldValueCompletions(CompletionContext context)
    {
        // 提供通用的布尔值补全
        return new[]
        {
            new CompletionItem
            {
                Label = "true",
                DisplayText = "true",
                Description = "布尔值：真",
                InsertText = "true",
                Kind = CompletionKind.Value
            },
            new CompletionItem
            {
                Label = "false",
                DisplayText = "false",
                Description = "布尔值：假",
                InsertText = "false",
                Kind = CompletionKind.Value
            }
        };
    }

    /// <summary>
    /// 获取三级字段值补全
    /// </summary>
    private IEnumerable<CompletionItem> GetThreeLevelFieldValueCompletions(CompletionContext context)
    {
        // 提供通用的布尔值补全
        return new[]
        {
            new CompletionItem
            {
                Label = "true",
                DisplayText = "true",
                Description = "布尔值：真",
                InsertText = "true",
                Kind = CompletionKind.Value
            },
            new CompletionItem
            {
                Label = "false",
                DisplayText = "false",
                Description = "布尔值：假",
                InsertText = "false",
                Kind = CompletionKind.Value
            }
        };
    }

    /// <summary>
    /// 获取时间序列函数方法返回值枚举值补全
    /// </summary>
    private IEnumerable<CompletionItem> GetTimeSeriesFunctionMethodValueCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.FunctionName) || string.IsNullOrEmpty(context.FieldName))
        {
            return Enumerable.Empty<CompletionItem>();
        }

        var items = _intelliSense.GetTimeSeriesFunctionMethodValueCompletions(context.FunctionName, context.FieldName).ToList();
        return items;
    }

    /// <summary>
    /// 获取数据函数字段补全
    /// </summary>
    private IEnumerable<CompletionItem> GetDataFunctionFieldCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.FunctionName))
            return Enumerable.Empty<CompletionItem>();

        // 先尝试从数据函数中获取字段
        var dataFuncFields = _intelliSense.GetDataFunctionFieldCompletions(context.FunctionName).ToList();
        if (dataFuncFields.Count > 0)
            return dataFuncFields;
        
        // 如果没有找到，尝试从SMC函数中获取字段
        return _intelliSense.GetSmcFunctionFieldCompletions(context.FunctionName);
    }

    /// <summary>
    /// 获取信号函数或自定义函数补全（仅在根部，不在信号函数内）
    /// </summary>
    private IEnumerable<CompletionItem> GetSignalFunctionOrCustomCompletions(CompletionContext context)
    {
        var items = new List<CompletionItem>();
        
        // 添加信号函数（仅在根部，不在信号函数内）
        items.AddRange(_intelliSense.GetSignalFunctionCompletions());
        
        // 添加自定义函数模板
        if (string.IsNullOrEmpty(context.FilterPrefix) || 
            context.FilterPrefix.StartsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            var functionTemplates = new[]
            {
                ("Double", "浮点函数（最常用）"),
                ("Integer", "整数函数"),
                ("Boolean", "布尔函数"),
                ("String", "字符串函数"),
                ("Enum", "枚举函数")
            };
            
            foreach (var (returnType, description) in functionTemplates)
            {
                items.Add(new CompletionItem
                {
                    Label = $"funcName(param: {returnType}): {returnType} {{}}",
                    DisplayText = $"funcName(...): {returnType} {{}}",
                    Description = $"定义{description}",
                    InsertText = $"funcName(param: {returnType}): {returnType} {{\n    \n}}",
                    Kind = CompletionKind.Snippet,
                    Category = "自定义函数"
                });
            }
        }
        
        return items;
    }

    /// <summary>
    /// 获取表达式中的函数补全（数据函数、数学函数、时间序列函数、自定义函数，不包含信号函数）
    /// </summary>
    private IEnumerable<CompletionItem> GetFunctionCompletionsForExpression(CompletionContext context)
    {
        var items = new List<CompletionItem>();
        
        // 数据函数
        items.AddRange(_intelliSense.GetDataFunctionCompletions());
        
        // 数学函数
        items.AddRange(_intelliSense.GetMathFunctionCompletions());
        
        // 时间序列函数
        items.AddRange(_intelliSense.GetTimeSeriesFunctionCompletions());
        
        // 自定义函数（从当前代码中提取）
        items.AddRange(_intelliSense.GetCustomFunctionCompletions(_currentCode));
        
        return items;
    }

    /// <summary>
    /// 获取链式方法补全
    /// </summary>
    private IEnumerable<CompletionItem> GetChainedMethodCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.FunctionName))
            return Enumerable.Empty<CompletionItem>();

        // 先尝试从IntelliSenseService获取
        var dataFunctionFields = _intelliSense.GetDataFunctionFieldCompletions(context.FunctionName);
        if (dataFunctionFields.Any())
            return dataFunctionFields;
        
        var smcFunctionFields = _intelliSense.GetSmcFunctionFieldCompletions(context.FunctionName);
        if (smcFunctionFields.Any())
            return smcFunctionFields;
        
        return Enumerable.Empty<CompletionItem>();
    }

    /// <summary>
    /// 获取数据函数方法参数补全
    /// </summary>
    private IEnumerable<CompletionItem> GetDataFunctionMethodParameterCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.FunctionName) || string.IsNullOrEmpty(context.FieldName))
            return Enumerable.Empty<CompletionItem>();

        // 通过 IntelliSenseService 获取数据函数定义
        var function = _intelliSense.GetDataFunction(context.FunctionName);
        if (function == null)
            return Enumerable.Empty<CompletionItem>();

        // 检查是否是子方法参数
        if (!string.IsNullOrEmpty(context.ParentFieldName))
        {
            // 子方法参数：Func(5m).method(10).submethod(
            var method = function.Methods?.FirstOrDefault(m => m.Name.Equals(context.ParentFieldName, StringComparison.OrdinalIgnoreCase));
            if (method?.SubMethods != null)
            {
                var subMethod = method.SubMethods.FirstOrDefault(m => m.Name.Equals(context.FieldName, StringComparison.OrdinalIgnoreCase));
                if (subMethod?.Parameters != null && subMethod.Parameters.Any())
                {
                    var parameters = subMethod.Parameters;
                    var enteredParams = context.Tag as HashSet<string> ?? new HashSet<string>();
                    
                    return parameters
                        .Where(p => !enteredParams.Contains(p.Name))
                        .Select(p => new CompletionItem
                        {
                            Label = p.Name,
                            Kind = CompletionKind.Parameter,
                            Detail = $"{p.Type}" + (p.Min.HasValue || p.Max.HasValue 
                                ? $" [{p.Min ?? 0}..{p.Max ?? int.MaxValue}]" 
                                : ""),
                            Documentation = $"默认值: {p.Default}" + (!string.IsNullOrEmpty(p.Description) ? $"\n{p.Description}" : ""),
                            InsertText = p.Name
                        });
                }
            }
            return Enumerable.Empty<CompletionItem>();
        }
        else
        {
            // 方法参数：Func(5m).method(
            var method = function.Methods?.FirstOrDefault(m => m.Name.Equals(context.FieldName, StringComparison.OrdinalIgnoreCase));
            if (method?.Parameters != null && method.Parameters.Any())
            {
                var parameters = method.Parameters;
                var enteredParams = context.Tag as HashSet<string> ?? new HashSet<string>();
                
                return parameters
                    .Where(p => !enteredParams.Contains(p.Name))
                    .Select(p => new CompletionItem
                    {
                        Label = p.Name,
                        Kind = CompletionKind.Parameter,
                        Detail = $"{p.Type}" + (p.Min.HasValue || p.Max.HasValue 
                            ? $" [{p.Min ?? 0}..{p.Max ?? int.MaxValue}]" 
                            : ""),
                        Documentation = $"默认值: {p.Default}" + (!string.IsNullOrEmpty(p.Description) ? $"\n{p.Description}" : ""),
                        InsertText = p.Name
                    });
            }
        }
        
        return Enumerable.Empty<CompletionItem>();
    }

    /// <summary>
    /// 获取时间序列函数方法补全
    /// </summary>
    private IEnumerable<CompletionItem> GetTimeSeriesFunctionMethodCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.FunctionName))
            return Enumerable.Empty<CompletionItem>();

        return _intelliSense.GetTimeSeriesFunctionMethodCompletions(context.FunctionName);
    }

    /// <summary>
    /// 获取时间序列函数方法参数补全
    /// </summary>
    private IEnumerable<CompletionItem> GetTimeSeriesFunctionMethodParameterCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.FunctionName) || string.IsNullOrEmpty(context.FieldName))
            return Enumerable.Empty<CompletionItem>();

        var function = _intelliSense.GetTimeSeriesFunction(context.FunctionName);
        if (function == null)
            return Enumerable.Empty<CompletionItem>();

        var method = function.Methods?.FirstOrDefault(m => m.Name.Equals(context.FieldName, StringComparison.OrdinalIgnoreCase));
        if (method?.Parameters != null && method.Parameters.Any())
        {
            var parameters = method.Parameters;
            var enteredParams = context.Tag as HashSet<string> ?? new HashSet<string>();
            
            return parameters
                .Where(p => !enteredParams.Contains(p.Name))
                .Select(p => new CompletionItem
                {
                    Label = p.Name,
                    Kind = CompletionKind.Parameter,
                    Detail = $"{p.Type}" + (p.Min.HasValue || p.Max.HasValue 
                        ? $" [{p.Min ?? 0}..{p.Max ?? int.MaxValue}]" 
                        : ""),
                    Documentation = $"默认值: {p.Default}" + (!string.IsNullOrEmpty(p.Description) ? $"\n{p.Description}" : ""),
                    InsertText = p.Name
                });
        }
        
        return Enumerable.Empty<CompletionItem>();
    }

    /// <summary>
    /// 获取三级链式调用补全
    /// </summary>
    private IEnumerable<CompletionItem> GetThreeLevelChainedCompletions(CompletionContext context)
    {
        if (string.IsNullOrEmpty(context.FunctionName) || string.IsNullOrEmpty(context.ParentFieldName))
            return Enumerable.Empty<CompletionItem>();

        // 数据函数子字段
        var dataFuncSubFields = _intelliSense.GetDataFunctionSubFieldCompletions(
            context.FunctionName, 
            context.ParentFieldName
        );
        
        if (dataFuncSubFields.Any())
            return dataFuncSubFields;
        
        // SMC嵌套对象
        return _intelliSense.GetSmcNestedObjectCompletions(
            context.FunctionName, 
            context.ParentFieldName
        );
    }

    /// <summary>
    /// 获取运算符补全
    /// </summary>
    private IEnumerable<CompletionItem> GetOperatorCompletions()
    {
        return new[]
        {
            new CompletionItem
            {
                Label = "==",
                DisplayText = "== (等于) 推荐",
                Description = "比较运算符：相等判断（推荐在条件块内使用，避免歧义）",
                InsertText = "== ",
                Kind = CompletionKind.Operator
            },
            new CompletionItem
            {
                Label = "=",
                DisplayText = "= (等于)",
                Description = "比较运算符：相等判断（仅顶层参数赋值使用 =）",
                InsertText = "= ",
                Kind = CompletionKind.Operator
            },
            new CompletionItem
            {
                Label = "!=",
                DisplayText = "!= (不等于)",
                Description = "比较运算符：不等判断",
                InsertText = "!= ",
                Kind = CompletionKind.Operator
            },
            new CompletionItem
            {
                Label = ">",
                DisplayText = "> (大于)",
                Description = "比较运算符：大于",
                InsertText = "> ",
                Kind = CompletionKind.Operator
            },
            new CompletionItem
            {
                Label = ">=",
                DisplayText = ">= (大于等于)",
                Description = "比较运算符：大于或等于",
                InsertText = ">= ",
                Kind = CompletionKind.Operator
            },
            new CompletionItem
            {
                Label = "<",
                DisplayText = "< (小于)",
                Description = "比较运算符：小于",
                InsertText = "< ",
                Kind = CompletionKind.Operator
            },
            new CompletionItem
            {
                Label = "<=",
                DisplayText = "<= (小于等于)",
                Description = "比较运算符：小于或等于",
                InsertText = "<= ",
                Kind = CompletionKind.Operator
            }
        };
    }

    /// <summary>
    /// 获取通用补全（已废弃，使用 GetFunctionCompletionsForExpression 代替）
    /// </summary>
    private IEnumerable<CompletionItem> GetGeneralCompletions(bool includeSnippets)
    {
        // 不再使用此方法，统一使用 GetFunctionCompletionsForExpression
        return GetFunctionCompletionsForExpression(new CompletionContext());
    }

    #endregion
}

