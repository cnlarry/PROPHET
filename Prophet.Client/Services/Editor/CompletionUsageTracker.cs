using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Prophet.Client.Services.Editor.Shared;

namespace Prophet.Client.Services.Editor;

/// <summary>
/// 补全使用频率跟踪器 - 记录和统计补全项的使用情况
/// </summary>
public class CompletionUsageTracker
{
    private Dictionary<string, UsageStats> _usageData = new();
    private readonly string _dataFilePath;
    private const int MaxHistorySize = 1000; // 最多保存1000个项的统计
    
    public CompletionUsageTracker()
    {
        // 数据保存在应用数据目录
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Prophet",
            "Editor"
        );
        Directory.CreateDirectory(appDataDir);
        _dataFilePath = Path.Combine(appDataDir, "completion_usage.json");
        
        LoadData();
    }
    
    /// <summary>
    /// 记录补全项使用
    /// </summary>
    public void RecordUsage(string itemLabel, CompletionContextType contextType)
    {
        if (string.IsNullOrEmpty(itemLabel))
            return;
            
        if (!_usageData.ContainsKey(itemLabel))
        {
            _usageData[itemLabel] = new UsageStats
            {
                Label = itemLabel,
                TotalCount = 0,
                LastUsed = DateTime.MinValue,
                ContextCounts = new Dictionary<CompletionContextType, int>()
            };
        }
        
        var stats = _usageData[itemLabel];
        stats.TotalCount++;
        stats.LastUsed = DateTime.Now;
        
        // 记录上下文相关的使用次数
        if (!stats.ContextCounts.ContainsKey(contextType))
        {
            stats.ContextCounts[contextType] = 0;
        }
        stats.ContextCounts[contextType]++;
        
        // 限制历史记录大小
        if (_usageData.Count > MaxHistorySize)
        {
            // 移除最少使用的项
            var leastUsed = _usageData
                .OrderBy(x => x.Value.TotalCount)
                .ThenBy(x => x.Value.LastUsed)
                .First();
            _usageData.Remove(leastUsed.Key);
        }
        
        // 异步保存（不阻塞UI）
        _ = SaveDataAsync();
    }
    
    /// <summary>
    /// 获取使用次数
    /// </summary>
    public int GetUsageCount(string itemLabel)
    {
        return _usageData.TryGetValue(itemLabel, out var stats) ? stats.TotalCount : 0;
    }
    
    /// <summary>
    /// 获取最后使用时间
    /// </summary>
    public DateTime? GetLastUsed(string itemLabel)
    {
        return _usageData.TryGetValue(itemLabel, out var stats) ? stats.LastUsed : null;
    }
    
    /// <summary>
    /// 获取上下文相关的使用次数
    /// </summary>
    public int GetContextUsageCount(string itemLabel, CompletionContextType contextType)
    {
        if (!_usageData.TryGetValue(itemLabel, out var stats))
            return 0;
            
        return stats.ContextCounts.TryGetValue(contextType, out var count) ? count : 0;
    }
    
    /// <summary>
    /// 计算使用频率分数 (0-100, 越高越好)
    /// </summary>
    public int GetUsageScore(string itemLabel)
    {
        var count = GetUsageCount(itemLabel);
        if (count == 0)
            return 0;
            
        // 对数缩放，避免使用次数极高的项完全压制其他项
        // score = min(100, log10(count + 1) * 20)
        var score = Math.Min(100, Math.Log10(count + 1) * 20);
        return (int)score;
    }
    
    /// <summary>
    /// 计算最近使用分数 (0-100, 越高越好)
    /// </summary>
    public int GetRecentScore(string itemLabel)
    {
        var lastUsed = GetLastUsed(itemLabel);
        if (!lastUsed.HasValue || lastUsed.Value == DateTime.MinValue)
            return 0;
            
        var timeSinceLastUse = DateTime.Now - lastUsed.Value;
        
        // 最近1分钟内使用: 100分
        // 1小时内: 80分
        // 1天内: 50分
        // 1周内: 20分
        // 更久: 0分
        if (timeSinceLastUse.TotalMinutes < 1)
            return 100;
        if (timeSinceLastUse.TotalHours < 1)
            return 80;
        if (timeSinceLastUse.TotalDays < 1)
            return 50;
        if (timeSinceLastUse.TotalDays < 7)
            return 20;
        return 0;
    }
    
    /// <summary>
    /// 计算上下文相关性分数 (0-100, 越高越好)
    /// </summary>
    public int GetContextScore(string itemLabel, CompletionContextType contextType)
    {
        var totalCount = GetUsageCount(itemLabel);
        if (totalCount == 0)
            return 50; // 未使用过的项给中等分数
            
        var contextCount = GetContextUsageCount(itemLabel, contextType);
        
        // 上下文使用占比越高，分数越高
        var ratio = (double)contextCount / totalCount;
        return (int)(ratio * 100);
    }
    
    /// <summary>
    /// 加载使用数据
    /// </summary>
    private void LoadData()
    {
        try
        {
            if (!File.Exists(_dataFilePath))
            {
                Console.WriteLine($"[UsageTracker] Data file not found, starting fresh");
                return;
            }
                
            var json = File.ReadAllText(_dataFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, UsageStats>>(json);
            
            if (data != null)
            {
                _usageData = data;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UsageTracker] Failed to load data: {ex.Message}");
            _usageData = new Dictionary<string, UsageStats>();
        }
    }
    
    /// <summary>
    /// 异步保存使用数据
    /// </summary>
    private async System.Threading.Tasks.Task SaveDataAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_usageData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(_dataFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UsageTracker] Failed to save data: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 清除所有统计数据
    /// </summary>
    public void ClearAll()
    {
        _usageData.Clear();
        _ = SaveDataAsync();
    }
    
    /// <summary>
    /// 获取最常用的补全项 (Top N)
    /// </summary>
    public List<string> GetTopUsed(int count = 10)
    {
        return _usageData
            .OrderByDescending(x => x.Value.TotalCount)
            .Take(count)
            .Select(x => x.Key)
            .ToList();
    }
}

/// <summary>
/// 使用统计数据
/// </summary>
public class UsageStats
{
    public string Label { get; set; } = "";
    public int TotalCount { get; set; }
    public DateTime LastUsed { get; set; }
    public Dictionary<CompletionContextType, int> ContextCounts { get; set; } = new();
}

