# Prophet.Client - 客户端应用

这是Prophet量化交易平台的桌面客户端应用。

## 技术栈

- **.NET 8** - 运行时框架
- **Avalonia UI 11.0** - 跨平台UI框架
- **ReactiveUI** - MVVM响应式框架
- **Prism** - 模块化MVVM框架

## 项目结构

```
Prophet.Client/
├── Views/              # 视图（XAML界面）
│   └── MainWindow.axaml
├── ViewModels/         # 视图模型（MVVM）
│   └── MainWindowViewModel.cs
├── Services/           # 业务服务层
├── Models/             # 数据模型
├── Styles/             # 样式和主题
│   ├── Colors.axaml    # 颜色定义
│   └── Styles.axaml    # 样式定义
├── Assets/             # 资源文件
│   ├── Icons/          # 图标
│   ├── Images/         # 图片
│   └── Monaco/         # Monaco Editor资源
├── WebViews/           # WebView HTML资源
├── Converters/         # XAML值转换器
├── Behaviors/          # XAML行为
├── App.axaml           # 应用程序定义
├── App.axaml.cs        # 应用程序代码
└── Program.cs          # 程序入口
```

## 快速开始

### 前置要求

- .NET 8 SDK
- Visual Studio 2022 或 Rider

### 运行项目

1. 还原NuGet包：
```bash
dotnet restore Prophet.Client.csproj
```

2. 运行项目：
```bash
dotnet run
```

或者在Visual Studio中按F5运行。

### 开发调试

在Debug模式下运行时，按F12可以打开Avalonia DevTools进行UI调试。

## 当前状态

✅ 项目结构已搭建  
✅ 基础UI框架完成  
✅ 暗色主题实现  
✅ 主窗口静态布局完成  

⏳ 待实现功能：
- 钱包登录
- 策略编辑器集成
- 回测功能
- 实盘交易
- 更多...

## 设计规范

### 颜色系统

项目使用语义化颜色系统，定义在 `Styles/Colors.axaml`：
- Background1Brush - 主背景
- Text1Brush - 主文字
- PrimaryBrush - 主色
- SuccessBrush - 成功/盈利
- DangerBrush - 危险/亏损
- WarningBrush - 警告

### 样式类

定义在 `Styles/Styles.axaml`：
- `.Card` - 卡片容器
- `.Title1/.Title2/.Title3` - 标题文本
- `.Body/.Caption` - 正文文本
- `.Primary/.Success/.Danger` - 按钮样式
- `.MenuItem` - 侧边栏菜单项

### 使用示例

```xml
<!-- 使用卡片样式 -->
<Border Classes="Card">
    <TextBlock Text="标题" Classes="Title1"/>
</Border>

<!-- 使用主色按钮 -->
<Button Classes="Primary" Content="确定"/>
```

## 后续开发计划

参见项目根目录的 `客户端规划蓝图.md` 文档。

