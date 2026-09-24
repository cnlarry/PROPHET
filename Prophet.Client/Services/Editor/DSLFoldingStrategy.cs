using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace Prophet.Client.Services.Editor;

/// <summary>
/// DSL 代码折叠策略 - 折叠信号函数块
/// </summary>
public class DSLFoldingStrategy
{
    /// <summary>
    /// 更新折叠信息
    /// </summary>
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var newFoldings = CreateNewFoldings(document);
        manager.UpdateFoldings(newFoldings, -1);
    }

    /// <summary>
    /// 创建折叠区域
    /// </summary>
    private IEnumerable<NewFolding> CreateNewFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var text = document.Text;

        // 折叠信号函数块：ALL{...} = BUY、ANY{...} = SELL、WEIGHTED(...){...} = BUY、MIN(2){...} = BUY 等
        var signalFunctionPattern = @"(ALL|ANY|WEIGHTED|MIN|MAX|COUNT|VOTE|NONE)(\([^)]+\))?(\s*)\{";
        var matches = Regex.Matches(text, signalFunctionPattern);

        foreach (Match match in matches)
        {
            var startOffset = match.Index;
            var openBraceOffset = match.Index + match.Length - 1;
            
            // 查找匹配的闭合花括号
            var closeBraceOffset = FindMatchingBrace(text, openBraceOffset);
            if (closeBraceOffset > openBraceOffset)
            {
                // 查找后面的信号类型，并获取折叠结束位置
                var (signal, signalEndOffset) = ExtractSignalTypeWithEndOffset(text, closeBraceOffset + 1);
                
                // 创建折叠名称：ALL{...} = BUY
                var functionName = match.Groups[1].Value;
                var parameters = match.Groups[2].Value;
                var foldingName = $"{functionName}{parameters}{{...}}{signal}";
                
                // 折叠范围包含到信号类型之后（包括 = BUY）
                var foldEndOffset = signalEndOffset > closeBraceOffset ? signalEndOffset : closeBraceOffset + 1;
                
                foldings.Add(new NewFolding(startOffset, foldEndOffset)
                {
                    Name = foldingName
                });
            }
        }

        // 折叠自定义函数：functionName(params): ReturnType {...}
        var customFunctionPattern = @"^([a-z][a-zA-Z0-9_]*)\s*\(([^)]*)\)\s*(?::\s*(\w+))?\s*\{";
        var lines = text.Split('\n');
        int currentOffset = 0;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedLine = line.Trim();
            
            // 检查是否是自定义函数定义行
            var funcMatch = Regex.Match(trimmedLine, customFunctionPattern);
            if (funcMatch.Success)
            {
                // 计算开始位置（在原文中的偏移量）
                var lineStartOffset = currentOffset;
                var trimStart = line.Length - line.TrimStart().Length;
                var funcStartOffset = lineStartOffset + trimStart;
                
                // 找到开花括号的位置
                var openBraceOffset = funcStartOffset + funcMatch.Value.Length - 1;
                
                // 查找匹配的闭合花括号
                var closeBraceOffset = FindMatchingBrace(text, openBraceOffset);
                if (closeBraceOffset > openBraceOffset)
                {
                    // 创建折叠名称：functionName(params): ReturnType {...}
                    var functionName = funcMatch.Groups[1].Value;
                    var parameters = funcMatch.Groups[2].Value;
                    var returnType = funcMatch.Groups[3].Success ? funcMatch.Groups[3].Value : "";
                    
                    // 简化参数显示（只显示参数名）
                    var simplifiedParams = "";
                    if (!string.IsNullOrEmpty(parameters))
                    {
                        var paramNames = new List<string>();
                        var paramParts = parameters.Split(',');
                        foreach (var part in paramParts)
                        {
                            var paramMatch = Regex.Match(part.Trim(), @"^([a-z][a-zA-Z0-9_]*)\s*:\s*(\w+)$");
                            if (paramMatch.Success)
                            {
                                paramNames.Add(paramMatch.Groups[1].Value);
                            }
                        }
                        simplifiedParams = string.Join(", ", paramNames);
                    }
                    
                    var foldingName = !string.IsNullOrEmpty(returnType) 
                        ? $"{functionName}({simplifiedParams}): {returnType} {{...}}"
                        : $"{functionName}({simplifiedParams}) {{...}}";
                    
                    foldings.Add(new NewFolding(funcStartOffset, closeBraceOffset + 1)
                    {
                        Name = foldingName
                    });
                }
            }
            
            // 更新当前偏移量（包括换行符）
            currentOffset += line.Length + 1; // +1 for \n
        }
        
        // 折叠多行注释
        var commentPattern = @"/\*(.|\n)*?\*/";
        var commentMatches = Regex.Matches(text, commentPattern);
        foreach (Match match in commentMatches)
        {
            // 只折叠多行注释
            if (match.Value.Contains('\n'))
            {
                foldings.Add(new NewFolding(match.Index, match.Index + match.Length)
                {
                    Name = "/* ... */"
                });
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    /// <summary>
    /// 查找匹配的闭合花括号
    /// </summary>
    private int FindMatchingBrace(string text, int openBraceOffset)
    {
        int depth = 1;
        for (int i = openBraceOffset + 1; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// 提取信号类型（BUY/SELL/HOLD）及其结束位置
    /// </summary>
    private (string signal, int endOffset) ExtractSignalTypeWithEndOffset(string text, int startOffset)
    {
        var remainingText = text.Substring(startOffset);
        var match = Regex.Match(remainingText, @"=\s*(BUY|SELL|HOLD)(;?)");
        if (match.Success)
        {
            var signal = $" = {match.Groups[1].Value}";
            var endOffset = startOffset + match.Index + match.Length;
            return (signal, endOffset);
        }
        return ("", startOffset);
    }
    
    /// <summary>
    /// 提取信号类型（BUY/SELL/HOLD） - 保留旧方法以兼容
    /// </summary>
    private string ExtractSignalType(string text, int startOffset)
    {
        var (signal, _) = ExtractSignalTypeWithEndOffset(text, startOffset);
        return signal;
    }
}

