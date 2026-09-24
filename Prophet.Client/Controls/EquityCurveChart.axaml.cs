using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Prophet.Client.Backtest.Models;

namespace Prophet.Client.Controls;

public partial class EquityCurveChart : UserControl
{
    private const double LeftMargin = 20;
    private const double RightMargin = 70;
    private const double TopMargin = 20;
    private const double BottomMargin = 40;
    private INotifyCollectionChanged? _currentCollection;

    public static readonly StyledProperty<IEnumerable<EquityPoint>?> DataProperty =
        AvaloniaProperty.Register<EquityCurveChart, IEnumerable<EquityPoint>?>(
            nameof(Data),
            defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public IEnumerable<EquityPoint>? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public EquityCurveChart()
    {
        InitializeComponent();
        DataProperty.Changed.AddClassHandler<EquityCurveChart>((x, e) => x.OnDataChanged());
        
        // Canvas 现在有固定尺寸 (600x300)，不需要动态调整
        // Viewbox 会自动缩放来填充父容器
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // Viewbox 会自动处理缩放，不需要手动调整 Canvas 尺寸
    }

    private void OnDataChanged()
    {
        // 取消之前的集合变化订阅
        if (_currentCollection != null)
        {
            _currentCollection.CollectionChanged -= OnCollectionChanged;
            _currentCollection = null;
        }

        // 如果新数据实现了 INotifyCollectionChanged，订阅集合变化事件
        if (Data is INotifyCollectionChanged notifyCollection)
        {
            _currentCollection = notifyCollection;
            notifyCollection.CollectionChanged += OnCollectionChanged;
        }

        // 立即重绘，确保数据变化能立即显示
        Redraw();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 在 UI 线程上重绘
        Dispatcher.UIThread.Post(() => Redraw(), DispatcherPriority.Background);
    }

    private void Redraw()
    {
        try
        {
            if (Data == null || !Data.Any())
            {
                ChartCanvas.Children.Clear();
                return;
            }

            var dataList = Data.ToList();
            if (dataList.Count < 2)
            {
                ChartCanvas.Children.Clear();
                return;
            }

            // 使用固定的 Canvas 尺寸 (在 AXAML 中定义为 600x300)
            const double width = 600;
            const double height = 300;

            var chartWidth = width - LeftMargin - RightMargin;
            var chartHeight = height - TopMargin - BottomMargin;

            if (chartWidth <= 0 || chartHeight <= 0)
            {
                return;
            }

            // 清空现有元素
            ChartCanvas.Children.Clear();

            var minEquity = dataList.Min(p => p.Equity);
            var maxEquity = dataList.Max(p => p.Equity);
            var equityRange = maxEquity - minEquity;
            if (equityRange == 0) equityRange = 1;

            // 绘制网格线
            DrawGridLines(chartWidth, chartHeight, minEquity, maxEquity);

            // 绘制收益曲线
            DrawEquityCurve(dataList, chartWidth, chartHeight, minEquity, equityRange);

            // 绘制Y轴标签
            DrawYAxisLabels(chartHeight, minEquity, maxEquity);

            // 绘制X轴标签
            DrawXAxisLabels(dataList, chartWidth);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 收益曲线绘制错误: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void DrawGridLines(double chartWidth, double chartHeight, decimal minEquity, decimal maxEquity)
    {
        var gridBrush = new SolidColorBrush(Color.Parse("#2B3139"));
        
        // 水平网格线（5条）
        for (int i = 0; i <= 5; i++)
        {
            var y = TopMargin + (chartHeight / 5) * i;
            var line = new Line
            {
                StartPoint = new Point(LeftMargin, y),
                EndPoint = new Point(LeftMargin + chartWidth, y),
                Stroke = gridBrush,
                StrokeThickness = 0.5
            };
            ChartCanvas.Children.Add(line);
        }

        // 垂直网格线（5条）
        for (int i = 0; i <= 5; i++)
        {
            var x = LeftMargin + (chartWidth / 5) * i;
            var line = new Line
            {
                StartPoint = new Point(x, TopMargin),
                EndPoint = new Point(x, TopMargin + chartHeight),
                Stroke = gridBrush,
                StrokeThickness = 0.5
            };
            ChartCanvas.Children.Add(line);
        }
    }

    private void DrawEquityCurve(List<EquityPoint> data, double chartWidth, double chartHeight, decimal minEquity, decimal equityRange)
    {
        if (data.Count < 2) return;

        // 智能数据采样：当数据点过多时，保留关键点
        var sampledData = SampleData(data, maxPoints: 300);
        
        var points = new List<Point>();
        var spacing = chartWidth / (sampledData.Count - 1);

        for (int i = 0; i < sampledData.Count; i++)
        {
            var equity = sampledData[i].Equity;
            var ratio = (equity - minEquity) / equityRange;
            var x = LeftMargin + i * spacing;
            var y = TopMargin + chartHeight - (chartHeight * (double)ratio);
            points.Add(new Point(x, y));
        }

        // 使用 Catmull-Rom 样条生成平滑曲线
        var smoothSegments = CreateCatmullRomSpline(points);
        
        // 绘制平滑曲线（单条线）
        var curvePath = new Path
        {
            Stroke = new SolidColorBrush(Color.Parse("#0ECB81")),
            StrokeThickness = 2,
            Data = new PathGeometry
            {
                Figures = new PathFigures
                {
                    new PathFigure
                    {
                        StartPoint = points[0],
                        Segments = smoothSegments,
                        IsClosed = false
                    }
                }
            }
        };
        ChartCanvas.Children.Add(curvePath);

        // 绘制最后一个点
        if (points.Count > 0)
        {
            var lastPoint = points.Last();
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.Parse("#0ECB81")),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(dot, lastPoint.X - 3);
            Canvas.SetTop(dot, lastPoint.Y - 3);
            ChartCanvas.Children.Add(dot);
        }
    }
    
    /// <summary>
    /// 使用 Catmull-Rom 样条算法生成平滑曲线
    /// </summary>
    private PathSegments CreateCatmullRomSpline(List<Point> points)
    {
        var segments = new PathSegments();
        
        if (points.Count < 2) return segments;
        
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = i == 0 ? points[0] : points[i - 1];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i == points.Count - 2 ? points[i + 1] : points[i + 2];

            // Catmull-Rom 控制点计算公式
            // 标准 Tension = 0.5
            var cp1 = new Point(
                p1.X + (p2.X - p0.X) / 6.0,
                p1.Y + (p2.Y - p0.Y) / 6.0
            );

            var cp2 = new Point(
                p2.X - (p3.X - p1.X) / 6.0,
                p2.Y - (p3.Y - p1.Y) / 6.0
            );

            segments.Add(new BezierSegment
            {
                Point1 = cp1,
                Point2 = cp2,
                Point3 = p2
            });
        }
        
        return segments;
    }
    
    /// <summary>
    /// 智能数据采样：保留关键数据点，减少渲染负担
    /// 使用改进的 Largest-Triangle-Three-Buckets (LTTB) 算法
    /// </summary>
    private List<EquityPoint> SampleData(List<EquityPoint> data, int maxPoints)
    {
        if (data.Count <= maxPoints)
            return data;
        
        var sampled = new List<EquityPoint> { data[0] }; // 保留第一个点
        var bucketSize = (double)(data.Count - 2) / (maxPoints - 2);
        
        for (int i = 0; i < maxPoints - 2; i++)
        {
            var avgRangeStart = (int)Math.Floor((i + 1) * bucketSize) + 1;
            var avgRangeEnd = (int)Math.Floor((i + 2) * bucketSize) + 1;
            if (avgRangeEnd >= data.Count)
                avgRangeEnd = data.Count - 1;
            
            // 计算下一个桶的平均点
            var avgX = 0.0;
            var avgY = 0.0;
            var avgRangeLength = avgRangeEnd - avgRangeStart;
            
            for (int j = avgRangeStart; j < avgRangeEnd; j++)
            {
                avgX += j;
                avgY += (double)data[j].Equity;
            }
            
            if (avgRangeLength > 0)
            {
                avgX /= avgRangeLength;
                avgY /= avgRangeLength;
            }
            
            // 在当前桶中找到与平均点形成最大三角形面积的点
            var rangeStart = (int)Math.Floor(i * bucketSize) + 1;
            var rangeEnd = (int)Math.Floor((i + 1) * bucketSize) + 1;
            
            var maxArea = -1.0;
            var maxAreaPoint = data[rangeStart];
            
            var pointAX = i > 0 ? sampled.Count - 1 : 0;
            var pointAY = (double)sampled[^1].Equity;
            
            for (int j = rangeStart; j < rangeEnd && j < data.Count; j++)
            {
                var area = Math.Abs(
                    (pointAX - avgX) * ((double)data[j].Equity - pointAY) -
                    (pointAX - j) * (avgY - pointAY)
                ) * 0.5;
                
                if (area > maxArea)
                {
                    maxArea = area;
                    maxAreaPoint = data[j];
                }
            }
            
            sampled.Add(maxAreaPoint);
        }
        
        sampled.Add(data[^1]); // 保留最后一个点
        return sampled;
    }

    private void DrawYAxisLabels(double chartHeight, decimal minEquity, decimal maxEquity)
    {
        var textBrush = new SolidColorBrush(Color.Parse("#848E9C"));
        var tickBrush = new SolidColorBrush(Color.Parse("#848E9C"));
        const double chartWidth = 600 - LeftMargin - RightMargin;
        const double tickLength = 5; // 刻度线长度
        
        for (int i = 0; i <= 5; i++)
        {
            var ratio = i / 5.0;
            var equity = minEquity + (maxEquity - minEquity) * (decimal)ratio;
            var y = TopMargin + chartHeight - (chartHeight * ratio);

            // 绘制刻度指示线（从图表右边缘延伸到标签）
            var tickLine = new Line
            {
                StartPoint = new Point(LeftMargin + chartWidth, y),
                EndPoint = new Point(LeftMargin + chartWidth + tickLength, y),
                Stroke = tickBrush,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(tickLine);

            // 绘制Y轴标签（左对齐）
            var textBlock = new TextBlock
            {
                Text = $"${equity:N2}",
                FontSize = 10,
                Foreground = textBrush,
                TextAlignment = Avalonia.Media.TextAlignment.Left,
                Width = 60
            };
            // 将Y轴标签放在右侧，紧接刻度线
            Canvas.SetLeft(textBlock, LeftMargin + chartWidth + tickLength + 3);
            Canvas.SetTop(textBlock, y - 8);
            ChartCanvas.Children.Add(textBlock);
        }
    }

    private void DrawXAxisLabels(List<EquityPoint> data, double chartWidth)
    {
        if (data.Count == 0) return;

        var textBrush = new SolidColorBrush(Color.Parse("#848E9C"));
        var tickBrush = new SolidColorBrush(Color.Parse("#848E9C"));
        var spacing = chartWidth / (data.Count - 1);
        var labelCount = Math.Min(5, data.Count);
        const double chartHeight = 300 - TopMargin - BottomMargin;
        const double tickLength = 5; // 刻度线长度
        const double labelWidth = 60; // 标签宽度（用于计算位置，避免溢出）

        for (int i = 0; i < labelCount; i++)
        {
            var index = (int)((data.Count - 1) * (i / (double)(labelCount - 1)));
            if (index >= data.Count) index = data.Count - 1;

            var point = data[index];
            var x = LeftMargin + index * spacing;

            // 绘制刻度指示线（从图表底边缘延伸到标签）
            var tickLine = new Line
            {
                StartPoint = new Point(x, TopMargin + chartHeight),
                EndPoint = new Point(x, TopMargin + chartHeight + tickLength),
                Stroke = tickBrush,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(tickLine);

            // 计算标签位置，确保不超出容器边界
            // 第一个标签左对齐到刻度线，最后一个标签右对齐到刻度线，中间的居中对齐
            double labelLeft;
            if (i == 0)
            {
                // 第一个标签：左边界对齐到刻度线，但不超出左边界
                labelLeft = Math.Max(0, x - labelWidth / 2);
            }
            else if (i == labelCount - 1)
            {
                // 最后一个标签：右边界对齐到刻度线，但不超出右边界
                var maxRight = 600 - labelWidth;
                labelLeft = Math.Min(maxRight, x - labelWidth / 2);
            }
            else
            {
                // 中间标签：居中对齐到刻度线
                labelLeft = x - labelWidth / 2;
            }

            var textBlock = new TextBlock
            {
                Text = point.Time.ToString("MM-dd HH:mm"),
                FontSize = 10,
                Foreground = textBrush,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
                Width = labelWidth
            };
            Canvas.SetLeft(textBlock, labelLeft);
            Canvas.SetTop(textBlock, TopMargin + chartHeight + tickLength + 3);
            ChartCanvas.Children.Add(textBlock);
        }
    }
}
