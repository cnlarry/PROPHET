using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Models;

public class BacktestConfigRequest
{
    public StrategyInfo Strategy { get; set; } = null!;
    public VersionListItem Version { get; set; } = null!;
    public BacktestConfig Config { get; set; } = null!;
}

