using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Data.Models;

namespace Prophet.Client.Data.Abstractions;

/// <summary>
/// 永续合约资金费率能力（跨交易所统一）
/// </summary>
public interface IFundingRateProvider
{
    Task<FundingRateSnapshot?> FetchFundingRateSnapshotAsync(string symbol, CancellationToken cancellationToken = default);
}


