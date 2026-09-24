using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Data.Models;

namespace Prophet.Client.Data.Abstractions;

/// <summary>
/// 永续合约持仓量（Open Interest）能力（跨交易所统一）
/// </summary>
public interface IOpenInterestProvider
{
    Task<OpenInterest?> FetchOpenInterestAsync(string symbol, CancellationToken cancellationToken = default);
}


