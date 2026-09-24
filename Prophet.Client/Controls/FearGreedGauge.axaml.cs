using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Prophet.Client.Controls;

/// <summary>
/// 恐惧与贪婪指数仪表盘控件
/// 显示0-100的仪表盘，带指针指向具体值
/// </summary>
public partial class FearGreedGauge : UserControl
{
    // 依赖属性：当前值
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<FearGreedGauge, int>(nameof(Value), 0);

    // 依赖属性：分类文本
    public static readonly StyledProperty<string> ClassificationProperty =
        AvaloniaProperty.Register<FearGreedGauge, string>(nameof(Classification), "--");

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Classification
    {
        get => GetValue(ClassificationProperty);
        set => SetValue(ClassificationProperty, value);
    }

    // 仪表盘参数
    private const double CenterX = 140;
    private const double CenterY = 120; // 向上移动中心点，为下方文字留空间
    private const double Radius = 100;
    private const double StartAngle = 210; // 起始角度（度）- 比半圆更大
    private const double EndAngle = -30;   // 结束角度（度）
    private const double AngleRange = 240; // 角度范围（240度，比半圆大）

    private readonly List<Line> _scaleMarkLines = new();
    private readonly List<TextBlock> _scaleLabelTexts = new();

    public FearGreedGauge()
    {
        InitializeComponent();
        
        // 订阅属性变化
        this.PropertyChanged += OnPropertyChanged;
        
        // 等待控件加载完成后再初始化
        this.Loaded += (s, e) =>
        {
            InitializeScale();
            InitializeBackgroundArc();
            UpdateGauge();
            UpdateClassification();
        };
    }

