using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Prophet.Client.Core;
using Prophet.Client.Data;
using Prophet.Client.Data.Services;
using Prophet.Client.Database;
using Prophet.Client.Database.Migrations;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Services;
using Prophet.Client.Services.Data;
using Prophet.Client.Services.Data.Collectors;
using Prophet.Client.Services.Market;
using Prophet.Client.Services.Settings;
using Prophet.Client.ViewModels;
using Prophet.Client.Views;

namespace Prophet.Client;

/// <summary>
/// Prophet 离线版应用程序
/// v10.0: 数据按需准备，增量更新模式
/// </summary>
public partial class App : Application
{
    public static MarketDataContext MarketDataContext { get; private set; } = null!;
    public static DataPlaneService DataPlane { get; private set; } = null!;
    public static DataQueryService DataQuery { get; private set; } = null!;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // 初始化日志系统
        InitializeLogging();
        
        // 初始化 DI 容器（优先）
        try
        {
            _ = Core.ServiceContainer.Services; // 触发 DI 容器初始化
            Logger.Info("DI 容器已就绪");
        }
        catch (Exception ex)
        {
            Logger.Error("DI 容器初始化失败", ex);
        }
        
        // 初始化市场数据上下文
        MarketDataContext = new MarketDataContext();

        // 初始化 Data Plane（多交易所统一入口）
        var appSettings = ServiceContainer.GetService<AppSettingsService>();
        DataPlane = DataPlaneFactory.CreateFromSettings(appSettings.Settings);

        // 初始化 Data Query（统一读入口：主页/回测/实盘都应通过它读取数据）
        DataQuery = new DataQueryService(MarketDataContext.Repository, MarketDataContext.Gateway);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 离线模式：初始化数据库并直接启动主窗口
            InitializeDatabaseAndStartApp(desktop);
            desktop.Exit += (_, _) =>
            {
                // 关闭日志系统
                Logger.Shutdown();
                
                // 释放资源
                MarketDataContext?.Dispose();
                DataPlane?.Dispose();
                DataQuery?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 初始化数据库并启动应用
    /// </summary>
    private async void InitializeDatabaseAndStartApp(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            // 异步预加载关键服务（不阻塞UI）
            var preloadTask = ServicePreloader.PreloadServicesAsync(
                new Progress<(int progress, string task)>((report) =>
                {
                    // 在控制台输出进度（将来可以用启动画面显示）
                    Logger.Info($"⏳ [{report.progress}%] {report.task}");
                })
            );

            // 验证数据库连接（不会创建或修改数据库）
            await DBHelper.InitializeDatabaseAsync();

            // ✅ 应用客户端数据库迁移（幂等）
            await DBHelper.ApplyMigrationsAsync(ClientMigrations.All);
            
            // ✅ 更新数据库版本号
            await DatabaseVersionManager.UpdateToCurrentVersionAsync();
            Logger.Info($"数据库 Schema 版本: {DatabaseVersionManager.CurrentSchemaVersion}");

            // ✅ 验证关键表是否存在
            var validationResult = await DatabaseValidator.ValidateCriticalTablesAsync();
            if (!validationResult.IsValid)
            {
                Logger.Error($"数据库验证失败: {validationResult.ErrorMessage}");
                throw new InvalidOperationException($"数据库结构不完整: {validationResult.ErrorMessage}");
            }
            Logger.Info(validationResult.Message!);
            
            // 📊 可选：打印完整状态报告（仅在 DEBUG 模式）
            #if DEBUG
            var statusReport = await DatabaseValidator.GenerateStatusReportAsync();
            Console.WriteLine(statusReport);
            var versionReport = await DatabaseVersionManager.GetVersionReportAsync();
            Console.WriteLine(versionReport);
            #endif

            // ✅ 确保 instruments 表有默认种子数据（幂等）
            var instrumentRepository = new InstrumentRepository();
            await instrumentRepository.EnsureDefaultSeedAsync();
            var enabledCount = await instrumentRepository.GetEnabledCountAsync();
            Logger.Info($"instruments 表已就绪，已启用: {enabledCount}");
            
            // 等待服务预加载完成
            await preloadTask;
            
            // 直接启动主窗口（无需登录）
            desktop.MainWindow = new MainWindow
            {
                DataContext = ServiceContainer.GetService<MainWindowViewModel>(),
            };

            desktop.MainWindow.Show();
            
            Logger.Info("应用启动完成");
        }
        catch (Exception ex)
        {
            Logger.Fatal("应用初始化失败", ex);
            Console.WriteLine("==============================================");
            Console.WriteLine("❌ 应用初始化失败");
            Console.WriteLine("==============================================");
            Console.WriteLine($"错误类型: {ex.GetType().Name}");
            Console.WriteLine($"错误信息: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            Console.WriteLine("==============================================");
            Console.WriteLine();
            Console.WriteLine("💡 请检查：");
            Console.WriteLine("   1. PROPHET.db 文件是否存在于应用程序目录");
            Console.WriteLine("   2. PROPHET.db 文件是否可读写");
            Console.WriteLine("   3. 数据库文件是否损坏");
            Console.WriteLine("==============================================");

            // 仍然尝试启动主窗口，让用户看到错误
            desktop.MainWindow = new MainWindow
            {
                DataContext = ServiceContainer.GetService<MainWindowViewModel>(),
            };

            desktop.MainWindow.Show();
        }
    }
    
    private void InitializeLogging()
    {
        try
        {
#if DEBUG
            // Debug模式：关闭控制台输出，避免日志刷屏，只输出到文件
            LogService.Initialize(
                level: LogService.LogLevel.DEBUG,
                enableConsole: false,  // 关闭控制台输出
                enableFile: true
            );
#else
            // Release模式：关闭控制台输出，只记录到文件
            LogService.Initialize(
                level: LogService.LogLevel.INFO,
                enableConsole: false,  // 关闭控制台输出
                enableFile: true
            );
#endif
            
            LogService.Info("App", "Prophet 量化交易平台启动");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 日志系统初始化失败: {ex.Message}");
        }
    }
}

