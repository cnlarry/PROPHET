using System;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;
using Prophet.Client.Services.AI.Core;
using Prophet.Client.Services.Strategy;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Services;

/// <summary>
/// BacktestSessionViewModel 工厂：统一注入依赖，避免在各处手动 new / ServiceLocator
/// </summary>
public class BacktestSessionFactory
{
    private readonly LocalStrategyService _strategyService;
    private readonly AiServiceManager _aiService;

    public BacktestSessionFactory(LocalStrategyService strategyService, AiServiceManager aiService)
    {
        _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
    }

    public BacktestSessionViewModel Create(
        StrategyInfo strategy,
        VersionListItem version,
        BacktestConfig config,
        int maxLogEntries = 100)
    {
        return new BacktestSessionViewModel(strategy, version, config, _strategyService, _aiService, maxLogEntries);
    }
}


