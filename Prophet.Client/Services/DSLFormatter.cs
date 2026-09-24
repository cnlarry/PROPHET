using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Prophet.Client.Services.Editor.Formatting;

namespace Prophet.Client.Services;

/// <summary>
/// DSL 代码格式化器 - 使用 FormatRules.json 配置
/// </summary>
public static class DSLFormatter
{
    private static FormatRuleEngine? _ruleEngine;
    private static FormatRules? _rules;

    /// <summary>
    /// 获取格式化规则
    /// </summary>
    private static FormatRules GetRules()
    {
        if (_rules == null)
        {
            _ruleEngine = FormatRuleEngine.Instance;
            _rules = _ruleEngine.GetRules();
        }
        return _rules;
    }

    /// <summary>
    /// 格式化 DSL 代码
    /// </summary>
    public static string Format(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;

        var rules = GetRules();
        var indentSize = rules.Indentation?.Size ?? 4;
        var indent = rules.Indentation?.Type == "tabs" ? "\t" : new string(' ', indentSize);

        // 预处理：应用自定义规则
        code = ApplyCustomRules(code, rules);
        
        // 预处理：规范化信号赋值格式
        code = PreprocessCode(code, rules);

        var lines = code.Split('\n');
        var formatted = new StringBuilder();
        int indentLevel = 0;
        bool lastWasSignalFunction = false; // 标记上一行是否是信号函数结束

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.Trim();

            // 处理空行 - 不保留空行（但会在信号函数之间添加）
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            // 处理注释
            if (trimmed.StartsWith("//"))
            {
                formatted.Append(FormatComment(trimmed, indentLevel, indent, rules));
                formatted.AppendLine();
                lastWasSignalFunction = false;
                continue;
            }

            // 检查是否是信号函数开始（ALL {, ANY {, 等）
            bool isSignalFunctionStart = Regex.IsMatch(trimmed, @"^\s*(ALL|ANY|NONE|MIN|COUNT|MAX|WEIGHTED|VOTE)\s*\{");
            
            // 如果上一行是信号函数结束，且当前行是新的信号函数开始，添加空行
            if (lastWasSignalFunction && isSignalFunctionStart)
            {
                formatted.AppendLine();
            }

            // 检查是否需要减少缩进（闭合花括号）
            if (trimmed.StartsWith("}"))
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }

            // 应用缩进
            formatted.Append(new string(' ', indentLevel * indent.Length));

            // 格式化行内容
            var formattedLine = FormatLine(trimmed, rules);
            
            // 处理长行换行
            var maxLineLength = rules.Wrapping?.MaxLineLength ?? 120;
            if (rules.Wrapping?.WrapLongConditions == true && formattedLine.Length > maxLineLength)
            {
                formattedLine = WrapLongLine(formattedLine, indentLevel, indent, rules);
            }
            
            formatted.AppendLine(formattedLine);

            // 检查是否是信号函数结束（} = BUY/SELL/HOLD）
            bool isSignalFunctionEnd = Regex.IsMatch(trimmed, @"\}\s*=\s*(BUY|SELL|HOLD)");
            lastWasSignalFunction = isSignalFunctionEnd;

