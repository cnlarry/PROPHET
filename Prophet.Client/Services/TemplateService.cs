using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using Prophet.Client.Models;

namespace Prophet.Client.Services;

/// <summary>
/// 模板元数据（对应JSON配置）
/// </summary>
public class TemplateMetadata
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "📊";
    
    [JsonPropertyName("rating")]
    public int Rating { get; set; } = 3;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("suggestedTimeframe")]
    public string? SuggestedTimeframe { get; set; }
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
    
    [JsonPropertyName("dslFile")]
    public string DslFile { get; set; } = string.Empty;
    
    [JsonPropertyName("author")]
    public string Author { get; set; } = "Prophet System";
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// 模板配置文件根对象
/// </summary>
public class TemplateConfig
{
    [JsonPropertyName("templates")]
    public List<TemplateMetadata> Templates { get; set; } = new();
}

/// <summary>
/// 策略模板管理服务
/// </summary>
public class TemplateService
{
    private readonly List<StrategyTemplate> _templates = new();
    private const string TemplatesDirectory = "Resources/Templates";

    public TemplateService()
    {
        LoadTemplatesFromFiles();
    }

    /// <summary>
    /// 从文件加载模板
    /// </summary>
    private void LoadTemplatesFromFiles()
    {
        try
        {
            _templates.Clear();
            
            // 获取可执行文件所在目录
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var templatesPath = Path.Combine(basePath, TemplatesDirectory);
            var configPath = Path.Combine(templatesPath, "templates.json");
            
            // 检查目录和配置文件是否存在
            if (!Directory.Exists(templatesPath))
            {
                Console.WriteLine($"⚠️ 模板目录不存在: {templatesPath}");
                LoadFallbackTemplates();
                return;
            }
            
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"⚠️ 模板配置文件不存在: {configPath}");
                LoadFallbackTemplates();
                return;
            }
            
            // 读取JSON配置
            var jsonContent = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            
            var config = JsonSerializer.Deserialize<TemplateConfig>(jsonContent, options);
            
            if (config == null || config.Templates.Count == 0)
            {
                Console.WriteLine("⚠️ 模板配置为空");
                LoadFallbackTemplates();
                return;
            }
            
            // 加载每个模板的DSL代码
            foreach (var meta in config.Templates)
            {
                try
                {
                    var dslPath = Path.Combine(templatesPath, meta.DslFile);
                    
                    if (!File.Exists(dslPath))
                    {
                        Console.WriteLine($"⚠️ DSL文件不存在: {meta.DslFile}");
                        continue;
                    }
                    
                    var dslCode = File.ReadAllText(dslPath);
                    
                    var template = new StrategyTemplate
                    {
                        Id = meta.Id,
                        Name = meta.Name,
                        Category = meta.Category,
                        Icon = meta.Icon,
                        Rating = meta.Rating,
                        Description = meta.Description,
                        SuggestedTimeframe = meta.SuggestedTimeframe,
                        Tags = meta.Tags,
                        DslCode = dslCode,
                        Author = meta.Author,
                        Notes = meta.Notes,
                        IsBuiltIn = true
                    };
                    
                    _templates.Add(template);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ 加载模板失败 {meta.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 加载模板失败: {ex.Message}");
            LoadFallbackTemplates();
        }
    }
    
    /// <summary>
    /// 加载后备模板（当文件加载失败时）
    /// </summary>
    private void LoadFallbackTemplates()
    {
        Console.WriteLine("🔄 加载后备模板...");
        
        // 简单的后备模板（当文件加载失败时使用）
        _templates.Add(new StrategyTemplate
        {
            Id = "fallback_simple",
            Name = "简单趋势策略",
            Category = "trend",
            Icon = "📈",
            Rating = 3,
            Description = "基础趋势跟踪策略（后备模板）",
            SuggestedTimeframe = "5m",
            Tags = new List<string> { "MACD", "RSI" },
            DslCode = @"// 简单趋势策略
ALL {
    $(5m).MACD().trend = BULLISH;
    $(5m).RSI().value < 70;
} = BUY;

ANY {
    $(5m).MACD().trend = BEARISH;
    $(5m).RSI().value > 70;
} = SELL;",
            Notes = "这是后备模板，请检查模板文件配置"
        });
    }

    /// <summary>
    /// 获取所有模板
    /// </summary>
    public List<StrategyTemplate> GetAllTemplates()
    {
        return _templates.ToList();
    }

    /// <summary>
    /// 按分类获取模板
    /// </summary>
    public List<StrategyTemplate> GetTemplatesByCategory(string category)
    {
        return _templates.Where(t => t.Category == category).ToList();
    }

    /// <summary>
    /// 根据ID获取模板
    /// </summary>
    public StrategyTemplate? GetTemplateById(string id)
    {
        return _templates.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>
    /// 获取常用模板（评分>=4的前5个）
    /// </summary>
    public List<StrategyTemplate> GetPopularTemplates(int count = 5)
    {
        return _templates
            .Where(t => t.Rating >= 4)
            .OrderByDescending(t => t.Rating)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// 搜索模板
    /// </summary>
    public List<StrategyTemplate> SearchTemplates(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return GetAllTemplates();

        keyword = keyword.ToLower();
        return _templates.Where(t =>
            t.Name.ToLower().Contains(keyword) ||
            t.Description.ToLower().Contains(keyword) ||
            t.Tags.Any(tag => tag.ToLower().Contains(keyword))
        ).ToList();
    }

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public List<string> GetAllCategories()
    {
        return _templates
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    /// <summary>
    /// 获取分类显示名称
    /// </summary>
    public string GetCategoryDisplayName(string category)
    {
        return category switch
        {
            "trend" => "📈 趋势跟踪",
            "momentum" => "⚡ 动量突破",
            "mean_revert" => "↕️ 均值回归",
            "smc" => "🎯 SMC策略",
            "volatility" => "💥 波动率",
            "volume" => "📊 成交量",
            "pattern" => "🕯️ 形态识别",
            "grid" => "🔢 网格交易",
            _ => category
        };
    }
}

