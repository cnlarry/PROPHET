using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Database;
using Microsoft.Data.Sqlite;

namespace Prophet.Client.Services.Data.Preparation;

/// <summary>
/// 币安历史数据下载器
/// 从 data.binance.vision 下载 Futures 历史数据
/// </summary>
public class BinanceHistoricalDataDownloader
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL_MONTHLY = "https://data.binance.vision/data/futures/um/monthly/klines";
    private const string BASE_URL_DAILY = "https://data.binance.vision/data/futures/um/daily/klines";
    private const int MAX_RETRIES = 3;
    private const int RETRY_DELAY_MS = 5000;
    
    public BinanceHistoricalDataDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }
    
    /// <summary>
    /// 下载并导入缺失的数据
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="timeframe">时间框架</param>
    /// <param name="gaps">缺口列表</param>
    /// <param name="progress">进度报告</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DownloadAndImportAsync(
        string symbol,
        string timeframe,
        List<DataGap> gaps,
        IProgress<(string Message, double Percent)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (gaps.Count == 0)
        {
            Console.WriteLine($"✅ {symbol} {timeframe}: 无需下载");
            return;
        }
        
        Console.WriteLine($"📥 开始下载 {symbol} {timeframe} 数据...");
        Console.WriteLine($"   缺口数量: {gaps.Count}");
        Console.WriteLine($"   总缺失: {gaps.Sum(g => g.MissingCount)} 根");
        
        // 确定下载策略
        var downloadPlan = PlanDownloads(gaps);
        
        Console.WriteLine($"   下载计划: {downloadPlan.MonthlyFiles.Count} 个月文件 + {downloadPlan.DailyFiles.Count} 个日文件");
        
        int totalFiles = downloadPlan.MonthlyFiles.Count + downloadPlan.DailyFiles.Count;
        int completedFiles = 0;
        
        // 创建临时目录
        var tempDir = Path.Combine(Path.GetTempPath(), $"prophet_download_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // 下载月度文件
            foreach (var month in downloadPlan.MonthlyFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                double baseProgress = 20 + (completedFiles * 70.0 / totalFiles);
                progress?.Report(($"下载月度数据 {month:yyyy-MM}...", baseProgress));
                
                await DownloadAndImportMonthlyAsync(
                    symbol, timeframe, month, tempDir, cancellationToken
                );
                
                completedFiles++;
            }
            
            // 下载日度文件
            foreach (var date in downloadPlan.DailyFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                double baseProgress = 20 + (completedFiles * 70.0 / totalFiles);
                progress?.Report(($"下载日度数据 {date:yyyy-MM-dd}...", baseProgress));
                
                await DownloadAndImportDailyAsync(
                    symbol, timeframe, date, tempDir, cancellationToken
                );
                
                completedFiles++;
            }
            
            Console.WriteLine($"✅ {symbol} {timeframe} 数据下载完成");
        }
        finally
        {
            // 清理临时目录
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                    Console.WriteLine($"🗑️  已清理临时文件: {tempDir}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 清理临时文件失败: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 规划下载策略（按月 vs 按日）
    /// </summary>
    private DownloadPlan PlanDownloads(List<DataGap> gaps)
    {
        var plan = new DownloadPlan();
        
        foreach (var gap in gaps)
        {
            // 如果缺口 >= 30天，按月下载
            if (gap.DurationDays >= 30)
            {
                var start = new DateTime(gap.StartTime.Year, gap.StartTime.Month, 1);
                var end = gap.EndTime;
                
                while (start <= end)
                {
                    plan.MonthlyFiles.Add(start);
                    start = start.AddMonths(1);
                }
            }
            // 如果缺口 >= 7天，按日下载
            else if (gap.DurationDays >= 7)
            {
                var current = gap.StartTime.Date;
                while (current <= gap.EndTime.Date)
                {
                    plan.DailyFiles.Add(current);
                    current = current.AddDays(1);
                }
            }
            // 如果缺口 < 7天，标记为API补齐
            else
            {
                plan.ApiGaps.Add(gap);
            }
        }
        
        // 去重
        plan.MonthlyFiles = plan.MonthlyFiles.Distinct().OrderBy(d => d).ToList();
        plan.DailyFiles = plan.DailyFiles.Distinct().OrderBy(d => d).ToList();
        
        return plan;
    }
    
    /// <summary>
    /// 下载并导入月度数据（如果月度文件不存在，自动降级为日度）
    /// </summary>
    private async Task DownloadAndImportMonthlyAsync(
        string symbol,
        string timeframe,
        DateTime month,
        string tempDir,
        CancellationToken cancellationToken)
    {
        var fileName = $"{symbol}-{timeframe}-{month:yyyy-MM}.zip";
        var url = $"{BASE_URL_MONTHLY}/{symbol}/{timeframe}/{fileName}";
        var zipPath = Path.Combine(tempDir, fileName);
        
        Console.WriteLine($"   📥 下载: {fileName}");
        
        try
        {
            // 尝试下载月度文件
            await DownloadFileWithRetryAsync(url, zipPath, cancellationToken);
            
            // 解压并导入
            await ExtractAndImportAsync(symbol, timeframe, zipPath, tempDir, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // 月度文件不存在（当前月份或未来月份），降级为按日下载
            Console.WriteLine($"      ⚠️ 月度文件不存在，降级为按日下载...");
            
            // 计算该月的所有日期（排除今天和未来日期）
            var today = DateTime.UtcNow.Date;
            var startDay = month;
            var endDay = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            
            if (endDay >= today)
                endDay = today.AddDays(-1); // 排除今天
            
            var current = startDay;
            while (current <= endDay)
            {
                await DownloadAndImportDailyAsync(symbol, timeframe, current, tempDir, cancellationToken);
                current = current.AddDays(1);
            }
        }
    }
    
    /// <summary>
    /// 下载并导入日度数据（如果日度文件不存在，跳过由API补齐）
    /// </summary>
    private async Task DownloadAndImportDailyAsync(
        string symbol,
        string timeframe,
        DateTime date,
        string tempDir,
        CancellationToken cancellationToken)
    {
        // 不下载今天和未来的数据
        if (date >= DateTime.UtcNow.Date)
        {
            Console.WriteLine($"      ⏭️ 跳过未生成的日期: {date:yyyy-MM-dd}");
            return;
        }
        
        var fileName = $"{symbol}-{timeframe}-{date:yyyy-MM-dd}.zip";
        var url = $"{BASE_URL_DAILY}/{symbol}/{timeframe}/{fileName}";
        var zipPath = Path.Combine(tempDir, fileName);
        
        Console.WriteLine($"   📥 下载: {fileName}");
        
        try
        {
            // 下载ZIP文件（带重试）
            await DownloadFileWithRetryAsync(url, zipPath, cancellationToken);
            
            // 解压并导入
            await ExtractAndImportAsync(symbol, timeframe, zipPath, tempDir, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // 日度文件不存在（通常是今天或数据未生成），跳过
            Console.WriteLine($"      ⚠️ 日度文件不存在，将通过API补齐");
        }
    }
    
    /// <summary>
    /// 下载文件（带重试机制）
    /// </summary>
    private async Task DownloadFileWithRetryAsync(
        string url,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, cancellationToken);
                
                Console.WriteLine($"      ✅ 下载成功 ({new FileInfo(destinationPath).Length / 1024.0:F2} KB)");
                return;
            }
            catch (Exception ex) when (attempt < MAX_RETRIES)
            {
                Console.WriteLine($"      ⚠️ 下载失败 (尝试 {attempt}/{MAX_RETRIES}): {ex.Message}");
                Console.WriteLine($"      等待 {RETRY_DELAY_MS / 1000} 秒后重试...");
                await Task.Delay(RETRY_DELAY_MS, cancellationToken);
            }
        }
        
        throw new Exception($"下载失败（已重试 {MAX_RETRIES} 次）: {url}");
    }
    
    /// <summary>
    /// 解压并导入CSV数据
    /// </summary>
    private async Task ExtractAndImportAsync(
        string symbol,
        string timeframe,
        string zipPath,
        string tempDir,
        CancellationToken cancellationToken)
    {
        // 解压
        var extractDir = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(zipPath));
        ZipFile.ExtractToDirectory(zipPath, extractDir);
        
        // 查找CSV文件
        var csvFiles = Directory.GetFiles(extractDir, "*.csv");
        if (csvFiles.Length == 0)
        {
            throw new Exception($"ZIP文件中未找到CSV文件: {zipPath}");
        }
        
        var csvPath = csvFiles[0];
        Console.WriteLine($"      📄 解压: {Path.GetFileName(csvPath)}");
        
        // 导入数据库
        await ImportCsvToDatabase(symbol, timeframe, csvPath, cancellationToken);
        
        // 清理解压的文件
        File.Delete(csvPath);
        Directory.Delete(extractDir, true);
        File.Delete(zipPath);
    }
    
    /// <summary>
    /// 将CSV数据导入数据库
    /// </summary>
    private async Task ImportCsvToDatabase(
        string symbol,
        string timeframe,
        string csvPath,
        CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(csvPath, cancellationToken);
        
        Console.WriteLine($"      📊 导入数据库: {lines.Length} 行");
        
        using var connection = (SqliteConnection)DBHelper.CreateConnection();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            int importedCount = 0;
            int skippedCount = 0;
            
            // 跳过第一行（表头）
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                cancellationToken.ThrowIfCancellationRequested();
                
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var fields = line.Split(',');
                if (fields.Length < 12) continue;
                
                // 解析CSV字段
                // 0: Open time, 1: Open, 2: High, 3: Low, 4: Close, 5: Volume
                // 6: Close time, 7: Quote asset volume, 8: Number of trades
                // 9: Taker buy base asset volume, 10: Taker buy quote asset volume, 11: Ignore
                
                long openTime = long.Parse(fields[0]);
                decimal open = decimal.Parse(fields[1], CultureInfo.InvariantCulture);
                decimal high = decimal.Parse(fields[2], CultureInfo.InvariantCulture);
                decimal low = decimal.Parse(fields[3], CultureInfo.InvariantCulture);
                decimal close = decimal.Parse(fields[4], CultureInfo.InvariantCulture);
                decimal volume = decimal.Parse(fields[5], CultureInfo.InvariantCulture);
                long closeTime = long.Parse(fields[6]);
                decimal quoteVolume = decimal.Parse(fields[7], CultureInfo.InvariantCulture);
                long count = long.Parse(fields[8]);
                decimal takerBuyVolume = decimal.Parse(fields[9], CultureInfo.InvariantCulture);
                decimal takerBuyQuoteVolume = decimal.Parse(fields[10], CultureInfo.InvariantCulture);
                
                var openTimeStr = DateTimeOffset.FromUnixTimeMilliseconds(openTime).DateTime;
                var closeTimeStr = DateTimeOffset.FromUnixTimeMilliseconds(closeTime).DateTime;
                
                // 兼容SQLite表结构（trade_count 而不是 count）
                using var cmd = new SqliteCommand(@"
                    INSERT OR IGNORE INTO klines (
                        symbol, `interval`, open_time, open, high, low, close, volume,
                        close_time, quote_volume, trade_count, 
                        taker_buy_volume, taker_buy_quote_volume, created_at
                    ) VALUES (
                        @symbol, @interval, @open_time, @open, @high, @low, @close, @volume,
                        @close_time, @quote_volume, @trade_count,
                        @taker_buy_volume, @taker_buy_quote_volume, @created_at
                    )", (SqliteConnection)connection, (SqliteTransaction)transaction);
                
                cmd.Parameters.AddWithValue("@symbol", symbol);
                cmd.Parameters.AddWithValue("@interval", timeframe);
                cmd.Parameters.AddWithValue("@open_time", openTime);
                cmd.Parameters.AddWithValue("@open", open);
                cmd.Parameters.AddWithValue("@high", high);
                cmd.Parameters.AddWithValue("@low", low);
                cmd.Parameters.AddWithValue("@close", close);
                cmd.Parameters.AddWithValue("@volume", volume);
                cmd.Parameters.AddWithValue("@close_time", closeTime);
                cmd.Parameters.AddWithValue("@quote_volume", quoteVolume);
                cmd.Parameters.AddWithValue("@trade_count", count);
                cmd.Parameters.AddWithValue("@taker_buy_volume", takerBuyVolume);
                cmd.Parameters.AddWithValue("@taker_buy_quote_volume", takerBuyQuoteVolume);
                cmd.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                
                int affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                if (affected > 0)
                    importedCount++;
                else
                    skippedCount++;
            }
            
            transaction.Commit();
            
            Console.WriteLine($"      ✅ 导入完成: {importedCount} 新增, {skippedCount} 跳过（已存在）");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}

/// <summary>
/// 下载计划
/// </summary>
internal class DownloadPlan
{
    /// <summary>
    /// 需要下载的月度文件
    /// </summary>
    public List<DateTime> MonthlyFiles { get; set; } = new();
    
    /// <summary>
    /// 需要下载的日度文件
    /// </summary>
    public List<DateTime> DailyFiles { get; set; } = new();
    
    /// <summary>
    /// 需要API补齐的缺口
    /// </summary>
    public List<DataGap> ApiGaps { get; set; } = new();
}

