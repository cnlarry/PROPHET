using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Database;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Models;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Data.Collectors;

/// <summary>
/// 资金费率采集器
/// 从币安期货API获取资金费率数据
/// 启动时检查缺失数据并补齐，之后每8小时采集（UTC 00:00, 08:00, 16:00）
/// </summary>
public class FundingRateCollector : BaseDataCollector<List<FundingRateData>>
{
    private readonly IBinanceExchangeGateway _gateway;
    private readonly string _symbolKey;
    private readonly string _baseSymbol;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _periodicTask;
    // 资金费率更新时间点（UTC）：00:00, 08:00, 16:00
    private static readonly int[] FundingRateHours = { 0, 8, 16 };
    
    public FundingRateCollector(IBinanceExchangeGateway gateway, string symbolOrKey)
        : base($"FundingRate-{NormalizeSymbolKey(symbolOrKey)}", 28800) // 8小时采集一次（28800秒）
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        if (string.IsNullOrWhiteSpace(symbolOrKey))
        {
            throw new ArgumentNullException(nameof(symbolOrKey));
        }

        if (InstrumentKey.TryParse(symbolOrKey, out var key))
        {
            _symbolKey = key.ToString().ToUpperInvariant();
            _baseSymbol = key.BaseSymbol.ToUpperInvariant();
        }
        else
        {
            _baseSymbol = symbolOrKey.ToUpperInvariant();
            _symbolKey = NormalizeSymbolKey(_baseSymbol);
        }
    }

    private static string NormalizeSymbolKey(string symbolOrKey)
    {
        if (string.IsNullOrWhiteSpace(symbolOrKey))
        {
            return "BTCUSDT-BINANCE-SWAP";
        }

        if (InstrumentKey.TryParse(symbolOrKey, out var key))
        {
            return key.ToString().ToUpperInvariant();
        }

        return $"{symbolOrKey.ToUpperInvariant()}-BINANCE-SWAP";
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
        
        // 启动定时任务（每8小时采集，UTC 00:00, 08:00, 16:00）
        await StartPeriodicCollectionAsync();
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
        
        if (_periodicTask != null)
        {
            try
            {
                await _periodicTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
        }
        
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _periodicTask = null;
        
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
            
            // 获取数据库中已有的最新时间
            var latestTime = await GetLatestCalcTimeAsync();
            
            if (!latestTime.HasValue)
            {
                Console.WriteLine($"📊 [{Name}] 数据库中没有数据，开始首次采集");
                // 采集历史数据（最近1000条，约3个月）
                await CollectHistoryDataAsync();
                return;
            }
            
            // 检查是否有缺失
            var now = DateTime.UtcNow;
            var timeDiff = now - latestTime.Value;
            
            // 如果最新数据超过8小时，补齐缺失的数据
            if (timeDiff.TotalHours > 8)
            {
                Console.WriteLine($"⚠️ [{Name}] 最新数据: {latestTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"   ⏰ 距离现在: {timeDiff.TotalHours:F1} 小时");
                Console.WriteLine($"📥 [{Name}] 开始补齐缺失数据...");
                
                await FillMissingDataAsync(latestTime.Value, now);
            }
            else
            {
                Console.WriteLine($"✅ [{Name}] 数据完整性检查通过，最新数据: {latestTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [{Name}] 数据连续性检查失败: {ex.Message}");
            // 不抛出异常，允许继续启动
        }
    }
    
    /// <summary>
    /// 获取数据库中最新的calc_time
    /// </summary>
    private async Task<DateTime?> GetLatestCalcTimeAsync()
    {
        var sql = "SELECT MAX(calc_time) FROM fundingrate WHERE symbol_key = @symbolKey";
        var result = await DBHelper.ExecuteScalarAsync<long?>(sql, new { symbolKey = _symbolKey });
        
        if (result.HasValue)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(result.Value).UtcDateTime;
        }
        
        return null;
    }
    
    /// <summary>
    /// 补齐缺失的数据
    /// </summary>
    private async Task FillMissingDataAsync(DateTime startTime, DateTime endTime)
    {
        try
        {
            // 从API获取历史数据
            var fundingRates = (await _gateway.FetchFundingRatesAsync(
                _baseSymbol,
                1000,
                startTime,
                endTime)).ToList();
            
            if (fundingRates.Count > 0)
            {
                await SaveDataAsync(fundingRates);
                Console.WriteLine($"✅ [{Name}] 数据补齐完成，共补齐 {fundingRates.Count} 条数据");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [{Name}] 补齐数据失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 采集历史数据（首次采集）
    /// </summary>
    private async Task CollectHistoryDataAsync()
    {
        try
        {
            var fundingRates = (await _gateway.FetchFundingRatesAsync(_baseSymbol, 1000)).ToList();
            
            if (fundingRates.Count > 0)
            {
                await SaveDataAsync(fundingRates);
                Console.WriteLine($"✅ [{Name}] 首次采集完成，共 {fundingRates.Count} 条数据");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [{Name}] 首次采集失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 启动每8小时的定时采集（UTC 00:00, 08:00, 16:00）
    /// </summary>
    private async Task StartPeriodicCollectionAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        
        // 计算到下一个资金费率更新时间的时间
        var now = DateTime.UtcNow;
        var nextCollectionTime = GetNextFundingRateTime(now);
        
        var delay = (int)(nextCollectionTime - now).TotalMilliseconds;
        
        // 如果延迟时间 <= 0 或太小（小于5分钟），说明当前时间接近采集时间点
        // 为了避免与 CheckAndFillMissingDataAsync 中的补齐数据操作重复调用API，
        // 应该等待至少一个最小间隔（5分钟），或者直接等待下一个周期
        const int minDelayMs = 5 * 60 * 1000; // 5分钟
        if (delay <= 0 || delay < minDelayMs)
        {
            // 如果延迟太小，等待下一个周期（8小时后）
            nextCollectionTime = GetNextFundingRateTime(now.AddHours(8));
            delay = (int)(nextCollectionTime - now).TotalMilliseconds;
        }
        
        Console.WriteLine($"🚀 [{Name}] 启动定时采集");
        Console.WriteLine($"   📅 下次采集时间: {nextCollectionTime:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"   ⏰ 距离下次采集: {TimeSpan.FromMilliseconds(delay).TotalHours:F1} 小时");
        
        // 启动后台任务
        _periodicTask = Task.Run(async () =>
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
                    
                    // 计算下一个采集时间（8小时后）
                    var nextTime = GetNextFundingRateTime(DateTime.UtcNow);
                    var nextDelay = (int)(nextTime - DateTime.UtcNow).TotalMilliseconds;
                    
                    // 等待到下一个采集时间
                    await Task.Delay(nextDelay, _cancellationTokenSource.Token);
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
    /// 获取下一个资金费率更新时间
    /// </summary>
    private DateTime GetNextFundingRateTime(DateTime now)
    {
        var today = now.Date;
        var currentHour = now.Hour;
        
        // 找到下一个更新时间点
        foreach (var hour in FundingRateHours)
        {
            var targetTime = today.AddHours(hour);
            if (targetTime > now)
            {
                return targetTime;
            }
        }
        
        // 如果今天的所有时间点都已过，返回明天的第一个时间点
        return today.AddDays(1).AddHours(FundingRateHours[0]);
    }
    
    /// <summary>
    /// 执行数据采集
    /// </summary>
    protected override async Task<CollectResult<List<FundingRateData>>> DoCollectAsync()
    {
        try
        {
            // 从API获取最新数据（最近1000条）
            var apiData = await _gateway.FetchFundingRatesAsync(_baseSymbol, 1000);
            
            if (apiData == null || apiData.Count == 0)
            {
                return new CollectResult<List<FundingRateData>>
                {
                    Success = false,
                    ErrorMessage = "API返回空数据",
                    CollectTime = DateTime.UtcNow
                };
            }
            
            return new CollectResult<List<FundingRateData>>
            {
                Success = true,
                Data = apiData.ToList(),
                DataCount = apiData.Count,
                CollectTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new CollectResult<List<FundingRateData>>
            {
                Success = false,
                ErrorMessage = $"采集失败: {ex.Message}",
                CollectTime = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// 保存数据到数据库
    /// </summary>
    protected override async Task SaveDataAsync(List<FundingRateData> data)
    {
        if (data == null || data.Count == 0)
            return;
        
        try
        {
            var sql = @"
                INSERT OR IGNORE INTO fundingrate (
                    symbol_key, calc_time, calc_time_str, funding_interval_hours, last_funding_rate, created_at
                ) VALUES (
                    @symbolKey, @calcTime, @calcTimeStr, @fundingIntervalHours, @lastFundingRate, @createdAt
                )";
            
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var count = 0;
            var skipped = 0;
            
            foreach (var item in data)
            {
                var affected = await DBHelper.ExecuteAsync(sql, new
                {
                    symbolKey = NormalizeSymbolKey(item.Symbol),
                    calcTime = item.CalcTime,
                    calcTimeStr = item.CalcTimeStr?.ToString("yyyy-MM-dd HH:mm:ss"),
                    fundingIntervalHours = item.FundingIntervalHours,
                    lastFundingRate = item.LastFundingRate,
                    createdAt = now
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
    
}

