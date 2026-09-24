using System;
using System.Threading.Tasks;
using Prophet.Client.Core;
using Prophet.Client.Services.Editor;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services;

/// <summary>
/// 服务预加载器 - 异步预加载关键服务，提升启动性能
/// </summary>
public static class ServicePreloader
{
    private static bool _isPreloaded = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// 预加载状态
    /// </summary>
    public static bool IsPreloaded
    {
        get
        {
            lock (_lock)
            {
                return _isPreloaded;
            }
        }
    }

    /// <summary>
    /// 异步预加载所有关键服务
    /// </summary>
    /// <param name="progress">进度回调（可选），接收 0-100 的进度值和当前任务描述</param>
    /// <returns>预加载任务</returns>
    public static async Task PreloadServicesAsync(IProgress<(int progress, string task)>? progress = null)
    {
        lock (_lock)
        {
            if (_isPreloaded)
            {
                Logger.Info("服务已预加载，跳过");
                return;
            }
        }

        try
        {
            Logger.Info("开始预加载关键服务...");
            var startTime = DateTime.Now;

            // 1. DSLRulesService (最底层，其他服务依赖它)
            progress?.Report((10, "加载 DSL 规则..."));
            await Task.Run(() =>
            {
                var rules = ServiceContainer.GetService<DSLRulesService>();
                if (rules.IsReady)
                {
                    Logger.Info("DSL 规则加载成功");
                }
                else
                {
                    Logger.Warn($"DSL 规则加载失败: {rules.LoadError}");
                }
            });

            // 2. IntelliSenseService (依赖 DSLRulesService)
            progress?.Report((40, "初始化智能提示..."));
            await Task.Run(() =>
            {
                var intelliSense = ServiceContainer.GetService<IntelliSenseService>();
                Logger.Info("智能提示服务初始化完成");
            });

            // 3. DSLValidator (依赖 IntelliSenseService 和 DSLRulesService)
            progress?.Report((70, "初始化语法验证器..."));
            await Task.Run(() =>
            {
                var validator = ServiceContainer.GetService<DSLValidator>();
                Logger.Info("语法验证器初始化完成");
            });

            // 4. CompletionUsageTracker (独立服务)
            progress?.Report((90, "加载补全使用统计..."));
            await Task.Run(() =>
            {
                var tracker = ServiceContainer.GetService<CompletionUsageTracker>();
                Logger.Info("补全使用统计加载完成");
            });

            progress?.Report((100, "预加载完成"));

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            Logger.Info($"服务预加载完成，耗时: {elapsed:F0}ms");

            lock (_lock)
            {
                _isPreloaded = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("服务预加载失败", ex);
            // 即使失败也标记为已尝试，避免重复加载
            lock (_lock)
            {
                _isPreloaded = true;
            }
            throw;
        }
    }

    /// <summary>
    /// 重置预加载状态（用于测试或重新初始化）
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _isPreloaded = false;
        }
        Logger.Info("服务预加载状态已重置");
    }
}

