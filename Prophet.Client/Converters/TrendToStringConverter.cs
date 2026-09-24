using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Prophet.Client.Converters;

/// <summary>
/// 趋势值转换器：将BULLISH/BEARISH/NEUTRAL转换为中文显示
/// </summary>
public class TrendToStringConverter : IValueConverter
{
    public static TrendToStringConverter Instance { get; } = new TrendToStringConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || value is not string trend)
        {
            return "-";
        }

        return trend.ToUpper() switch
        {
            "BULLISH" => "看涨",
            "BEARISH" => "看跌",
            "NEUTRAL" => "中性",
            _ => trend  // 如果无法识别，返回原始值
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || value is not string trend)
        {
            return null;
        }

        return trend switch
        {
            "看涨" => "BULLISH",
            "看跌" => "BEARISH",
            "中性" => "NEUTRAL",
            _ => trend  // 如果无法识别，返回原始值
        };
    }
}
