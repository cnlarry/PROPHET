using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Prophet.Client.Models;

namespace Prophet.Client.Converters;

/// <summary>
/// 订单状态转换器：将OrderStatus转换为Holding/Closed字符串
/// </summary>
public class OrderStatusToStringConverter : IValueConverter
{
    public static OrderStatusToStringConverter Instance { get; } = new OrderStatusToStringConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 处理null和DBNull
        if (value == null || value == DBNull.Value)
        {
            return "-";
        }

        // 处理OrderStatus枚举
        if (value is OrderStatus status)
        {
            return status switch
            {
                OrderStatus.PENDING => "Pending",
                OrderStatus.FILLED => "Filled",
                OrderStatus.OPEN => "Holding",
                OrderStatus.CLOSED => "Closed",
                OrderStatus.CANCELLED => "Cancelled",
                _ => status.ToString()
            };
        }

        // 如果传入的是整数（枚举的底层值）
        if (value is int intValue)
        {
            if (Enum.IsDefined(typeof(OrderStatus), intValue))
            {
                var statusFromInt = (OrderStatus)intValue;
                return statusFromInt switch
                {
                    OrderStatus.PENDING => "Pending",
                    OrderStatus.FILLED => "Filled",
                    OrderStatus.OPEN => "Holding",
                    OrderStatus.CLOSED => "Closed",
                    OrderStatus.CANCELLED => "Cancelled",
                    _ => statusFromInt.ToString()
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
            "PENDING" => OrderStatus.PENDING,
            "FILLED" => OrderStatus.FILLED,
            "OPEN" or "HOLDING" => OrderStatus.OPEN,
            "CLOSED" => OrderStatus.CLOSED,
            "CANCELLED" => OrderStatus.CANCELLED,
            _ => throw new ArgumentException($"无法将 '{str}' 转换为 OrderStatus")
        };
    }
}
