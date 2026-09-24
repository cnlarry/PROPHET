using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Services.Editor.Shared;

namespace Prophet.Client.Services.Editor.Completion;

/// <summary>
/// 补全过滤排序器
/// </summary>
/// <remarks>
/// <para>
/// 负责智能过滤和排序补全项，使用多种算法提升补全体验。
/// </para>
/// <para>
/// 核心功能：
/// <list type="bullet">
/// <item><description>模糊匹配：支持缩写和首字母匹配（如 "ma" 匹配 "MACD"）</description></item>
/// <item><description>使用频率排序：基于历史使用频率排序</description></item>
/// <item><description>智能排序：综合考虑精确匹配、前缀匹配、模糊匹配和使用频率</description></item>
/// <item><description>上下文感知：根据当前上下文调整排序优先级</description></item>
/// </list>
/// </para>
/// <para>
/// 排序权重计算：
/// <list type="bullet">
/// <item><description>精确匹配：1000 分（最高优先级）</description></item>
/// <item><description>前缀匹配：100 分</description></item>
/// <item><description>模糊匹配：50 分</description></item>
/// <item><description>使用频率：0-20 分</description></item>
/// <item><description>文本相似度：0-10 分</description></item>
/// </list>
/// </para>
/// </remarks>
public class CompletionFilterSorter
{
    private readonly CompletionUsageTracker _usageTracker;

