using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Services.Data.Preparation;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 封装K线数据完整性检查与补齐流程
/// </summary>
public class KlineSyncService
{
    private readonly DataIntegrityChecker _integrityChecker = new();
    private readonly BinanceHistoricalDataDownloader _downloader;
    private readonly BinanceGapFiller _gapFiller;

    public KlineSyncService(BinanceHistoricalDataDownloader downloader, BinanceGapFiller gapFiller)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _gapFiller = gapFiller ?? throw new ArgumentNullException(nameof(gapFiller));
    }

    public async Task EnsureCoverageAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        IProgress<(string Message, double Percent)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var initialResult = await _integrityChecker.CheckAsync(symbol, interval, startTime, endTime, cancellationToken);

        if (initialResult.IsComplete)
        {
            Console.WriteLine($"✅ [{interval}] 数据已完整: {symbol} {startTime:yyyy-MM-dd} -> {endTime:yyyy-MM-dd}");
            return;
        }

        Console.WriteLine($"🔧 [{interval}] 发现 {initialResult.Gaps.Count} 个缺口，开始自动补齐...");

        var largeGaps = initialResult.Gaps.Where(g => g.DurationDays >= 7).ToList();
        if (largeGaps.Count > 0)
        {
            try
            {
                await _downloader.DownloadAndImportAsync(symbol, interval, largeGaps, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [{interval}] 历史下载失败部分缺口: {ex.Message}");
            }
        }

        var recheckResult = await _integrityChecker.CheckAsync(symbol, interval, startTime, endTime, cancellationToken);
        if (recheckResult.Gaps.Count > 0)
        {
            await _gapFiller.FillGapsAsync(symbol, interval, recheckResult.Gaps, progress, cancellationToken);
        }

        Console.WriteLine($"✅ [{interval}] 数据同步完成: {symbol} {startTime:yyyy-MM-dd} -> {endTime:yyyy-MM-dd}");
    }
}
