using System;
using Microsoft.Extensions.DependencyInjection;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Services;
using Prophet.Client.Services.AI.Core;
using Prophet.Client.Services.Cache;
using Prophet.Client.Services.Data;
using Prophet.Client.Services.Editor;
using Prophet.Client.Services.Market;
using Prophet.Client.Services.Network;
using Prophet.Client.Services.Rules;
using Prophet.Client.Services.Settings;
using Prophet.Client.Services.Strategy;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Core;

/// <summary>
/// 服务容器配置 - 管理依赖注入
/// </summary>
public static class ServiceContainer
{
    private static IServiceProvider? _serviceProvider;
    private static readonly object _lock = new object();

    /// <summary>
    /// 获取服务提供者
    /// </summary>
    public static IServiceProvider Services
    {
        get
        {
            if (_serviceProvider == null)
            {
                lock (_lock)
                {
                    if (_serviceProvider == null)
                    {
                        _serviceProvider = BuildServiceProvider();
                    }
                }
            }
            return _serviceProvider;
        }
    }

    /// <summary>
    /// 构建服务提供者
    /// </summary>
    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // 注册核心服务
        ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        
        Logger.Info("DI 容器初始化完成");
        
        return provider;
    }

    /// <summary>
    /// 配置服务注册
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // ========== 单例服务（Singleton） ==========
        // 这些服务在应用程序生命周期内只有一个实例

        // 规则和配置服务（按依赖顺序注册）
        services.AddSingleton<DSLRulesService>();
        services.AddSingleton<IntelliSenseService>();
        services.AddSingleton<DSLValidator>();
        services.AddSingleton<CompletionUsageTracker>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<TemplateService>();
        
        // 网络代理管理服务（单例）
        services.AddSingleton<ProxyManagementService>();

        // AI 服务管理器
        services.AddSingleton<AiServiceManager>();

        // 本地策略服务（回测/版本/AI分析等使用）
        services.AddSingleton<LocalStrategyService>();

        // Repository（供服务层复用，避免内部 new）
        services.AddSingleton<StrategyVersionRepository>();
        services.AddSingleton<StrategyRepository>();
        services.AddSingleton<MarketDataRepository>();
        services.AddSingleton<KlineCache>();

        // AI 操作服务（由 DI 管理，避免静态单例）
        services.AddSingleton<AiOperationService>();

        // 回测 session 工厂
        services.AddSingleton<BacktestSessionFactory>();

        // ========== ViewModels（DI落地：统一由容器创建） ==========
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<BacktestViewModel>();
        services.AddSingleton<LiveTradingViewModel>();
        services.AddSingleton<StrategyListViewModel>();

        // ========== 作用域服务（Scoped） ==========
        // 这些服务在每个作用域内只有一个实例（例如每个页面/窗口）

        // 市场数据服务
        services.AddScoped<IBinanceExchangeGateway, BinanceExchangeGateway>();
        services.AddScoped<MarketDataCacheService>();

        // ========== 瞬时服务（Transient） ==========
        // 每次请求都创建新实例

        // 编辑器服务（每个编辑器实例独立）
        // 注意：CodeEditorHelper 需要 TextEditor 参数，不能通过 DI 创建
        // 保留工厂模式创建

        Logger.Info("服务注册完成：单例、作用域、瞬时服务");
    }

    /// <summary>
    /// 获取服务（泛型）
    /// </summary>
    public static T GetService<T>() where T : notnull
    {
        var service = Services.GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"服务未注册: {typeof(T).Name}");
        }
        return service;
    }

    /// <summary>
    /// 获取可选服务（可能返回 null）
    /// </summary>
    public static T? GetOptionalService<T>() where T : class
    {
        return Services.GetService<T>();
    }

    /// <summary>
    /// 创建作用域
    /// </summary>
    /// <remarks>
    /// 用于创建服务作用域，例如为每个窗口/页面创建独立的服务实例
    /// </remarks>
    public static IServiceScope CreateScope()
    {
        return Services.CreateScope();
    }

    /// <summary>
    /// 重置服务容器（用于测试）
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _serviceProvider = null;
            Logger.Info("DI 容器已重置");
        }
    }
}

