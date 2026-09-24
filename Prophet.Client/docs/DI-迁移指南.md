# 依赖注入（DI）迁移完成报告

## 概述

Prophet.Client 已完成从单例模式到依赖注入模式的全面迁移。本文档记录了迁移的结果和新的使用方式。

## 迁移状态

### ✅ 已完成（2025-01-03）
- ✅ 移除所有服务类的单例模式代码（`.Instance` 属性）
- ✅ 更新 DI 容器配置，使用构造函数创建服务实例
- ✅ 迁移所有使用单例的代码到 DI 模式
- ✅ 验证编译无错误
- ✅ 删除向后兼容的示例代码

## 已迁移的服务列表

### Singleton（单例）
- `DSLRulesService` - DSL 规则服务
- `IntelliSenseService` - 智能提示服务
- `DSLValidator` - DSL 验证器
- `CompletionUsageTracker` - 补全使用跟踪器
- `AppSettingsService` - 应用设置服务
- `TemplateService` - 模板服务
- `AiServiceManager` - AI 服务管理器

### Scoped（作用域）
- `MarketDataRepository` - 市场数据仓储
- `IBinanceExchangeGateway` - Binance 交易所网关
- `MarketDataCacheService` - 市场数据缓存服务
- `KlineCache` - K线缓存

## 使用方式

### 获取服务

```csharp
using Prophet.Client.Core;

// 获取单例服务
var validator = ServiceContainer.GetService<DSLValidator>();
var appSettings = ServiceContainer.GetService<AppSettingsService>();

// 获取可选服务
var optional = ServiceContainer.GetOptionalService<SomeService>();

// 创建作用域
using var scope = ServiceContainer.CreateScope();
var scopedService = scope.ServiceProvider.GetService<MarketDataCacheService>();
```

### 构造函数注入（推荐）

```csharp
public class MyService
{
    private readonly DSLValidator _validator;
    private readonly AppSettingsService _settings;
    
    public MyService(DSLValidator validator, AppSettingsService settings)
    {
        _validator = validator;
        _settings = settings;
    }
}
```

### 服务定位器模式（简单场景）

```csharp
public class MyView : UserControl
{
    public MyView()
    {
        InitializeComponent();
        
        var validator = ServiceContainer.GetService<DSLValidator>();
        // 使用服务...
    }
}
```

## 服务生命周期

### Singleton（单例）
- 应用程序生命周期内只有一个实例
- 适用于：配置服务、规则服务、全局管理器

### Scoped（作用域）
- 每个作用域（页面/窗口）内只有一个实例
- 适用于：数据访问、缓存服务

### Transient（瞬时）
- 每次请求都创建新实例
- 适用于：轻量级服务、工具类

## 添加新服务

在 `ServiceContainer.ConfigureServices()` 中注册：

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // 添加单例服务
    services.AddSingleton<IMyService, MyService>();
    
    // 添加作用域服务
    services.AddScoped<IScopedService, ScopedService>();
    
    // 添加瞬时服务
    services.AddTransient<ITransientService, TransientService>();
}
```

## 最佳实践

### ✅ 推荐
1. **构造函数注入优先** - 明确依赖关系
2. **依赖接口而非实现** - 提高可测试性
3. **避免循环依赖** - 保持依赖关系清晰
4. **服务定位器作为备选** - 用于简单场景

### ❌ 避免
1. 在构造函数中调用服务的复杂逻辑
2. 存储 `IServiceProvider` 引用（使用 `ServiceContainer`）
3. 过度使用服务定位器模式

## FAQ

### Q: 如何在 ViewModel 中使用 DI？
A: 在构造函数中通过 `ServiceContainer.GetService<T>()` 获取依赖。

### Q: DI 会影响性能吗？
A: 几乎没有。单例服务只创建一次，后续获取非常快。

### Q: 如何处理 IDisposable 服务？
A: DI 容器会自动管理生命周期，Dispose 时自动释放服务。

### Q: 可以在任何地方使用 ServiceContainer 吗？
A: 可以。`ServiceContainer` 是静态的，全局可访问。

## 迁移历史

- **2025-01-03**: 完成全面迁移，移除所有单例模式代码
- **2024-11-10**: 引入 DI 容器，采用渐进式迁移策略

## 总结

✅ **迁移已完成**  
✅ **所有服务已使用 DI 管理**  
✅ **代码更清晰、更易测试**  
✅ **无向后兼容负担**

**原则：使用 DI 容器管理所有服务，构造函数注入优先。**
