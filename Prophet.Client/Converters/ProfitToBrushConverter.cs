using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Prophet.Client.Converters;

/// <summary>
/// 盈亏金额转颜色转换器
/// 盈利显示绿色，亏损显示红色，持平显示灰色
/// </summary>
public class ProfitToBrushConverter : IValueConverter
{
    public static readonly ProfitToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 处理null和DBNull
        if (value == null || value == DBNull.Value)
        {
            return new SolidColorBrush(Color.Parse("#9CA3AF")); // 灰色（中性色）
        }

        // 处理decimal类型
        decimal decimalValue = 0;
        if (value is decimal d)
        {
            decimalValue = d;
        }
        else
        {
            // 尝试转换（包括可空类型和其他数值类型）
            try
            {
                if (value != null)
                {
                    decimalValue = System.Convert.ToDecimal(value);
                }
                else
                {
                    return new SolidColorBrush(Color.Parse("#9CA3AF")); // 灰色（默认）
                }
            }
            catch
            {
                return new SolidColorBrush(Color.Parse("#9CA3AF")); // 灰色（默认）
            }
        }

        if (decimalValue > 0)
            return new SolidColorBrush(Color.Parse("#10B981")); // 绿色（成功色）
        else if (decimalValue < 0)
            return new SolidColorBrush(Color.Parse("#EF4444")); // 红色（危险色）
        else
            return new SolidColorBrush(Color.Parse("#9CA3AF")); // 灰色（中性色）
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 胜率转颜色转换器
/// 高胜率显示绿色，中等胜率显示黄色，低胜率显示红色
/// </summary>
public class WinRateToBrushConverter : IValueConverter
{
    public static readonly WinRateToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            // 转换为百分比
            var percentage = decimalValue * 100;
            
            if (percentage >= 50)
                return new SolidColorBrush(Color.Parse("#10B981")); // 绿色 - 高胜率
            else if (percentage >= 30)
                return new SolidColorBrush(Color.Parse("#F59E0B")); // 黄色 - 中等胜率
            else
                return new SolidColorBrush(Color.Parse("#EF4444")); // 红色 - 低胜率
        }
        
        return new SolidColorBrush(Color.Parse("#9CA3AF")); // 默认灰色
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 回撤转颜色转换器
/// 回撤越大颜色越红
/// </summary>
public class DrawdownToBrushConverter : IValueConverter
{
    public static readonly DrawdownToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            // 转换为百分比
            var percentage = decimalValue * 100;
            
            if (percentage >= 30)
                return new SolidColorBrush(Color.Parse("#DC2626")); // 深红色 - 严重回撤
            else if (percentage >= 20)
                return new SolidColorBrush(Color.Parse("#EF4444")); // 红色 - 较大回撤
            else if (percentage >= 10)
                return new SolidColorBrush(Color.Parse("#F59E0B")); // 黄色 - 中等回撤
            else
                return new SolidColorBrush(Color.Parse("#10B981")); // 绿色 - 小回撤
        }
        
        return new SolidColorBrush(Color.Parse("#9CA3AF")); // 默认灰色
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

