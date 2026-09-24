using System;
using System.Threading.Tasks;

namespace Prophet.Client.Services.Data.Collectors;

/// <summary>
/// 数据采集器接口
/// 所有数据采集器必须实现此接口
/// </summary>
/// <typeparam name="T">采集的数据类型</typeparam>
public interface IDataCollector<T>
{
    /// <summary>
    /// 采集器名称（用于日志和标识）
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 采集间隔（秒）
    /// </summary>
    int IntervalSeconds { get; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// 最后一次采集时间
    /// </summary>
    DateTime? LastCollectTime { get; }
    
    /// <summary>
    /// 最后一次采集是否成功
    /// </summary>
    bool LastCollectSuccess { get; }
    
    /// <summary>
    /// 最后一次错误信息
    /// </summary>
    string? LastError { get; }
    
    /// <summary>
    /// 执行一次数据采集
    /// </summary>
    Task<CollectResult<T>> CollectAsync();
    
    /// <summary>
    /// 启动定时采集
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// 停止定时采集
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// 数据采集完成事件
    /// </summary>
    event EventHandler<CollectCompletedEventArgs<T>>? CollectCompleted;
    
    /// <summary>
    /// 采集错误事件
    /// </summary>
    event EventHandler<CollectErrorEventArgs>? CollectError;
}

/// <summary>
/// 采集结果
/// </summary>
public class CollectResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CollectTime { get; set; }
    public int DataCount { get; set; }
}

/// <summary>
/// 采集完成事件参数
/// </summary>
public class CollectCompletedEventArgs<T> : EventArgs
{
    public CollectResult<T> Result { get; set; } = null!;
}

/// <summary>
/// 采集错误事件参数
/// </summary>
public class CollectErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}

