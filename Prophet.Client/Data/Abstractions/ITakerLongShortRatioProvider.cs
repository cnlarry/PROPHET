using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Data.Models;

namespace Prophet.Client.Data.Abstractions;

public interface ITakerLongShortRatioProvider
{
    Task<TakerLongShortRatio?> FetchTakerLongShortRatioAsync(string symbol, string period, CancellationToken cancellationToken = default);
}


