using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services;

/// <summary>
/// IntelliSense 数据服务 - 基于 DSLRulesService 提供代码补全数据
/// 版本: 2.0 (完全使用统一规则系统 DSLRules.yaml)
/// </summary>
public partial class IntelliSenseService
{
    private readonly DSLRulesService _rulesService;

    public IntelliSenseService(DSLRulesService rulesService)
    {
        _rulesService = rulesService;
    }

    #region 代码补全 - Completion

    /// <summary>
    /// 获取所有指标补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetIndicatorCompletions()
    {
        var indicators = _rulesService.GetAllIndicators();

        foreach (var indicator in indicators)
        {
            // 生成带默认参数的版本
            var parameters = indicator.Parameters ?? new List<ParameterDefinition>();
            var defaultParams = string.Join(", ", parameters.Select(p => p.Default?.ToString() ?? "0"));
            
            yield return new CompletionItem
            {
                Label = $"{indicator.Id}({defaultParams})",
                Kind = CompletionKind.Indicator,
                Detail = indicator.Name,  // 中文名称作为详情
                Documentation = indicator.Description,
                InsertText = $"{indicator.Id}({defaultParams})"
            };
            
            // 生成不带参数的版本
            yield return new CompletionItem
            {
                Label = $"{indicator.Id}()",
                Kind = CompletionKind.Indicator,
                Detail = indicator.Name,
                Documentation = $"{indicator.Description}\n\n使用默认参数",
                InsertText = $"{indicator.Id}()"
            };
            
            // 生成不带括号的版本
            yield return new CompletionItem
            {
                Label = indicator.Id,
                Kind = CompletionKind.Indicator,
                Detail = indicator.Name,
                Documentation = indicator.Description,
                InsertText = indicator.Id
            };
        }
    }

    /// <summary>
    /// 获取指标字段补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetIndicatorFieldCompletions(string indicatorName)
    {
        var indicator = _rulesService.GetIndicator(indicatorName);
        if (indicator == null)
            yield break;

        // 字段补全
        if (indicator.Fields != null)
        {
            foreach (var field in indicator.Fields)
            {
                yield return new CompletionItem
            {
                Label = field.Name,
                Kind = CompletionKind.Field,
                    Detail = field.Type,
                    Documentation = field.Description + (field.Readonly ? " (只读)" : ""),
                    InsertText = field.Name
                };
            }
        }

        // 参数补全
        if (indicator.Parameters != null)
        {
            foreach (var param in indicator.Parameters)
            {
                var detail = $"{param.Type}";
                if (param.Min.HasValue || param.Max.HasValue)
                {
                    detail += $" [{param.Min ?? 0}..{param.Max ?? int.MaxValue}]";
                }
                
                yield return new CompletionItem
                {
                    Label = param.Name,
                    Kind = CompletionKind.Parameter,
                    Detail = detail,
                    Documentation = $"默认值: {param.Default}",
                    InsertText = param.Name
                };
            }
        }
    }

    /// <summary>
    /// 获取时间周期补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetTimeframeCompletions()
    {
        var timeframes = _rulesService.GetAllTimeframes();
        
        foreach (var tf in timeframes)
        {
            yield return new CompletionItem
            {
                Label = tf.Id,
                Kind = CompletionKind.Timeframe,
                Detail = tf.Name,
                Documentation = tf.Description,
                InsertText = tf.Id
            };
        }
    }

    /// <summary>
    /// 获取环境变量补全项（已废弃 - 环境变量概念已移除）
    /// </summary>
    public IEnumerable<CompletionItem> GetEnvVariableCompletions()
    {
        // 环境变量概念已废弃，返回空列表
        yield break;
    }

    /// <summary>
    /// 获取变量类型声明补全项
    /// </summary>
    /// <remarks>
    /// 当用户输入 @varname: 后，提供 Prophet DSL 支持的数据类型补全
    /// 支持的类型：Integer, Double, Boolean, String, Enum
    /// </remarks>
    public IEnumerable<CompletionItem> GetVariableTypeCompletions()
    {
        // Prophet DSL 支持的数据类型
        var types = new[]
        {
            new { Name = "Integer", Description = "整数类型 - 用于表示整数值（如：1, 100, -50）" },
            new { Name = "Double", Description = "浮点数类型 - 用于表示小数值（如：1.5, 3.14, 100.0）" },
            new { Name = "Boolean", Description = "布尔类型 - 用于表示真/假值（true 或 false）" },
            new { Name = "String", Description = "字符串类型 - 用于表示文本值" },
            new { Name = "Enum", Description = "枚举类型 - 用于表示预定义的枚举值（如：BULLISH, BEARISH）" }
        };

        foreach (var type in types)
        {
            yield return new CompletionItem
            {
                Label = type.Name,
                Kind = CompletionKind.Type,
                Detail = type.Name,
                Documentation = type.Description,
                InsertText = type.Name
            };
        }
    }

