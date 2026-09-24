using System;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Strategy;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Services;

/// <summary>
/// AI操作服务 - 协调AI与应用程序的交互操作
/// </summary>
public class AiOperationService
{
    private IStrategyEditorHost? _strategyHost;
    private BacktestViewModel? _backtestViewModel;
    
    private readonly LocalStrategyService _strategyService;
    private readonly StrategyVersionRepository _versionRepository;

    public AiOperationService(LocalStrategyService strategyService, StrategyVersionRepository versionRepository)
    {
        _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
    }
    
    /// <summary>
    /// 设置策略编辑器宿主引用
    /// </summary>
    public void SetStrategyHost(IStrategyEditorHost strategyHost)
    {
        _strategyHost = strategyHost;
    }
    
    /// <summary>
    /// 设置回测视图模型引用
    /// </summary>
    public void SetBacktestViewModel(BacktestViewModel backtestViewModel)
    {
        _backtestViewModel = backtestViewModel;
    }
    
    /// <summary>
    /// 获取当前策略信息
    /// </summary>
    public (StrategyInfo? strategy, string? dsl) GetCurrentStrategy()
    {
        if (_strategyHost == null)
            return (null, null);
        
        return _strategyHost.GetCurrentStrategyInfo();
    }
    
    /// <summary>
    /// 场景1：创建新Tab并插入代码
    /// </summary>
    public async Task<bool> CreateNewTabWithCodeAsync(string dslCode, string strategyName = "AI生成策略")
    {
        try
        {
            if (_strategyHost == null)
            {
                Console.WriteLine("❌ 策略编辑器未设置");
                return false;
            }
            
            // 创建临时策略
            var tempStrategy = new StrategyInfo
            {
                Id = $"temp_{Guid.NewGuid():N}",
                Name = strategyName,
                Dsl = dslCode,
                Status = "draft",
                Version = "未保存",
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            _strategyHost.OpenEditorTab(tempStrategy);
            Console.WriteLine($"✅ 已创建新Tab: {strategyName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建新Tab失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 场景2：替换当前编辑器代码
    /// </summary>
    public async Task<bool> ReplaceCurrentCodeAsync(string newDslCode)
    {
        try
        {
            if (_strategyHost == null)
            {
                Console.WriteLine("❌ 策略编辑器未设置");
                return false;
            }
            
            _strategyHost.ReplaceCurrentCode(newDslCode);
            Console.WriteLine("✅ 已替换当前编辑器代码");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 替换代码失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 场景3：保存新版本并启动回测
    /// </summary>
    public async Task<(bool success, string? versionString, string? message)> SaveVersionAndBacktestAsync(
        string strategyId, 
        string dslCode, 
        string changeDescription = "AI优化建议",
        BacktestConfig? backtestConfigOverride = null)
    {
        try
        {
            // 1. 保存新版本
            var versionId = await _versionRepository.CreatePatchVersionAsync(
                strategyId, 
                dslCode, 
                changeDescription, 
                "ai_assistant");
            
            if (versionId == 0)
            {
                return (false, null, "创建新版本失败：DSL代码未变化或策略不存在");
            }
            
            // 2. 获取最新版本（刚创建的版本）
            var latestVersion = await _versionRepository.GetLatestVersionAsync(strategyId);
            if (latestVersion == null)
            {
                return (false, null, "无法获取新版本信息");
            }
            
            var versionString = latestVersion.VersionString;
            
            // 3. 获取策略信息
            var strategy = await _strategyService.GetStrategyByIdAsync(strategyId);
            if (strategy == null)
            {
                return (false, versionString, "无法获取策略信息");
            }
            
            // 4. 创建版本列表项（用于回测）
            var versionListItem = new VersionListItem
            {
                VersionString = versionString,
                ChangeType = latestVersion.ChangeType ?? "patch",
                Status = latestVersion.Status ?? "draft",
                ChangeDescription = changeDescription,
                CreatedAt = latestVersion.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 5. 启动回测（如果BacktestViewModel已设置）
            if (_backtestViewModel != null)
            {
                var config = backtestConfigOverride ?? CreateDefaultBacktestConfig(strategy);
                var request = new BacktestConfigRequest
                {
                    Strategy = strategy,
                    Version = versionListItem,
                    Config = config
                };
                
                await _backtestViewModel.StartBacktestAsync(request);
                
                return (true, versionString, $"已保存为新版本 {versionString}，回测任务已启动");
            }
            
            return (true, versionString, $"已保存为新版本 {versionString}，请手动启动回测");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 保存版本并回测失败: {ex.Message}");
            return (false, null, $"操作失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 创建默认回测配置
    /// </summary>
    private BacktestConfig CreateDefaultBacktestConfig(StrategyInfo strategy)
    {
        return new BacktestConfig
        {
            // 策略不绑定标的；这里先用默认值，后续会迁移为 SymbolKey（InstrumentKey）并由 UI 选择
            Symbol = "BTCUSDT",
            StartDate = DateTime.UtcNow.AddMonths(-1), // 默认回测1个月
            EndDate = DateTime.UtcNow,
            InitialCapital = 10000,
            TakerFeeRate = 0.001m,
            SlippageRate = 0.0005m
        };
    }
}
