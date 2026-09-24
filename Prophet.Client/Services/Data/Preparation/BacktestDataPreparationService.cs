using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Services.Market;
using Prophet.Client.Models;

namespace Prophet.Client.Services.Data.Preparation;

/// <summary>
/// 回测数据准备服务
/// v10.0: 智能数据准备系统
/// - 自动分析DSL提取时间框架
/// - 检查数据完整性
/// - 智能下载缺失数据（按月/按日）
/// - API补齐小缺口
/// - 数据对齐验证
/// </summary>
public class BacktestDataPreparationService
{
    private readonly DataIntegrityChecker _integrityChecker;
    private readonly TimeSeriesDataIntegrityChecker _timeSeriesIntegrityChecker;
    private readonly KlineSyncService _klineSyncService;
    private readonly MarketDataRepository _repository;
    private readonly IBinanceExchangeGateway? _gateway;
    
    // 时间序列数据（在PrepareAsync中准备）
    public List<FearGreedData> FearGreedData { get; private set; } = new();
    public List<FundingRateData> FundingRateData { get; private set; } = new();
    public List<LongShortRatioData> LongShortRatioData { get; private set; } = new();
    
    public BacktestDataPreparationService(
        HttpClient? httpClient = null,
        MarketDataRepository? marketDataRepository = null,
        IBinanceExchangeGateway? gateway = null,
        KlineSyncService? klineSyncService = null)
    {
        _integrityChecker = new DataIntegrityChecker();
        _repository = marketDataRepository ?? new MarketDataRepository();
        
        IBinanceExchangeGateway effectiveGateway;
        if (gateway != null)
        {
            effectiveGateway = gateway;
            _gateway = gateway;
        }
        else
        {
            effectiveGateway = new BinanceExchangeGateway(httpClient);
            _gateway = effectiveGateway;
        }
        
        _timeSeriesIntegrityChecker = new TimeSeriesDataIntegrityChecker(effectiveGateway);
        
        if (klineSyncService != null)
        {
            _klineSyncService = klineSyncService;
        }
        else
        {
            var downloader = new BinanceHistoricalDataDownloader(effectiveGateway.HttpClient);
            var gapFiller = new BinanceGapFiller(effectiveGateway, _repository);
            _klineSyncService = new KlineSyncService(downloader, gapFiller);
        }
    }
    
    /// <summary>
    /// 准备回测数据
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="dslCode">策略DSL代码</param>
    /// <param name="startDate">回测开始时间</param>
    /// <param name="endDate">回测结束时间</param>
    /// <param name="progress">进度报告</param>
    /// <param name="signalSamplingInterval">信号采样频率（可选，也是时间框架）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task PrepareAsync(
        string symbol,
        string dslCode,
        DateTime startDate,
        DateTime endDate,
        IProgress<(string Message, double Percent)>? progress = null,
        string? signalSamplingInterval = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report(("分析策略DSL...", 0));
            
            // 1. 分析DSL，提取时间框架
            HashSet<string> timeframes;
            try
            {
                timeframes = DSLAnalyzer.ExtractTimeframes(dslCode);
                Console.WriteLine($"📋 策略需要 {timeframes.Count} 个时间框架: {string.Join(", ", timeframes)}");
            }
            catch (Exception ex)
            {
                throw new Exception($"分析DSL失败: {ex.Message}", ex);
            }
            
            // 🔧 将信号采样频率添加到时间框架集合中（如果提供且不在DSL中）
            if (!string.IsNullOrWhiteSpace(signalSamplingInterval) && !timeframes.Contains(signalSamplingInterval))
            {
                timeframes.Add(signalSamplingInterval);
                Console.WriteLine($"📊 添加信号采样频率到数据准备流程: {signalSamplingInterval}");
            }
            
            progress?.Report(("检查数据完整性...", 10));
            
            // 2. 检查数据完整性（包含预热期）
            var integrityResults = new Dictionary<string, DataIntegrityResult>();
            var timeframeRanges = new Dictionary<string, (DateTime Start, DateTime End)>();
            
