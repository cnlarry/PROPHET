using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Prophet.Client.Converters;

/// <summary>
/// 盈亏金额转颜色转换器（返回Color而不是Brush）
/// 用于 SolidColorBrush.Color 绑定
/// </summary>
public class ProfitColorConverter : IMultiValueConverter
{
    public static readonly ProfitColorConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count == 0 || values[0] == null)
        {
            return Color.Parse("#9CA3AF"); // 灰色（中性色）
        }

        decimal decimalValue = 0;
        var value = values[0];
        
        if (value is decimal d)
        {
            decimalValue = d;
        }
        else
        {
            try
            {
                decimalValue = System.Convert.ToDecimal(value);
            }
            catch
            {
                return Color.Parse("#9CA3AF"); // 灰色（默认）
            }
        }

        if (decimalValue > 0)
            return Color.Parse("#10B981"); // 绿色（成功色）
        else if (decimalValue < 0)
            return Color.Parse("#EF4444"); // 红色（危险色）
        else
            return Color.Parse("#9CA3AF"); // 灰色（中性色）
    }
}