    /// <summary>
    /// 获取数据函数补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetDataFunctionCompletions()
    {
        var dataFunctions = _rulesService.GetAllDataFunctions();
        
        foreach (var func in dataFunctions)
        {
            // 生成函数参数
            var parameters = func.Parameters ?? new List<ParameterDefinition>();
            var defaultParams = string.Join(", ", parameters.Select(p => p.Default?.ToString() ?? ""));
            
            var insertText = parameters.Any() 
                ? $"{func.Id}({defaultParams})"
                : $"{func.Id}()";

            yield return new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.DataFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = insertText
            };
        }
    }

    /// <summary>
    /// 获取数据函数字段/方法补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetDataFunctionFieldCompletions(string functionName)
    {
        var function = _rulesService.GetDataFunction(functionName);
        if (function == null)
            yield break;

        // 返回值字段
        if (function.Fields != null)
        {
            foreach (var field in function.Fields)
            {
                yield return new CompletionItem
            {
                Label = field.Name,
                Kind = CompletionKind.Field,
                    Detail = field.Type,
                    Documentation = field.Description,
                    InsertText = field.Name
                };
            }
        }

        // 方法
        if (function.Methods != null)
        {
            foreach (var method in function.Methods)
            {
                var parameters = method.Parameters ?? new List<ParameterDefinition>();
                var paramList = string.Join(", ", parameters.Select(p => $"{p.Name}"));
                
                yield return new CompletionItem
                {
                    Label = $"{method.Name}({paramList})",
                    Kind = CompletionKind.Method,
                    Detail = method.ReturnType,
                    Documentation = method.Description,
                    InsertText = $"{method.Name}()" // 补全为完整的方法名和括号，光标定位在括号内
                };
            }
        }
    }

    /// <summary>
    /// 获取数据函数子字段补全项（用于嵌套对象）
    /// </summary>
    public IEnumerable<CompletionItem> GetDataFunctionSubFieldCompletions(string functionName, string fieldName)
    {
        var function = _rulesService.GetDataFunction(functionName);
        if (function == null)
            yield break;

        // 查找方法
        var method = function.Methods?.FirstOrDefault(m => m.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (method != null)
        {
            // 1. 子字段（不需要括号）
            if (method.SubFields != null)
            {
                foreach (var subField in method.SubFields)
                {
                    yield return new CompletionItem
                    {
                        Label = subField.Name,
                        Kind = CompletionKind.Field,
                        Detail = subField.Type,
                        Documentation = subField.Description,
                        InsertText = subField.Name
                    };
                }
            }
            
            // 2. 子方法（需要括号）
            if (method.SubMethods != null)
            {
                foreach (var subMethod in method.SubMethods)
                {
                    var parameters = subMethod.Parameters ?? new List<ParameterDefinition>();
                    var paramList = string.Join(", ", parameters.Select(p => $"{p.Name}"));
                    
                    yield return new CompletionItem
                    {
                        Label = $"{subMethod.Name}({paramList})",
                        Kind = CompletionKind.Method,
                        Detail = subMethod.ReturnType,
                        Documentation = subMethod.Description,
                        InsertText = $"{subMethod.Name}()" // 补全为完整的方法名和括号，光标定位在括号内
                    };
                }
            }
        }
        
        // 查找字段的子字段（用于 HT.PHASOR.inphase）
        var field = function.Fields?.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (field?.SubFields != null)
        {
            foreach (var subField in field.SubFields)
            {
                yield return new CompletionItem
                {
                    Label = subField.Name,
                    Kind = CompletionKind.Field,
                    Detail = subField.Type,
                    Documentation = subField.Description,
                    InsertText = subField.Name
                };
            }
        }
    }

    /// <summary>
    /// 获取信号函数补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetSignalFunctionCompletions()
    {
        var signalFunctions = _rulesService.GetAllSignalFunctions();
        
        foreach (var func in signalFunctions)
        {
            var insertText = GenerateSignalFunctionInsertText(func);
            
            yield return new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.SignalFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = insertText
            };
        }
    }

    private string GenerateSignalFunctionInsertText(SignalFunctionDefinition func)
    {
        var parameters = func.Parameters ?? new List<ParameterDefinition>();
        if (!parameters.Any())
            return $"{func.Id}()";

        var paramList = string.Join(", ", parameters.Select((p, i) => $"${{{i + 1}:{p.Name}}}"));
        return $"{func.Id}({paramList})";
    }

    /// <summary>
    /// 获取SMC函数补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetSmcFunctionCompletions()
    {
        var dataFunctions = _rulesService.GetAllDataFunctions();
        var smcFunctions = dataFunctions.Where(f => f.Category?.Equals("smc", StringComparison.OrdinalIgnoreCase) == true);
        
        foreach (var func in smcFunctions)
        {
            var parameters = func.Parameters ?? new List<ParameterDefinition>();
            var defaultParams = string.Join(", ", parameters.Select(p => p.Default?.ToString() ?? ""));
            
            var insertText = parameters.Any() 
                ? $"{func.Id}({defaultParams})"
                : $"{func.Id}()";

            yield return new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.DataFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = insertText
            };
        }
    }

    /// <summary>
    /// 获取SMC函数字段补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetSmcFunctionFieldCompletions(string functionName)
    {
        return GetDataFunctionFieldCompletions(functionName);
    }

    /// <summary>
    /// 获取SMC嵌套对象补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetSmcNestedObjectCompletions(string functionName, string objectName)
    {
        return GetDataFunctionSubFieldCompletions(functionName, objectName);
    }

    /// <summary>
    /// 获取时间序列函数补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetTimeSeriesFunctionCompletions()
    {
        var timeSeriesFunctions = _rulesService.GetAllTimeSeriesFunctions();
        
        foreach (var func in timeSeriesFunctions)
        {
            var insertText = $"{func.Id}()";
            
            yield return new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.TimeSeriesFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = insertText,
                Category = func.Category
            };
        }
    }

    /// <summary>
    /// 获取时间序列函数方法补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetTimeSeriesFunctionMethodCompletions(string functionName)
    {
        var function = _rulesService.GetTimeSeriesFunction(functionName);
        if (function == null)
            yield break;

        // 方法
        if (function.Methods != null)
        {
            foreach (var method in function.Methods)
            {
                var parameters = method.Parameters ?? new List<ParameterDefinition>();
                var paramList = string.Join(", ", parameters.Select(p => $"{p.Name}"));
                
                yield return new CompletionItem
                {
                    Label = $"{method.Name}({paramList})",
                    Kind = CompletionKind.Method,
                    Detail = method.ReturnType,
                    Documentation = method.Description,
                    InsertText = $"{method.Name}()" // 补全为完整的方法名和括号，光标定位在括号内
                };
            }
        }
    }

    /// <summary>
    /// 获取所有函数补全项（数据函数 + 数学函数 + 时间序列函数）
    /// </summary>
    public IEnumerable<CompletionItem> GetAllFunctionCompletions(string? prefix = null)
    {
        var allFunctions = new List<CompletionItem>();

        // 数据函数
        var dataFunctions = _rulesService.GetAllDataFunctions();
        foreach (var func in dataFunctions)
        {
            if (prefix != null && !func.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var parameters = func.Parameters ?? new List<ParameterDefinition>();
            var defaultParams = string.Join(", ", parameters.Select(p => p.Default?.ToString() ?? ""));
            
            var insertText = parameters.Any() 
                ? $"{func.Id}({defaultParams})"
                : $"{func.Id}()";

            allFunctions.Add(new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.DataFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = insertText
            });
        }

        // 数学函数
        var mathFunctions = _rulesService.GetAllMathFunctions();
        foreach (var func in mathFunctions)
        {
            if (prefix != null && !func.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var parameters = func.Parameters ?? new List<ParameterDefinition>();
            var paramList = string.Join(", ", parameters.Select((p, i) => $"${{{i + 1}:{p.Name}}}"));
            
            var insertText = parameters.Any() 
                ? $"{func.Id}({paramList})"
                : $"{func.Id}()";

            allFunctions.Add(new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.MathFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = insertText
            });
        }

        // 时间序列函数
        var timeSeriesFunctions = _rulesService.GetAllTimeSeriesFunctions();
        foreach (var func in timeSeriesFunctions)
        {
            if (prefix != null && !func.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            allFunctions.Add(new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.TimeSeriesFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = $"{func.Id}()",
                Category = func.Category
            });
        }

        return allFunctions;
    }
    
    /// <summary>
    /// 获取数学函数补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetMathFunctionCompletions()
    {
        var mathFunctions = _rulesService.GetAllMathFunctions();
        
        foreach (var func in mathFunctions)
        {
            var parameters = func.Parameters ?? new List<ParameterDefinition>();
            var paramList = string.Join(", ", parameters.Select((p, i) => $"${{{i + 1}:{p.Name}}}"));
            
            var insertText = parameters.Any() 
                ? $"{func.Id}({paramList})"
                : $"{func.Id}()";

            yield return new CompletionItem
            {
                Label = func.Id,
                Kind = CompletionKind.MathFunction,
                Detail = func.Name,  // 中文名称作为详情
                Documentation = func.Description,
                InsertText = insertText
            };
        }
    }

    /// <summary>
    /// 获取信号补全项 (BUY/SELL/HOLD)
    /// </summary>
    public IEnumerable<CompletionItem> GetSignalCompletions()
    {
        yield return new CompletionItem
        {
            Label = "BUY",
            Kind = CompletionKind.Signal,
            Detail = "买入信号",
            Documentation = "生成买入信号",
            InsertText = "BUY"
        };

        yield return new CompletionItem
        {
            Label = "SELL",
            Kind = CompletionKind.Signal,
            Detail = "卖出信号",
            Documentation = "生成卖出信号",
            InsertText = "SELL"
        };

        yield return new CompletionItem
        {
            Label = "HOLD",
            Kind = CompletionKind.Signal,
            Detail = "持仓信号",
            Documentation = "不进行任何操作",
            InsertText = "HOLD"
        };
    }

    /// <summary>
    /// 获取时间序列函数方法返回值枚举值补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetTimeSeriesFunctionMethodValueCompletions(string functionName, string methodName)
    {
        var function = _rulesService.GetTimeSeriesFunction(functionName);
        if (function == null)
        {
            yield break;
        }

        var method = function.Methods?.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
        if (method == null)
        {
            yield break;
        }

        // 如果方法返回类型是枚举
        if (method.ReturnType.Equals("enum", StringComparison.OrdinalIgnoreCase))
        {
            // trend() 方法返回 TrendDirectionType 枚举
            if (methodName.Equals("trend", StringComparison.OrdinalIgnoreCase))
            {
                var enumValues = _rulesService.GetEnumValues("TrendDirectionType") ?? Enumerable.Empty<string>();
                
                foreach (var value in enumValues.Where(v => !string.IsNullOrEmpty(v)))
                {
                    yield return new CompletionItem
                    {
                        Label = value,
                        Kind = CompletionKind.EnumValue,
                        Detail = "趋势方向枚举值",
                        Documentation = $"TrendDirectionType 枚举值：{value}",
                        InsertText = value
                    };
                }
            }
            // 如果将来有其他返回枚举的方法，可以在这里添加
        }
    }

    /// <summary>
    /// 获取字段值补全项（用于参数值提示）
    /// </summary>
    public IEnumerable<CompletionItem> GetFieldValueCompletions(string indicatorName, string fieldName)
    {
        var indicator = _rulesService.GetIndicator(indicatorName);
        if (indicator == null)
        {
            yield break;
        }

        // 查找字段
        var field = indicator.Fields?.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (field != null)
        {
            // 如果字段有枚举值（优先检查字段定义中的 enum_values）
            if (field.EnumValues != null && field.EnumValues.Count > 0)
            {
                foreach (var value in field.EnumValues.Where(v => !string.IsNullOrEmpty(v)))
                {
                    yield return new CompletionItem
                    {
                        Label = value,
                        Kind = CompletionKind.EnumValue,
                        Detail = "枚举值",
                        Documentation = $"枚举值：{value}",
                        InsertText = value
                    };
                }
                yield break;
            }
            
            // 如果字段类型是 enum:EnumName 格式
            if (field.Type.StartsWith("enum:", StringComparison.OrdinalIgnoreCase))
            {
                var enumName = field.Type.Substring(5);
                var enumValues = _rulesService.GetEnumValues(enumName) ?? Enumerable.Empty<string>();
                
                foreach (var value in enumValues.Where(v => !string.IsNullOrEmpty(v)))
                {
                    yield return new CompletionItem
                    {
                        Label = value,
                        Kind = CompletionKind.EnumValue,
                        Detail = "枚举值",
                        Documentation = $"{enumName} 枚举值",
                        InsertText = value
                    };
                }
                yield break;
            }
            
            // 如果字段类型是 "enum"（没有指定枚举名称，但有 enum_values）
            if (field.Type.Equals("enum", StringComparison.OrdinalIgnoreCase) && field.EnumValues != null && field.EnumValues.Count > 0)
            {
                foreach (var value in field.EnumValues.Where(v => !string.IsNullOrEmpty(v)))
                {
                    yield return new CompletionItem
                    {
                        Label = value,
                        Kind = CompletionKind.EnumValue,
                        Detail = "枚举值",
                        Documentation = $"枚举值：{value}",
                        InsertText = value
                    };
                }
                yield break;
            }
            
            // 布尔类型
            if (field.Type.Equals("boolean", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionItem
                {
                    Label = "true",
                    Kind = CompletionKind.Keyword,
                    Detail = "布尔值",
                    Documentation = "真",
                    InsertText = "true"
                };
                
                yield return new CompletionItem
                {
                    Label = "false",
                    Kind = CompletionKind.Keyword,
                    Detail = "布尔值",
                    Documentation = "假",
                    InsertText = "false"
                };
            }
            yield break;
        }

        // 查找参数
        var parameter = indicator.Parameters?.FirstOrDefault(p => p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (parameter != null)
                {
            // 如果参数有枚举值
            if (parameter.Type.StartsWith("enum:", StringComparison.OrdinalIgnoreCase))
            {
                var enumName = parameter.Type.Substring(5);
                var enumValues = _rulesService.GetEnumValues(enumName) ?? Enumerable.Empty<string>();
                
                foreach (var value in enumValues.Where(v => !string.IsNullOrEmpty(v)))
                {
                    yield return new CompletionItem
                    {
                        Label = value,
                        Kind = CompletionKind.EnumValue,
                        Detail = "枚举值",
                        Documentation = $"{enumName} 枚举值",
                        InsertText = value
                    };
                }
            }
            // 布尔类型
            else if (parameter.Type.Equals("boolean", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionItem
                {
                    Label = "true",
                    Kind = CompletionKind.Keyword,
                    Detail = "布尔值",
                    Documentation = "真",
                    InsertText = "true"
                };
                
                yield return new CompletionItem
                {
                    Label = "false",
                    Kind = CompletionKind.Keyword,
                    Detail = "布尔值",
                    Documentation = "假",
                    InsertText = "false"
                };
            }
            // 数值类型 - 提供范围提示
            else if (parameter.Type.Equals("integer", StringComparison.OrdinalIgnoreCase) || parameter.Type.Equals("double", StringComparison.OrdinalIgnoreCase))
            {
                var rangeHint = "";
                if (parameter.Min.HasValue && parameter.Max.HasValue)
                {
                    rangeHint = $"[{parameter.Min}..{parameter.Max}]";
                }
                else if (parameter.Min.HasValue)
                {
                    rangeHint = $"[{parameter.Min}+]";
                }
                else if (parameter.Max.HasValue)
                {
                    rangeHint = $"[..{parameter.Max}]";
                }

                yield return new CompletionItem
                {
                    Label = parameter.Default?.ToString() ?? "0",
                    Kind = CompletionKind.Value,
                    Detail = rangeHint,
                    Documentation = $"默认值: {parameter.Default}",
                    InsertText = parameter.Default?.ToString() ?? "0"
                };
            }
        }
    }

    /// <summary>
    /// 获取枚举值补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetEnumValueCompletions(string indicatorName, string fieldName)
    {
        return GetFieldValueCompletions(indicatorName, fieldName);
    }

    #endregion

    #region 悬停提示 - Hover

    /// <summary>
    /// 获取悬停提示信息
    /// </summary>
    public (string title, string content, string? example) GetHoverInfo(string word, string context)
    {
        return GetHoverInfo(word, context, null);
    }
    
    /// <summary>
    /// 获取悬停提示信息（支持自定义函数）
    /// </summary>
    /// <param name="word">悬停的单词</param>
    /// <param name="context">单词所在行的上下文</param>
    /// <param name="fullCode">完整的DSL代码（用于提取自定义函数）</param>
    public (string title, string content, string? example) GetHoverInfo(string word, string context, string? fullCode)
    {
        // 优先检查自定义函数（如果提供了完整代码）
        if (!string.IsNullOrEmpty(fullCode))
        {
            var customFunctions = ExtractCustomFunctions(fullCode);
            var customFunc = customFunctions.FirstOrDefault(f => f.Name.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (customFunc != null)
            {
                return GenerateCustomFunctionHoverInfo(customFunc);
            }
        }
        
        // 尝试匹配指标
        if (_rulesService.IsValidIndicator(word))
        {
            return GenerateIndicatorHoverInfo(word);
        }

        // 尝试匹配数据函数
        if (_rulesService.IsValidDataFunction(word))
        {
            return GenerateDataFunctionHoverInfo(word);
        }

        // 尝试匹配信号函数
        if (_rulesService.IsValidSignalFunction(word))
        {
            return GenerateSignalFunctionHoverInfo(word);
        }

        // 尝试匹配数学函数
        if (_rulesService.IsValidMathFunction(word))
        {
            return GenerateMathFunctionHoverInfo(word);
        }

        // 尝试匹配时间序列函数
        if (_rulesService.IsValidTimeSeriesFunction(word))
        {
            return GenerateTimeSeriesFunctionHoverInfo(word);
        }

        // 尝试匹配时间周期
        if (_rulesService.IsValidTimeframe(word))
        {
            return GenerateTimeframeHoverInfo(word);
        }

        // 环境变量概念已废弃
        // if (_rulesService.IsValidEnvironmentVariable(word))
        // {
        //     return GenerateEnvVariableHoverInfo(word);
        // }

        // 尝试从上下文提取字段信息
        var fieldInfo = TryGetFieldFromContext(context, word);
        if (fieldInfo.HasValue)
        {
            return GenerateFieldHoverInfo(fieldInfo.Value.indicatorName, fieldInfo.Value.fieldName);
        }

        // 尝试从上下文提取函数字段信息
        var funcFieldInfo = TryGetFunctionFieldFromContext(context, word);
        if (funcFieldInfo.HasValue)
        {
            return GenerateFunctionFieldHoverInfo(funcFieldInfo.Value.functionName, funcFieldInfo.Value.fieldName);
        }

        // 尝试从上下文提取指标参数信息
        var paramInfo = TryGetIndicatorParameterFromContext(context, word);
        if (paramInfo.HasValue)
        {
            return GenerateParameterHoverInfo(paramInfo.Value.indicatorName, paramInfo.Value.paramName);
        }

        // 尝试匹配枚举值
        var enumInfo = FindEnumValue(word);
        if (enumInfo != null)
        {
            return GenerateEnumValueHoverInfo(word, enumInfo.Value.enumDef, enumInfo.Value.description);
        }

        return ("未知", $"未找到 `{word}` 的相关信息", null);
    }

    private (string title, string content, string? example) GenerateIndicatorHoverInfo(string indicatorName)
    {
        var indicator = _rulesService.GetIndicator(indicatorName);
        if (indicator == null)
            return ("指标", indicatorName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{indicator.Name}** - {indicator.Category}");
        sb.AppendLine();
        sb.AppendLine(indicator.Description);

        if (indicator.Parameters != null && indicator.Parameters.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**参数:**");
            foreach (var param in indicator.Parameters)
            {
                var range = "";
                if (param.Min.HasValue || param.Max.HasValue)
                {
                    range = $" [{param.Min ?? 0}..{param.Max ?? int.MaxValue}]";
                }
                sb.AppendLine($"- `{param.Name}`: {param.Type}{range} (默认: {param.Default})");
            }
        }

        if (indicator.Fields != null && indicator.Fields.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**字段:**");
            foreach (var field in indicator.Fields)
            {
                var readonlyMark = field.Readonly ? " (只读)" : "";
                sb.AppendLine($"- `{field.Name}`: {field.Type}{readonlyMark} - {field.Description}");
            }
        }

        var example = indicator.Syntax ?? $"$(5m).{indicator.Name}().value";
        return ("指标", sb.ToString(), example);
    }

    private (string title, string content, string? example) GenerateFieldHoverInfo(string indicatorName, string fieldName)
    {
        var indicator = _rulesService.GetIndicator(indicatorName);
        if (indicator == null)
            return ("字段", fieldName, null);

        var field = indicator.Fields?.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (field == null)
            return ("字段", fieldName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{field.Name}** ({field.Type})");
        sb.AppendLine();
        sb.AppendLine(field.Description);
        
        if (field.Readonly)
        {
            sb.AppendLine();
            sb.AppendLine("*此字段为只读*");
        }

        var example = $"$(5m).{indicatorName}().{field.Name}";
        return ("字段", sb.ToString(), example);
    }

    private (string title, string content, string? example) GenerateTimeframeHoverInfo(string timeframe)
    {
        var tf = _rulesService.GetAllTimeframes()
            .FirstOrDefault(t => t.Id.Equals(timeframe, StringComparison.OrdinalIgnoreCase));
        
        if (tf == null)
            return ("时间周期", timeframe, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{tf.Name}**");
        sb.AppendLine();
        sb.AppendLine(tf.Description);

        return ("时间周期", sb.ToString(), $"$(${timeframe}).close");
    }

    // 环境变量概念已废弃
    // private (string title, string content, string? example) GenerateEnvVariableHoverInfo(string envName)
    // {
    //     return ("环境变量", envName, null);
    // }

    private (string title, string content, string? example) GenerateDataFunctionHoverInfo(string functionName)
    {
        var function = _rulesService.GetDataFunction(functionName);
        if (function == null)
            return ("数据函数", functionName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{function.Name}** - {function.Category ?? "数据函数"}");
        sb.AppendLine();
        sb.AppendLine(function.Description);

        if (function.Parameters != null && function.Parameters.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**参数:**");
            foreach (var param in function.Parameters)
            {
                sb.AppendLine($"- `{param.Name}`: {param.Type} (默认: {param.Default})");
            }
        }

        if (function.Methods != null && function.Methods.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**方法:**");
            foreach (var method in function.Methods.Take(5))
            {
                sb.AppendLine($"- `{method.Name}()`: {method.Description}");
            }
            if (function.Methods.Count > 5)
            {
                sb.AppendLine($"- ... 共 {function.Methods.Count} 个方法");
            }
        }

        var example = function.Syntax ?? $"{function.Name}(5m)";
        return ("数据函数", sb.ToString(), example);
    }

    private (string title, string content, string? example) GenerateTimeSeriesFunctionHoverInfo(string functionName)
    {
        var function = _rulesService.GetTimeSeriesFunction(functionName);
        if (function == null)
            return ("时间序列函数", functionName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{function.Name}** - {function.Category ?? "时间序列函数"}");
        sb.AppendLine();
        sb.AppendLine(function.Description);

        if (!string.IsNullOrEmpty(function.DataSource))
        {
            sb.AppendLine();
            sb.AppendLine($"**数据来源:** {function.DataSource}");
        }

        if (!string.IsNullOrEmpty(function.UpdateFrequency))
        {
            sb.AppendLine();
            sb.AppendLine($"**更新频率:** {function.UpdateFrequency}");
        }

        if (function.Methods != null && function.Methods.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**方法:**");
            foreach (var method in function.Methods.Take(10))
            {
                var returnType = method.ReturnType;
                var paramList = "";
                if (method.Parameters != null && method.Parameters.Any())
                {
                    paramList = string.Join(", ", method.Parameters.Select(p => $"{p.Name}"));
                }
                sb.AppendLine($"- `{method.Name}({paramList})` → {returnType}: {method.Description}");
            }
            if (function.Methods.Count > 10)
            {
                sb.AppendLine($"- ... 共 {function.Methods.Count} 个方法");
            }
        }

        var example = function.Syntax ?? $"{function.Name}().method()";
        return ("时间序列函数", sb.ToString(), example);
    }

    private (string title, string content, string? example) GenerateFunctionFieldHoverInfo(string functionName, string fieldName)
    {
        var function = _rulesService.GetDataFunction(functionName);
        if (function == null)
            return ("函数方法", fieldName, null);

        var method = function.Methods?.FirstOrDefault(m => m.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (method == null)
            return ("函数方法", fieldName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{method.Name}()** → {method.ReturnType}");
        sb.AppendLine();
        sb.AppendLine(method.Description);

        if (method.Parameters != null && method.Parameters.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**参数:**");
            foreach (var param in method.Parameters)
            {
                sb.AppendLine($"- `{param.Name}`: {param.Type} (默认: {param.Default})");
            }
        }

        var example = $"{functionName}(5m).{method.Name}()";
        return ("函数方法", sb.ToString(), example);
    }

    private (string title, string content, string? example) GenerateSignalFunctionHoverInfo(string functionName)
    {
        var function = _rulesService.GetSignalFunction(functionName);
        if (function == null)
            return ("信号函数", functionName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{function.Name}**");
        sb.AppendLine();
        sb.AppendLine(function.Description);

        if (function.Parameters != null && function.Parameters.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**参数:**");
            foreach (var param in function.Parameters)
            {
                sb.AppendLine($"- `{param.Name}`: {param.Type}");
            }
        }

        var example = function.Syntax ?? $"{function.Name}()";
        return ("信号函数", sb.ToString(), example);
    }

    private (string title, string content, string? example) GenerateMathFunctionHoverInfo(string functionName)
    {
        var function = _rulesService.GetMathFunction(functionName);
        if (function == null)
            return ("数学函数", functionName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{function.Name}**");
        sb.AppendLine();
        sb.AppendLine(function.Description);

        if (function.Parameters != null && function.Parameters.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**参数:**");
            foreach (var param in function.Parameters)
            {
                sb.AppendLine($"- `{param.Name}`: {param.Type}");
            }
        }

        var example = function.Syntax ?? $"{function.Name}(x)";
        return ("数学函数", sb.ToString(), example);
    }

    private (string title, string content, string? example) GenerateParameterHoverInfo(string indicatorName, string paramName)
    {
        var indicator = _rulesService.GetIndicator(indicatorName);
        if (indicator == null)
            return ("参数", paramName, null);

        var parameter = indicator.Parameters?.FirstOrDefault(p => p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));
        if (parameter == null)
            return ("参数", paramName, null);

        var sb = new StringBuilder();
        sb.AppendLine($"**{parameter.Name}** ({parameter.Type})");
        sb.AppendLine();
        sb.AppendLine($"默认值: {parameter.Default}");
        
        if (parameter.Min.HasValue || parameter.Max.HasValue)
        {
            sb.AppendLine($"范围: [{parameter.Min ?? 0}..{parameter.Max ?? int.MaxValue}]");
        }

        return ("参数", sb.ToString(), null);
    }

    private (EnumDefinition enumDef, string? description)? FindEnumValue(string word)
    {
        var allEnums = _rulesService.GetAllEnums();
        
        foreach (var enumDef in allEnums)
        {
            if (enumDef.Values != null && enumDef.Values.Any(v => v.Equals(word, StringComparison.OrdinalIgnoreCase)))
            {
                var description = enumDef.GetValueDescription(word);
                return (enumDef, description);
            }
        }

        return null;
    }
    
    /// <summary>
    /// 生成自定义函数的悬停提示信息
    /// </summary>
    private (string title, string content, string? example) GenerateCustomFunctionHoverInfo(CustomFunctionDefinition customFunc)
    {
        var sb = new StringBuilder();
        
        // 标题：函数签名
        var paramsStr = string.Join(", ", customFunc.Parameters.Select(p => $"{p.Name}: {p.Type}"));
        var signature = $"{customFunc.Name}({paramsStr}): {customFunc.ReturnType}";
        
        sb.AppendLine($"**自定义函数**: {customFunc.Name}");
        sb.AppendLine();
        
        // 函数签名
        sb.AppendLine($"```dsl");
        sb.AppendLine(signature);
        sb.AppendLine("```");
        sb.AppendLine();
        
        // 参数列表
        if (customFunc.Parameters.Count > 0)
        {
            sb.AppendLine("**参数**:");
            foreach (var param in customFunc.Parameters)
            {
                sb.AppendLine($"  - `{param.Name}`: {param.Type}");
            }
            sb.AppendLine();
        }
        
        // 返回类型
        sb.AppendLine($"**返回类型**: {customFunc.ReturnType}");
        sb.AppendLine();
        
        // 文档注释（如果有）
        if (!string.IsNullOrEmpty(customFunc.Documentation))
        {
            sb.AppendLine("**说明**:");
            sb.AppendLine(customFunc.Documentation);
        }
        
        sb.AppendLine();
        sb.AppendLine($"**定义位置**: 第 {customFunc.LineNumber} 行");
        
        return ("自定义函数", sb.ToString(), null);
    }
    
    private (string title, string content, string? example) GenerateEnumValueHoverInfo(string enumValue, EnumDefinition enumDef, string? description)
    {
        var sb = new StringBuilder();
        
        // 标题：枚举值名称
        sb.AppendLine($"**{enumValue}**");
        sb.AppendLine();
        
        // 枚举类型信息
        if (!string.IsNullOrEmpty(enumDef.Name))
        {
            sb.AppendLine($"**类型**: {enumDef.Name}");
        }
        else
        {
            sb.AppendLine($"**类型**: {enumDef.Id}");
        }
        
        // 枚举类型描述
        if (!string.IsNullOrEmpty(enumDef.Description))
        {
            sb.AppendLine();
            sb.AppendLine(enumDef.Description);
        }
        
        // 枚举值描述
        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine();
            sb.AppendLine($"**含义**: {description}");
        }
        
        // 显示该枚举类型的所有可能值
        if (enumDef.Values != null && enumDef.Values.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**可选值**:");
            foreach (var value in enumDef.Values)
            {
                var valueDesc = enumDef.GetValueDescription(value);
                if (!string.IsNullOrEmpty(valueDesc))
                {
                    sb.AppendLine($"- `{value}`: {valueDesc}");
                }
                else
                {
                    sb.AppendLine($"- `{value}`");
                }
            }
        }
        
        return ("枚举值", sb.ToString(), null);
    }

    private (string indicatorName, string fieldName)? TryGetFieldFromContext(string context, string word)
    {
        // 匹配 $(timeframe).INDICATOR().field 或 $(timeframe).INDICATOR(params).field
        // 支持带参数的情况，如: $(5m).EMA(20).slope
        var match = Regex.Match(context, @"\$\([^)]+\)\.(\w+)\([^)]*\)[^\s]*\.(\w+)");
        if (match.Success && match.Groups[2].Value.Equals(word, StringComparison.OrdinalIgnoreCase))
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        return null;
    }

    private (string functionName, string fieldName)? TryGetFunctionFieldFromContext(string context, string word)
    {
        // 匹配 FUNCTION(timeframe).field
        var match = Regex.Match(context, @"(\w+)\([^)]*\)\.(\w+)");
        if (match.Success && match.Groups[2].Value.Equals(word, StringComparison.OrdinalIgnoreCase))
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        return null;
    }

    private (string indicatorName, string paramName)? TryGetIndicatorParameterFromContext(string context, string word)
    {
        // 匹配 $(timeframe).INDICATOR(param1=value, param2=value)
        var match = Regex.Match(context, @"\$\([^)]+\)\.(\w+)\([^)]*\b" + Regex.Escape(word) + @"\b");
        if (match.Success)
        {
            return (match.Groups[1].Value, word);
        }

        return null;
}

    #endregion

    #region 自定义函数支持 - Custom Functions

    /// <summary>
    /// 从代码中提取所有自定义函数定义
    /// </summary>
    public List<CustomFunctionDefinition> ExtractCustomFunctions(string code)
    {
        var functions = new List<CustomFunctionDefinition>();
        if (string.IsNullOrWhiteSpace(code))
            return functions;
        
        var lines = code.Split('\n');
        
        // 匹配自定义函数定义
        // 格式：functionName(param1: Type, param2: Type): ReturnType {
        var funcDefPattern = @"^([a-z][a-zA-Z0-9_]*)\s*\(([^)]*)\)\s*(?::\s*(\w+))?\s*\{";
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var match = Regex.Match(line, funcDefPattern);
            
            if (match.Success)
            {
                var funcName = match.Groups[1].Value;
                var paramsStr = match.Groups[2].Value.Trim();
                var returnType = match.Groups[3].Success ? match.Groups[3].Value : "void";
                
                // 解析参数
                var parameters = new List<ParameterDefinition>();
                if (!string.IsNullOrEmpty(paramsStr))
                {
                    var paramParts = paramsStr.Split(',');
                    foreach (var part in paramParts)
                    {
                        var paramMatch = Regex.Match(part.Trim(), @"^([a-z][a-zA-Z0-9_]*)\s*:\s*(\w+)$");
                        if (paramMatch.Success)
                        {
                            parameters.Add(new ParameterDefinition
                            {
                                Name = paramMatch.Groups[1].Value,
                                Type = paramMatch.Groups[2].Value.ToLower()
                            });
                        }
                    }
                }
                
                // 提取函数体（用于生成文档）
                var functionBody = ExtractFunctionBody(lines, i);
                
                functions.Add(new CustomFunctionDefinition
                {
                    Name = funcName,
                    Parameters = parameters,
                    ReturnType = returnType,
                    LineNumber = i + 1,
                    Documentation = GenerateCustomFunctionDocumentation(funcName, parameters, returnType, functionBody)
                });
            }
        }
        
        return functions;
    }
    
    /// <summary>
    /// 提取函数体代码
    /// </summary>
    private string ExtractFunctionBody(string[] lines, int startLineIndex)
    {
        var sb = new StringBuilder();
        int braceDepth = 0;
        bool started = false;
        
        for (int i = startLineIndex; i < lines.Length && i < startLineIndex + 20; i++)
        {
            var line = lines[i];
            
            foreach (char c in line)
            {
                if (c == '{')
                {
                    braceDepth++;
                    started = true;
                }
                else if (c == '}')
                {
                    braceDepth--;
                }
            }
            
            if (started)
            {
                sb.AppendLine(line);
            }
            
            if (started && braceDepth == 0)
            {
                break;
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 生成自定义函数的文档字符串
    /// </summary>
    private string GenerateCustomFunctionDocumentation(string name, List<ParameterDefinition> parameters, string returnType, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**自定义函数**: {name}");
        sb.AppendLine();
        
        if (parameters.Any())
        {
            sb.AppendLine("**参数**:");
            foreach (var param in parameters)
            {
                sb.AppendLine($"- `{param.Name}`: {param.Type}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine($"**返回类型**: {returnType}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 获取自定义函数补全项
    /// </summary>
    public IEnumerable<CompletionItem> GetCustomFunctionCompletions(string currentCode)
    {
        var customFunctions = ExtractCustomFunctions(currentCode);
        
        foreach (var func in customFunctions)
        {
            // 生成参数列表
            var paramList = string.Join(", ", func.Parameters.Select(p => p.Name));
            
            yield return new CompletionItem
            {
                Label = $"{func.Name}({paramList})",
                Kind = CompletionKind.Function,
                Detail = $"→ {func.ReturnType}",
                Documentation = func.Documentation,
                InsertText = $"{func.Name}({paramList})",
                Category = "自定义函数"
            };
        }
    }
    
    /// <summary>
    /// 获取自定义函数的参数提示信息
    /// </summary>
    public (string signature, string documentation, List<(string name, string type)> parameters)? GetCustomFunctionParameterHint(string functionName, string currentCode)
    {
        var customFunctions = ExtractCustomFunctions(currentCode);
        var func = customFunctions.FirstOrDefault(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));
        
        if (func == null)
            return null;
        
        // 生成函数签名
        var paramList = string.Join(", ", func.Parameters.Select(p => $"{p.Name}: {p.Type}"));
        var signature = $"{func.Name}({paramList}): {func.ReturnType}";
        
        // 生成参数列表
        var parameters = func.Parameters.Select(p => (p.Name, p.Type)).ToList();
        
        return (signature, func.Documentation ?? "", parameters);
    }

    #endregion

    #region 验证方法 - Validation

    /// <summary>
    /// 验证指标名称是否有效
    /// </summary>
    public bool IsValidIndicator(string name)
    {
        return _rulesService.IsValidIndicator(name);
    }

    /// <summary>
    /// 验证时间周期是否有效
    /// </summary>
    public bool IsValidTimeframe(string timeframe)
    {
        return _rulesService.IsValidTimeframe(timeframe);
    }

    /// <summary>
    /// 验证环境变量是否有效
    /// </summary>
    public bool IsValidEnvVariable(string name)
    {
        // 环境变量概念已废弃
        return false;
    }

    /// <summary>
    /// 验证数据函数是否有效
    /// </summary>
    public bool IsValidDataFunction(string name)
    {
        return _rulesService.IsValidDataFunction(name);
    }

    /// <summary>
    /// 获取数据函数定义（用于参数补全）
    /// </summary>
    public DataFunctionDefinition? GetDataFunction(string functionName)
    {
        return _rulesService.GetDataFunction(functionName);
    }

    /// <summary>
    /// 验证信号函数是否有效
    /// </summary>
    public bool IsValidSignalFunction(string name)
    {
        return _rulesService.IsValidSignalFunction(name);
    }

    /// <summary>
    /// 验证时间序列函数是否有效
    /// </summary>
    public bool IsValidTimeSeriesFunction(string name)
    {
        return _rulesService.IsValidTimeSeriesFunction(name);
    }

    /// <summary>
    /// 获取时间序列函数定义（用于参数补全）
    /// </summary>
    public TimeSeriesFunctionDefinition? GetTimeSeriesFunction(string functionName)
    {
        return _rulesService.GetTimeSeriesFunction(functionName);
    }

    /// <summary>
    /// 验证信号是否有效 (BUY/SELL/HOLD)
    /// </summary>
    public bool IsValidSignal(string name)
    {
        // 从规则系统获取SignalType枚举
        var signalEnum = _rulesService.GetAllEnums()
            .FirstOrDefault(e => e.Id == "SignalType");
        
        if (signalEnum?.Values == null)
            return false;
        
        return signalEnum.Values.Contains(name.ToUpperInvariant());
    }

    /// <summary>
    /// 验证指标字段是否有效
    /// </summary>
    public bool IsValidIndicatorField(string indicatorName, string fieldName)
    {
        return _rulesService.IsValidIndicatorField(indicatorName, fieldName);
    }

    #endregion

    #region 辅助方法 - Helper Methods

    /// <summary>
    /// 获取所有指标名称
    /// </summary>
    public IEnumerable<string> GetAllIndicatorNames()
    {
        return _rulesService.GetAllIndicators()
            .Select(i => i.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!);
    }
    
    /// <summary>
    /// 获取所有函数名称
    /// </summary>
    public IEnumerable<string> GetAllFunctionNames()
    {
        var dataFunctions = _rulesService.GetAllDataFunctions()
            .Select(f => f.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!);
        var mathFunctions = _rulesService.GetAllMathFunctions()
            .Select(f => f.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!);
        var timeSeriesFunctions = _rulesService.GetAllTimeSeriesFunctions()
            .Select(f => f.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!);
        return dataFunctions.Concat(mathFunctions).Concat(timeSeriesFunctions);
    }
    
    /// <summary>
    /// 获取所有环境变量名称
    /// </summary>
    public IEnumerable<string> GetAllEnvVariableNames()
    {
        // 环境变量概念已废弃
        return Enumerable.Empty<string>();
    }
    
    /// <summary>
    /// 获取所有枚举值
    /// </summary>
    public IEnumerable<EnumDefinition> GetAllEnumValues()
    {
        return _rulesService.GetAllEnums();
    }

    /// <summary>
    /// 获取所有时间周期
    /// </summary>
    public IEnumerable<TimeframeDefinition> GetAllTimeframes()
    {
        return _rulesService.GetAllTimeframes();
    }

    #endregion
}

#region 数据模型 - Data Models (用于IntelliSense返回值)

/// <summary>
/// 代码补全项
/// </summary>
public class CompletionItem
{
    public string Label { get; set; } = string.Empty;
    public string? DisplayText { get; set; }
    public CompletionKind Kind { get; set; }
    public string? Detail { get; set; }
    public string? Documentation { get; set; }
    public string? InsertText { get; set; }
    
    // UI扩展属性
    public string? Description { get; set; }
    public string? DataType { get; set; }
    public string? Category { get; set; }
    public List<string>? EnumValues { get; set; }
    public bool IsReadOnly { get; set; }
    public object? Tag { get; set; }
    }

    /// <summary>
/// 代码补全项类型
    /// </summary>
public enum CompletionKind
{
    Indicator,
    Field,
    Parameter,
    Timeframe,
    EnvVariable,
    DataFunction,
    SignalFunction,
    MathFunction,
    Method,
    Signal,
    EnumValue,
    Keyword,
    Value,
    Function,        // 泛化的函数类型
    TimeSeriesFunction,  // 时间序列函数
    Snippet,         // 代码片段
    CategoryHeader,  // 分类头部
    Operator,        // 运算符
    Type             // 数据类型
}

/// <summary>
/// 自定义函数定义
/// </summary>
public class CustomFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterDefinition> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string? Documentation { get; set; }
}

#endregion