            foreach (var timeframe in timeframes.OrderBy(DSLAnalyzer.TimeframeToMinutes))
            {
                // 计算包含预热期的实际时间范围
                var (actualStart, actualEnd) = DSLAnalyzer.CalculateDateRange(
                    dslCode, timeframe, startDate, endDate
                );
                timeframeRanges[timeframe] = (actualStart, actualEnd);
                
                Console.WriteLine($"🔍 检查 {timeframe} 数据完整性（含预热期）...");
                Console.WriteLine($"   时间范围: {actualStart:yyyy-MM-dd} ~ {actualEnd:yyyy-MM-dd}");
                
                var result = await _integrityChecker.CheckAsync(
                    symbol, timeframe, actualStart, actualEnd, cancellationToken
                );
                
                integrityResults[timeframe] = result;
                
                Console.WriteLine($"   现有数据: {result.ExistingCount}/{result.ExpectedCount} " +
                                $"({result.CompletenessPercentage:F2}%)");
                Console.WriteLine($"   缺口数量: {result.Gaps.Count}");
                
                if (result.Gaps.Count > 0)
                {
                    foreach (var gap in result.Gaps.Take(3))
                    {
                        Console.WriteLine($"     - {gap}");
                    }
                    if (result.Gaps.Count > 3)
                    {
                        Console.WriteLine($"     ... 还有 {result.Gaps.Count - 3} 个缺口");
                    }
                }
            }
            
            progress?.Report(("数据完整性检查完成", 20));
            
            // 3. 检查并准备时间序列数据（资金费率和恐惧与贪婪指数）
            progress?.Report(("检查时间序列数据完整性...", 25));
            await CheckAndPrepareTimeSeriesDataAsync(symbol, startDate, endDate, cancellationToken);
            
            // 4. 判断是否需要下载/补齐数据
            bool needsDataPreparation = integrityResults.Values.Any(r => !r.IsComplete);
            
            if (!needsDataPreparation)
            {
                Console.WriteLine("✅ 所有数据完整，无需下载");
                progress?.Report(("数据准备完成", 100));
                return;
            }
            
            Console.WriteLine();
            Console.WriteLine("📥 开始自动准备数据...");
            
            // 5. 下载缺失数据
            foreach (var timeframe in timeframes.OrderBy(DSLAnalyzer.TimeframeToMinutes))
            {
                var result = integrityResults[timeframe];
                if (result.IsComplete) continue;
                
                cancellationToken.ThrowIfCancellationRequested();
                var (rangeStart, rangeEnd) = timeframeRanges[timeframe];
                await _klineSyncService.EnsureCoverageAsync(
                    symbol,
                    timeframe,
                    rangeStart,
                    rangeEnd,
                    progress,
                    cancellationToken);
            }
            
            progress?.Report(("验证数据完整性...", 90));
            
            // 6. 再次验证数据完整性（补齐后需要等待一小段时间确保数据库写入完成）
            // 增加等待时间，确保数据库事务完全提交（SQLite可能需要更多时间）
            await Task.Delay(1000, cancellationToken); // 等待1秒确保数据库写入完成
            
            Console.WriteLine();
            Console.WriteLine("🔍 验证数据完整性...");
            
            bool allComplete = true;
            foreach (var timeframe in timeframes.OrderBy(DSLAnalyzer.TimeframeToMinutes))
            {
                var (actualStart, actualEnd) = DSLAnalyzer.CalculateDateRange(
                    dslCode, timeframe, startDate, endDate
                );
                
                // 重新创建DataIntegrityChecker实例，确保使用最新的数据库连接
                var integrityChecker = new DataIntegrityChecker();
                var result = await integrityChecker.CheckAsync(
                    symbol, timeframe, actualStart, actualEnd, cancellationToken
                );
                
                if (result.IsComplete)
                {
                    Console.WriteLine($"   ✅ {timeframe}: 100% 完整");
                }
                else
                {
                    Console.WriteLine($"   ⚠️ {timeframe}: {result.CompletenessPercentage:F2}% " +
                                    $"(仍有 {result.Gaps.Count} 个缺口)");
                    allComplete = false;
                }
            }
            
