namespace Prophet.Client.Trading.Engine;

/// <summary>
/// 交易引擎状态
/// </summary>
public enum TradingEngineStatus
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