    /// <summary>
    /// 属性变化事件处理
    /// </summary>
    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ValueProperty)
        {
            UpdateGauge();
        }
        else if (e.Property == ClassificationProperty)
        {
            UpdateClassification();
        }
    }

    /// <summary>
    /// 初始化背景弧线
    /// </summary>
    private void InitializeBackgroundArc()
    {
        if (BackgroundArc == null) return;
        
        var pathData = CreateArcPath(CenterX, CenterY, Radius, StartAngle, EndAngle);
        BackgroundArc.Data = pathData;
    }

    /// <summary>
    /// 初始化刻度
    /// </summary>
    private void InitializeScale()
    {
        if (GaugeCanvas == null) return;

        // 清除旧的刻度线和标签
        foreach (var line in _scaleMarkLines)
        {
            GaugeCanvas.Children.Remove(line);
        }
        foreach (var text in _scaleLabelTexts)
        {
            GaugeCanvas.Children.Remove(text);
        }
        _scaleMarkLines.Clear();
        _scaleLabelTexts.Clear();

        // 创建刻度线（每10一个主刻度，每5一个次刻度）
        for (int i = 0; i <= 100; i += 5)
        {
            var angle = StartAngle - (i / 100.0 * AngleRange);
            var radian = angle * Math.PI / 180.0;
            
            var x1 = CenterX + (Radius - 10) * Math.Cos(radian);
            var y1 = CenterY - (Radius - 10) * Math.Sin(radian);
            var x2 = CenterX + (Radius - (i % 10 == 0 ? 0 : 5)) * Math.Cos(radian);
            var y2 = CenterY - (Radius - (i % 10 == 0 ? 0 : 5)) * Math.Sin(radian);

            var line = new Line
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                Stroke = new SolidColorBrush(Color.Parse("#848E9C")), // 使用默认文本颜色
                StrokeThickness = 2
            };
            
            GaugeCanvas.Children.Add(line);
            _scaleMarkLines.Add(line);

            // 每10添加一个标签
            if (i % 10 == 0)
            {
                var labelX = CenterX + (Radius + 15) * Math.Cos(radian);
                var labelY = CenterY - (Radius + 15) * Math.Sin(radian);
                
                var textBlock = new TextBlock
                {
                    Text = i.ToString(),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#848E9C")),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                
                Canvas.SetLeft(textBlock, labelX - 10);
                Canvas.SetTop(textBlock, labelY - 8);
                
                GaugeCanvas.Children.Add(textBlock);
                _scaleLabelTexts.Add(textBlock);
            }
        }
    }

    /// <summary>
    /// 更新仪表盘显示
    /// </summary>
    private void UpdateGauge()
    {
        if (GaugeCanvas == null) return;

        var value = Math.Clamp(Value, 0, 100);
        
        // 更新数值显示
        if (ValueText != null)
        {
            ValueText.Text = value.ToString();
        }

        // 计算角度（0对应180度，100对应0度）
        var angle = StartAngle - (value / 100.0 * AngleRange);
        var radian = angle * Math.PI / 180.0;

        // 更新前景弧线（从起始角度到当前值）
        UpdateForegroundArc(value);

        // 更新指针位置
        UpdateNeedle(radian);

        // 根据值更新颜色
        UpdateColor(value);
    }

    /// <summary>
    /// 更新前景弧线
    /// </summary>
    private void UpdateForegroundArc(int value)
    {
        if (ForegroundArc == null) return;

        var startAngle = StartAngle;
        var endAngle = StartAngle - (value / 100.0 * AngleRange);
        
        var pathData = CreateArcPath(CenterX, CenterY, Radius, startAngle, endAngle);
        ForegroundArc.Data = pathData;
    }

    /// <summary>
    /// 更新指针位置
    /// </summary>
    private void UpdateNeedle(double radian)
    {
        if (Needle == null) return;

        // 指针长度
        var needleLength = Radius - 20;
        var tipX = CenterX + needleLength * Math.Cos(radian);
        var tipY = CenterY - needleLength * Math.Sin(radian);

        // 指针宽度
        var needleWidth = 8;
        var perpAngle = radian + Math.PI / 2;
        var offsetX = needleWidth / 2 * Math.Cos(perpAngle);
        var offsetY = -needleWidth / 2 * Math.Sin(perpAngle);

        // 创建指针路径（三角形）
        var pathData = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(tipX, tipY),
            IsClosed = true
        };
        
        if (figure.Segments != null)
        {
            figure.Segments.Add(new LineSegment
            {
                Point = new Point(CenterX + offsetX, CenterY + offsetY)
            });
            
            figure.Segments.Add(new LineSegment
            {
                Point = new Point(CenterX - offsetX, CenterY - offsetY)
            });
        }

        if (pathData.Figures != null)
        {
            pathData.Figures.Add(figure);
        }
        Needle.Data = pathData;
    }

    /// <summary>
    /// 创建弧线路径
    /// </summary>
    private Geometry CreateArcPath(double centerX, double centerY, double radius, double startAngle, double endAngle)
    {
        var startRadian = startAngle * Math.PI / 180.0;
        var endRadian = endAngle * Math.PI / 180.0;

        var startX = centerX + radius * Math.Cos(startRadian);
        var startY = centerY - radius * Math.Sin(startRadian);
        var endX = centerX + radius * Math.Cos(endRadian);
        var endY = centerY - radius * Math.Sin(endRadian);

        var pathGeometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(startX, startY),
            IsClosed = false
        };

        var arcSegment = new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise, // Avalonia 使用 Clockwise
            IsLargeArc = Math.Abs(endAngle - startAngle) > 180
        };

        if (figure.Segments != null)
        {
            figure.Segments.Add(arcSegment);
        }
        if (pathGeometry.Figures != null)
        {
            pathGeometry.Figures.Add(figure);
        }

        return pathGeometry;
    }

    /// <summary>
    /// 根据值更新颜色
    /// </summary>
    private void UpdateColor(int value)
    {
        if (ForegroundArc == null || Needle == null) return;

        Color color;
        if (value <= 24)
            color = Color.Parse("#F6465D"); // 极恐 - 红色
        else if (value <= 44)
            color = Color.Parse("#FF6B6B"); // 恐惧 - 橙红色
        else if (value <= 55)
            color = Color.Parse("#FFA500"); // 中性 - 橙色
        else if (value <= 75)
            color = Color.Parse("#4ECDC4"); // 贪婪 - 青色
        else
            color = Color.Parse("#0ECB81"); // 极贪 - 绿色

        var brush = new SolidColorBrush(color);
        ForegroundArc.Stroke = brush;
        Needle.Fill = brush;
        Needle.Stroke = brush;

        if (ValueText != null)
        {
            ValueText.Foreground = brush;
        }
    }

    /// <summary>
    /// 更新分类文本
    /// </summary>
    private void UpdateClassification()
    {
        if (ClassificationText != null)
        {
            ClassificationText.Text = Classification;
        }
    }

}

