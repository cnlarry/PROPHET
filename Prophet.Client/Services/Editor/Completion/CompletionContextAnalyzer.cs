using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using Prophet.Client.Services.Editor.Shared;

namespace Prophet.Client.Services.Editor.Completion;

/// <summary>
/// 补全上下文分析器
/// </summary>
/// <remarks>
/// <para>
/// 负责分析光标位置的上下文，决定应该显示什么类型的补全项。
/// 通过解析当前行的文本，识别用户正在输入的语法结构（如指标引用、函数调用、字段访问等）。
/// </para>
/// <para>
/// 支持的上下文类型包括：
/// <list type="bullet">
/// <item><description>新语法指标引用: $(5m).MACD</description></item>
/// <item><description>时间框架输入: $(</description></item>
/// <item><description>数据函数调用: KLINE(5m)</description></item>
/// <item><description>字段访问: .open, .close</description></item>
/// <item><description>环境变量: @symbol</description></item>
/// <item><description>运算符右侧补全: = $(5m).MACD</description></item>
/// </list>
/// </para>
/// </remarks>
public class CompletionContextAnalyzer
{
    private readonly IntelliSenseService _intelliSense;

    /// <summary>
    /// 初始化 <see cref="CompletionContextAnalyzer"/> 类的新实例
    /// </summary>
    /// <param name="intelliSense">智能提示服务，用于验证指标名称等</param>
    public CompletionContextAnalyzer(IntelliSenseService intelliSense)
    {
        _intelliSense = intelliSense;
    }

    /// <summary>
    /// 分析当前光标位置的上下文
    /// </summary>
    /// <param name="document">文档对象</param>
    /// <param name="offset">光标在文档中的偏移量</param>
    /// <returns>
    /// 返回 <see cref="CompletionContext"/> 对象，包含：
    /// <list type="bullet">
    /// <item><description><see cref="CompletionContext.Type"/>: 上下文类型</description></item>
    /// <item><description><see cref="CompletionContext.FilterPrefix"/>: 用户已输入的过滤前缀</description></item>
    /// <item><description><see cref="CompletionContext.IndicatorName"/>: 指标名称（如果适用）</description></item>
    /// <item><description><see cref="CompletionContext.FunctionName"/>: 函数名称（如果适用）</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// 分析过程按优先级检查各种上下文：
    /// <list type="number">
    /// <item><description>运算符右侧的上下文（优先级最高）</description></item>
    /// <item><description>新语法时间框架输入</description></item>
    /// <item><description>新语法指标引用</description></item>
    /// <item><description>字段值赋值</description></item>
    /// <item><description>环境变量</description></item>
    /// <item><description>数据函数和SMC函数</description></item>
    /// <item><description>三级链式调用</description></item>
    /// <item><description>信号赋值</description></item>
    /// <item><description>根部输入</description></item>
    /// <item><description>信号函数内部</description></item>
    /// <item><description>运算符提示</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var analyzer = new CompletionContextAnalyzer(intelliSense);
    /// var context = analyzer.AnalyzeContext(document, caretOffset);
    /// 
    /// if (context.Type == CompletionContextType.Indicator)
    /// {
    ///     // 显示指标补全
    ///     var indicators = GetIndicatorCompletions(context.FilterPrefix);
    /// }
    /// </code>
    /// </example>
    public CompletionContext AnalyzeContext(TextDocument document, int offset)
    {
        var context = new CompletionContext();
        
        if (offset <= 0)
            return context;

        // 获取光标前的文本（当前行）
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, offset - line.Offset);
        
        // 检查是否在完整表达式后（以分号结尾，后面只有空格）
        // 例如：$(5m).MACD().trend == BULLISH;  后面不应该有补全
        if (IsAfterCompleteExpression(lineText))
        {
            // 完整表达式后，不显示补全
            context.Type = CompletionContextType.General;
            return context;
        }
        
        // 按优先级检查各种上下文
        
        // 1. 运算符右侧的上下文（优先级最高）
        var operatorContext = AnalyzeOperatorRightSide(lineText);
        if (operatorContext != null)
            return operatorContext;
        
