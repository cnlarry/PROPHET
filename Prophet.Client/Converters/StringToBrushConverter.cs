using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Prophet.Client.Converters;

/// <summary>
/// 字符串颜色转Brush转换器
/// 将颜色字符串（如 "#FF0000"）转换为 SolidColorBrush
/// </summary>
public class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || value == DBNull.Value)
        {
            return new SolidColorBrush(Color.Parse("#848E9C")); // 默认灰色
        }

        if (value is string colorString)
        {
            try
            {
                return new SolidColorBrush(Color.Parse(colorString));
            }
            catch
            {
                return new SolidColorBrush(Color.Parse("#848E9C")); // 默认灰色
            }
        }

        return new SolidColorBrush(Color.Parse("#848E9C")); // 默认灰色
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 单向转换器：不支持回转，返回 UnsetValue 避免双向绑定误用时崩溃
        return Avalonia.AvaloniaProperty.UnsetValue;
    }
}

