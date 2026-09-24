namespace Prophet.Client.Trading.Models;

/// <summary>
/// 实盘交易实例状态
/// </summary>
public enum TradingInstanceStatus
{
    /// <summary>
    /// 已停止
    /// </summary>
    Stopped = 0,
    
    /// <summary>
    /// 启动中
    /// </summary>
    Starting = 1,
    
    /// <summary>
    /// 运行中
    /// </summary>
    Running = 2,
    
    /// <summary>
    /// 已暂停
    /// </summary>
    Paused = 3,
    
    /// <summary>
    /// 停止中
    /// </summary>
    Stopping = 4,
    
    /// <summary>
    /// 错误
    /// </summary>
    Error = 5
}

/// <summary>
/// 状态扩展方法
/// </summary>
public static class TradingInstanceStatusExtensions
{
    /// <summary>
    /// 是否可以启动
    /// </summary>
    public static bool CanStart(this TradingInstanceStatus status)
    {
        return status == TradingInstanceStatus.Stopped || 
               status == TradingInstanceStatus.Error;
    }
    
    /// <summary>
    /// 是否可以停止
    /// </summary>
    public static bool CanStop(this TradingInstanceStatus status)
    {
        return status == TradingInstanceStatus.Running || 
               status == TradingInstanceStatus.Paused;
    }
    
    /// <summary>
    /// 是否可以暂停
    /// </summary>
    public static bool CanPause(this TradingInstanceStatus status)
    {
        return status == TradingInstanceStatus.Running;
    }
    
    /// <summary>
    /// 是否可以恢复
    /// </summary>
    public static bool CanResume(this TradingInstanceStatus status)
    {
        return status == TradingInstanceStatus.Paused;
    }
    
    /// <summary>
    /// 是否处于运行状态（包括暂停）
    /// </summary>
    public static bool IsActive(this TradingInstanceStatus status)
    {
        return status == TradingInstanceStatus.Running || 
               status == TradingInstanceStatus.Paused ||
               status == TradingInstanceStatus.Starting ||
               status == TradingInstanceStatus.Stopping;
    }
    
    /// <summary>
    /// 获取状态显示文本
    /// </summary>
    public static string GetDisplayText(this TradingInstanceStatus status)
    {
        return status switch
        {
            TradingInstanceStatus.Stopped => "已停止",
            TradingInstanceStatus.Starting => "启动中",
            TradingInstanceStatus.Running => "运行中",
            TradingInstanceStatus.Paused => "已暂停",
            TradingInstanceStatus.Stopping => "停止中",
            TradingInstanceStatus.Error => "错误",
            _ => "未知"
        };
    }
    
    /// <summary>
    /// 获取状态颜色（用于UI显示）
    /// </summary>
    public static string GetStatusColor(this TradingInstanceStatus status)
    {
        return status switch
        {
            TradingInstanceStatus.Stopped => "#808080", // 灰色
            TradingInstanceStatus.Starting => "#FFA500", // 橙色
            TradingInstanceStatus.Running => "#00FF00", // 绿色
            TradingInstanceStatus.Paused => "#FFFF00", // 黄色
            TradingInstanceStatus.Stopping => "#FFA500", // 橙色
            TradingInstanceStatus.Error => "#FF0000", // 红色
            _ => "#808080"
        };
    }
}