        // 2. 新语法时间框架输入: $( 或 $(5
        var timeframeContext = AnalyzeTimeframeInput(lineText);
        if (timeframeContext != null)
            return timeframeContext;
        
        // 3. 新语法指标引用
        var indicatorContext = AnalyzeIndicatorReference(lineText, document, offset);
        if (indicatorContext != null)
            return indicatorContext;
        
        // 4. 字段值赋值
        var fieldValueContext = AnalyzeFieldValueAssignment(lineText);
        if (fieldValueContext != null)
            return fieldValueContext;
        
        // 5. 变量类型声明 (@varname:)
        var varTypeContext = AnalyzeVariableTypeDeclaration(lineText);
        if (varTypeContext != null)
            return varTypeContext;
        
        // 6. 环境变量
        var envVarContext = AnalyzeEnvVariable(lineText);
        if (envVarContext != null)
            return envVarContext;
        
        // 8. 时间序列函数
        var timeSeriesContext = AnalyzeTimeSeriesFunction(lineText);
        if (timeSeriesContext != null)
            return timeSeriesContext;
        
        // 9. 数据函数和SMC函数
        var dataFuncContext = AnalyzeDataFunction(lineText);
        if (dataFuncContext != null)
            return dataFuncContext;
        
        // 10. 三级链式调用
        var threeLevelContext = AnalyzeThreeLevelChainedCall(lineText);
        if (threeLevelContext != null)
            return threeLevelContext;
        
        // 11. 信号赋值
        var signalContext = AnalyzeSignalValue(lineText);
        if (signalContext != null)
            return signalContext;
        
        // 12. 根部输入（信号函数或自定义函数）
        var rootContext = AnalyzeRootInput(lineText, document, offset);
        if (rootContext != null)
            return rootContext;
        
        // 13. 信号函数内部
        var insideFunctionContext = AnalyzeInsideFunction(lineText, document, offset);
        if (insideFunctionContext != null)
            return insideFunctionContext;
        
        // 14. 运算符提示
        var operatorHintContext = AnalyzeOperatorHint(lineText);
        if (operatorHintContext != null)
            return operatorHintContext;
        
