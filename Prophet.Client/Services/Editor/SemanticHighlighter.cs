using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using Prophet.Client.Core;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services.Editor;

/// <summary>
/// 语义高亮分析器 - 基于代码语义分析添加智能高亮
/// </summary>
public class SemanticHighlighter
{
    private readonly DSLValidator _validator;
    private readonly DSLRulesService _rulesService;
    
    // 已知的指标列表（使用 ID）
    private readonly HashSet<string> _knownIndicators;
    
    // 已知的函数列表（使用 ID）
    private readonly HashSet<string> _knownFunctions;
    
    // 已知的环境变量列表
    private readonly HashSet<string> _knownEnvVariables;
    
    // 已知的枚举值列表
    private readonly HashSet<string> _knownEnumValues;

    public SemanticHighlighter()
    {
        _validator = ServiceContainer.GetService<DSLValidator>();
        _rulesService = ServiceContainer.GetService<DSLRulesService>();
        
        // 初始化已知符号列表（使用 ID 而不是 Name）
        _knownIndicators = new HashSet<string>(
            _rulesService.GetAllIndicators().Select(i => i.Id),
            StringComparer.OrdinalIgnoreCase);
        
        _knownFunctions = new HashSet<string>(
            _rulesService.GetAllDataFunctions().Select(f => f.Id)
                .Concat(_rulesService.GetAllMathFunctions().Select(f => f.Id))
                .Concat(_rulesService.GetAllSignalFunctions().Select(f => f.Id))
                .Concat(_rulesService.GetAllTimeSeriesFunctions().Select(f => f.Id)),
            StringComparer.OrdinalIgnoreCase);
        
        _knownEnvVariables = new HashSet<string>(
            Enumerable.Empty<string>(), // 环境变量概念已废弃
            StringComparer.OrdinalIgnoreCase);
        
        _knownEnumValues = new HashSet<string>(
            _rulesService.GetAllEnums().SelectMany(e => e.Values ?? Enumerable.Empty<string>()),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 分析文档并生成语义高亮信息
    /// </summary>
    public List<SemanticHighlight> AnalyzeDocument(TextDocument document, ValidationResult? existingValidationResult = null)
    {
        var highlights = new List<SemanticHighlight>();
        
        if (document == null || string.IsNullOrEmpty(document.Text))
            return highlights;
        
        var code = document.Text;
        
        // 1. 从验证器获取错误信息（语法错误）
        var validationResult = existingValidationResult ?? _validator.Validate(code);
        foreach (var error in validationResult.Errors)
        {
            // 验证行号有效性（行号必须 >= 1）
            if (error.Line < 1 || error.Line > document.LineCount)
                continue;
            
            try
            {
            var line = document.GetLineByNumber(error.Line);
            var startOffset = line.Offset + Math.Max(0, error.Column - 1);
            var length = error.Length > 0 ? error.Length : GetWordLength(document, startOffset);
            var endOffset = Math.Min(line.EndOffset, startOffset + length);
            
                if (startOffset < endOffset && startOffset < document.TextLength)
            {
                highlights.Add(new SemanticHighlight
                {
                    StartOffset = startOffset,
                    EndOffset = endOffset,
                    Type = SemanticHighlightType.SyntaxError,
                    Message = error.Message
                });
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // 忽略无效的行号
                continue;
            }
        }
        
        // 2. 识别未定义的标识符
        FindUndefinedSymbols(document, highlights);
        
        return highlights;
    }

    /// <summary>
    /// 查找未定义的标识符
    /// </summary>
    private void FindUndefinedSymbols(TextDocument document, List<SemanticHighlight> highlights)
    {
        var code = document.Text;
        
        // 查找未定义的指标引用: $.XXX(timeframe) 或 $.XXX
        // 修改正则以只匹配指标名称（不包括时间框架）
        var indicatorPattern = @"\$\.([A-Z_][A-Z0-9_]*)(?:\(|\.|\s|;|$)";
        foreach (Match match in Regex.Matches(code, indicatorPattern))
        {
            var indicatorName = match.Groups[1].Value;
            
            if (!_knownIndicators.Contains(indicatorName))
            {
                // 未知指标
                var startOffset = match.Index + 2; // 跳过 $.
                var endOffset = startOffset + indicatorName.Length;
                
                // 检查是否已经有语法错误高亮（避免重复）
                if (!highlights.Any(h => h.StartOffset <= startOffset && h.EndOffset >= endOffset))
                {
                    highlights.Add(new SemanticHighlight
                    {
                        StartOffset = startOffset,
                        EndOffset = endOffset,
                        Type = SemanticHighlightType.UndefinedSymbol,
                        Message = $"未定义的指标: '{indicatorName}'"
                    });
                }
            }
        }
        
        // 查找未定义的函数调用
        var functionPattern = @"\b([A-Z][A-Z0-9_]*)\s*\(";
        foreach (Match match in Regex.Matches(code, functionPattern))
        {
            var functionName = match.Groups[1].Value;
            
            // 跳过已知函数（数据函数、数学函数、信号函数、时间序列函数）
            if (_knownFunctions.Contains(functionName))
            {
                continue;
            }
            
            // 跳过指标（指标也可能有括号，如 $(5m).MACD()，但不应该被标记为未定义函数）
            if (_knownIndicators.Contains(functionName))
            {
                continue;
            }
            
            // 额外检查时间序列函数（双重保险）
            if (_rulesService.IsValidTimeSeriesFunction(functionName))
            {
                continue;
            }
            
            var startOffset = match.Groups[1].Index;
            var endOffset = startOffset + functionName.Length;
            
            // 检查是否已经有高亮
            if (!highlights.Any(h => h.StartOffset == startOffset && h.EndOffset == endOffset))
            {
                highlights.Add(new SemanticHighlight
                {
                    StartOffset = startOffset,
                    EndOffset = endOffset,
                    Type = SemanticHighlightType.UndefinedSymbol,
                    Message = $"未定义的函数: '{functionName}'"
                });
            }
        }
        
        // 查找未定义的环境变量: @XXX
        var envVarPattern = @"@([A-Z_][A-Z0-9_]*)";
        foreach (Match match in Regex.Matches(code, envVarPattern))
        {
            var envVarName = match.Groups[1].Value;
            if (!_knownEnvVariables.Contains(envVarName))
            {
                var startOffset = match.Index + 1; // 跳过 @
                var endOffset = match.Index + match.Length;
                
                if (!highlights.Any(h => h.StartOffset == startOffset && h.EndOffset == endOffset))
                {
                    highlights.Add(new SemanticHighlight
                    {
                        StartOffset = startOffset,
                        EndOffset = endOffset,
                        Type = SemanticHighlightType.UndefinedSymbol,
                        Message = $"未定义的环境变量: '@{envVarName}'"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 获取从指定位置开始的单词长度
    /// </summary>
    private int GetWordLength(TextDocument document, int offset)
    {
        if (offset >= document.TextLength)
            return 1;
        
        int length = 0;
        while (offset + length < document.TextLength)
        {
            var ch = document.GetCharAt(offset + length);
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.' && ch != '$' && ch != '@')
                break;
            length++;
        }
        
        return Math.Max(1, length);
    }
}

/// <summary>
/// 语义高亮信息
/// </summary>
public class SemanticHighlight
{
    /// <summary>
    /// 起始偏移量
    /// </summary>
    public int StartOffset { get; set; }
    
    /// <summary>
    /// 结束偏移量
    /// </summary>
    public int EndOffset { get; set; }
    
    /// <summary>
    /// 高亮类型
    /// </summary>
    public SemanticHighlightType Type { get; set; }
    
    /// <summary>
    /// 错误/警告消息
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// 语义高亮类型
/// </summary>
public enum SemanticHighlightType
{
    /// <summary>
    /// 语法错误（红色波浪线）
    /// </summary>
    SyntaxError,
    
    /// <summary>
    /// 未定义的符号（灰色虚线）
    /// </summary>
    UndefinedSymbol,
    
    /// <summary>
    /// 警告（黄色波浪线）
    /// </summary>
    Warning,
    
    /// <summary>
    /// 提示（蓝色波浪线）
    /// </summary>
    Info
}