            if (!allComplete)
            {
                Console.WriteLine();
                Console.WriteLine("⚠️ 数据准备未完全成功，部分缺口可能无法填补");
                Console.WriteLine("   可能原因：");
                Console.WriteLine("   1. 币安数据源中确实没有该时间段的数据");
                Console.WriteLine("   2. 网络问题导致下载失败");
                Console.WriteLine("   3. API限流");
                Console.WriteLine();
                Console.WriteLine("💡 建议：检查缺口时间段，或稍后重试");
                
                // 不抛出异常，允许用户选择是否继续
            }
            
            progress?.Report(("数据准备完成", 100));
            Console.WriteLine();
            Console.WriteLine("✅ 数据准备流程完成");
            
            // TODO: 实现自动下载和补齐
            // progress?.Report(("下载缺失数据...", 30));
            // await DownloadMissingDataAsync(...);
            
            // progress?.Report(("补齐数据缺口...", 70));
            // await FillGapsAsync(...);
            
            // progress?.Report(("验证数据完整性...", 90));
            // await VerifyDataAsync(...);
            
            // progress?.Report(("数据准备完成", 100));
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("⚠️ 数据准备已取消");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 数据准备失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 检查并准备时间序列数据（资金费率和恐惧与贪婪指数）
    /// </summary>
    private async Task CheckAndPrepareTimeSeriesDataAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("📊 检查时间序列数据完整性...");
            
            // 🆕 v12.0: 统一偏移量范围 [-100, 0]
            // 所有时间序列函数的偏移量范围统一为 [-100, 0]
            // 需要基于回测开始日期向前偏移100天（足够覆盖所有偏移量需求）
            var fearGreedStartDate = startDate.Date.AddDays(-100);
            var fearGreedEndDate = endDate.Date;
            
            // 资金费率：偏移量范围统一为 [-100, 0]
            // 需要根据实际的结算周期计算偏移量（不是固定的8小时）
            // 先从数据库获取实际的结算周期，如果不存在则使用默认值8小时
            var fundingIntervalHours = await _timeSeriesIntegrityChecker.GetFundingIntervalHoursAsync(symbol, cancellationToken);
            if (fundingIntervalHours <= 0)
            {
                fundingIntervalHours = 8; // 默认8小时
            }
            
            // 100次 × 结算周期 = 需要的总小时数
            var fundingRateHoursNeeded = 100 * fundingIntervalHours;
            var fundingRateDaysNeeded = (int)Math.Ceiling(fundingRateHoursNeeded / 24.0);
            var fundingRateStartTime = startDate.AddDays(-fundingRateDaysNeeded);
            var fundingRateEndTime = endDate;
            
            Console.WriteLine($"   资金费率结算周期: {fundingIntervalHours} 小时");
            Console.WriteLine($"   需要向前偏移: {fundingRateDaysNeeded} 天 ({fundingRateHoursNeeded} 小时)");
            
            // 1. 检查恐惧与贪婪指数数据完整性
            Console.WriteLine($"🔍 检查恐惧与贪婪指数数据完整性（含预热期）...");
            Console.WriteLine($"   时间范围: {fearGreedStartDate:yyyy-MM-dd} ~ {fearGreedEndDate:yyyy-MM-dd}");
            var fearGreedResult = await _timeSeriesIntegrityChecker.CheckFearGreedAsync(
                fearGreedStartDate, fearGreedEndDate, cancellationToken
            );
            
            Console.WriteLine($"   期望数量: {fearGreedResult.ExpectedCount}");
            Console.WriteLine($"   现有数量: {fearGreedResult.ExistingCount} ({fearGreedResult.CompletenessPercentage:F2}%)");
            Console.WriteLine($"   缺口数量: {fearGreedResult.Gaps.Count}");
            
