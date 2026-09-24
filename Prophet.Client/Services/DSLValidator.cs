using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Prophet.Client.Services.Rules;
using Prophet.Client.Backtest.Strategy;

namespace Prophet.Client.Services;

/// <summary>
/// DSL 语法验证器（版本 2.0 - 基于统一规则系统）
/// </summary>
public class DSLValidator
{
    private readonly IntelliSenseService _intelliSense;
    private readonly DSLRulesService _rulesService;

    public DSLValidator(IntelliSenseService intelliSense, DSLRulesService rulesService)
    {
        _intelliSense = intelliSense;
        _rulesService = rulesService;
    }

    /// <summary>
    /// 验证 DSL 代码
    /// 双层验证：1. 客户端快速验证 2. 核心引擎深度验证
    /// </summary>
    public ValidationResult Validate(string code)
    {
        var result = new ValidationResult();
        
        // ========== 第1层：客户端快速验证 ==========
        // 预处理：识别哪些行在条件块内
        var conditionBlockLines = IdentifyConditionBlockLines(code);
        
        // 预处理：识别哪些行在自定义函数内
        var customFunctionLines = IdentifyCustomFunctionLines(code);
        
        // 提取所有自定义函数定义
        var customFunctions = ExtractCustomFunctions(code);
        
        // 验证自定义函数定义
        ValidateCustomFunctionDefinitions(code, result);
        
        // 验证自定义函数调用
        ValidateCustomFunctionCalls(code, customFunctions, result);
        
        // 全局括号平衡检查（跨行）
        ValidateGlobalBrackets(code, result);
        
        // 检查信号函数是否有完整的信号赋值
        ValidateSignalFunctionAssignments(code, result);
        
        // 验证变量声明和使用
        ValidateVariableDeclarationAndUsage(code, result);
        
        var lines = code.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNumber = i + 1;

            // 跳过空行和注释
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            // 检查各种语法错误（传递上下文信息）
            ValidateLine(line, lineNumber, result, conditionBlockLines.Contains(lineNumber), customFunctionLines.Contains(lineNumber));
        }
        
        // 如果客户端验证已经发现错误，直接返回（不进行核心引擎验证）
        if (result.HasErrors)
        {
            return result;
        }
        
        // ========== 第2层：核心引擎深度验证 ==========
        // 调用核心引擎进行词法和语法分析验证
        try
        {
            var engineResult = ProphetCoreEngine.ValidateDSL(code);
            
            // 合并核心引擎的错误信息
            if (engineResult.HasErrors)
            {
                foreach (var error in engineResult.Errors)
                {
                    result.AddError(error.Line, error.Column, error.Message, error.Length);
                }
            }
            
            // 合并核心引擎的警告信息
            if (engineResult.HasWarnings)
            {
                foreach (var warning in engineResult.Warnings)
                {
                    result.AddWarning(warning.Line, warning.Column, warning.Message, warning.Length);
                }
            }
        }
        catch (Exception ex)
        {
            // 核心引擎验证失败（可能是DLL不可用），只记录警告，不阻止保存
            result.AddWarning(0, 0, $"核心引擎验证不可用: {ex.Message}");
            Console.WriteLine($"⚠️ 核心引擎验证失败: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 识别哪些行在条件块内（ALL{}, ANY{}, WEIGHTED{} 等）
    /// </summary>
    private HashSet<int> IdentifyConditionBlockLines(string code)
    {
        var conditionBlockLines = new HashSet<int>();
        var lines = code.Split('\n');
        int braceDepth = 0;
        bool inConditionBlock = false;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNumber = i + 1;
            
            // 跳过空行和注释
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;
            
            // 检查是否开始条件块（ALL{, ANY{, WEIGHTED{ 等）
            if (Regex.IsMatch(line, @"\b(ALL|ANY|NONE|MIN|COUNT|MAX|WEIGHTED|VOTE)\s*\{"))
            {
                inConditionBlock = true;
                braceDepth = 0; // 重置深度
            }
            
            // 在条件块内时，记录行号
            if (inConditionBlock)
            {
                // 统计当前行的花括号
                foreach (char c in line)
                {
                    if (c == '{') braceDepth++;
                    else if (c == '}') braceDepth--;
                }
                
                conditionBlockLines.Add(lineNumber);
                
                // 如果花括号闭合，退出条件块
                if (braceDepth <= 0)
                {
                    inConditionBlock = false;
                }
            }
        }
        
        return conditionBlockLines;
    }
    
    /// <summary>
    /// 识别哪些行在自定义函数内（用于识别局部变量作用域）
    /// </summary>
    private HashSet<int> IdentifyCustomFunctionLines(string code)
    {
        var customFunctionLines = new HashSet<int>();
        var lines = code.Split('\n');
        int braceDepth = 0;
        bool inCustomFunction = false;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNumber = i + 1;
            
            // 跳过空行和注释
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;
            
            // 检查是否开始自定义函数定义
            // 格式：functionName(params): ReturnType { 或 functionName(params) {
            if (Regex.IsMatch(line, @"^[a-z][a-zA-Z0-9_]*\s*\([^)]*\)(\s*:\s*\w+)?\s*\{"))
            {
                inCustomFunction = true;
                braceDepth = 0; // 重置深度
            }
            
            // 在自定义函数内时，记录行号
            if (inCustomFunction)
            {
                // 统计当前行的花括号
                foreach (char c in line)
                {
                    if (c == '{') braceDepth++;
                    else if (c == '}') braceDepth--;
                }
                
                customFunctionLines.Add(lineNumber);
                
                // 如果花括号闭合，退出自定义函数
                if (braceDepth <= 0)
                {
                    inCustomFunction = false;
                }
            }
        }
        
        return customFunctionLines;
    }
    
