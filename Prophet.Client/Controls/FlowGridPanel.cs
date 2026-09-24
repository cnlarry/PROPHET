using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Prophet.Client.Controls;

/// <summary>
/// 流式网格布局面板
/// 每行4列，支持子元素占据1-4列，自动换行
/// </summary>
public class FlowGridPanel : Panel
{
    /// <summary>
    /// 定义每个子元素占据的列数（1-4）
    /// </summary>
    public static readonly AttachedProperty<int> ColumnSpanProperty =
        AvaloniaProperty.RegisterAttached<FlowGridPanel, Control, int>("ColumnSpan", defaultValue: 1);

    public static int GetColumnSpan(Control element)
    {
        return element.GetValue(ColumnSpanProperty);
    }

    public static void SetColumnSpan(Control element, int value)
    {
        element.SetValue(ColumnSpanProperty, value);
    }

    /// <summary>
    /// 列间距
    /// </summary>
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<FlowGridPanel, double>(nameof(ColumnSpacing), defaultValue: 10.0);

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// 行间距
    /// </summary>
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<FlowGridPanel, double>(nameof(RowSpacing), defaultValue: 10.0);

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        const int columnsPerRow = 4;
        var columnSpacing = ColumnSpacing;
        var rowSpacing = RowSpacing;
        
        var availableWidth = availableSize.Width;
        var columnWidth = (availableWidth - (columnsPerRow - 1) * columnSpacing) / columnsPerRow;
        
        var currentRow = 0;
        var currentColumn = 0;
        var rowHeights = new List<double>();
        var currentRowHeight = 0.0;

        foreach (Control child in Children)
        {
            if (child == null || !child.IsVisible)
                continue;

            var columnSpan = Math.Clamp(GetColumnSpan(child), 1, columnsPerRow);
            var childWidth = columnSpan * columnWidth + (columnSpan - 1) * columnSpacing;
            
            // 测量子元素
            child.Measure(new Size(childWidth, double.PositiveInfinity));
            var childHeight = child.DesiredSize.Height;

            // 检查是否需要换行
            if (currentColumn + columnSpan > columnsPerRow)
            {
                // 保存当前行的高度
                rowHeights.Add(currentRowHeight);
                currentRow++;
                currentColumn = 0;
                currentRowHeight = 0.0;
            }

            // 更新当前行高度
            currentRowHeight = Math.Max(currentRowHeight, childHeight);

            // 移动到下一列
            currentColumn += columnSpan;
        }

        // 添加最后一行的高度
        if (currentRowHeight > 0 || rowHeights.Count == 0)
        {
            rowHeights.Add(currentRowHeight);
        }

        // 计算总高度
        var totalHeight = 0.0;
        foreach (var height in rowHeights)
        {
            totalHeight += height;
        }
        if (rowHeights.Count > 1)
        {
            totalHeight += (rowHeights.Count - 1) * rowSpacing;
        }

        return new Size(availableWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        const int columnsPerRow = 4;
        var columnSpacing = ColumnSpacing;
        var rowSpacing = RowSpacing;
        
        var availableWidth = finalSize.Width;
        var columnWidth = (availableWidth - (columnsPerRow - 1) * columnSpacing) / columnsPerRow;
        
        var currentRow = 0;
        var currentColumn = 0;
        var rowHeights = new List<double>();
        var rowYPositions = new List<double>();
        var currentRowHeight = 0.0;
        var currentY = 0.0;

        // 第一遍：计算每行高度和Y位置
        foreach (Control child in Children)
        {
            if (child == null || !child.IsVisible)
                continue;

            var columnSpan = Math.Clamp(GetColumnSpan(child), 1, columnsPerRow);
            
            // 检查是否需要换行
            if (currentColumn + columnSpan > columnsPerRow)
            {
                // 保存当前行的高度和Y位置
                rowHeights.Add(currentRowHeight);
                rowYPositions.Add(currentY);
                currentY += currentRowHeight + rowSpacing;
                currentRow++;
                currentColumn = 0;
                currentRowHeight = 0.0;
            }

            // 更新当前行高度
            currentRowHeight = Math.Max(currentRowHeight, child.DesiredSize.Height);
            currentColumn += columnSpan;
        }

        // 添加最后一行
        if (currentRowHeight > 0 || rowHeights.Count == 0)
        {
            rowHeights.Add(currentRowHeight);
            rowYPositions.Add(currentY);
        }

        // 第二遍：排列子元素
        currentRow = 0;
        currentColumn = 0;

        foreach (Control child in Children)
        {
            if (child == null || !child.IsVisible)
                continue;

            var columnSpan = Math.Clamp(GetColumnSpan(child), 1, columnsPerRow);
            
            // 检查是否需要换行
            if (currentColumn + columnSpan > columnsPerRow)
            {
                currentRow++;
                currentColumn = 0;
            }

            // 计算位置和大小
            var x = currentColumn * (columnWidth + columnSpacing);
            var y = rowYPositions[currentRow];
            var width = columnSpan * columnWidth + (columnSpan - 1) * columnSpacing;
            var height = rowHeights[currentRow];

            child.Arrange(new Rect(x, y, width, height));

            // 移动到下一列
            currentColumn += columnSpan;
        }

        return finalSize;
    }
}

