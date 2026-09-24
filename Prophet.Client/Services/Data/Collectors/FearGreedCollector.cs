using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Database;
using Prophet.Client.Models;

namespace Prophet.Client.Services.Data.Collectors;

/// <summary>
/// 恐惧与贪婪指数采集器
/// 从 Alternative.me API 获取加密货币恐惧与贪婪指数
/// 启动时检查缺失数据并补齐，之后每天UTC 00:05采集
/// </summary>
public class FearGreedCollector : BaseDataCollector<List<FearGreedData>>
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _dailyTask;
    
    private const string API_URL = "https://api.alternative.me/fng/?limit=0&format=json";
    
    public FearGreedCollector(HttpClient httpClient)
        : base("FearGreedIndex", 86400) // 每天采集一次（86400秒）
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }
    
    /// <summary>
    /// 启动定时采集（重写以添加数据连续性检查和UTC定时）
    /// </summary>
    public new async Task StartAsync()
    {
        if (IsRunning)
        {
            Console.WriteLine($"⚠️ [{Name}] 采集器已在运行");
            return;
        }
        
        if (!IsEnabled)
        {
            Console.WriteLine($"⚠️ [{Name}] 采集器已禁用，无法启动");
            return;
        }
        
        // 🔑 启动前检查缺失数据并补齐
        await CheckAndFillMissingDataAsync();
        
        // 启动定时任务（UTC每天00:05采集）
        await StartDailyCollectionAsync();
    }
    
    /// <summary>
    /// 停止定时采集
    /// </summary>
    public new async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }
        
        Console.WriteLine($"🛑 [{Name}] 停止定时采集");
        
        _cancellationTokenSource?.Cancel();
        
        if (_dailyTask != null)
        {
            try
            {
                await _dailyTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
        }
        
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _dailyTask = null;
        
        // 调用基类的StopAsync来更新状态
        await base.StopAsync();
    }
    
    
    /// <summary>
    /// 检查并补齐缺失数据
    /// </summary>
    private async Task CheckAndFillMissingDataAsync()
    {
        try
        {
            Console.WriteLine($"🔍 [{Name}] 检查数据连续性...");
            
            // 获取数据库中已有的日期列表
            var existingDates = await GetExistingDatesAsync();
            
            if (existingDates.Count == 0)
            {
                Console.WriteLine($"📊 [{Name}] 数据库中没有数据，开始首次采集");
                // 采集所有历史数据
                await CollectAllHistoryDataAsync();
                return;
            }
            
            // 找出缺失的日期
            var missingDates = await FindMissingDatesAsync(existingDates);
            
            if (missingDates.Count == 0)
            {
                Console.WriteLine($"✅ [{Name}] 数据完整性检查通过，无缺失数据");
                return;
            }
            
            Console.WriteLine($"⚠️ [{Name}] 发现 {missingDates.Count} 个缺失日期");
            
            // 补齐缺失的数据
            var filledCount = await FillMissingDatesAsync(missingDates);
            Console.WriteLine($"✅ [{Name}] 数据补齐完成，共补齐 {filledCount} 条数据");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [{Name}] 数据连续性检查失败: {ex.Message}");
            // 不抛出异常，允许继续启动
        }
    }
    
    /// <summary>
    /// 获取数据库中已有的日期列表
    /// </summary>
    private async Task<HashSet<string>> GetExistingDatesAsync()
    {
        var sql = "SELECT date FROM fear_greed_index ORDER BY date";
        var dates = await DBHelper.QueryAsync<string>(sql);
        return dates.ToHashSet();
    }
    
    /// <summary>
    /// 找出缺失的日期（从最早日期到今天）
    /// </summary>
    private Task<List<DateTime>> FindMissingDatesAsync(HashSet<string> existingDates)
    {
        var missingDates = new List<DateTime>();
        
        if (existingDates.Count == 0)
            return Task.FromResult(missingDates);
        
        // 获取最早的日期
        var earliestDateStr = existingDates.Min();
        if (!DateTime.TryParse(earliestDateStr, out var earliestDate))
        {
            Console.WriteLine($"⚠️ [{Name}] 无法解析最早日期: {earliestDateStr}");
            return Task.FromResult(missingDates);
        }
        
        // 从最早日期到今天，检查每个日期
        var today = DateTime.UtcNow.Date;
        var currentDate = earliestDate;
        
        while (currentDate <= today)
        {
            var dateStr = currentDate.ToString("yyyy-MM-dd");
            if (!existingDates.Contains(dateStr))
            {
                missingDates.Add(currentDate);
            }
            currentDate = currentDate.AddDays(1);
        }
        
        return Task.FromResult(missingDates);
    }
    
    /// <summary>
    /// 补齐缺失的日期数据
    /// </summary>
    private async Task<int> FillMissingDatesAsync(List<DateTime> missingDates)
    {
        if (missingDates.Count == 0)
            return 0;
        
        // 从API获取所有历史数据（包含缺失的日期）
        var allData = await FetchAllDataFromApiAsync();
        
        if (allData == null || allData.Count == 0)
            return 0;
        
        // 筛选出缺失日期的数据
        var missingDateStrs = missingDates.Select(d => d.ToString("yyyy-MM-dd")).ToHashSet();
        var dataToFill = allData.Where(d => missingDateStrs.Contains(d.Date.ToString("yyyy-MM-dd"))).ToList();
        
        // 保存到数据库
        if (dataToFill.Count > 0)
        {
            await SaveDataAsync(dataToFill);
        }
        
        return dataToFill.Count;
    }
    
    /// <summary>
    /// 采集所有历史数据
    /// </summary>
    private async Task CollectAllHistoryDataAsync()
    {
        var allData = await FetchAllDataFromApiAsync();
        if (allData != null && allData.Count > 0)
        {
            await SaveDataAsync(allData);
            Console.WriteLine($"✅ [{Name}] 首次采集完成，共 {allData.Count} 条数据");
        }
    }
    
    /// <summary>
    /// 启动每天UTC 00:05的定时采集
    /// </summary>
    private async Task StartDailyCollectionAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        
        // 计算到下一个UTC 00:05的时间
        var now = DateTime.UtcNow;
        var nextCollectionTime = new DateTime(now.Year, now.Month, now.Day, 0, 5, 0, DateTimeKind.Utc);
        
        // 如果今天的00:05已过，则设置为明天的00:05
        if (now >= nextCollectionTime)
        {
            nextCollectionTime = nextCollectionTime.AddDays(1);
        }
        
        var delay = (int)(nextCollectionTime - now).TotalMilliseconds;
        
        // 如果延迟时间 <= 0 或太小（小于5分钟），说明当前时间接近采集时间点
        // 为了避免与 CheckAndFillMissingDataAsync 中的补齐数据操作重复调用API，
        // 应该等待至少一个最小间隔（5分钟），或者直接等待下一个周期
        const int minDelayMs = 5 * 60 * 1000; // 5分钟
        if (delay <= 0 || delay < minDelayMs)
        {
            // 如果延迟太小，等待下一个周期（明天）
            nextCollectionTime = nextCollectionTime.AddDays(1);
            delay = (int)(nextCollectionTime - now).TotalMilliseconds;
        }
        
        Console.WriteLine($"🚀 [{Name}] 启动定时采集");
        Console.WriteLine($"   📅 下次采集时间: {nextCollectionTime:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"   ⏰ 距离下次采集: {TimeSpan.FromMilliseconds(delay).TotalHours:F1} 小时");
        
        // 启动后台任务
        _dailyTask = Task.Run(async () =>
        {
            try
            {
                // 等待到下一个采集时间
                await Task.Delay(delay, _cancellationTokenSource.Token);
                
                // 开始定时采集循环
                while (!_cancellationTokenSource.Token.IsCancellationRequested && IsEnabled)
                {
                    try
                    {
                        await CollectAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ [{Name}] 定时采集失败: {ex.Message}");
                    }
                    
                    // 等待24小时后再次采集
                    await Task.Delay(24 * 60 * 60 * 1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
        }, _cancellationTokenSource.Token);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 执行数据采集
    /// </summary>
    protected override async Task<CollectResult<List<FearGreedData>>> DoCollectAsync()
    {
        try
        {
            // 从API获取所有数据（API返回的是所有历史数据）
            var allData = await FetchAllDataFromApiAsync();
            
            if (allData == null || allData.Count == 0)
            {
                return new CollectResult<List<FearGreedData>>
                {
                    Success = false,
                    ErrorMessage = "API返回空数据",
                    CollectTime = DateTime.UtcNow
                };
            }
            
            // 返回所有数据，让数据库的INSERT OR IGNORE来处理重复
            return new CollectResult<List<FearGreedData>>
            {
                Success = true,
                Data = allData,
                DataCount = allData.Count,
                CollectTime = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            return new CollectResult<List<FearGreedData>>
            {
                Success = false,
                ErrorMessage = $"网络请求失败: {ex.Message}",
                CollectTime = DateTime.UtcNow
            };
        }
        catch (JsonException ex)
        {
            return new CollectResult<List<FearGreedData>>
            {
                Success = false,
                ErrorMessage = $"JSON解析失败: {ex.Message}",
                CollectTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new CollectResult<List<FearGreedData>>
            {
                Success = false,
                ErrorMessage = $"采集失败: {ex.Message}",
                CollectTime = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// 从API获取所有历史数据
    /// </summary>
    private async Task<List<FearGreedData>?> FetchAllDataFromApiAsync()
    {
        try
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "Mozilla/5.0 (compatible; fear-greed-index-collector/1.0)" }
            };
            
            var request = new HttpRequestMessage(HttpMethod.Get, API_URL);
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
            
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ [{Name}] API请求失败: HTTP {response.StatusCode} - {errorContent}");
                return null;
            }
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<FearGreedApiResponse>(jsonContent);
            
            if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
            {
                Console.WriteLine($"⚠️ [{Name}] API返回数据为空");
                return null;
            }
            
            // 转换为FearGreedData对象
            var result = new List<FearGreedData>();
            foreach (var item in apiResponse.Data)
            {
                // 解析时间戳
                if (!long.TryParse(item.Timestamp, out var timestamp))
                {
                    Console.WriteLine($"⚠️ [{Name}] 无法解析时间戳: {item.Timestamp}");
                    continue;
                }
                
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.Date;
                
                // 解析value
                if (!int.TryParse(item.Value, out var value))
                {
                    Console.WriteLine($"⚠️ [{Name}] 无法解析value: {item.Value}");
                    continue;
                }
                
                result.Add(new FearGreedData
                {
                    Date = date,
                    Value = value,
                    Classification = item.ValueClassification ?? string.Empty
                });
            }
            
            Console.WriteLine($"✅ [{Name}] 成功获取 {result.Count} 条数据");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [{Name}] 获取API数据失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 保存数据到数据库
    /// </summary>
    protected override async Task SaveDataAsync(List<FearGreedData> data)
    {
        if (data == null || data.Count == 0)
            return;
        
        try
        {
            var sql = @"
                INSERT OR IGNORE INTO fear_greed_index (date, value, classification)
                VALUES (@date, @value, @classification)";
            
            var count = 0;
            var skipped = 0;
            
            foreach (var item in data)
            {
                var dateStr = item.Date.ToString("yyyy-MM-dd");
                
                var affected = await DBHelper.ExecuteAsync(sql, new
                {
                    date = dateStr,
                    value = item.Value,
                    classification = item.Classification
                });
                
                if (affected > 0)
                    count++;
                else
                    skipped++;
            }
            
            Console.WriteLine($"✅ [{Name}] 数据保存完成: {count} 条新增, {skipped} 条已存在（跳过）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [{Name}] 保存数据失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// API响应模型
    /// </summary>
    private class FearGreedApiResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("data")]
        public List<FearGreedApiItem>? Data { get; set; }
    }
    
    /// <summary>
    /// API数据项模型
    /// </summary>
    private class FearGreedApiItem
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }
        
        [JsonPropertyName("value_classification")]
        public string? ValueClassification { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }
}