            // 检查是否需要增加缩进（开放花括号）
            if (trimmed.EndsWith("{"))
            {
                indentLevel++;
            }
        }

        return formatted.ToString().TrimEnd();
    }

    /// <summary>
    /// 预处理代码
    /// </summary>
    private static string PreprocessCode(string code, FormatRules rules)
    {
        // 【新增】将条件块内的单等号 = 转换为双等号 ==（提升代码清晰度）
        code = NormalizeSingleEqualsInConditionBlocks(code);
        
        // 将 }=SELL 规范化为 } = SELL（同一行，添加空格）
        code = Regex.Replace(code, @"\}\s*=\s*(BUY|SELL|HOLD)", "} = $1");

        // 为信号枚举后自动补充分号（如果缺少）
        code = Regex.Replace(code, @"(}\s*=\s*(BUY|SELL|HOLD))(?!;)(?=\s*[\$@\w])", "$1;");

        // 将 ;} 修正为 ;\n}（分两行）
        if (rules.Braces?.ClosingOnNewLine == true)
        {
            code = Regex.Replace(code, @";\s*\}", ";\n}");
        }

        // 将 ;后紧跟非空白字符（除了}）换行
        if (rules.LineBreaks?.BetweenConditions == false)
        {
            // 如果 betweenConditions 为 false，则条件之间不换行
            // 但这里我们仍然需要分隔条件，所以只在信号块内换行
            code = Regex.Replace(code, @";(?!\s*$)(?!\s*\})", ";\n");
        }
        else
        {
            code = Regex.Replace(code, @";(?!\s*$)(?!\s*\})", ";\n");
        }

        return code;
    }

    /// <summary>
    /// 将条件块内的单等号 = 转换为双等号 ==
    /// </summary>
    /// <remarks>
    /// 这个方法会扫描代码，识别信号函数的条件块（ALL{}, ANY{}, WEIGHTED{} 等），
    /// 并将其中的单等号 = 转换为双等号 ==，以提升代码清晰度并避免歧义。
    /// 
    /// 处理逻辑：
    /// 1. 识别条件块的开始（ALL{, ANY{, 等）
    /// 2. 在条件块内查找单等号 = （排除 ==, !=, >=, <=）
    /// 3. 替换为双等号 ==
    /// 4. 识别条件块的结束（} =）
    /// </remarks>
    private static string NormalizeSingleEqualsInConditionBlocks(string code)
    {
        var lines = code.Split('\n');
        var result = new List<string>();
        bool inConditionBlock = false;
        int braceDepth = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // 跳过空行和注释
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
            {
                result.Add(line);
                continue;
            }
            
            // 检查是否进入条件块
            if (Regex.IsMatch(trimmed, @"^\s*(ALL|ANY|NONE|MIN|COUNT|MAX|WEIGHTED|VOTE)\s*\{"))
            {
                inConditionBlock = true;
                braceDepth = 0;
            }
            
            var processedLine = line;
            
            if (inConditionBlock)
            {
                // 统计花括号深度
                foreach (char c in trimmed)
                {
                    if (c == '{') braceDepth++;
                    else if (c == '}') braceDepth--;
                }
                
                // 在条件块内，将单等号 = 转换为双等号 ==
                // 排除：已经是 ==, !=, >=, <=, += 等的情况
                processedLine = Regex.Replace(processedLine, 
                    @"(?<![=!<>+\-*/])=(?![=])(?!\s*$)", // 匹配单独的 =，排除已经是运算符的情况
                    "==");
                
                // 检查是否退出条件块（} = BUY/SELL/HOLD）
                if (braceDepth <= 0 && Regex.IsMatch(trimmed, @"\}\s*=\s*(BUY|SELL|HOLD)"))
                {
                    // 将 } = 中的 = 保持为单等号（这是信号赋值，不是比较）
                    processedLine = Regex.Replace(processedLine, @"\}\s*==\s*(BUY|SELL|HOLD)", "} = $1");
                    inConditionBlock = false;
                }
            }
            
            result.Add(processedLine);
        }
        
        return string.Join("\n", result);
    }

    /// <summary>
    /// 格式化注释
    /// </summary>
    private static string FormatComment(string comment, int indentLevel, string indent, FormatRules rules)
    {
        var commentRules = rules.Comments;
        if (commentRules == null)
            return new string(' ', indentLevel * indent.Length) + comment;

        // 添加斜杠后空格
        if (commentRules.AddSpaceAfterSlash && comment.StartsWith("//") && comment.Length > 2 && comment[2] != ' ')
        {
            comment = "// " + comment.Substring(2).TrimStart();
        }

        return new string(' ', indentLevel * indent.Length) + comment;
    }

    /// <summary>
    /// 格式化单行代码
    /// </summary>
    private static string FormatLine(string line, FormatRules rules)
    {
        var spacing = rules.Spacing;
        if (spacing == null)
            return line;

        // 在运算符周围添加空格
        if (spacing.AroundOperators)
        {
            line = AddSpacesAroundOperators(line, spacing);
        }

        // 格式化逗号后面的空格
        if (spacing.AfterComma)
        {
            line = Regex.Replace(line, @",(?!\s)", ", ");
        }

        // 格式化冒号后面的空格
        if (spacing.AfterColon)
        {
            line = Regex.Replace(line, @":(?!\s)", ": ");
        }

        // 移除多余的空格（但保留运算符两边的空格）
        line = Regex.Replace(line, @"\s+", " ");

        // 格式化分号前的空格
        if (!spacing.BeforeSemicolon)
        {
            line = Regex.Replace(line, @"\s+;", ";");
        }

        // 为完整的条件表达式末尾添加分号（如果缺少）
        var trimmedLine = line.Trim();
        // 排除已经以分号、花括号结尾的行，以及信号赋值行
        if (!trimmedLine.EndsWith(";") && 
            !trimmedLine.EndsWith("{") && 
            !trimmedLine.EndsWith("}") &&
            !Regex.IsMatch(trimmedLine, @"\}\s*=\s*(BUY|SELL|HOLD)"))
        {
            // 检查是否包含条件运算符（比较运算符或赋值运算符）
            // 匹配模式：包含 =, ==, !=, <, >, <=, >= 等运算符
            if (Regex.IsMatch(trimmedLine, @"[<>=!]|==|!=|>=|<="))
            {
                // 确保行末尾有分号
                line = trimmedLine + ";";
            }
        }

        return line;
    }

    /// <summary>
    /// 在运算符周围添加空格
    /// </summary>
    private static string AddSpacesAroundOperators(string line, SpacingRules spacing)
    {
        // 先处理双字符运算符（避免被单字符运算符误匹配）
        // >=, <=, ==, !=
        line = Regex.Replace(line, @"(\S)(>=|<=|==|!=)(\S)", "$1 $2 $3");
        
        // 处理单字符比较运算符 <, >
        line = Regex.Replace(line, @"(\S)(<|>)(\S)", "$1 $2 $3");
        
        // 处理赋值运算符 =（但不包括信号定义中的 } = BUY/SELL/HOLD）
        if (spacing.AroundAssignment)
        {
            // 排除信号赋值的情况：} = BUY/SELL/HOLD
            if (!Regex.IsMatch(line, @"\}\s*=\s*(BUY|SELL|HOLD)"))
            {
                // 匹配单独的 =，排除已经是 ==, !=, >=, <= 的情况
                // 使用负向前瞻和负向后顾，确保不是 == 的一部分
                line = Regex.Replace(line, @"(?<!==)(?<![=!<>])(\S)(=)(?!=)(\S)(?![=])", "$1 $2 $3");
            }
        }

        // 算术运算符 +, -, *, /（排除负号在数字前的情况）
        line = Regex.Replace(line, @"(\S)([\+\*/])(\S)", "$1 $2 $3");
        // 处理减号，但要排除负号在数字前的情况和括号内的负号
        // 排除模式：括号内的负号，如 value(-5) 中的 -5
        // 使用 MatchEvaluator 来智能判断是否是括号内的负数
        line = Regex.Replace(line, @"(\S)(-)(\S)", match =>
        {
            var fullLine = line;
            var matchIndex = match.Index;
            var beforeMatch = fullLine.Substring(0, matchIndex);
            var afterMatch = fullLine.Substring(matchIndex + match.Length);
            
            // 检查减号是否在括号内（前面是左括号，后面是数字）
            // 匹配模式：左括号后直接是减号，减号后是数字
            if (Regex.IsMatch(beforeMatch, @"\(\s*$") && Regex.IsMatch(afterMatch, @"^\s*\d"))
            {
                // 这是括号内的负数，不添加空格
                return match.Value;
            }
            
            // 否则是运算符，添加空格
            return $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value}";
        });
        
        // 最后，修复可能被错误格式化的括号内负数（如 value( - 5) -> value(-5)）
        line = Regex.Replace(line, @"\(\s+-\s+(\d+)", "(-$1");

        return line;
    }

    /// <summary>
    /// 长行换行处理
    /// </summary>
    private static string WrapLongLine(string line, int indentLevel, string indent, FormatRules rules)
    {
        var maxLength = rules.Wrapping?.MaxLineLength ?? 120;
        var wrapOperator = rules.Wrapping?.WrapOperator ?? "end";

        if (line.Length <= maxLength)
            return line;

        // 简单的换行策略：在运算符后换行
        if (wrapOperator == "end")
        {
            // 查找最后一个运算符位置
            var lastOperatorMatch = Regex.Match(line, @"\s([<>=!+\-*/])\s");
            if (lastOperatorMatch.Success)
            {
                var operatorPos = lastOperatorMatch.Index + lastOperatorMatch.Length;
                if (operatorPos < line.Length - 20) // 确保换行后还有足够内容
                {
                    var beforeOp = line.Substring(0, operatorPos).TrimEnd();
                    var afterOp = line.Substring(operatorPos).TrimStart();
                    var nextIndent = new string(' ', (indentLevel + 1) * indent.Length);
                    return beforeOp + "\n" + nextIndent + afterOp;
                }
            }
        }

        return line;
    }

    /// <summary>
    /// 应用自定义规则
    /// </summary>
    private static string ApplyCustomRules(string code, FormatRules rules)
    {
        if (rules.CustomRules == null || rules.CustomRules.Count == 0)
            return code;

        var lines = code.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            var processedLine = line;
            foreach (var customRule in rules.CustomRules)
            {
                if (string.IsNullOrEmpty(customRule.Pattern))
                    continue;

                try
                {
                    var regex = new Regex(customRule.Pattern);
                    if (regex.IsMatch(line))
                    {
                        processedLine = ApplyCustomRuleAction(processedLine, customRule);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"自定义规则 '{customRule.Name}' 应用失败: {ex.Message}");
                }
            }
            result.Add(processedLine);
        }

        return string.Join("\n", result);
    }

    /// <summary>
    /// 应用自定义规则动作
    /// </summary>
    private static string ApplyCustomRuleAction(string line, CustomRule rule)
    {
        if (string.IsNullOrEmpty(rule.Action))
            return line;

        switch (rule.Action)
        {
            case "ensureNewLineAfterOpenBrace":
                // 确保开括号后换行：ALL{ -> ALL {\n
                line = Regex.Replace(line, @"(ALL|ANY|WEIGHTED|MIN|COUNT|MAX|NONE|VOTE)\s*\{", "$1 {");
                break;

            case "indentToLevel":
                // 条件对齐（这个在格式化循环中处理）
                break;

            case "alignWithBlock":
                // 信号赋值对齐（这个在格式化循环中处理）
                break;
        }

        return line;
    }
}
