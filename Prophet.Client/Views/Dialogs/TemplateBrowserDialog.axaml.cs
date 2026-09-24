using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Prophet.Client.Models;

namespace Prophet.Client.Views.Dialogs;

public partial class TemplateBrowserDialog : Window
{
    private List<StrategyTemplate> _allTemplates = new();
    private List<StrategyTemplate> _filteredTemplates = new();
    private StrategyTemplate? _selectedTemplate;

    public TemplateBrowserDialog()
    {
        InitializeComponent();
        _allTemplates = new List<StrategyTemplate>();
    }

    public TemplateBrowserDialog(List<StrategyTemplate> templates)
    {
        InitializeComponent();
        _allTemplates = templates;
        _filteredTemplates = templates.ToList();
        
        // 绑定数据
        var itemsControl = this.FindControl<ItemsControl>("TemplatesItemsControl");
        if (itemsControl != null)
        {
            itemsControl.ItemsSource = _filteredTemplates;
        }

        Console.WriteLine($"✅ 模板浏览对话框已加载 {_allTemplates.Count} 个模板");
    }

    /// <summary>
    /// 搜索文本变化事件
    /// </summary>
    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            FilterTemplates(textBox.Text ?? string.Empty, GetSelectedCategory());
        }
    }

    /// <summary>
    /// 分类选择变化事件
    /// </summary>
    private void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            var searchText = this.FindControl<TextBox>("SearchTextBox")?.Text ?? string.Empty;
            FilterTemplates(searchText, GetSelectedCategory());
        }
    }

    /// <summary>
    /// 获取当前选择的分类
    /// </summary>
    private string GetSelectedCategory()
    {
        var comboBox = this.FindControl<ComboBox>("CategoryComboBox");
        if (comboBox == null || comboBox.SelectedIndex == 0)
            return string.Empty;

        return comboBox.SelectedIndex switch
        {
            1 => "trend",
            2 => "mean_revert",
            3 => "momentum",
            4 => "smc",
            5 => "grid",
            6 => "volatility",
            _ => string.Empty
        };
    }

    /// <summary>
    /// 过滤模板
    /// </summary>
    private void FilterTemplates(string searchText, string category)
    {
        _filteredTemplates = _allTemplates.Where(t =>
        {
            // 分类筛选
            if (!string.IsNullOrEmpty(category) && t.Category != category)
                return false;

            // 搜索筛选
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var keyword = searchText.ToLower();
                return t.Name.ToLower().Contains(keyword) ||
                       t.Description.ToLower().Contains(keyword) ||
                       t.Tags.Any(tag => tag.ToLower().Contains(keyword));
            }

            return true;
        }).ToList();

        // 更新显示
        var itemsControl = this.FindControl<ItemsControl>("TemplatesItemsControl");
        if (itemsControl != null)
        {
            itemsControl.ItemsSource = _filteredTemplates;
        }

        Console.WriteLine($"✅ 过滤后显示 {_filteredTemplates.Count} 个模板");
    }

    /// <summary>
    /// 模板卡片单击事件（选中）
    /// </summary>
    private void OnTemplateCardClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is StrategyTemplate template)
        {
            SelectTemplate(template);
        }
    }

    /// <summary>
    /// 模板卡片双击事件（选中并确认）
    /// </summary>
    private void OnTemplateCardDoubleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is StrategyTemplate template)
        {
            SelectTemplate(template);
            ConfirmSelection();
        }
    }

    /// <summary>
    /// 选择模板
    /// </summary>
    private void SelectTemplate(StrategyTemplate template)
    {
        _selectedTemplate = template;

        // 更新UI状态
        var itemsControl = this.FindControl<ItemsControl>("TemplatesItemsControl");
        if (itemsControl?.ItemsSource != null)
        {
            // 移除所有卡片的选中样式
            foreach (var item in itemsControl.GetRealizedContainers())
            {
                if (item is Border border)
                {
                    border.Classes.Remove("Selected");
                }
            }

            // 添加选中样式到当前卡片（需要通过Visual Tree查找）
            // 简化处理：通过重新应用ItemsSource来触发UI更新
        }

        // 更新选中提示文本
        var selectedText = this.FindControl<TextBlock>("SelectedTemplateText");
        if (selectedText != null)
        {
            selectedText.Text = $"✅ 已选择: {template.Name}";
        }

        // 启用确认按钮
        var confirmButton = this.FindControl<Button>("ConfirmButton");
        if (confirmButton != null)
        {
            confirmButton.IsEnabled = true;
        }

        Console.WriteLine($"✅ 选中模板: {template.Name}");
    }

    /// <summary>
    /// 确认选择
    /// </summary>
    private void ConfirmSelection()
    {
        if (_selectedTemplate != null)
        {
            Console.WriteLine($"✅ 确认使用模板: {_selectedTemplate.Name}");
            Close(_selectedTemplate);
        }
    }

    /// <summary>
    /// 确定按钮点击事件
    /// </summary>
    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("ℹ️ 用户取消了模板选择");
        Close(null);
    }
}

