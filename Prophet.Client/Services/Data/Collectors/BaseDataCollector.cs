using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prophet.Client.Services.Data.Collectors;

/// <summary>
/// 数据采集器基类
/// 提供通用的定时采集、错误处理、事件通知等功能
/// </summary>
/// <typeparam name="T">采集的数据类型</typeparam>
public abstract class BaseDataCollector<T> : IDataCollector<T>
{
    protected readonly string _name;
    protected readonly int _intervalSeconds;
    
    private bool _isEnabled = true;
    private bool _isRunning = false;
    private DateTime? _lastCollectTime;
    private bool _lastCollectSuccess = false;
    private string? _lastError;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _collectTask;
    
    public string Name => _name;
    public int IntervalSeconds => _intervalSeconds;
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                Console.WriteLine($"📊 [{_name}] {(value ? "已启用" : "已禁用")}");
            }
        }
    }
    
    public bool IsRunning => _isRunning;
    public DateTime? LastCollectTime => _lastCollectTime;
    public bool LastCollectSuccess => _lastCollectSuccess;
    public string? LastError => _lastError;
    
    public event EventHandler<CollectCompletedEventArgs<T>>? CollectCompleted;
    public event EventHandler<CollectErrorEventArgs>? CollectError;
    
    protected BaseDataCollector(string name, int intervalSeconds)
    {
        _name = name;
        _intervalSeconds = intervalSeconds;
    }
    
    /// <summary>
    /// 执行实际的数据采集逻辑（子类实现）
    /// </summary>
    protected abstract Task<CollectResult<T>> DoCollectAsync();
    
    /// <summary>
    /// 保存采集到的数据到数据库（子类实现）
    /// </summary>
    protected abstract Task SaveDataAsync(T data);
    
    /// <summary>
    /// 执行一次数据采集（包含错误处理）
    /// </summary>
    public async Task<CollectResult<T>> CollectAsync()
    {
        if (!_isEnabled)
        {
            return new CollectResult<T>
            {
                Success = false,
                ErrorMessage = "采集器已禁用",
                CollectTime = DateTime.UtcNow
            };
        }
        
        try
        {
            Console.WriteLine($"📥 [{_name}] 开始采集数据...");
            var result = await DoCollectAsync();
            _lastCollectTime = DateTime.UtcNow;
            _lastCollectSuccess = result.Success;
            _lastError = result.ErrorMessage;
            
            if (result.Success && result.Data != null)
            {
                try
                {
                    await SaveDataAsync(result.Data);
                    Console.WriteLine($"✅ [{_name}] 采集成功，数据已保存（{result.DataCount} 条）");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [{_name}] 数据保存失败: {ex.Message}");
                    result.ErrorMessage = $"保存失败: {ex.Message}";
                }
            }
            else
            {
                Console.WriteLine($"❌ [{_name}] 采集失败: {result.ErrorMessage}");
            }
            
            // 触发事件
            CollectCompleted?.Invoke(this, new CollectCompletedEventArgs<T> { Result = result });
            
            return result;
        }
        catch (Exception ex)
        {
            _lastCollectSuccess = false;
            _lastError = ex.Message;
            
            var errorArgs = new CollectErrorEventArgs
            {
                ErrorMessage = ex.Message,
                Exception = ex
            };
            
            CollectError?.Invoke(this, errorArgs);
            
            Console.WriteLine($"❌ [{_name}] 采集异常: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            
            return new CollectResult<T>
            {
                Success = false,
                ErrorMessage = ex.Message,
                CollectTime = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// 启动定时采集
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            Console.WriteLine($"⚠️ [{_name}] 采集器已在运行");
            return;
        }
        
        if (!_isEnabled)
        {
            Console.WriteLine($"⚠️ [{_name}] 采集器已禁用，无法启动");
            return;
        }
        
        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        Console.WriteLine($"🚀 [{_name}] 启动定时采集（间隔: {_intervalSeconds}秒）");
        
        // 立即执行一次采集
        await CollectAsync();
        
        // 启动定时任务
        _collectTask = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && _isEnabled)
            {
                await Task.Delay(_intervalSeconds * 1000, _cancellationTokenSource.Token);
                
                if (!_cancellationTokenSource.Token.IsCancellationRequested && _isEnabled)
                {
                    await CollectAsync();
                }
            }
        }, _cancellationTokenSource.Token);
    }
    
    /// <summary>
    /// 停止定时采集
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }
        
        Console.WriteLine($"🛑 [{_name}] 停止定时采集");
        
        _cancellationTokenSource?.Cancel();
        
        if (_collectTask != null)
        {
            try
            {
                await _collectTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
        }
        
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _collectTask = null;
        _isRunning = false;
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public virtual void Dispose()
    {
        StopAsync().Wait(5000);
    }
}