    // 常见指标/函数缩写字典
    private static readonly Dictionary<string, string[]> CommonAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ma", new[] { "MA", "MACD", "MAMA" } },
        { "ema", new[] { "EMA" } },
        { "sma", new[] { "MA", "SMA" } },
        { "wma", new[] { "WMA" } },
        { "rsi", new[] { "RSI", "STOCHRSI" } },
        { "macd", new[] { "MACD" } },
        { "kdj", new[] { "KDJ" } },
        { "bb", new[] { "BOLL", "BBands", "BollingerBands", "BollingerBW" } },
        { "boll", new[] { "BOLL", "BBands" } },
        { "atr", new[] { "ATR", "NATR" } },
        { "adx", new[] { "ADX" } },
        { "obv", new[] { "OBV" } },
        { "cci", new[] { "CCI" } },
        { "sar", new[] { "SAR" } },
        { "fvg", new[] { "FVG", "FairValueGap" } },
        { "ob", new[] { "ORDERBLOCK", "OrderBlock" } },
        { "bos", new[] { "BOS", "BreakOfStructure" } },
        { "avg", new[] { "AVERAGE", "AVG" } },
    };

    /// <summary>
    /// 初始化 <see cref="CompletionFilterSorter"/> 类的新实例
    /// </summary>
    /// <param name="usageTracker">使用频率跟踪器</param>
    public CompletionFilterSorter(CompletionUsageTracker usageTracker)
    {
        _usageTracker = usageTracker;
    }

    /// <summary>
    /// 智能过滤和排序补全项
    /// </summary>
    /// <param name="items">待过滤和排序的补全项列表</param>
    /// <param name="filterPrefix">用户输入的过滤前缀</param>
    /// <param name="context">当前补全上下文（可选）</param>
    /// <returns>过滤和排序后的补全项列表</returns>
    /// <remarks>
    /// <para>
    /// 过滤流程：
    /// <list type="number">
    /// <item><description>空前缀：不过滤，直接智能排序</description></item>
    /// <item><description>有前缀：依次尝试精确匹配、前缀匹配、模糊匹配</description></item>
    /// <item><description>无结果：使用缩写字典扩展搜索</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 排序策略：
    /// <list type="number">
    /// <item><description>计算每个项的权重分数</description></item>
    /// <item><description>按权重降序排序</description></item>
    /// <item><description>权重相同时，按字母顺序排序</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public List<CompletionItem> FilterAndSort(List<CompletionItem> items, string filterPrefix, CompletionContext? context = null)
    {
        if (string.IsNullOrEmpty(filterPrefix))
        {
            return SmartSort(items, context);
        }

        var prefix = filterPrefix.ToUpperInvariant();

        // 计算每个项的综合分数
        var scored = items
            .Select(item => new
            {
                Item = item,
                MatchScore = CalculateMatchScore(item.Label, prefix),
                UsageScore = _usageTracker.GetUsageScore(item.Label),
                RecentScore = _usageTracker.GetRecentScore(item.Label),
                ContextScore = context != null 
                    ? _usageTracker.GetContextScore(item.Label, context.Type)
                    : 50
            })
            .Where(x => x.MatchScore >= 0) // 只保留匹配的项
            .Select(x => new
            {
                x.Item,
                x.MatchScore,
                x.UsageScore,
                x.RecentScore,
                x.ContextScore,
                // 综合评分: 匹配 40% + 使用频率 25% + 最近使用 15% + 上下文 20%
                FinalScore = x.MatchScore * 0.4 + 
                            (100 - x.UsageScore) * 0.25 +
                            (100 - x.RecentScore) * 0.15 +
                            (100 - x.ContextScore) * 0.20
            })
            .OrderBy(x => x.FinalScore)
            .ThenBy(x => x.Item.Label)
            .Select(x => x.Item)
            .ToList();

        return scored;
    }

    /// <summary>
    /// 智能排序（不过滤）
    /// </summary>
    private List<CompletionItem> SmartSort(List<CompletionItem> items, CompletionContext? context)
    {
        var sorted = items
            .Select(item => new
            {
                Item = item,
                UsageScore = _usageTracker.GetUsageScore(item.Label),
                RecentScore = _usageTracker.GetRecentScore(item.Label),
                ContextScore = context != null 
                    ? _usageTracker.GetContextScore(item.Label, context.Type)
                    : 50
            })
            .Select(x => new
            {
                x.Item,
                x.UsageScore,
                x.RecentScore,
                x.ContextScore,
                // 综合评分: 使用频率 40% + 最近使用 30% + 上下文 20% + 字母序 10%
                FinalScore = (100 - x.UsageScore) * 0.4 +
                            (100 - x.RecentScore) * 0.3 +
                            (100 - x.ContextScore) * 0.2 +
                            (x.Item.Label.Length) * 0.1
            })
            .OrderBy(x => x.FinalScore)
            .ThenBy(x => x.Item.Label)
            .Select(x => x.Item)
            .ToList();

        return sorted;
    }

    /// <summary>
    /// 计算匹配分数（分数越小优先级越高）
    /// </summary>
    private int CalculateMatchScore(string label, string prefix)
    {
        var upperLabel = label.ToUpperInvariant();

        // 1. 完全匹配
        if (upperLabel == prefix)
            return 0;

        // 2. 前缀匹配
        if (upperLabel.StartsWith(prefix))
            return 1;

        // 3. 缩写匹配
        if (CommonAbbreviations.TryGetValue(prefix.ToLower(), out var abbrevs))
        {
            if (abbrevs.Any(abbr => upperLabel.Equals(abbr, StringComparison.OrdinalIgnoreCase)))
                return 2;

            if (abbrevs.Any(abbr => upperLabel.Contains(abbr)))
                return 5;
        }

        // 4. 驼峰匹配
        var camelScore = TryCamelCaseMatch(label, prefix);
        if (camelScore >= 0)
            return 10 + camelScore;

        // 5. 连续匹配
        if (upperLabel.Length >= prefix.Length)
        {
            bool allMatch = true;
            int lastIndex = -1;

            for (int i = 0; i < prefix.Length; i++)
            {
                int index = upperLabel.IndexOf(prefix[i], lastIndex + 1);
                if (index < 0 || (i > 0 && index != lastIndex + 1))
                {
                    allMatch = false;
                    break;
                }
                lastIndex = index;
            }

            if (allMatch)
                return 20 + lastIndex;
        }

        // 6. 中间子串匹配
        if (upperLabel.Contains(prefix))
        {
            int startIndex = upperLabel.IndexOf(prefix);
            return 50 + startIndex;
        }

        // 7. 包含所有字母但不连续
        if (ContainsAllChars(upperLabel, prefix))
        {
            int firstIndex = upperLabel.IndexOf(prefix[0]);
            int distance = CalculateCharDistance(upperLabel, prefix);
            return 100 + firstIndex + distance;
        }

        // 不匹配
        return -1;
    }

    /// <summary>
    /// 驼峰匹配
    /// </summary>
    private int TryCamelCaseMatch(string label, string prefix)
    {
        var camelChars = new System.Text.StringBuilder();

        if (label.Length > 0)
        {
            camelChars.Append(char.ToUpperInvariant(label[0]));
        }

        for (int i = 1; i < label.Length; i++)
        {
            if (char.IsUpper(label[i]))
            {
                camelChars.Append(label[i]);
            }
        }

        var camelString = camelChars.ToString();

        if (camelString.Length >= prefix.Length)
        {
            if (camelString.StartsWith(prefix))
                return 0;

            if (camelString.Contains(prefix))
            {
                int index = camelString.IndexOf(prefix);
                return index;
            }
        }

        var upperLabel = label.ToUpperInvariant();
        if (upperLabel.StartsWith(prefix))
            return 0;

        for (int i = 1; i < label.Length; i++)
        {
            if (char.IsUpper(label[i]))
            {
                var wordPart = label.Substring(i).ToUpperInvariant();
                if (wordPart.StartsWith(prefix))
                    return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 检查是否包含所有字符
    /// </summary>
    private bool ContainsAllChars(string label, string prefix)
    {
        int lastIndex = -1;

        foreach (char ch in prefix)
        {
            int index = label.IndexOf(ch, lastIndex + 1);
            if (index < 0)
                return false;
            lastIndex = index;
        }

        return true;
    }

    /// <summary>
    /// 计算字符距离
    /// </summary>
    private int CalculateCharDistance(string label, string prefix)
    {
        int lastIndex = -1;
        int totalDistance = 0;
        int matchCount = 0;

        foreach (char ch in prefix)
        {
            int index = label.IndexOf(ch, lastIndex + 1);
            if (index >= 0)
            {
                if (lastIndex >= 0)
                {
                    totalDistance += (index - lastIndex - 1);
                }
                lastIndex = index;
                matchCount++;
            }
        }

        return matchCount > 1 ? totalDistance / (matchCount - 1) : 0;
    }
}