            // 如果数据不完整，补齐数据
            // 恐惧与贪婪指数API可以一次性获取所有历史数据，所以如果缺失就请求一次API
            if (!fearGreedResult.IsComplete && fearGreedResult.Gaps.Count > 0)
            {
                Console.WriteLine($"📥 开始补齐恐惧与贪婪指数缺失数据...");
                var filledCount = await _timeSeriesIntegrityChecker.FillFearGreedGapsAsync(
                    fearGreedResult.Gaps, cancellationToken
                );
                Console.WriteLine($"✅ 恐惧与贪婪指数数据补齐完成，共补齐 {filledCount} 条");
            }
            
            // 2. 检查资金费率数据完整性
            Console.WriteLine($"🔍 检查资金费率数据完整性（含预热期）...");
            Console.WriteLine($"   时间范围: {fundingRateStartTime:yyyy-MM-dd HH:mm} ~ {fundingRateEndTime:yyyy-MM-dd HH:mm}");
            var fundingRateResult = await _timeSeriesIntegrityChecker.CheckFundingRateAsync(
                symbol, fundingRateStartTime, fundingRateEndTime, cancellationToken
            );
            
            Console.WriteLine($"   期望数量: {fundingRateResult.ExpectedCount}");
            Console.WriteLine($"   现有数量: {fundingRateResult.ExistingCount} ({fundingRateResult.CompletenessPercentage:F2}%)");
            Console.WriteLine($"   缺口数量: {fundingRateResult.Gaps.Count}");
            
            if (!fundingRateResult.IsComplete && fundingRateResult.Gaps.Count > 0)
            {
                Console.WriteLine($"📥 开始补齐资金费率缺失数据...");
                var filledCount = await _timeSeriesIntegrityChecker.FillFundingRateGapsAsync(
                    symbol, fundingRateResult.Gaps, cancellationToken
                );
                Console.WriteLine($"✅ 资金费率数据补齐完成，共补齐 {filledCount} 条");
            }
            
            // 3. 加载时间序列数据到内存
            // 恐惧与贪婪指数：加载从 fearGreedStartDate 到 endDate 的数据
            var fearGreedData = await _repository.GetFearGreedDataAsync(
                days: (fearGreedEndDate - fearGreedStartDate).Days + 1,
                endDate: fearGreedEndDate.AddDays(1) // 包含结束日期当天
            );
            
            FearGreedData = fearGreedData;
            Console.WriteLine($"   ✅ 已加载 {FearGreedData.Count} 条恐惧与贪婪指数数据");
            if (FearGreedData.Count > 0)
            {
                Console.WriteLine($"   数据范围: {FearGreedData.First().Date:yyyy-MM-dd} ~ {FearGreedData.Last().Date:yyyy-MM-dd}");
            }
            
            // 资金费率：加载从 fundingRateStartTime 到 endDate 的数据
            // 计算需要的条数：根据实际的结算周期计算
            var fundingRateHoursTotal = (fundingRateEndTime - fundingRateStartTime).TotalHours;
            var fundingRateCountNeeded = (int)Math.Ceiling(fundingRateHoursTotal / fundingIntervalHours) + 50; // 加50条缓冲
            var fundingRateData = await _repository.GetFundingRateDataAsync(
                symbol: symbol,
                count: fundingRateCountNeeded,
                endTime: fundingRateEndTime
            );
            
            FundingRateData = fundingRateData;
            Console.WriteLine($"   ✅ 已加载 {FundingRateData.Count} 条资金费率数据");
            if (FundingRateData.Count > 0)
            {
                var firstTime = DateTimeOffset.FromUnixTimeMilliseconds(FundingRateData.First().CalcTime).UtcDateTime;
                var lastTime = DateTimeOffset.FromUnixTimeMilliseconds(FundingRateData.Last().CalcTime).UtcDateTime;
                Console.WriteLine($"   数据范围: {firstTime:yyyy-MM-dd HH:mm} ~ {lastTime:yyyy-MM-dd HH:mm}");
            }
            