    /// <summary>
    /// 提取所有自定义函数的定义信息
    /// </summary>
    private Dictionary<string, CustomFunctionInfo> ExtractCustomFunctions(string code)
    {
        var functions = new Dictionary<string, CustomFunctionInfo>(StringComparer.OrdinalIgnoreCase);
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
                var parameters = new List<(string name, string type)>();
                if (!string.IsNullOrEmpty(paramsStr))
                {
                    var paramParts = paramsStr.Split(',');
                    foreach (var part in paramParts)
                    {
                        var paramMatch = Regex.Match(part.Trim(), @"^([a-z][a-zA-Z0-9_]*)\s*:\s*(\w+)$");
                        if (paramMatch.Success)
                        {
                            parameters.Add((paramMatch.Groups[1].Value, paramMatch.Groups[2].Value));
                        }
                    }
                }
                
                functions[funcName] = new CustomFunctionInfo
                {
                    Name = funcName,
                    Parameters = parameters,
                    ReturnType = returnType,
                    LineNumber = i + 1
                };
            }
        }
        
        return functions;
    }
    
    /// <summary>
    /// 验证自定义函数定义
    /// </summary>
    private void ValidateCustomFunctionDefinitions(string code, ValidationResult result)
    {
        var lines = code.Split('\n');
        
        // 控制流关键字列表
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "if", "else", "for", "while", "return", "break", "continue"
        };
        
        // 验证函数定义格式
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNumber = i + 1;
            
            // 跳过空行和注释
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;
            
            // 检测可能的自定义函数定义（以小写字母开头且有括号和花括号）
            // 必须在行首，且后面有 { 才算函数定义
            var possibleFuncDefMatch = Regex.Match(line, @"^([a-z][a-zA-Z0-9_]*)\s*\(([^)]*)\)(\s*:\s*\w+)?\s*\{");
            if (possibleFuncDefMatch.Success)
            {
                var funcName = possibleFuncDefMatch.Groups[1].Value;
                var paramsStr = possibleFuncDefMatch.Groups[2].Value.Trim();
                var returnTypePart = possibleFuncDefMatch.Groups[3].Value;
                
                // 跳过控制流关键字
                if (keywords.Contains(funcName))
                    continue;
                
                // 检查函数名命名规范
                if (!Regex.IsMatch(funcName, @"^[a-z][a-zA-Z0-9_]*$"))
                {
                    result.AddError(lineNumber, 0, 
                        $"自定义函数名 '{funcName}' 格式不正确，必须以小写字母开头", 
                        funcName.Length);
                }
                
                // 检查是否有返回类型声明
                if (string.IsNullOrEmpty(returnTypePart))
                {
                    result.AddWarning(lineNumber, possibleFuncDefMatch.Length, 
                        $"建议为自定义函数 '{funcName}' 声明返回类型，例如: {funcName}(...): Double {{");
                }
                
                // 验证参数格式
                if (!string.IsNullOrEmpty(paramsStr))
                {
                    var paramParts = paramsStr.Split(',');
                    foreach (var part in paramParts)
                    {
                        var trimmedPart = part.Trim();
                        if (!string.IsNullOrEmpty(trimmedPart))
                        {
                            // 检查参数格式：paramName: Type
                            if (!Regex.IsMatch(trimmedPart, @"^[a-z][a-zA-Z0-9_]*\s*:\s*(Double|Integer|Boolean|String)$"))
                            {
                                result.AddError(lineNumber, 0, 
                                    $"参数 '{trimmedPart}' 格式不正确，应为: paramName: Type（如 price: Double）");
                            }
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 验证自定义函数调用
    /// </summary>
    private void ValidateCustomFunctionCalls(string code, Dictionary<string, CustomFunctionInfo> definedFunctions, ValidationResult result)
    {
        var lines = code.Split('\n');
        var customFunctionLines = IdentifyCustomFunctionLines(code);
        
        // 关键字列表（不是函数调用）
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "if", "else", "for", "while", "return", "break", "continue"
        };
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNumber = i + 1;
            
            // 跳过空行、注释和函数定义行
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || customFunctionLines.Contains(lineNumber))
                continue;
            
            // 使用手动解析来查找函数调用（处理嵌套括号）
            var functionCalls = ExtractFunctionCalls(line);
            
            foreach (var (funcName, argsStr, startIndex) in functionCalls)
            {
                // 跳过控制流关键字
                if (keywords.Contains(funcName))
                    continue;
                
                // 检查前面是否有 . （字段访问），如果有则跳过
                if (startIndex > 0 && line[startIndex - 1] == '.')
                    continue;
                
                // 检查函数是否已定义
                if (definedFunctions.ContainsKey(funcName))
                {
                    var funcInfo = definedFunctions[funcName];
                    
                    // 验证参数数量（智能分割，考虑嵌套括号）
                    var argCount = CountFunctionArguments(argsStr);
                    
                    if (argCount != funcInfo.Parameters.Count)
                    {
                        result.AddError(lineNumber, startIndex, 
                            $"函数 '{funcName}' 需要 {funcInfo.Parameters.Count} 个参数，但提供了 {argCount} 个", 
                            funcName.Length);
                    }
                }
                // 如果不是已知的内置函数，给出警告
                else if (!_rulesService.IsValidDataFunction(funcName) && 
                         !_rulesService.IsValidMathFunction(funcName) &&
                         !_rulesService.IsValidTimeSeriesFunction(funcName))
                {
                    result.AddWarning(lineNumber, startIndex, 
                        $"调用了未定义的自定义函数 '{funcName}'，请确保在使用前已定义", 
                        funcName.Length);
                }
            }
        }
    }
    
    /// <summary>
    /// 提取一行中的所有函数调用（正确处理嵌套括号）
    /// </summary>
    private List<(string name, string args, int index)> ExtractFunctionCalls(string line)
    {
        var calls = new List<(string, string, int)>();
        
        for (int i = 0; i < line.Length; i++)
        {
            // 查找函数名（小写字母开头）
            if (i == 0 || !char.IsLetterOrDigit(line[i - 1]))
            {
                var match = Regex.Match(line.Substring(i), @"^([a-z][a-zA-Z0-9_]*)\s*\(");
                if (match.Success)
                {
                    var funcName = match.Groups[1].Value;
                    var openParenIndex = i + match.Length - 1;
                    
                    // 提取完整的参数列表（处理嵌套括号）
                    var argsStr = ExtractBalancedParentheses(line, openParenIndex);
                    if (argsStr != null)
                    {
                        calls.Add((funcName, argsStr, i));
                        i = openParenIndex + argsStr.Length + 1; // 跳过已处理的部分
                    }
                }
            }
        }
        
        return calls;
    }
    
    /// <summary>
    /// 从指定位置提取平衡括号内的内容
    /// </summary>
    private string? ExtractBalancedParentheses(string text, int openParenIndex)
    {
        if (openParenIndex >= text.Length || text[openParenIndex] != '(')
            return null;
        
        int depth = 1;
        int startIndex = openParenIndex + 1;
        
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    // 找到匹配的右括号
                    return text.Substring(startIndex, i - startIndex);
                }
            }
        }
        
        return null; // 括号不匹配
    }
    
    /// <summary>
    /// 智能计算函数参数数量（考虑嵌套括号）
    /// </summary>
    private int CountFunctionArguments(string argsStr)
    {
        if (string.IsNullOrWhiteSpace(argsStr))
            return 0;
        
        int count = 0;
        int parenDepth = 0;
        bool inArg = false;
        
        foreach (char c in argsStr)
        {
            if (c == '(')
            {
                parenDepth++;
                inArg = true;
            }
            else if (c == ')')
            {
                parenDepth--;
            }
            else if (c == ',' && parenDepth == 0)
            {
                // 顶层逗号，分隔参数
                if (inArg)
                {
                    count++;
                    inArg = false;
                }
            }
            else if (!char.IsWhiteSpace(c))
            {
                inArg = true;
            }
        }
        
        // 最后一个参数
        if (inArg)
            count++;
        
        return count;
    }
    
    private class CustomFunctionInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<(string name, string type)> Parameters { get; set; } = new();
        public string ReturnType { get; set; } = string.Empty;
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// 验证信号函数是否有完整的信号赋值（= BUY/SELL/HOLD）
    /// 使用规则系统验证信号函数名称
    /// </summary>
    private void ValidateSignalFunctionAssignments(string code, ValidationResult result)
    {
        // 获取所有信号函数
        var signalFunctions = _rulesService.GetAllSignalFunctions()
            .Select(f => f.Id)
            .ToArray();
        
        if (signalFunctions.Length == 0)
        {
            // 回退到硬编码列表（兼容性）
            signalFunctions = new[] { "ALL", "ANY", "WEIGHTED", "MIN", "MAX", "COUNT", "VOTE", "NONE" };
        }
        
        var functionNames = string.Join("|", signalFunctions);
        var signalFunctionPattern = $@"({functionNames})(\([^)]+\))?\s*\{{[^}}]*\}}";
        var matches = Regex.Matches(code, signalFunctionPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var fullMatch = match.Value;
            var functionName = match.Groups[1].Value;
            
            // 获取这个信号函数块后面的文本（最多100个字符）
            var endPos = match.Index + match.Length;
            var remainingText = code.Substring(endPos, Math.Min(100, code.Length - endPos));
            
            // 检查是否紧跟着 = BUY/SELL/HOLD
            var assignmentMatch = Regex.Match(remainingText, @"^\s*=\s*(BUY|SELL|HOLD)");
            
            if (!assignmentMatch.Success)
            {
                // 计算错误位置的行号
                var lineNumber = code.Substring(0, match.Index).Split('\n').Length;
                result.AddError(lineNumber, 1, 
                    $"信号函数 '{functionName}' 缺少信号赋值，必须赋值为 BUY、SELL 或 HOLD", 
                    functionName.Length);
            }
        }
    }

    /// <summary>
    /// 全局括号和花括号平衡检查（支持多行结构）
    /// </summary>
    private void ValidateGlobalBrackets(string code, ValidationResult result)
    {
        int parenDepth = 0;
        int braceDepth = 0;
        int lineNumber = 1;
        int columnNumber = 1;
        int lastUnmatchedParenLine = -1;
        int lastUnmatchedBraceLine = -1;

        for (int i = 0; i < code.Length; i++)
        {
            char ch = code[i];

            if (ch == '\n')
            {
                lineNumber++;
                columnNumber = 1;
                continue;
            }

            if (ch == '(')
            {
                parenDepth++;
                if (lastUnmatchedParenLine == -1)
                    lastUnmatchedParenLine = lineNumber;
            }
            else if (ch == ')')
            {
                parenDepth--;
                if (parenDepth < 0)
                {
                    result.AddError(lineNumber, columnNumber, "多余的右括号 ')'", 1);
                    parenDepth = 0; // 重置以继续检查
                }
                else if (parenDepth == 0)
                {
                    lastUnmatchedParenLine = -1;
                }
            }
            else if (ch == '{')
            {
                braceDepth++;
                if (lastUnmatchedBraceLine == -1)
                    lastUnmatchedBraceLine = lineNumber;
            }
            else if (ch == '}')
            {
                braceDepth--;
                if (braceDepth < 0)
                {
                    result.AddError(lineNumber, columnNumber, "多余的右花括号 '}'", 1);
                    braceDepth = 0;
                }
                else if (braceDepth == 0)
                {
                    lastUnmatchedBraceLine = -1;
                }
            }

            columnNumber++;
        }

        // 检查是否有未闭合的括号
        if (parenDepth > 0 && lastUnmatchedParenLine > 0)
        {
            result.AddError(lastUnmatchedParenLine, 1, $"括号未闭合（缺少 {parenDepth} 个 ')'）", 1);
        }

        if (braceDepth > 0 && lastUnmatchedBraceLine > 0)
        {
            result.AddError(lastUnmatchedBraceLine, 1, $"花括号未闭合（缺少 {braceDepth} 个 '}}'）", 1);
        }
    }

    private void ValidateLine(string line, int lineNumber, ValidationResult result, bool isInConditionBlock, bool isInCustomFunction = false)
    {
        // 不再逐行检查括号平衡，因为已在 ValidateGlobalBrackets 中全局检查

        // 检查新语法指标引用格式：$(timeframe).INDICATOR(params).field
        // 支持三种形式：
        //   1. $(5m).MACD(12,26,9).trend  - 位置参数
        //   2. $(5m).MACD().trend         - 空参数列表
        //   3. $(5m).MACD().trend          - 无括号
        if (line.Contains("$("))
        {
            // 匹配 $(timeframe).INDICATOR 模式
            var indicatorPattern = @"\$\(([^)]+)\)\.(\w+)";
            var indicatorMatches = Regex.Matches(line, indicatorPattern);
            
            foreach (Match match in indicatorMatches)
            {
                var timeframe = match.Groups[1].Value.Trim();
                var indicatorName = match.Groups[2].Value;
                var matchStart = match.Index;
                
                // 验证时间框架（使用规则系统）
                if (!_rulesService.IsValidTimeframe(timeframe))
                {
                    result.AddWarning(lineNumber, matchStart + 2, 
                        $"无效的时间框架：{timeframe}，有效值：1m, 5m, 15m, 30m, 1h, 4h, 1d", 
                        timeframe.Length);
                }
                
                // 验证指标名称是否有效（使用规则系统）
                if (!_rulesService.IsValidIndicator(indicatorName))
                {
                    // 检查是否是数据函数（如 KLINE, HIGHEST 等）
                    if (!_rulesService.IsValidDataFunction(indicatorName))
                    {
                        // 只对明显不是 DSL 函数的名称给出警告
                        if (!Regex.IsMatch(indicatorName, @"^[A-Z][A-Z0-9_]*$", RegexOptions.IgnoreCase))
                        {
                            result.AddWarning(lineNumber, matchStart + match.Groups[1].Length + 4, 
                                $"未知的指标或函数：{indicatorName}，请检查拼写", 
                                indicatorName.Length);
                        }
                    }
                }
                
                // 检查是否在参数赋值上下文中：$(timeframe).INDICATOR.PARAM_NAME = value
                var afterIndicator = line.Substring(matchStart + match.Length);
                var paramAssignmentMatch = Regex.Match(afterIndicator, @"^\.(\w+)\s*=\s*(.+?)(?:;|$)");
                if (paramAssignmentMatch.Success)
                {
                    var paramName = paramAssignmentMatch.Groups[1].Value;
                    var paramValue = paramAssignmentMatch.Groups[2].Value.Trim();
                    
                    // 验证参数名是否存在
                    if (_rulesService.IsValidIndicator(indicatorName))
                    {
                        if (!_rulesService.IsValidIndicatorParameter(indicatorName, paramName))
                        {
                            result.AddError(lineNumber, matchStart + match.Length + 1, 
                                $"指标 '{indicatorName}' 没有参数 '{paramName}'", 
                                paramName.Length);
                        }
                        else
                        {
                            // 验证参数值的类型和范围
                            ValidateParameterValue(indicatorName, paramName, paramValue, lineNumber, result);
                        }
                    }
                    
                    // 参数名应该是全大写+下划线格式
                    if (!Regex.IsMatch(paramName, @"^[A-Z][A-Z0-9_]*$"))
                    {
                        result.AddWarning(lineNumber, matchStart + match.Length + 1, 
                            $"参数名 '{paramName}' 格式不正确，建议使用全大写+下划线（如：FAST_PERIOD）", 
                            paramName.Length);
                    }
                }
                else
                {
                    // 检查字段访问：支持多种形式
                    // 1. $(timeframe).INDICATOR().field - 空括号
                    // 2. $(timeframe).INDICATOR(params).field - 带参数括号（内联参数）
                    // 3. $(timeframe).INDICATOR.field - 无括号
                    // 4. $(timeframe).INDICATOR(params).field(offset) - 带offset参数
                    // 5. $(timeframe).INDICATOR(params).field() - 空offset（等同于offset=0）
                    var fieldAccessMatch = Regex.Match(afterIndicator, @"^(\([^)]*\))?\.(\w+)(\([^)]*\))?");
                    if (fieldAccessMatch.Success)
                    {
                        var fieldName = fieldAccessMatch.Groups[2].Value;
                        var offsetPart = fieldAccessMatch.Groups[3].Value; // 可能为空
                        
                        // 如果有offset参数，验证其值是否合法
                        if (!string.IsNullOrEmpty(offsetPart))
                        {
                            // 提取括号内的值
                            var offsetMatch = Regex.Match(offsetPart, @"^\(([^)]*)\)$");
                            if (offsetMatch.Success)
                            {
                                var offsetValueStr = offsetMatch.Groups[1].Value.Trim();
                                
                                // 空括号是合法的（等同于offset=0）
                                if (!string.IsNullOrEmpty(offsetValueStr))
                                {
                                    // 验证offset是否为数字
                                    if (double.TryParse(offsetValueStr, out double offsetValue))
                                    {
                                        // 验证offset范围：[-100, 0]
                                        if (offsetValue < -100 || offsetValue > 0)
                                        {
                                            result.AddError(lineNumber, matchStart + match.Length + fieldAccessMatch.Groups[1].Length + 1 + fieldName.Length + 1,
                                                $"字段offset必须在范围 [-100, 0] 内，当前值: {offsetValue}",
                                                offsetValueStr.Length);
                                        }
                                        // 警告：offset应该是整数
                                        else if (offsetValue != Math.Floor(offsetValue))
                                        {
                                            result.AddWarning(lineNumber, matchStart + match.Length + fieldAccessMatch.Groups[1].Length + 1 + fieldName.Length + 1,
                                                $"字段offset通常应该是整数，当前值: {offsetValue}");
                                        }
                                    }
                                    else
                                    {
                                        result.AddError(lineNumber, matchStart + match.Length + fieldAccessMatch.Groups[1].Length + 1 + fieldName.Length + 1,
                                            $"字段offset必须是数字常量，例如: value(-1)，当前值: {offsetValueStr}",
                                            offsetValueStr.Length);
                                    }
                                }
                            }
                        }
                        
                        // 验证字段是否存在
                        if (_rulesService.IsValidIndicator(indicatorName))
                        {
                            if (!_rulesService.IsValidIndicatorField(indicatorName, fieldName))
                            {
                                result.AddError(lineNumber, matchStart + match.Length + fieldAccessMatch.Groups[1].Length + 1, 
                                    $"指标 '{indicatorName}' 没有字段 '{fieldName}'", 
                                    fieldName.Length);
                            }
                            else
                            {
                                // 检查是否对只读字段进行赋值（单个=，而不是==比较）
                                // 注意：在条件块内（ALL/ANY/WEIGHTED等），= 是比较运算符，不是赋值
                                var afterField = afterIndicator.Substring(fieldAccessMatch.Length);
                                if (Regex.IsMatch(afterField, @"^\s*=(?!=)"))  // 修改：排除 ==
                                {
                                    if (!isInConditionBlock && _rulesService.IsFieldReadonly(indicatorName, fieldName))
                                    {
                                        result.AddError(lineNumber, matchStart + match.Length + fieldAccessMatch.Length, 
                                            $"字段 '{fieldName}' 是只读的，不能赋值。只有参数（如 {indicatorName.ToUpper()}_PERIOD）可以修改。");
                                    }
                                    else if (isInConditionBlock)
                                    {
                                        // 在条件块内使用单个 =，给出提示建议使用 ==
                                        result.AddWarning(lineNumber, matchStart + match.Length + fieldAccessMatch.Length,
                                            $"建议在条件块内使用 '==' 进行比较以避免歧义（虽然 '=' 也可用）");
                                    }
                                }
                            }
                        }
                    }
                    else if (!Regex.IsMatch(afterIndicator, @"^(\([^)]*\))?(\.\w+(\([^)]*\))?)?\s*(;|$|\)|}|=|>|<|!|AND|OR|\|\||&&)"))
                    {
                        // 既不是参数赋值，也不是字段访问，也不是布尔简写
                        result.AddWarning(lineNumber, matchStart + match.Length, 
                            $"指标 '{indicatorName}' 后应访问字段或参数，例如：$(timeframe).{indicatorName}().field 或 $(timeframe).{indicatorName}(params).field(offset)");
                    }
                }
            }
        }
        
        // 验证数据函数调用（KLINE, HIGHEST, AVERAGE, FVG, ORDERBLOCK 等）
        ValidateDataFunctions(line, lineNumber, result);
        
        // 验证时间序列函数调用（FUNDINGRATE, FEARGREED 等）
        ValidateTimeSeriesFunctions(line, lineNumber, result);
        
        // 验证数学函数调用（ABS, ROUND, SIN, COS 等）
        ValidateMathFunctions(line, lineNumber, result);
        
        // 验证枚举值比较
        ValidateEnumComparisons(line, lineNumber, result);
        
        // 检查是否还有旧语法 $.（应该报错）
        if (line.Contains("$."))
        {
            var oldSyntaxMatches = Regex.Matches(line, @"\$\.(\w+)");
            foreach (Match match in oldSyntaxMatches)
            {
                result.AddError(lineNumber, match.Index + 1, 
                    $"旧语法已废弃，请使用新语法：$(timeframe).{match.Groups[1].Value}(params).field，例如：$(5m).{match.Groups[1].Value}(12,26,9).trend", 
                    match.Groups[1].Value.Length + 2);
            }
        }

        // 检查用户变量格式（已废弃环境变量概念）
        if (line.Contains("@"))
        {
            var matches = Regex.Matches(line, @"@(\w+)");
            foreach (Match match in matches)
            {
                var varName = match.Groups[1].Value;
                
                // 用户变量命名规范：以_或字母开头，可包含字母、数字、下划线
                // 符合大多数编程语言的命名规范（如 @score, @MY_VAR, @_temp, @value123）
                if (!Regex.IsMatch(varName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                {
                    result.AddWarning(lineNumber, match.Index + 1, 
                        $"变量 '@{varName}' 不符合命名规范，应以字母或下划线开头，可包含字母、数字、下划线");
                }
            }
        }

        // 检查信号定义
        if (line.Contains("BUY") || line.Contains("SELL") || line.Contains("HOLD"))
        {
            // 信号应该以 = 开头或结尾
            if (!line.Contains("="))
            {
                result.AddError(lineNumber, 1, "信号定义缺少赋值符号");
            }
        }

        // 检查常见的拼写错误
        CheckTypos(line, lineNumber, result);
    }

    /// <summary>
    /// 验证参数值的类型和范围
    /// </summary>
    private void ValidateParameterValue(string indicatorName, string paramName, string value, int lineNumber, ValidationResult result)
    {
        var indicator = _rulesService.GetIndicator(indicatorName);
        if (indicator?.Parameters == null) return;

        var param = indicator.Parameters.FirstOrDefault(p => p.Name == paramName);
        if (param == null) return;

        // 验证类型
        if (param.Type == "integer")
        {
            if (!int.TryParse(value, out int intValue))
            {
                result.AddError(lineNumber, 1, $"参数 '{paramName}' 需要整数值，但得到: {value}");
                return;
            }

            // 验证范围
            if (param.Min.HasValue && intValue < param.Min.Value)
            {
                result.AddError(lineNumber, 1, 
                    $"参数 '{paramName}' 的值 {intValue} 小于最小值 {param.Min.Value}");
            }
            if (param.Max.HasValue && intValue > param.Max.Value)
            {
                result.AddError(lineNumber, 1, 
                    $"参数 '{paramName}' 的值 {intValue} 大于最大值 {param.Max.Value}");
            }
        }
        else if (param.Type == "double" || param.Type == "float")
        {
            if (!double.TryParse(value, out double doubleValue))
            {
                result.AddError(lineNumber, 1, $"参数 '{paramName}' 需要数值，但得到: {value}");
                return;
            }

            // 验证范围
            if (param.Min.HasValue && doubleValue < param.Min.Value)
            {
                result.AddError(lineNumber, 1, 
                    $"参数 '{paramName}' 的值 {doubleValue} 小于最小值 {param.Min.Value}");
            }
            if (param.Max.HasValue && doubleValue > param.Max.Value)
            {
                result.AddError(lineNumber, 1, 
                    $"参数 '{paramName}' 的值 {doubleValue} 大于最大值 {param.Max.Value}");
            }
        }
        else if (param.Type == "boolean" || param.Type == "bool")
        {
            if (value.ToLower() != "true" && value.ToLower() != "false")
            {
                result.AddError(lineNumber, 1, $"参数 '{paramName}' 需要布尔值（true/false），但得到: {value}");
            }
        }
    }

    /// <summary>
    /// 验证数据函数调用的格式（使用规则系统）
    /// </summary>
    private void ValidateDataFunctions(string line, int lineNumber, ValidationResult result)
    {
        // 获取所有数据函数
        var dataFunctions = _rulesService.GetAllDataFunctions();
        
        foreach (var func in dataFunctions)
        {
            // 匹配函数调用：FUNCTION(args)
            var funcPattern = $@"\b{func.Id}\(([^)]+)\)";
            var funcMatches = Regex.Matches(line, funcPattern);
            
            foreach (Match match in funcMatches)
            {
                var args = match.Groups[1].Value.Trim();
                var matchStart = match.Index;
                
                // 检查这是否是指标引用（前面有 $(...).)
                // 例如：$(5m).ATR(14).value - 这是指标，不是数据函数
                // 数据函数应该是：ATR(5m).close(14).value
                if (matchStart > 0)
                {
                    var beforeMatch = line.Substring(0, matchStart);
                    // 检查前面是否有 $(timeframe). 模式
                    if (Regex.IsMatch(beforeMatch, @"\$\([^)]+\)\.\s*$"))
                    {
                        // 这是指标引用，不是数据函数调用，跳过验证
                        continue;
                    }
                }
                
                // 第一个参数通常是时间框架（对大多数数据函数）
                var firstArg = args.Split(',')[0].Trim();
                
                // 验证时间框架（如果第一个参数看起来像时间框架）
                if (Regex.IsMatch(firstArg, @"^\d+[mhdw]$"))
                {
                    if (!_rulesService.IsValidTimeframe(firstArg))
                    {
                        result.AddWarning(lineNumber, matchStart, 
                            $"函数 '{func.Id}' 的时间框架 '{firstArg}' 无效");
                    }
                }
                
                // 检查函数后是否有方法访问
                var afterFunc = line.Substring(matchStart + match.Length);
                var methodAccessMatch = Regex.Match(afterFunc, @"^\.(\w+)");
                
                if (methodAccessMatch.Success)
                {
                    var methodName = methodAccessMatch.Groups[1].Value;
                    
                    // 验证方法是否存在
                    if (!_rulesService.IsValidDataFunctionMethod(func.Id, methodName))
                    {
                        result.AddError(lineNumber, matchStart + match.Length + 1, 
                            $"函数 '{func.Id}' 没有方法 '{methodName}'", 
                            methodName.Length);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 验证时间序列函数调用（使用规则系统）
    /// </summary>
    private void ValidateTimeSeriesFunctions(string line, int lineNumber, ValidationResult result)
    {
        // 获取所有时间序列函数
        var timeSeriesFunctions = _rulesService.GetAllTimeSeriesFunctions();
        
        foreach (var func in timeSeriesFunctions)
        {
            // 匹配时间序列函数调用：FUNCTION().method(args)
            // 注意：需要匹配完整的调用，包括后面可能有运算符的情况
            // 例如：FUNDINGRATE().trend() == RISING 或 FUNDINGRATE().trend()==RISING
            // 使用非贪婪匹配，允许后面有内容
            var funcPattern = $@"\b{func.Id}\(\)\.(\w+)\(([^)]*)\)";
            var funcMatches = Regex.Matches(line, funcPattern);
            
            foreach (Match match in funcMatches)
            {
                var methodName = match.Groups[1].Value;
                var matchStart = match.Index;
                
                // 验证方法是否存在
                if (!_rulesService.IsValidTimeSeriesFunctionMethod(func.Id, methodName))
                {
                    result.AddError(lineNumber, matchStart + func.Id.Length + 3, $"时间序列函数 '{func.Id}' 没有方法 '{methodName}'", methodName.Length);
                }
            }
        }
    }

    /// <summary>
    /// 验证枚举值比较（如 == RISING, == BULLISH 等）
    /// </summary>
    private void ValidateEnumComparisons(string line, int lineNumber, ValidationResult result)
    {
        // 匹配模式：表达式 == 枚举值 或 表达式 = 枚举值（支持有空格和没有空格）
        // 例如：FUNDINGRATE().trend() == RISING
        //      FUNDINGRATE().trend() ==RISING
        //      $(5m).RSI().trend == BULLISH
        
        // 匹配时间序列函数方法返回值比较：FUNC().method() == ENUM_VALUE 或 FUNC().method() ==ENUM_VALUE
        var timeSeriesPattern = @"\b([A-Z][A-Z0-9_]*)\(\)\.(\w+)\([^)]*\)\s*([=!<>]+)\s*([A-Z][A-Z0-9_]*)";
        var timeSeriesMatches = Regex.Matches(line, timeSeriesPattern);
            
        foreach (Match match in timeSeriesMatches)
        {
            var funcName = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            var enumValue = match.Groups[4].Value;
            var matchStart = match.Index;
            
            if (_rulesService.IsValidTimeSeriesFunction(funcName))
            {
                var function = _rulesService.GetTimeSeriesFunction(funcName);
                var method = function?.Methods?.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
                
                if (method != null && method.ReturnType.Equals("enum", StringComparison.OrdinalIgnoreCase))
                {
                    // trend() 方法返回 TrendDirectionType 枚举
                    if (methodName.Equals("trend", StringComparison.OrdinalIgnoreCase))
                    {
                        var validValues = _rulesService.GetEnumValues("TrendDirectionType");
                        
                        if (validValues != null && !validValues.Contains(enumValue))
                        {
                            var enumValueStart = matchStart + match.Length - enumValue.Length;
                            result.AddError(lineNumber, enumValueStart, $"无效的枚举值 '{enumValue}'，有效值：{string.Join(", ", validValues)}", enumValue.Length);
                        }
                    }
                    // 如果将来有其他返回枚举的方法，可以在这里添加
                }
            }
        }
        
        // 匹配指标字段值比较：$(timeframe).INDICATOR().field == ENUM_VALUE 或 $(timeframe).INDICATOR().field ==ENUM_VALUE
        var indicatorPattern = @"\$\(([^)]+)\)\.(\w+)\([^)]*\)\.(\w+)\s*([=!<>]+)\s*([A-Z][A-Z0-9_]*)";
        var indicatorMatches = Regex.Matches(line, indicatorPattern);
        
        foreach (Match match in indicatorMatches)
        {
            var indicatorName = match.Groups[2].Value;
            var fieldName = match.Groups[3].Value;
            var enumValue = match.Groups[5].Value;
            var matchStart = match.Index;
            
            if (_rulesService.IsValidIndicator(indicatorName))
            {
                var indicator = _rulesService.GetIndicator(indicatorName);
                var field = indicator?.Fields?.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                
                if (field != null)
                {
                    // 检查字段是否有枚举值定义
                    if (field.EnumValues != null && field.EnumValues.Count > 0)
                    {
                        if (!field.EnumValues.Contains(enumValue))
                        {
                            var enumValueStart = matchStart + match.Length - enumValue.Length;
                            result.AddError(lineNumber, enumValueStart, 
                                $"无效的枚举值 '{enumValue}'，有效值：{string.Join(", ", field.EnumValues)}", 
                                enumValue.Length);
                        }
                    }
                    // 检查字段类型是否为 enum:EnumName 格式
                    else if (field.Type.StartsWith("enum:", StringComparison.OrdinalIgnoreCase))
                    {
                        var enumName = field.Type.Substring(5);
                        var validValues = _rulesService.GetEnumValues(enumName);
                        if (validValues != null && !validValues.Contains(enumValue))
                        {
                            var enumValueStart = matchStart + match.Length - enumValue.Length;
                            result.AddError(lineNumber, enumValueStart, 
                                $"无效的枚举值 '{enumValue}'，有效值：{string.Join(", ", validValues)}", 
                                enumValue.Length);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 验证数学函数调用（使用规则系统）
    /// </summary>
    private void ValidateMathFunctions(string line, int lineNumber, ValidationResult result)
    {
        // 获取所有数学函数
        var mathFunctions = _rulesService.GetAllMathFunctions();
        
        foreach (var func in mathFunctions)
        {
            // 匹配函数调用：FUNCTION(args)
            var funcPattern = $@"\b{func.Id}\(";
            var funcMatches = Regex.Matches(line, funcPattern);
            
            foreach (Match match in funcMatches)
            {
                var matchStart = match.Index;
                
                // 验证数学函数名称有效（基本已经通过正则匹配）
                // 这里主要是记录使用情况，未来可以扩展为参数数量验证
                
                // 可以在这里添加参数数量验证
                // 例如：ABS(x) 应该只有一个参数
                // 但这需要更复杂的括号匹配逻辑
            }
        }
    }

    private void CheckTypos(string line, int lineNumber, ValidationResult result)
    {
        // 检查常见拼写错误
        var typos = new Dictionary<string, string>
        {
            { "MAD", "MACD" },
            { "MACDD", "MACD" },
            { "BBOLLINGER", "BBANDS" },
            { "BOLINGER", "BBANDS" },
        };

        foreach (var typo in typos)
        {
            if (line.Contains(typo.Key))
            {
                var index = line.IndexOf(typo.Key);
                result.AddWarning(lineNumber, index + 1, $"可能的拼写错误: '{typo.Key}', 是否应为 '{typo.Value}'?");
            }
        }
    }

    /// <summary>
    /// 验证变量声明和使用
    /// 检查：1. 使用的变量是否已声明  2. 变量作用域规则
    /// </summary>
    private void ValidateVariableDeclarationAndUsage(string code, ValidationResult result)
    {
        var lines = code.Split('\n');
        var globalDeclaredVars = new HashSet<string>(); // 全局声明的变量
        var currentFunctionVars = new HashSet<string>(); // 当前函数内的局部变量
        var inFunctionBody = false;
        var functionDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNumber = i + 1;

            // 跳过空行和注释
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            // 检测函数定义开始
            var funcDefMatch = Regex.Match(line, @"^([a-z][a-zA-Z0-9_]*)\s*\([^)]*\)\s*:");
            if (funcDefMatch.Success)
            {
                inFunctionBody = false; // 函数定义行本身不算函数体
                currentFunctionVars.Clear();
                continue;
            }

            // 检测函数体开始
            if (line.Contains("{"))
            {
                if (!inFunctionBody && !line.Contains("ALL{") && !line.Contains("ANY{") && 
                    !line.Contains("MIN(") && !line.Contains("WEIGHTED(") && !line.Contains("NONE{"))
                {
                    inFunctionBody = true;
                    functionDepth = 0;
                }
                functionDepth += line.Count(c => c == '{');
            }

            // 检测函数体结束
            if (line.Contains("}"))
            {
                functionDepth -= line.Count(c => c == '}');
                if (inFunctionBody && functionDepth <= 0)
                {
                    inFunctionBody = false;
                    currentFunctionVars.Clear();
                }
            }

            // 检测变量声明（@var: Type 或 @var: Type = value）
            var declMatches = Regex.Matches(line, @"@([a-zA-Z_][a-zA-Z0-9_]*)\s*:");
            foreach (Match match in declMatches)
            {
                var varName = match.Groups[1].Value;
                if (inFunctionBody)
                {
                    currentFunctionVars.Add(varName);
                }
                else
                {
                    globalDeclaredVars.Add(varName);
                }
            }

            // 检测变量赋值（@var = value，非声明）
            var assignMatches = Regex.Matches(line, @"@([a-zA-Z_][a-zA-Z0-9_]*)\s*=");
            foreach (Match match in assignMatches)
            {
                // 排除声明语句（@var: Type = value）
                var beforeAt = line.Substring(0, match.Index);
                if (beforeAt.Contains(":"))
                    continue;
                    
                var varName = match.Groups[1].Value;
                
                // 如果不在函数体，这是全局变量赋值，应该已经声明
                if (!inFunctionBody && !globalDeclaredVars.Contains(varName))
                {
                    result.AddError(lineNumber, match.Index + 1, 
                        $"变量 '@{varName}' 在赋值前未声明，请先使用 '@{varName}: Type' 进行声明");
                }
            }

            // 检测变量使用（不是声明也不是赋值）
            var usageMatches = Regex.Matches(line, @"@([a-zA-Z_][a-zA-Z0-9_]*)");
            foreach (Match match in usageMatches)
            {
                var varName = match.Groups[1].Value;
                
                // 检查是否是声明或赋值语句的一部分
                var afterVar = line.Substring(match.Index + match.Length);
                if (afterVar.TrimStart().StartsWith(":") || afterVar.TrimStart().StartsWith("="))
                    continue;

                // 检查变量是否已声明
                bool isDeclared = globalDeclaredVars.Contains(varName) || 
                                 (inFunctionBody && currentFunctionVars.Contains(varName));
                
                if (!isDeclared)
                {
                    result.AddError(lineNumber, match.Index + 1, 
                        $"变量 '@{varName}' 在使用前未声明，请先使用 '@{varName}: Type' 进行声明", 
                        match.Length);
                }
            }
        }
    }

    /// <summary>
    /// 快速验证（只检查严重错误）
    /// </summary>
    public List<ValidationError> QuickValidate(string code)
    {
        var errors = new List<ValidationError>();
        
        // 检查括号匹配
        var openParens = code.Count(c => c == '(');
        var closeParens = code.Count(c => c == ')');
        if (openParens != closeParens)
        {
            errors.Add(new ValidationError(0, 0, "括号数量不匹配", ValidationSeverity.Error));
        }

        var openBraces = code.Count(c => c == '{');
        var closeBraces = code.Count(c => c == '}');
        if (openBraces != closeBraces)
        {
            errors.Add(new ValidationError(0, 0, "花括号数量不匹配", ValidationSeverity.Error));
        }

        return errors;
    }
}

public class ValidationResult
{
    public List<ValidationError> Errors { get; } = new();
    public List<ValidationError> Warnings { get; } = new();

    public bool HasErrors => Errors.Count > 0;
    public bool HasWarnings => Warnings.Count > 0;
    public bool IsValid => !HasErrors;

    public void AddError(int line, int column, string message, int length = 0)
    {
        Errors.Add(new ValidationError(line, column, message, ValidationSeverity.Error, length));
    }

    public void AddWarning(int line, int column, string message, int length = 0)
    {
        Warnings.Add(new ValidationError(line, column, message, ValidationSeverity.Warning, length));
    }
    
    public void AddInfo(int line, int column, string message, int length = 0)
    {
        Warnings.Add(new ValidationError(line, column, message, ValidationSeverity.Info, length));
    }

    public IEnumerable<ValidationError> AllIssues => Errors.Concat(Warnings);
}

public class ValidationError
{
    public int Line { get; }
    public int Column { get; }
    public string Message { get; }
    public ValidationSeverity Severity { get; }
    public int Length { get; }  // 错误范围长度，用于精确定位波浪线

    public ValidationError(int line, int column, string message, ValidationSeverity severity, int length = 0)
    {
        Line = line;
        Column = column;
        Message = message;
        Severity = severity;
        Length = length;
    }

    public override string ToString()
    {
        var severityText = Severity == ValidationSeverity.Error ? "错误" : 
                          Severity == ValidationSeverity.Warning ? "警告" : "提示";
        return $"第 {Line} 行, 第 {Column} 列: {severityText} - {Message}";
    }
}

public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}

