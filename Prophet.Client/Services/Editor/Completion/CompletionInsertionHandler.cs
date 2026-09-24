using System;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Prophet.Client.Services.Editor.Snippets;
using Prophet.Client.Services.Editor.Shared;

namespace Prophet.Client.Services.Editor.Completion;

/// <summary>
/// 补全插入处理器
/// </summary>
/// <remarks>
/// <para>
/// 负责处理补全确认后的插入逻辑，包括文本替换、语法修正和后续补全触发。
/// </para>
/// <para>
/// 核心功能：
/// <list type="bullet">
/// <item><description>智能文本替换：自动计算需要删除的文本范围</description></item>
/// <item><description>语法修正：自动添加必要的语法符号（如时间框架的括号）</description></item>
/// <item><description>光标定位：将光标移动到合适的位置</description></item>
/// <item><description>自动触发：插入后根据上下文自动触发后续补全</description></item>
/// <item><description>代码片段插入：支持代码片段的特殊插入逻辑</description></item>
/// </list>
/// </para>
/// <para>
/// 自动触发规则：
/// <list type="bullet">
/// <item><description>时间框架插入后：触发指标补全（如 $(5m). → 显示指标列表）</description></item>
/// <item><description>函数名插入后：触发时间框架补全（如 KLINE( → 显示时间框架）</description></item>
/// <item><description>指标名插入后：触发字段/参数补全（如 MACD( → 显示参数）</description></item>
/// </list>
/// </para>
/// </remarks>
public class CompletionInsertionHandler
{
    private const string RulesNotReadyTag = "__RULES_NOT_READY__";

    private readonly TextEditor _editor;
    private readonly IntelliSenseService _intelliSense;

    /// <summary>
    /// 初始化 <see cref="CompletionInsertionHandler"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    /// <param name="intelliSense">智能提示服务</param>
    public CompletionInsertionHandler(TextEditor editor, IntelliSenseService intelliSense)
    {
        _editor = editor;
        _intelliSense = intelliSense;
    }

    /// <summary>
    /// 处理补全确认
    /// </summary>
    /// <param name="item">确认的补全项</param>
    /// <param name="offset">当前光标偏移量</param>
    /// <param name="closeWindowCallback">关闭补全窗口的回调函数</param>
    /// <param name="showCompletionCallback">显示补全窗口的回调函数，参数表示是否包含代码片段</param>
    /// <remarks>
    /// <para>
    /// 执行流程：
    /// <list type="number">
    /// <item><description>检查是否为代码片段，如果是则使用代码片段插入逻辑</description></item>
    /// <item><description>计算需要删除的文本范围</description></item>
    /// <item><description>删除旧文本并插入新文本</description></item>
    /// <item><description>根据插入内容执行语法修正</description></item>
    /// <item><description>调整光标位置</description></item>
    /// <item><description>记录使用频率</description></item>
    /// <item><description>根据上下文决定是否自动触发后续补全</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void HandleCompletionConfirmed(CompletionItem item, int offset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        // 系统提示项：不插入任何文本，只关闭窗口
        if (item.Tag is string tag && tag == RulesNotReadyTag)
        {
            closeWindowCallback();
            return;
        }

        // 检查是否是代码片段
        if (SnippetProvider.IsSnippetCompletion(item))
        {
            var snippet = SnippetProvider.GetSnippetFromCompletion(item);
            if (snippet != null)
            {
                var insertionHelper = new SnippetInsertionHelper(_editor);
                insertionHelper.InsertSnippet(snippet, offset);
                return;
            }
        }

        var document = _editor.Document;

        // 智能确定需要删除的文本范围
        var (deleteStart, deleteLength) = CalculateDeleteRange(document, offset, item);

        // 删除需要替换的文本
        if (deleteLength > 0)
        {
            document.Remove(deleteStart, deleteLength);
            offset = deleteStart;
        }