            // 3. 多空比数据：检查数据完整性并加载
            Console.WriteLine($"🔍 检查多空比数据完整性（含预热期）...");
            // 多空比数据周期：默认使用5m（与K线周期一致，或根据策略需求调整）
            var longShortPeriod = "5m"; // TODO: 可以从策略配置或参数中获取
            // 计算需要的时间范围：向前偏移100个周期（根据周期计算）
            var periodMinutes = ParsePeriodToMinutes(longShortPeriod);
            var longShortHoursNeeded = 100 * periodMinutes / 60.0;
            var longShortDaysNeeded = (int)Math.Ceiling(longShortHoursNeeded / 24.0);
            var longShortStartTime = startDate.AddDays(-longShortDaysNeeded);
            var longShortEndTime = endDate;
            
            Console.WriteLine($"   周期: {longShortPeriod} ({periodMinutes}分钟)");
            Console.WriteLine($"   时间范围: {longShortStartTime:yyyy-MM-dd HH:mm} ~ {longShortEndTime:yyyy-MM-dd HH:mm}");
            
            var longShortResult = await _timeSeriesIntegrityChecker.CheckLongShortRatioAsync(
                symbol, longShortPeriod, longShortStartTime, longShortEndTime, cancellationToken
            );
            
            Console.WriteLine($"   期望数量: {longShortResult.ExpectedCount}");
            Console.WriteLine($"   现有数量: {longShortResult.ExistingCount} ({longShortResult.CompletenessPercentage:F2}%)");
            Console.WriteLine($"   缺口数量: {longShortResult.Gaps.Count}");
            
            // 如果数据不完整，补齐数据
            if (!longShortResult.IsComplete && longShortResult.Gaps.Count > 0)
            {
                Console.WriteLine($"📥 开始补齐多空比缺失数据...");
                var filledCount = await _timeSeriesIntegrityChecker.FillLongShortRatioGapsAsync(
                    symbol, longShortPeriod, longShortResult.Gaps, cancellationToken
                );
                Console.WriteLine($"✅ 多空比数据补齐完成，共补齐 {filledCount} 条");
            }
            
            // 加载多空比数据到内存
            var longShortCountNeeded = (int)Math.Ceiling((longShortEndTime - longShortStartTime).TotalMinutes / periodMinutes) + 50; // 加50条缓冲
            var longShortData = await _repository.GetLongShortRatioDataAsync(
                symbol: symbol,
                period: longShortPeriod,
                count: longShortCountNeeded,
                endTime: longShortEndTime
            );
            
            LongShortRatioData = longShortData;
            Console.WriteLine($"   ✅ 已加载 {LongShortRatioData.Count} 条多空比数据");
            if (LongShortRatioData.Count > 0)
            {
                Console.WriteLine($"   数据范围: {LongShortRatioData.First().UpdateTime:yyyy-MM-dd HH:mm} ~ {LongShortRatioData.Last().UpdateTime:yyyy-MM-dd HH:mm}");
            }
            
            Console.WriteLine("✅ 时间序列数据准备完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 准备时间序列数据失败: {ex.Message}");
            // 不抛出异常，允许回测继续（如果策略不使用这些数据）
            FearGreedData = new List<FearGreedData>();
            FundingRateData = new List<FundingRateData>();
            LongShortRatioData = new List<LongShortRatioData>();
        }
    }
    
    /// <summary>
    /// 解析周期字符串为分钟数（辅助方法）
    /// </summary>
    private int ParsePeriodToMinutes(string period)
    {
        if (string.IsNullOrWhiteSpace(period))
            return 0;
        
        period = period.ToLowerInvariant().Trim();
        
        if (period.EndsWith("m"))
        {
            if (int.TryParse(period.Substring(0, period.Length - 1), out var minutes))
                return minutes;
        }
        else if (period.EndsWith("h"))
        {
            if (int.TryParse(period.Substring(0, period.Length - 1), out var hours))
                return hours * 60;
        }
        else if (period.EndsWith("d"))
        {
            if (int.TryParse(period.Substring(0, period.Length - 1), out var days))
                return days * 24 * 60;
        }
        
        return 0;
    }
}

