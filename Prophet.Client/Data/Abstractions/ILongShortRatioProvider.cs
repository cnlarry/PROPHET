using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Data.Models;

namespace Prophet.Client.Data.Abstractions;

public interface ILongShortRatioProvider
{
    Task<LongShortRatio?> FetchLongShortRatioAsync(string symbol, string period, CancellationToken cancellationToken = default);
}


