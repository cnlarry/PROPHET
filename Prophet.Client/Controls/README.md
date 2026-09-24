# ModernDialog 对话框库

现代化的 Avalonia 对话框类库，提供统一的对话框样式和简洁的 API。

## 🎯 核心类

### 1. ModernDialog
基础对话框类，提供核心功能：
- 无边框窗口样式
- 自定义标题栏（可拖动）
- 内容区域管理
- 按钮管理
- 半透明遮罩

### 2. ModernDialogBuilder  
流畅构建器，提供链式 API 构建对话框。

### 3. ModernDialogPresets
预设对话框集合，提供常用对话框类型。

## 📖 使用指南

### 方式一：使用预设（推荐，覆盖90%场景）

```csharp
// 确认对话框
var dialog = ModernDialogPresets.Confirm(
    "删除确认",
    "确定要删除吗？",
    confirmed => {
        if (confirmed) {
            // 执行删除
        }
    }
);
await dialog.ShowDialog(window);

// 警告对话框
var dialog = ModernDialogPresets.Warning("警告", "操作失败！");
await dialog.ShowDialog(window);

// 错误对话框
var dialog = ModernDialogPresets.Error("错误", "发生了错误");
await dialog.ShowDialog(window);

// 信息对话框
var dialog = ModernDialogPresets.Info("提示", "操作成功！");
await dialog.ShowDialog(window);

// 输入对话框
var dialog = ModernDialogPresets.Input(
    "重命名",
    "输入新名称",
    "默认值",
    newValue => {
        // 处理输入
    }
);
await dialog.ShowDialog(window);

// 策略表单
var dialog = ModernDialogPresets.StrategyForm(
    (name, symbol, remarks) => {
        // 创建策略
    }
);
await dialog.ShowDialog(window);

// 编辑策略信息
var dialog = ModernDialogPresets.EditStrategyInfo(
    "策略名",
    "BTCUSDT",
    "备注",
    (symbol, remarks) => {
        // 更新策略
    }
);
await dialog.ShowDialog(window);

// 自定义内容对话框
var customControl = new MyControl();
var window = ModernDialogPresets.CustomContent(customControl, 600, 500);
await window.ShowDialog(parentWindow);
```

### 方式二：使用构建器（灵活自定义）

```csharp
TextBox? input = null;
ComboBox? combo = null;

var dialog = ModernDialogBuilder.Create()
    .WithTitle("创建项目")
    .WithSize(500, 400)
    .AddLabel("项目名称：")
    .AddTextBox(out input, "输入名称...")
    .AddLabel("交易对：")
    .AddComboBox(out combo, new[] { "BTC", "ETH" }, selectedIndex: 0)
    .AddTip("💡 提示：名称不能为空")
    .AddCancelButton()
    .AddConfirmButton("创建", () => {
        var name = input?.Text;
        var symbol = combo?.SelectedItem?.ToString();
        // 处理创建
    })
    .Build();

await dialog.ShowDialog(window);
```

### 方式三：手动构建（完全控制）

```csharp
var dialog = new ModernDialog
{
    Title = "自定义对话框",
    Width = 400,
    Height = 250
};

// 添加内容
dialog.AddContent(new TextBlock { Text = "消息内容" });

// 添加按钮
dialog.AddButton("选项1", () => {
    // 处理选项1
    dialog.Close(true);
});

dialog.AddButton("选项2", () => {
    // 处理选项2
    dialog.Close(true);
});

dialog.AddButton("取消", () => dialog.Close(false), isPrimary: false);

await dialog.ShowDialog(window);
```

## 🎨 构建器 API

### 窗口配置
- `WithTitle(string)` - 设置标题
- `WithSize(double, double)` - 设置尺寸
- `WithWidth(double)` - 设置宽度
- `WithHeight(double)` - 设置高度
- `WithOwner(Window)` - 设置拥有者窗口
- `WithContentSpacing(double)` - 设置内容间距
- `WithButtonSpacing(double)` - 设置按钮间距

### 添加内容
- `AddText(string, fontSize, wrap)` - 添加文本
- `AddLabel(string, fontSize, margin)` - 添加标签
- `AddTextBox(out TextBox, placeholder, defaultValue)` - 添加文本框
- `AddMultilineTextBox(out TextBox, placeholder, defaultValue, height)` - 添加多行文本框
- `AddComboBox(out ComboBox, items, selectedIndex)` - 添加下拉框
- `AddTip(string)` - 添加提示框
- `AddControl(Control)` - 添加自定义控件

### 添加按钮
- `AddCancelButton(text, onClick)` - 添加取消按钮
- `AddConfirmButton(text, onClick, closeAfterClick)` - 添加确认按钮
- `AddButton(text, onClick, isPrimary)` - 添加自定义按钮

### 构建
- `Build()` - 构建对话框
- `Show()` - 构建并显示
- `ShowDialog()` - 构建并以模态方式显示

## 📦 预设对话框

- `Confirm(title, message, callback)` - 确认对话框
- `Warning(title, message, onClose)` - 警告对话框
- `Error(title, message, onClose)` - 错误对话框
- `Info(title, message, onClose)` - 信息对话框
- `Input(title, label, defaultValue, callback)` - 输入对话框
- `StrategyForm(callback)` - 策略表单对话框
- `EditStrategyInfo(name, symbol, remarks, callback)` - 编辑策略信息对话框
- `CustomContent(control, width, height)` - 自定义内容对话框

## 💡 最佳实践

1. **优先使用预设**：大多数场景使用 `ModernDialogPresets` 即可
2. **需要自定义时使用构建器**：需要特殊布局时使用 `ModernDialogBuilder`
3. **完全控制时手动构建**：需要动态逻辑时直接使用 `new ModernDialog()`
4. **处理回调**：使用回调函数处理用户操作，避免返回值判断
5. **异步调用**：始终使用 `await` 调用 `ShowDialog()`

## 🎯 设计原则

- **关注点分离**：基础类、构建器、预设各司其职
- **易于使用**：一行代码创建标准对话框
- **易于扩展**：添加新对话框类型只需在 Presets 中添加方法
- **统一样式**：所有对话框使用相同的视觉风格
- **类型安全**：使用 out 参数获取控件引用

