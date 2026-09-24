# Prophet.Docs

> Prophet 量化交易框架官方文档中心

欢迎来到 Prophet.Docs - Prophet 量化交易框架的官方文档中心。在这里您可以找到关于 Prophet DSL 语法规范、使用指南、API 参考等详细信息。

## 关于 Prophet

Prophet 是一个先进的量化交易框架，专为数字货币和传统金融市场设计。它提供了强大的策略开发、回测和实盘交易功能。

## 文档内容

- Prophet DSL 语法规范
- 策略开发指南
- 回测引擎使用说明
- 实盘交易配置
- API 参考文档
- 最佳实践和案例研究

## 快速开始

要开始使用 Prophet，请参阅我们的[安装指南](#)和[快速入门教程](#)。

## 本地开发

### 安装依赖

```bash
npm install -g docsify-cli
```

### 创建符号链接（首次使用）

由于文档文件位于项目根目录的 `doc/` 文件夹中，而 Docsify 从 `Prophet.Docs/` 目录启动，需要创建一个符号链接来访问文档。

**方法1：使用 PowerShell 脚本（推荐）**

以管理员权限运行 PowerShell，然后执行：

```powershell
cd Prophet.Docs
.\create-symlink.ps1
```

**方法2：手动创建符号链接**

以管理员权限运行命令提示符（CMD），然后执行：

```cmd
cd Prophet.Docs
mklink /D doc ..\doc
```

### 启动本地服务器

```bash
cd Prophet.Docs
docsify serve .
```

服务器将在 `http://localhost:3000` 上运行。

## 部署

将来此文档系统将部署到 `docs.prophet.com`。

## 目录结构

```
Prophet.Docs/
├── index.html         # 主页面
├── docsify.config.js  # Docsify 配置
├── README.md          # 本文件
├── _navbar.md         # 导航栏
├── _sidebar.md        # 侧边栏
└── .nojekyll          # GitHub Pages 配置
```

文档内容主要来自项目中的以下目录：
- `/doc/` - 核心文档
- `/Prophet.Client/docs/` - 客户端文档

## 贡献

我们欢迎社区贡献！如果您发现文档中的错误或有改进建议，请提交 Issue 或 Pull Request。