        // 对于时间序列函数，特殊处理：先不插入，让 HandleSpecialCompletion 处理
        if (item.Kind == CompletionKind.TimeSeriesFunction)
        {
            bool timeSeriesHandled = HandleSpecialCompletion(item, document, offset, closeWindowCallback, showCompletionCallback);
            if (timeSeriesHandled)
                return;
        }

        // 插入补全文本
        var textToInsert = PrepareInsertText(item, offset);

        document.Insert(offset, textToInsert);

        // 自动语法校正
        AutoCorrectSyntax(document, offset + textToInsert.Length, textToInsert);

        var newOffset = offset + textToInsert.Length;

        // 根据补全类型执行特殊处理
        bool handled = HandleSpecialCompletion(item, document, newOffset, closeWindowCallback, showCompletionCallback);
        if (handled)
            return;

        // 默认行为
        _editor.CaretOffset = newOffset;
        closeWindowCallback();
        _editor.Focus();
    }

    /// <summary>
    /// 计算需要删除的文本范围
    /// </summary>
    private (int deleteStart, int deleteLength) CalculateDeleteRange(TextDocument document, int offset, CompletionItem item)
    {
        int deleteStart = offset;
        int deleteLength = 0;

        if (offset > 0)
        {
            var textBefore = document.GetText(Math.Max(0, offset - 50), Math.Min(50, offset));

            // 特殊情况1: 新语法指标补全
            if (item.Kind == CompletionKind.Indicator)
            {
                var newSyntaxWithDotMatch = Regex.Match(textBefore, @"\$\(([^)]+)\)\.(\w*)$");
                if (newSyntaxWithDotMatch.Success)
                {
                    var indicatorPrefix = newSyntaxWithDotMatch.Groups[2].Value;
                    if (!string.IsNullOrEmpty(indicatorPrefix))
                    {
                        deleteStart = offset - indicatorPrefix.Length;
                        deleteLength = indicatorPrefix.Length;
                    }
                }
                else
                {
                    var newSyntaxNoDotMatch = Regex.Match(textBefore, @"\$\(([^)]+)\)(\w*)$");
                    if (newSyntaxNoDotMatch.Success)
                    {
                        var indicatorPrefix = newSyntaxNoDotMatch.Groups[2].Value;
                        if (!string.IsNullOrEmpty(indicatorPrefix))
                        {
                            deleteStart = offset - indicatorPrefix.Length;
                            deleteLength = indicatorPrefix.Length;
                        }
                    }
                }
            }
            // 特殊情况2: 字段补全
            else if (item.Kind == CompletionKind.Field || item.Kind == CompletionKind.Parameter)
            {
                var prevChar = document.GetCharAt(offset - 1);
                
                if (prevChar == '.')
                {
                    // 点号后直接插入
                    deleteStart = offset;
                    deleteLength = 0;
                }
                else
                {
                    // 删除部分输入的字段名
                    var wordStart = FindWordStart(document, offset);
                    if (wordStart < offset)
                    {
                        deleteStart = wordStart;
                        deleteLength = offset - wordStart;
                    }
                }
            }
            // 情况3: 触发字符补全
            else
            {
                var prevChar = document.GetCharAt(offset - 1);
                if (prevChar == '@' || prevChar == '(')
                {
                    deleteStart = offset;
                    deleteLength = 0;
                }
                else if (prevChar == '.')
                {
                    deleteStart = offset;
                    deleteLength = 0;
                }
                else
                {
                    var wordStart = FindWordStart(document, offset);
                    if (wordStart < offset)
                    {
                        deleteStart = wordStart;
                        deleteLength = offset - wordStart;
                    }
                }
            }
        }

        return (deleteStart, deleteLength);
    }

    /// <summary>
    /// 准备插入文本
    /// </summary>
    private string PrepareInsertText(CompletionItem item, int offset)
    {
        var textToInsert = item.InsertText ?? string.Empty;

        // 移除触发字符（如环境变量的 @）
        if (item.Kind == CompletionKind.EnvVariable && textToInsert.StartsWith("@"))
        {
            textToInsert = textToInsert.Substring(1);
        }

        // 处理模板占位符
        if (textToInsert.Contains("${"))
        {
            var match = Regex.Match(textToInsert, @"\$\{1:([^}]*)\}");
            if (match.Success)
            {
                var placeholder = match.Groups[1].Value;
                textToInsert = textToInsert.Replace(match.Value, placeholder);
            }
        }

        return textToInsert;
    }

    /// <summary>
    /// 自动语法校正
    /// </summary>
    private void AutoCorrectSyntax(TextDocument document, int offset, string insertedText)
    {
        try
        {
            // 检测重复的右括号
            if (insertedText.EndsWith(")") && offset < document.TextLength)
            {
                var nextChar = document.GetCharAt(offset);
                if (nextChar == ')')
                {
                    Console.WriteLine($"[AutoCorrect] Removing duplicate ')' at offset {offset}");
                    document.Remove(offset, 1);
                }
            }

            // 检测重复的左括号
            if (insertedText.StartsWith("(") && offset > 1)
            {
                var prevChar = document.GetCharAt(offset - insertedText.Length - 1);
                if (prevChar == '(')
                {
                    Console.WriteLine($"[AutoCorrect] Removing duplicate '(' at offset {offset - insertedText.Length - 1}");
                    document.Remove(offset - insertedText.Length - 1, 1);
                }
            }

            // 检测重复的点号
            if (insertedText.EndsWith(".") && offset < document.TextLength)
            {
                var nextChar = document.GetCharAt(offset);
                if (nextChar == '.')
                {
                    Console.WriteLine($"[AutoCorrect] Removing duplicate '.' at offset {offset}");
                    document.Remove(offset, 1);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoCorrect] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理特殊补全类型
    /// </summary>
    private bool HandleSpecialCompletion(CompletionItem item, TextDocument document, int newOffset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        // 1. 时间序列函数补全后触发方法补全（优先级最高）
        if (item.Kind == CompletionKind.TimeSeriesFunction)
        {
            return HandleTimeSeriesFunctionCompletion(item, document, newOffset, closeWindowCallback, showCompletionCallback);
        }

        // 2. 数据函数补全后触发时间框架补全
        if (item.Kind == CompletionKind.DataFunction)
        {
            return HandleDataFunctionNameCompletion(item, document, newOffset, closeWindowCallback, showCompletionCallback);
        }

        // 3. 时间框架补全后自动添加 ).
        if (item.Kind == CompletionKind.Timeframe)
        {
            return HandleTimeframeCompletion(document, newOffset, closeWindowCallback, showCompletionCallback);
        }

        // 4. 指标补全后自动添加 ().
        if (item.Kind == CompletionKind.Indicator)
        {
            return HandleIndicatorCompletion(document, newOffset, closeWindowCallback, showCompletionCallback);
        }

        // 5. 数据函数字段/方法补全后检查是否有子字段/子方法
        if (item.Kind == CompletionKind.Field || item.Kind == CompletionKind.Method)
        {
            return HandleDataFunctionFieldOrMethodCompletion(item, document, newOffset, closeWindowCallback, showCompletionCallback);
        }

        // 5. 运算符补全后触发值补全
        if (item.Kind == CompletionKind.Operator)
        {
            return HandleOperatorCompletion(newOffset, closeWindowCallback, showCompletionCallback);
        }

        return false;
    }

    /// <summary>
    /// 处理时间框架补全
    /// </summary>
    private bool HandleTimeframeCompletion(TextDocument document, int newOffset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        // 检查是否是指标语法的时间框架补全: $(5m)
        var textAfterInsert = document.GetText(Math.Max(0, newOffset - 20), Math.Min(20, newOffset));
        var match = Regex.Match(textAfterInsert, @"\$\(([^)]+)$");

        if (match.Success)
        {
            if (newOffset < document.TextLength && document.GetCharAt(newOffset) == ')')
            {
                _editor.CaretOffset = newOffset + 1;
                document.Insert(newOffset + 1, ".");
                _editor.CaretOffset = newOffset + 2;
            }
            else
            {
                document.Insert(newOffset, ").");
                _editor.CaretOffset = newOffset + 2;
            }

            closeWindowCallback();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.Focus();
                showCompletionCallback(false);
            }, Avalonia.Threading.DispatcherPriority.Input);

            return true;
        }

        // 检查是否是数据函数的时间框架补全: FVG(5m) 或 KLINE(5m)
        var textBefore = document.GetText(Math.Max(0, newOffset - 50), Math.Min(50, newOffset));
        var dataFuncMatch = Regex.Match(textBefore, 
            @"\b(KLINE|HIGHEST|LOWEST|AVERAGE|STD|MEDIAN|VARIANCE|CHANGE|RANK|SLOPE|SUM|PRICE|PATTERN|ZSCORE|PERCENTILE|CROSS|VOLA|ATR|HT|POWER|CONSECUTIVE|CONSEC|COUNT|FVG|PREMIUM|ORDERBLOCK|SWING|BOS|CHOCH|LIQUIDITY|BREAKER)\(([^)]+)$");
        
        if (dataFuncMatch.Success)
        {
            // 自动补全右括号和点号
            if (newOffset < document.TextLength && document.GetCharAt(newOffset) == ')')
            {
                _editor.CaretOffset = newOffset + 1;
                document.Insert(newOffset + 1, ".");
                _editor.CaretOffset = newOffset + 2;
            }
            else
            {
                document.Insert(newOffset, ").");
                _editor.CaretOffset = newOffset + 2;
            }

            closeWindowCallback();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.Focus();
                showCompletionCallback(false);
            }, Avalonia.Threading.DispatcherPriority.Input);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 处理指标补全
    /// </summary>
    private bool HandleIndicatorCompletion(TextDocument document, int newOffset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        var textAfterInsert = document.GetText(Math.Max(0, newOffset - 100), Math.Min(100, newOffset));
        var match = Regex.Match(textAfterInsert, @"\$\(([^)]+)\)\.(\w+)(\([^)]*\))?$");

        if (match.Success)
        {
            var paramsPart = match.Groups[3].Value;

            if (string.IsNullOrEmpty(paramsPart))
            {
                document.Insert(newOffset, "().");
                _editor.CaretOffset = newOffset + 3;
            }
            else
            {
                document.Insert(newOffset, ".");
                _editor.CaretOffset = newOffset + 1;
            }

            closeWindowCallback();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.Focus();
                showCompletionCallback(false);
            }, Avalonia.Threading.DispatcherPriority.Input);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 处理时间序列函数补全：FEARGREED -> FEARGREED().
    /// </summary>
    private bool HandleTimeSeriesFunctionCompletion(CompletionItem item, TextDocument document, int offset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        // 检查是否是时间序列函数
        if (!_intelliSense.IsValidTimeSeriesFunction(item.Label))
            return false;

        var functionName = item.Label;

        // 计算需要删除的范围（删除用户已输入的部分）
        var (deleteStart, deleteLength) = CalculateDeleteRange(document, offset, item);
        
        // 删除需要替换的文本
        if (deleteLength > 0)
        {
            document.Remove(deleteStart, deleteLength);
            offset = deleteStart;
        }

        // 补全为 FUNDINGRATE(). 并触发方法补全
        var textToInsert = $"{functionName}().";
        document.Insert(offset, textToInsert);
        var finalOffset = offset + textToInsert.Length; // 光标在点号后

        _editor.CaretOffset = finalOffset;
        closeWindowCallback();
        
        // 触发方法补全
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _editor.Focus();
            showCompletionCallback(true);
        }, Avalonia.Threading.DispatcherPriority.Input);

        return true;
    }

    /// <summary>
    /// 处理数据函数名称补全（输入函数名后自动补全括号并触发时间框架补全）
    /// 示例：输入 FVG → FVG(|) 光标在括号内，触发时间框架补全
    /// </summary>
    private bool HandleDataFunctionNameCompletion(CompletionItem item, TextDocument document, int newOffset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        // 检查是否是数据函数
        if (!_intelliSense.IsValidDataFunction(item.Label))
            return false;

        var functionName = item.Label;
        var insertedText = item.InsertText ?? item.Label;
        
        // 如果插入文本已经包含括号和内容，说明是带默认参数的
        if (insertedText.Contains("(") && insertedText.Contains(")"))
        {
            // 检查括号内是否有内容
            var openParenIndex = insertedText.IndexOf('(');
            var closeParenIndex = insertedText.LastIndexOf(')');
            var parenContent = insertedText.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
            
            if (string.IsNullOrEmpty(parenContent))
            {
                // 括号为空，光标移动到括号内，触发时间框架补全
                var actualInsertOffset = newOffset - insertedText.Length;
                _editor.CaretOffset = actualInsertOffset + openParenIndex + 1;
                closeWindowCallback();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _editor.Focus();
                    showCompletionCallback(false);
                }, Avalonia.Threading.DispatcherPriority.Input);

                return true;
            }
            else
            {
                // 括号内有内容，光标移动到点号后，触发字段补全
            _editor.CaretOffset = newOffset;
            closeWindowCallback();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.Focus();
                showCompletionCallback(false);
            }, Avalonia.Threading.DispatcherPriority.Input);

            return true;
        }
        }
        else
        {
            // 插入文本不包含括号或括号未关闭，需要添加括号
            // 删除已插入的文本，重新插入带括号的版本
            var textToInsert = $"{functionName}(";
            
            // 计算需要删除的范围
            var deleteStart = newOffset - insertedText.Length;
            var deleteLength = insertedText.Length;
            
            document.Remove(deleteStart, deleteLength);
            document.Insert(deleteStart, textToInsert);
            
            _editor.CaretOffset = deleteStart + textToInsert.Length;
            closeWindowCallback();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.Focus();
                showCompletionCallback(false);
            }, Avalonia.Threading.DispatcherPriority.Input);

            return true;
        }
    }

    /// <summary>
    /// 处理数据函数字段/方法补全（选择字段或方法后智能补全）
    /// 示例：
    /// - 字段有子字段：FVG(5m).filled → FVG(5m).filled.| 触发子字段补全
    /// - 字段无子字段：FVG(5m).top → FVG(5m).top 完成
    /// - 方法有子字段：FVG(5m).bullish → FVG(5m).bullish(|) 光标在括号内，如果有子字段则补全为 FVG(5m).bullish().|
    /// - 方法无子字段：FVG(5m).bullish → FVG(5m).bullish(|) 光标在括号内
    /// </summary>
    private bool HandleDataFunctionFieldOrMethodCompletion(CompletionItem item, TextDocument document, int newOffset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        // 检查是否在数据函数或时间序列函数上下文中
        var textBefore = document.GetText(Math.Max(0, newOffset - 200), Math.Min(200, newOffset));
        
        // 匹配时间序列函数方法访问: FEARGREED().method
        var timeSeriesMatch = Regex.Match(textBefore, 
            @"\b([A-Z][A-Z0-9_]*)\(\)\.(\w+)(\([^)]*\))?$");
        
        if (timeSeriesMatch.Success && _intelliSense.IsValidTimeSeriesFunction(timeSeriesMatch.Groups[1].Value))
        {
            // 时间序列函数方法补全
            return HandleTimeSeriesFunctionMethodCompletion(item, document, newOffset, timeSeriesMatch.Groups[1].Value, closeWindowCallback, showCompletionCallback);
        }
        
        // 匹配数据函数字段/方法访问: Func(5m).field 或 Func(5m).method()
        // 使用通用模式匹配，然后验证是否是数据函数
        var dataFuncMatch = Regex.Match(textBefore, 
            @"\b([A-Z][A-Z0-9_]*)\([^)]+\)\.(\w+)(\([^)]*\))?$");
        
        if (!dataFuncMatch.Success || !_intelliSense.IsValidDataFunction(dataFuncMatch.Groups[1].Value))
        {
            // 不在数据函数上下文中，使用默认处理
            _editor.CaretOffset = newOffset;
            closeWindowCallback();
            return true;
        }

        var functionName = dataFuncMatch.Groups[1].Value;
        var fieldOrMethodName = dataFuncMatch.Groups[2].Value;
        var insertedText = item.InsertText ?? item.Label;
        var methodName = item.Label.Split('(')[0];
        
        // 检查该字段/方法是否有子字段或子方法
        var subFields = _intelliSense.GetDataFunctionSubFieldCompletions(functionName, fieldOrMethodName).ToList();
        var hasSubFields = subFields.Any();
        
        if (item.Kind == CompletionKind.Method)
        {
            // ========== 处理方法 ==========
            // 方法必须带括号，即使无参数也要有 ()
            
            // 检查插入文本是否包含括号
            if (!insertedText.Contains("("))
            {
                // 没有括号，需要添加括号
                // 删除已插入的文本，重新插入带括号的版本
                var deleteStart = newOffset - insertedText.Length;
                document.Remove(deleteStart, insertedText.Length);
                
                if (hasSubFields)
                {
                    // 有子字段/子方法，补全为 method().
                    document.Insert(deleteStart, $"{methodName}().");
                    newOffset = deleteStart + methodName.Length + 3; // +3 for (). and dot
                }
                else
                {
                    // 没有子字段/子方法，补全为 method(|) 光标在括号内
                    document.Insert(deleteStart, $"{methodName}()");
                    // 光标定位在左括号和右括号之间
                    newOffset = deleteStart + methodName.Length + 1; // +1 for (
                }
            }
            else if (insertedText == $"{methodName}()")
            {
                // 插入文本已经是完整的方法名和括号，光标定位在括号内
                var deleteStart = newOffset - insertedText.Length;
                document.Remove(deleteStart, insertedText.Length);
                document.Insert(deleteStart, $"{methodName}()");
                newOffset = deleteStart + methodName.Length + 1; // +1 for (，光标在括号内
                
                if (hasSubFields)
                {
                    // 有子字段，需要添加点号
                    if (newOffset < document.TextLength && document.GetCharAt(newOffset) != '.')
                    {
                        // 先关闭括号（如果还没有）
                        if (newOffset < document.TextLength && document.GetCharAt(newOffset) == ')')
                        {
                            newOffset++; // 跳过右括号
                        }
                        document.Insert(newOffset, ".");
                        newOffset++;
                    }
                }
            }
            else
            {
                // 已经有括号，但格式可能不完整
                var openParenCount = insertedText.Count(c => c == '(');
                var closeParenCount = insertedText.Count(c => c == ')');
                
                if (openParenCount > closeParenCount)
                {
                    // 括号未关闭，光标应该在括号内
                    // 如果有子字段，需要先关闭括号再添加点号
                    if (hasSubFields)
                    {
                        // 检查当前位置后面是否有右括号
                        if (newOffset < document.TextLength && document.GetCharAt(newOffset) != ')')
                        {
                            document.Insert(newOffset, ")");
                            newOffset++;
                        }
                        // 添加点号
                        if (newOffset < document.TextLength && document.GetCharAt(newOffset) != '.')
                        {
                            document.Insert(newOffset, ".");
                            newOffset++;
                        }
                    }
                    // 否则光标保持在括号内
                }
                else
                {
                    // 括号已关闭，需要将光标移到括号内
                    // 删除已插入的文本，重新插入并定位光标
                    var deleteStart = newOffset - insertedText.Length;
                    document.Remove(deleteStart, insertedText.Length);
                    document.Insert(deleteStart, $"{methodName}()");
                    newOffset = deleteStart + methodName.Length + 1; // +1 for (，光标在括号内
                    
                    if (hasSubFields)
                    {
                        // 有子字段，添加点号
                        if (newOffset < document.TextLength && document.GetCharAt(newOffset) == ')')
                        {
                            newOffset++; // 跳过右括号
                        }
                        if (newOffset < document.TextLength && document.GetCharAt(newOffset) != '.')
                        {
                            document.Insert(newOffset, ".");
                            newOffset++;
                        }
                    }
                }
            }
        }
        else
        {
            // ========== 处理字段 ==========
            // 字段不需要括号
            
            if (hasSubFields)
            {
                // 有子字段，添加点号并触发补全
                if (newOffset < document.TextLength && document.GetCharAt(newOffset) != '.')
                {
                    document.Insert(newOffset, ".");
                    newOffset++;
                }
            }
            // 否则完成补全（字段不需要括号）
        }
        
        _editor.CaretOffset = newOffset;
        closeWindowCallback();

        if (hasSubFields)
        {
            // 有子字段/子方法，触发补全
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.Focus();
                showCompletionCallback(false);
            }, Avalonia.Threading.DispatcherPriority.Input);
        }

        return true;
    }

    /// <summary>
    /// 处理时间序列函数方法补全：FEARGREED().value -> FEARGREED().value()
    /// </summary>
    private bool HandleTimeSeriesFunctionMethodCompletion(CompletionItem item, TextDocument document, int newOffset, string functionName, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        var insertedText = item.InsertText ?? item.Label;
        var methodName = item.Label.Split('(')[0];
        
        if (item.Kind == CompletionKind.Method)
        {
            // 处理方法补全
            // 检查插入文本是否包含括号
            if (!insertedText.Contains("("))
            {
                // 没有括号，需要添加括号
                var deleteStart = newOffset - insertedText.Length;
                document.Remove(deleteStart, insertedText.Length);
                
                // 补全为 method() 光标在括号内
                document.Insert(deleteStart, $"{methodName}()");
                newOffset = deleteStart + methodName.Length + 1; // +1 for (，光标在括号内
            }
            else if (insertedText == $"{methodName}()")
            {
                // 插入文本已经是完整的方法名和括号，光标定位在括号内
                var deleteStart = newOffset - insertedText.Length;
                document.Remove(deleteStart, insertedText.Length);
                document.Insert(deleteStart, $"{methodName}()");
                newOffset = deleteStart + methodName.Length + 1; // +1 for (，光标在括号内
            }
            else
            {
                // 已经有括号，但格式可能不完整
                var openParenCount = insertedText.Count(c => c == '(');
                var closeParenCount = insertedText.Count(c => c == ')');
                
                if (openParenCount > closeParenCount)
                {
                    // 括号未关闭，光标应该在括号内
                    // 光标保持在括号内
                }
                else
                {
                    // 括号已关闭，需要将光标移到括号内
                    var deleteStart = newOffset - insertedText.Length;
                    document.Remove(deleteStart, insertedText.Length);
                    document.Insert(deleteStart, $"{methodName}()");
                    newOffset = deleteStart + methodName.Length + 1; // +1 for (，光标在括号内
                }
            }
            
            _editor.CaretOffset = newOffset;
            closeWindowCallback();
            
            // 触发参数补全
            showCompletionCallback(true);
            _editor.Focus();
            
            return true;
        }
        else if (item.Kind == CompletionKind.Field)
        {
            // 字段补全（时间序列函数只有方法，没有字段）
            _editor.CaretOffset = newOffset;
            closeWindowCallback();
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 处理运算符补全
    /// </summary>
    private bool HandleOperatorCompletion(int newOffset, Action closeWindowCallback, Action<bool> showCompletionCallback)
    {
        _editor.CaretOffset = newOffset;
        closeWindowCallback();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _editor.TextArea?.Focus();
            showCompletionCallback(false);
        }, Avalonia.Threading.DispatcherPriority.Input);

        return true;
    }

    /// <summary>
    /// 查找单词起始位置
    /// </summary>
    private int FindWordStart(TextDocument document, int offset)
    {
        int start = offset;

        while (start > 0)
        {
            var ch = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                break;
            start--;
        }

        return start;
    }
}