        // 默认：通用上下文
        context.Type = CompletionContextType.General;
        return context;
    }

    #region 私有分析方法

    /// <summary>
    /// 分析运算符右侧的上下文
    /// </summary>
    private CompletionContext? AnalyzeOperatorRightSide(string lineText)
    {
        // 移除末尾的空格，以便正确匹配
        var trimmedText = lineText.TrimEnd();
        
        // 查找运算符位置（==, !=, >, <, >=, <=）
        // 先尝试匹配末尾的运算符（最常见的情况）
        var operatorMatch = Regex.Match(trimmedText, @"([=<>!]+)\s*$");
        if (!operatorMatch.Success)
        {
            // 如果没有找到运算符在末尾，尝试查找运算符后面有空格的情况
            operatorMatch = Regex.Match(trimmedText, @"([=<>!]+)\s+");
        }
        
        if (!operatorMatch.Success)
        {
            return null;
        }
        
        var operatorIndex = operatorMatch.Index;
        var beforeOperator = trimmedText.Substring(0, operatorIndex).TrimEnd();
        
        // 1. 检查运算符前是否有新语法指标字段值: $(5m).MACD().trend ==
        var match1 = Regex.Match(beforeOperator, @"\$\(([^)]+)\)\.(\w+)\(([^)]*)\)\.(\w+)$");
        if (match1.Success)
        {
            return new CompletionContext
            {
                Type = CompletionContextType.FieldValue,
                IndicatorName = match1.Groups[2].Value,
                FieldName = match1.Groups[4].Value,
                FilterPrefix = ""
            };
        }
        
        // 2. 检查运算符前是否有时间序列函数方法返回值: FUNC().method() ==
        // 注意：这个要放在数据函数之前，因为时间序列函数的模式更具体
        // 匹配模式：FUNC().method() 或 FUNC().method(param)
        // 使用更精确的正则：允许空括号 () 或带参数的括号 (param)
        var match5 = Regex.Match(beforeOperator, 
            @"\b([A-Z][A-Z0-9_]*)\(\)\.(\w+)\((.*?)\)$");
        if (match5.Success)
        {
            var funcName = match5.Groups[1].Value;
            var methodName = match5.Groups[2].Value;
            
            // 先验证是否是时间序列函数，避免误匹配数据函数
            if (_intelliSense.IsValidTimeSeriesFunction(funcName))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.TimeSeriesFunctionMethodValue,
                    FunctionName = funcName,
                    FieldName = methodName, // 使用方法名作为 FieldName
                    FilterPrefix = ""
                };
            }
        }
        
        // 3. 检查运算符前是否有三级字段值: Func(5m).field().subfield ==
        var match4 = Regex.Match(beforeOperator, 
            @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)(\([^)]*\))?\.(\w+)$");
        if (match4.Success && _intelliSense.IsValidDataFunction(match4.Groups[1].Value))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.ThreeLevelFieldValue,
                FunctionName = match4.Groups[1].Value,
                ParentFieldName = match4.Groups[2].Value,
                FieldName = match4.Groups[4].Value,
                FilterPrefix = ""
            };
        }
        
        // 4. 检查运算符前是否有数据函数字段值: Func(5m).field ==
        var match3 = Regex.Match(beforeOperator, 
            @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)$");
        if (match3.Success && _intelliSense.IsValidDataFunction(match3.Groups[1].Value))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.DataFunctionFieldValue,
                FunctionName = match3.Groups[1].Value,
                FieldName = match3.Groups[2].Value,
                FilterPrefix = ""
            };
        }
        
        // 5. 检查运算符前是否有新语法指标引用: $(5m).INDICATOR ==
        var match2 = Regex.Match(beforeOperator, @"\$\(([^)]+)\)\.(\w*)$");
        if (match2.Success)
        {
            return new CompletionContext
            {
                Type = CompletionContextType.Indicator,
                FilterPrefix = match2.Groups[2].Value
            };
        }
        
        // 6. 如果只有运算符（后面没有表达式，需要补全）
        // 例如: ... ==  或 ... !=  或 ... > 
        if (operatorMatch.Index + operatorMatch.Length >= trimmedText.Length)
        {
            // 返回通用上下文，允许补全所有表达式类型
            return new CompletionContext
            {
                Type = CompletionContextType.General,
                FilterPrefix = ""
            };
        }
        
        return null;
    }

    /// <summary>
    /// 分析时间框架输入: $(
    /// </summary>
    private CompletionContext? AnalyzeTimeframeInput(string lineText)
    {
        var match = Regex.Match(lineText, @"\$\(([^)]*)$");
        if (match.Success)
        {
            var afterTimeframe = lineText.Substring(match.Index + match.Length);
            if (!afterTimeframe.TrimStart().StartsWith("."))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.Timeframe,
                    FilterPrefix = match.Groups[1].Value.Trim()
                };
            }
        }
        
        return null;
    }

    /// <summary>
    /// 分析新语法指标引用
    /// </summary>
    private CompletionContext? AnalyzeIndicatorReference(string lineText, TextDocument document, int offset)
    {
        // 1. 带括号的指标字段: $(5m).MACD().
        var match1 = Regex.Match(lineText, @"\$\(([^)]+)\)\.(\w+)\(([^)]*)\)\.(\w*)$");
        if (match1.Success && IsValidPattern(lineText, match1))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.IndicatorField,
                IndicatorName = match1.Groups[2].Value,
                FilterPrefix = match1.Groups[4].Value,
                IsInsideFunctionBody = IsInsideBlock(document, offset)
            };
        }
        
        // 2. 不带括号的指标字段: $(5m).MACD.
        var match2 = Regex.Match(lineText, @"\$\(([^)]+)\)\.(\w+)\.(\w*)$");
        if (match2.Success && IsValidPattern(lineText, match2) && 
            _intelliSense.IsValidIndicator(match2.Groups[2].Value))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.IndicatorField,
                IndicatorName = match2.Groups[2].Value,
                FilterPrefix = match2.Groups[3].Value,
                IsInsideFunctionBody = IsInsideBlock(document, offset)
            };
        }
        
        // 3. 指标名补全: $(5m).
        var match3 = Regex.Match(lineText, @"\$\(([^)]*)\)\.(\w*)$");
        if (match3.Success && IsValidPattern(lineText, match3))
        {
            var timeframe = match3.Groups[1].Value.Trim();
            var indicatorPrefix = match3.Groups[2].Value;
            
            if (string.IsNullOrWhiteSpace(timeframe))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.Timeframe,
                    FilterPrefix = ""
                };
            }
            
            if (_intelliSense.IsValidIndicator(indicatorPrefix))
            {
                return new CompletionContext { Type = CompletionContextType.General };
            }
            
            return new CompletionContext
            {
                Type = CompletionContextType.Indicator,
                FilterPrefix = indicatorPrefix
            };
        }
        
        // 4. 指标参数: $(5m).MACD(
        var match4 = Regex.Match(lineText, @"\$\(([^)]+)\)\.(\w+)\(([^)]*)$");
        if (match4.Success && !match4.Groups[3].Value.EndsWith(")"))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.IndicatorParameter,
                IndicatorName = match4.Groups[2].Value,
                FilterPrefix = "",
                Tag = ParseEnteredParameters(match4.Groups[3].Value)
            };
        }
        
        return null;
    }

    /// <summary>
    /// 分析字段值赋值
    /// </summary>
    private CompletionContext? AnalyzeFieldValueAssignment(string lineText)
    {
        // 查找单个等号位置（不是 ==），排除 ==, !=, >=, <= 等情况
        var trimmedText = lineText.TrimEnd();
        var singleEqualsMatch = Regex.Match(trimmedText, @"([^=<>!]|^)=\s*(.*)$");
        if (!singleEqualsMatch.Success)
            return null;
        
        // 检查是否是 ==, !=, >=, <= 等情况
        var beforeEquals = trimmedText.Substring(0, singleEqualsMatch.Index);
        var afterEquals = singleEqualsMatch.Groups[2].Value.Trim();
        
        // 1. 新语法指标字段值: $(5m).MACD().trend =
        var match = Regex.Match(beforeEquals, @"\$\(([^)]+)\)\.(\w+)\(([^)]*)\)\.(\w+)$");
        if (match.Success)
        {
            var fieldName = match.Groups[4].Value;
            
            if (!Regex.IsMatch(fieldName, @"^[A-Z][A-Z0-9_]*$") && string.IsNullOrWhiteSpace(afterEquals))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.FieldValue,
                    IndicatorName = match.Groups[2].Value,
                    FieldName = fieldName,
                    FilterPrefix = ""
                };
            }
        }
        
        // 2. 时间序列函数方法返回值: FUNC().method() =
        // 使用更精确的正则：允许空括号 () 或带参数的括号 (param)
        var match3 = Regex.Match(beforeEquals, 
            @"\b([A-Z][A-Z0-9_]*)\(\)\.(\w+)\((.*?)\)$");
        if (match3.Success && _intelliSense.IsValidTimeSeriesFunction(match3.Groups[1].Value))
        {
            if (string.IsNullOrWhiteSpace(afterEquals) || Regex.IsMatch(afterEquals, @"^[A-Z][\w]*$"))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.TimeSeriesFunctionMethodValue,
                    FunctionName = match3.Groups[1].Value,
                    FieldName = match3.Groups[2].Value,
                    FilterPrefix = afterEquals
                };
            }
        }
        
        // 3. 数据函数字段值: Func(5m).field =
        var match2 = Regex.Match(beforeEquals, 
            @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)$");
        if (match2.Success && _intelliSense.IsValidDataFunction(match2.Groups[1].Value))
        {
            if (string.IsNullOrWhiteSpace(afterEquals) || Regex.IsMatch(afterEquals, @"^[A-Z][\w]*$"))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.DataFunctionFieldValue,
                    FunctionName = match2.Groups[1].Value,
                    FieldName = match2.Groups[2].Value,
                    FilterPrefix = afterEquals
                };
            }
        }
        
        return null;
    }

    /// <summary>
    /// 分析环境变量: @
    /// </summary>
    /// <summary>
    /// 分析变量类型声明: @varname:
    /// </summary>
    private CompletionContext? AnalyzeVariableTypeDeclaration(string lineText)
    {
        // 匹配模式：@varname: （变量名后跟冒号）
        // 变量名规则：以字母或下划线开头，可包含字母、数字、下划线
        var match = Regex.Match(lineText, @"@([a-zA-Z_][a-zA-Z0-9_]*)\s*:\s*$");
        if (match.Success)
        {
            return new CompletionContext
            {
                Type = CompletionContextType.VariableTypeDeclaration,
                FilterPrefix = "" // 类型补全不需要前缀过滤
            };
        }
        
        return null;
    }

    private CompletionContext? AnalyzeEnvVariable(string lineText)
    {
        var match = Regex.Match(lineText, @"@(\w*)$");
        if (match.Success)
        {
            return new CompletionContext
            {
                Type = CompletionContextType.EnvVariable,
                FilterPrefix = match.Groups[1].Value
            };
        }
        
        return null;
    }

    /// <summary>
    /// 分析时间序列函数: FEARGREED(). 或 FEARGREED().method(
    /// </summary>
    private CompletionContext? AnalyzeTimeSeriesFunction(string lineText)
    {
        // 时间序列函数方法参数: FEARGREED().method( 或 FEARGREED().method(param1,
        var methodParamMatch = Regex.Match(lineText, @"\b([A-Z][A-Z0-9_]*)\(\)\.(\w+)\(([^)]*)$");
        if (methodParamMatch.Success)
        {
            var funcName = methodParamMatch.Groups[1].Value;
            var methodName = methodParamMatch.Groups[2].Value;
            if (_intelliSense.IsValidTimeSeriesFunction(funcName))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.ChainedMethodParam,
                    FunctionName = funcName,
                    FieldName = methodName, // 使用 FieldName 存储方法名
                    FilterPrefix = "",
                    Tag = ParseEnteredParameters(methodParamMatch.Groups[3].Value)
                };
            }
        }
        
        // 时间序列函数方法访问: FEARGREED().method 或 FEARGREED().
        var methodMatch = Regex.Match(lineText, @"\b([A-Z][A-Z0-9_]*)\(\)\.(\w*)$");
        if (methodMatch.Success)
        {
            var funcName = methodMatch.Groups[1].Value;
            if (_intelliSense.IsValidTimeSeriesFunction(funcName))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.DataFunctionField, // 复用 DataFunctionField 类型，因为逻辑类似
                    FunctionName = funcName,
                    FilterPrefix = methodMatch.Groups[2].Value
                };
            }
        }
        
        // 时间序列函数（带括号）: FEARGREED(
        var parenMatch = Regex.Match(lineText, @"\b([A-Z][A-Z0-9_]*)\(([^)]*)$");
        if (parenMatch.Success)
        {
            var funcName = parenMatch.Groups[1].Value;
            if (_intelliSense.IsValidTimeSeriesFunction(funcName))
            {
                // 时间序列函数不需要参数，直接补全为 FEARGREED().
                return new CompletionContext
                {
                    Type = CompletionContextType.General, // 这种情况不应该发生，因为时间序列函数总是 FEARGREED()
                    FunctionName = funcName,
                    FilterPrefix = parenMatch.Groups[2].Value
                };
            }
        }
        
        return null;
    }

    /// <summary>
    /// 分析数据函数: KLINE( 或 KLINE
    /// </summary>
    private CompletionContext? AnalyzeDataFunction(string lineText)
    {
        // 数据函数方法参数: Func(5m).method( 或 Func(5m).method(param1,
        var methodParamMatch = Regex.Match(lineText, @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)\(([^)]*)$");
        if (methodParamMatch.Success)
        {
            var funcName = methodParamMatch.Groups[1].Value;
            var methodName = methodParamMatch.Groups[2].Value;
            if (_intelliSense.IsValidDataFunction(funcName))
            {
                return new CompletionContext
                {
                    Type = CompletionContextType.ChainedMethodParam,
                    FunctionName = funcName,
                    FieldName = methodName, // 使用 FieldName 存储方法名
                    FilterPrefix = "",
                    Tag = ParseEnteredParameters(methodParamMatch.Groups[3].Value)
                };
            }
        }
        
        // 数据函数字段访问: Func(5m).field 或 Func(5m).method
        // 使用通用模式匹配，然后验证是否是数据函数
        var fieldMatch = Regex.Match(lineText, @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w*)$");
        if (fieldMatch.Success)
        {
            var funcName = fieldMatch.Groups[1].Value;
            if (_intelliSense.IsValidDataFunction(funcName))
        {
            return new CompletionContext
            {
                    Type = CompletionContextType.DataFunctionField,
                    FunctionName = funcName,
                    FilterPrefix = fieldMatch.Groups[2].Value
            };
            }
        }
        
        // 数据函数（带括号）: Func(
        var parenMatch = Regex.Match(lineText, @"\b([A-Z][A-Z0-9_]*)\(([^)]*)$");
        if (parenMatch.Success)
        {
            var funcName = parenMatch.Groups[1].Value;
            if (_intelliSense.IsValidDataFunction(funcName))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.Timeframe,
                    FunctionName = funcName,
                    FilterPrefix = parenMatch.Groups[2].Value
            };
            }
        }
        
        // 数据函数名（不带括号）: Func（仅在行首或空白后）
        var trimmedLine = lineText.TrimEnd();
        var nameMatch = Regex.Match(trimmedLine, @"^([A-Z][A-Z0-9_]*)$");
        if (nameMatch.Success)
        {
            var funcName = nameMatch.Groups[1].Value;
            if (_intelliSense.IsValidDataFunction(funcName))
        {
            return new CompletionContext
            {
                    Type = CompletionContextType.General,
                    FunctionName = funcName,
                    FilterPrefix = ""
            };
            }
        }
        
        return null;
    }

    /// <summary>
    /// 分析三级链式调用
    /// </summary>
    private CompletionContext? AnalyzeThreeLevelChainedCall(string lineText)
    {
        // 数据函数子方法参数: Func(5m).method(10).submethod( 或 Func(5m).method(10).submethod(param1,
        var subMethodParamMatch = Regex.Match(lineText, @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)\([^)]*\)\.(\w+)\(([^)]*)$");
        if (subMethodParamMatch.Success && _intelliSense.IsValidDataFunction(subMethodParamMatch.Groups[1].Value))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.ChainedMethodParam,
                FunctionName = subMethodParamMatch.Groups[1].Value,
                ParentFieldName = subMethodParamMatch.Groups[2].Value, // 父方法名
                FieldName = subMethodParamMatch.Groups[3].Value, // 子方法名
                FilterPrefix = "",
                Tag = ParseEnteredParameters(subMethodParamMatch.Groups[4].Value)
            };
        }
        
        // 数据函数子字段: Func(5m).method(10).subfield 或 Func(5m).field.subfield
        var match = Regex.Match(lineText, @"([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)(\([^)]*\))?\.(\w*)$");
        if (match.Success && _intelliSense.IsValidDataFunction(match.Groups[1].Value))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.ThreeLevelChainedCall,
                FunctionName = match.Groups[1].Value,
                ParentFieldName = match.Groups[2].Value,
                FilterPrefix = match.Groups[4].Value
            };
        }
        
        return null;
    }

    /// <summary>
    /// 分析信号赋值: } =
    /// </summary>
    private CompletionContext? AnalyzeSignalValue(string lineText)
    {
        var match = Regex.Match(lineText, @"\}\s*=\s*(\w*)$");
        if (match.Success)
        {
            return new CompletionContext
            {
                Type = CompletionContextType.SignalValue,
                FilterPrefix = match.Groups[1].Value
            };
        }
        
        return null;
    }

    /// <summary>
    /// 分析根部输入（信号函数或自定义函数）
    /// </summary>
    private CompletionContext? AnalyzeRootInput(string lineText, TextDocument document, int offset)
    {
        var trimmedLine = lineText.TrimStart();
        var isAtRoot = !IsInsideBlock(document, offset);
        
        if (isAtRoot && Regex.IsMatch(trimmedLine, @"^[a-zA-Z]\w*$"))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.SignalFunctionOrCustom,
                FilterPrefix = trimmedLine
            };
        }
        
        return null;
    }

    /// <summary>
    /// 分析信号函数内部
    /// </summary>
    private CompletionContext? AnalyzeInsideFunction(string lineText, TextDocument document, int offset)
    {
        var trimmedLine = lineText.TrimStart();
        var isAtRoot = !IsInsideBlock(document, offset);
        
        if (!isAtRoot && Regex.IsMatch(trimmedLine, @"^[a-zA-Z]\w*$"))
        {
            return new CompletionContext
            {
                Type = CompletionContextType.InsideSignalFunction,
                FilterPrefix = trimmedLine
            };
        }
        
        return null;
    }

    /// <summary>
    /// 分析运算符提示
    /// </summary>
    private CompletionContext? AnalyzeOperatorHint(string lineText)
    {
        var match = Regex.Match(lineText,
            @"(\$\.(\w+)\([^)]*\)\.(\w+)|([A-Z]+)\([^)]+\)\.(\w+)(\(\))?(\.\w+)?)\s+$");
        if (match.Success)
        {
            return new CompletionContext
            {
                Type = CompletionContextType.Operator
            };
        }
        
        return null;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查是否在完整表达式后（以分号结尾，后面只有空格）
    /// </summary>
    private bool IsAfterCompleteExpression(string lineText)
    {
        // 检查是否以分号结尾，后面只有空格
        var trimmed = lineText.TrimEnd();
        if (!trimmed.EndsWith(";"))
            return false;
        
        // 检查分号前是否有完整的表达式
        // 匹配模式：
        // 1. $(5m).MACD().trend == BULLISH;
        // 2. KLINE(5m).open == 100;
        // 3. FVG(5m).bullish() == true;
        // 4. 其他类似的完整表达式
        
        // 移除分号，检查前面是否有完整的表达式
        var beforeSemicolon = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
        
        // 检查是否包含运算符和值
        var hasOperator = Regex.IsMatch(beforeSemicolon, @"[=<>!]+\s*[A-Z_][A-Z0-9_]*\s*$") ||
                         Regex.IsMatch(beforeSemicolon, @"[=<>!]+\s*\d+\.?\d*\s*$") ||
                         Regex.IsMatch(beforeSemicolon, @"[=<>!]+\s*(true|false)\s*$");
        
        if (hasOperator)
        {
            // 检查分号后是否只有空格
            var afterSemicolon = lineText.Substring(trimmed.Length);
            return string.IsNullOrWhiteSpace(afterSemicolon);
        }
        
        return false;
    }

    /// <summary>
    /// 判断当前位置是否在代码块内部（{}）
    /// </summary>
    private bool IsInsideBlock(TextDocument document, int offset)
    {
        var text = document.GetText(0, offset);
        int openBraces = 0;
        
        foreach (var ch in text)
        {
            if (ch == '{') openBraces++;
            if (ch == '}') openBraces--;
        }
        
        return openBraces > 0;
    }

    /// <summary>
    /// 检查模式是否有效（避免多个点号或空格）
    /// </summary>
    private bool IsValidPattern(string lineText, Match match)
    {
        var afterMatch = lineText.Substring(match.Index + match.Length);
        var trimmedAfter = afterMatch.TrimStart();
        
        // 如果有多个点号或多个空格，不唤起补全
        if (trimmedAfter.StartsWith("..") || trimmedAfter.StartsWith("  "))
        {
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// 解析已输入的参数列表
    /// </summary>
    private HashSet<string> ParseEnteredParameters(string paramsStr)
    {
        var enteredParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        if (string.IsNullOrWhiteSpace(paramsStr))
            return enteredParams;
        
        var parts = paramsStr.Split(',');
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            
            var namedParamMatch = Regex.Match(trimmed, @"^([A-Z][A-Z0-9_]*)\s*=");
            if (namedParamMatch.Success)
            {
                enteredParams.Add(namedParamMatch.Groups[1].Value);
            }
        }
        
        return enteredParams;
    }

    #endregion
}

