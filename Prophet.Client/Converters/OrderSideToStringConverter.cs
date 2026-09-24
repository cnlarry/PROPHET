using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Prophet.Client.Models;

namespace Prophet.Client.Converters;

/// <summary>
/// 订单方向转换器：将OrderSide转换为SELL/BUY字符串
/// </summary>
public class OrderSideToStringConverter : IValueConverter
{
    public static OrderSideToStringConverter Instance { get; } = new OrderSideToStringConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 处理null和DBNull
        if (value == null || value == DBNull.Value)
        {
            return "-";
        }

        // 处理OrderSide枚举
        if (value is OrderSide side)
        {
            return side switch
            {
                OrderSide.BUY => "BUY",
                OrderSide.SELL => "SELL",
                _ => side.ToString()
            };
        }

        // 如果传入的是整数（枚举的底层值）
        if (value is int intValue)
        {
            if (Enum.IsDefined(typeof(OrderSide), intValue))
            {
                var sideFromInt = (OrderSide)intValue;
                return sideFromInt switch
                {
                    OrderSide.BUY => "BUY",
                    OrderSide.SELL => "SELL",
                    _ => sideFromInt.ToString()
                };
            }
        }

        // 如果传入的是字符串，直接返回
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return str;
        }

        // 其他情况，尝试转换为字符串
        try
        {
            return value.ToString() ?? "-";
        }
        catch
        {
            return "-";
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || value is not string str)
        {
            return null;
        }

        return str.ToUpper() switch
        {
            "BUY" => OrderSide.BUY,
            "SELL" => OrderSide.SELL,
            _ => throw new ArgumentException($"无法将 '{str}' 转换为 OrderSide")
        };
    }
}
