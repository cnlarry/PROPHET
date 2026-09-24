using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using Prophet.Client.Services.Editor.Validation;

namespace Prophet.Client.Views.Dialogs;

public partial class DiagnosticsPanelDialog : Window
{
    private readonly TextEditor _editor;
    private readonly List<EditorDiagnosticItem> _items;
    private EditorDiagnosticItem? _selected;

    public DiagnosticsPanelDialog()
    {
        _editor = null!;
        _items = new List<EditorDiagnosticItem>();
        InitializeComponent();
    }

    public DiagnosticsPanelDialog(string strategyName, List<EditorDiagnosticItem> items, TextEditor editor)
    {
        _editor = editor;
        _items = items ?? new List<EditorDiagnosticItem>();

        InitializeComponent();

        var titleText = this.FindControl<TextBlock>("TitleText");
        var summaryText = this.FindControl<TextBlock>("SummaryText");

        if (titleText != null)
        {
            titleText.Text = $"诊断面板 - {strategyName}";
        }

        if (summaryText != null)
        {
            summaryText.Text = _items.Count > 0
                ? $"共 {_items.Count} 项，双击条目可跳转到对应位置"
                : "双击条目可跳转到对应位置";
        }

        SetupList();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupList()
    {
        var list = this.FindControl<ListBox>("DiagnosticsList");
        var emptyText = this.FindControl<TextBlock>("EmptyText");

        if (list == null || emptyText == null)
            return;

        if (_items.Count == 0)
        {
            list.IsVisible = false;
            emptyText.IsVisible = true;
            UpdateFooter(null);
            return;
        }

        list.IsVisible = true;
        emptyText.IsVisible = false;

        list.ItemTemplate = new FuncDataTemplate<EditorDiagnosticItem>((item, _) =>
        {
            var icon = new TextBlock
            {
                Text = GetKindIcon(item.Kind),
                FontSize = 12,
                Foreground = GetKindBrush(item.Kind),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var location = new TextBlock
            {
                Text = $"L{item.Line}:C{item.Column}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#9CDCFE")),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var message = new TextBlock
            {
                Text = item.Message,
                FontSize = 12,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
                ColumnSpacing = 10,
                Margin = new Thickness(10, 6)
            };

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(location, 1);
            Grid.SetColumn(message, 2);

            grid.Children.Add(icon);
            grid.Children.Add(location);
            grid.Children.Add(message);

            return grid;
        });

        list.ItemsSource = _items;
        list.SelectedIndex = 0;
    }

    private static string GetKindIcon(EditorDiagnosticKind kind) =>
        kind switch
        {
            EditorDiagnosticKind.Error => "❌",
            EditorDiagnosticKind.Warning => "⚠️",
            EditorDiagnosticKind.UndefinedSymbol => "❓",
            _ => "ℹ️"
        };

    private static IBrush GetKindBrush(EditorDiagnosticKind kind) =>
        kind switch
        {
            EditorDiagnosticKind.Error => new SolidColorBrush(Color.Parse("#FF5555")),
            EditorDiagnosticKind.Warning => new SolidColorBrush(Color.Parse("#FFA500")),
            EditorDiagnosticKind.UndefinedSymbol => new SolidColorBrush(Color.Parse("#808080")),
            _ => new SolidColorBrush(Color.Parse("#4FC1FF"))
        };

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        _selected = listBox.SelectedItem as EditorDiagnosticItem;
        UpdateFooter(_selected);
    }

    private void OnItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        JumpToSelected();
    }

    private void OnJumpClicked(object? sender, RoutedEventArgs e)
    {
        JumpToSelected();
    }

    private void JumpToSelected()
    {
        if (_selected == null)
            return;

        try
        {
            var startOffset = Math.Clamp(_selected.StartOffset, 0, Math.Max(0, _editor.Document.TextLength - 1));
            _editor.CaretOffset = startOffset;

            // 滚动到可见行
            _editor.ScrollToLine(Math.Max(1, _selected.Line));

            // 可选：选中问题范围
            var length = Math.Max(1, _selected.EndOffset - _selected.StartOffset);
            var selectionEnd = Math.Min(_editor.Document.TextLength, startOffset + length);
            _editor.SelectionStart = startOffset;
            _editor.SelectionLength = Math.Max(0, selectionEnd - startOffset);

            _editor.TextArea.Focus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"跳转失败: {ex.Message}");
        }
    }

    private void UpdateFooter(EditorDiagnosticItem? selected)
    {
        var hint = this.FindControl<TextBlock>("HintText");
        var jumpButton = this.FindControl<Button>("JumpButton");

        if (hint == null || jumpButton == null)
            return;

        if (selected == null)
        {
            hint.Text = _items.Count > 0 ? "请选择一个条目以跳转" : "";
            jumpButton.IsEnabled = false;
            return;
        }

        hint.Text = $"{GetKindText(selected.Kind)} | L{selected.Line}:C{selected.Column}";
        jumpButton.IsEnabled = true;
    }

    private static string GetKindText(EditorDiagnosticKind kind) =>
        kind switch
        {
            EditorDiagnosticKind.Error => "错误",
            EditorDiagnosticKind.Warning => "警告",
            EditorDiagnosticKind.UndefinedSymbol => "未定义符号",
            _ => "提示"
        };

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}


