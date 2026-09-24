using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Prophet.Client.Core;
using Prophet.Client.Services.AI.Core;

namespace Prophet.Client.ViewModels;

/// <summary>
/// AI 助手视图模型
/// </summary>
public class AiAssistantViewModel : INotifyPropertyChanged
{
    private string _inputText = string.Empty;
    private bool _isLoading;
    private bool _hasContext;
    private string _contextInfo = string.Empty;
    private AiChatSession? _currentSession;
    private readonly AiServiceManager _aiService;
    
    public AiAssistantViewModel()
    {
        Messages = new ObservableCollection<AiMessageModel>();
        Sessions = new ObservableCollection<AiChatSession>();
        
        // 获取 AI 服务
        _aiService = ServiceContainer.GetService<AiServiceManager>();
        
        // 创建默认会话
        CreateNewSession();
    }
    
    #region 属性
    
    /// <summary>
    /// 消息列表
    /// </summary>
    public ObservableCollection<AiMessageModel> Messages { get; }
    
    /// <summary>
    /// 会话列表
    /// </summary>
    public ObservableCollection<AiChatSession> Sessions { get; }
    
    /// <summary>
    /// 当前会话
    /// </summary>
    public AiChatSession? CurrentSession
    {
        get => _currentSession;
        set
        {
            if (_currentSession != value)
            {
                if (_currentSession != null)
                {
                    _currentSession.IsActive = false;
                }
                _currentSession = value;
                if (_currentSession != null)
                {
                    _currentSession.IsActive = true;
                    LoadSessionMessages(_currentSession);
                }
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 输入文本
    /// </summary>
    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText != value)
            {
                _inputText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSend));
            }
        }
    }
    
    /// <summary>
    /// 是否正在加载
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSend));
            }
        }
    }
    
    /// <summary>
    /// 是否有上下文
    /// </summary>
    public bool HasContext
    {
        get => _hasContext;
        set
        {
            if (_hasContext != value)
            {
                _hasContext = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 上下文信息
    /// </summary>
    public string ContextInfo
    {
        get => _contextInfo;
        set
        {
            if (_contextInfo != value)
            {
                _contextInfo = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 是否有消息
    /// </summary>
    public bool HasMessages => Messages.Count > 0;
    
    /// <summary>
    /// 是否可以发送
    /// </summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(InputText) && !IsLoading;
    
    #endregion
    
    #region 方法
    
    /// <summary>
    /// 创建新会话
    /// </summary>
    public void CreateNewSession()
    {
        var session = new AiChatSession
        {
            Id = Guid.NewGuid().ToString(),
            Title = $"新会话 {Sessions.Count + 1}",
            CreatedTime = DateTime.Now,
            LastMessageTime = DateTime.Now,
            Messages = new ObservableCollection<AiMessageModel>()
        };
        
        Sessions.Insert(0, session);
        CurrentSession = session;
    }
    
    /// <summary>
    /// 加载会话消息
    /// </summary>
    private void LoadSessionMessages(AiChatSession session)
    {
        Messages.Clear();
        foreach (var message in session.Messages)
        {
            Messages.Add(message);
        }
        OnPropertyChanged(nameof(HasMessages));
    }
    
    /// <summary>
    /// 添加用户消息
    /// </summary>
    public void AddUserMessage(string content)
    {
        var message = new AiMessageModel
        {
            Content = content,
            IsUser = true,
            Timestamp = DateTime.Now
        };
        
        Messages.Add(message);
        
        if (CurrentSession != null)
        {
            CurrentSession.Messages.Add(message);
            CurrentSession.LastMessageTime = DateTime.Now;
            
            // 更新会话标题（使用第一条用户消息的前20个字符）
            if (CurrentSession.Messages.Count == 1)
            {
                CurrentSession.Title = content.Length > 20 
                    ? content.Substring(0, 20) + "..." 
                    : content;
            }
        }
        
        OnPropertyChanged(nameof(HasMessages));
    }
    
    /// <summary>
    /// 添加 AI 消息
    /// </summary>
    public void AddAiMessage(string content, string code = "", AiOperationType operationType = AiOperationType.None, string? currentStrategyId = null)
    {
        var message = new AiMessageModel
        {
            Content = content,
            Code = code,
            IsUser = false,
            Timestamp = DateTime.Now,
            OperationType = operationType,
            CurrentStrategyId = currentStrategyId
        };
        
        Messages.Add(message);
        
        if (CurrentSession != null)
        {
            CurrentSession.Messages.Add(message);
            CurrentSession.LastMessageTime = DateTime.Now;
        }
        
        OnPropertyChanged(nameof(HasMessages));
    }
    
    /// <summary>
    /// 清空消息
    /// </summary>
    public void ClearMessages()
    {
        Messages.Clear();
        if (CurrentSession != null)
        {
            CurrentSession.Messages.Clear();
        }
        OnPropertyChanged(nameof(HasMessages));
    }
    
    /// <summary>
    /// 预测市场走势
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="timeframe">时间框架</param>
    /// <param name="marketDataSummary">市场数据摘要</param>
    /// <returns>预测结果</returns>
    public async Task<string> PredictMarketTrendAsync(string symbol, string timeframe, string marketDataSummary)
    {
        try
        {
            // 加载AI配置
            await _aiService.LoadFromSettingsAsync();
            
            if (!_aiService.IsInitialized)
            {
                return "请先在设置中配置 AI API 密钥";
            }
            
            // 调用AI服务进行市场预测
            return await _aiService.PredictMarketTrendAsync(symbol, timeframe, marketDataSummary);
        }
        catch (Exception ex)
        {
            return $"市场预测失败: {ex.Message}";
        }
    }
    
    #endregion
    
    #region INotifyPropertyChanged
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    #endregion
}

/// <summary>
/// AI操作类型
/// </summary>
public enum AiOperationType
{
    None,           // 无操作
    CreateNewTab,   // 场景1：创建新Tab并插入代码
    ReplaceCode,    // 场景2：替换当前编辑器代码
    SaveAndBacktest // 场景3：保存新版本并回测
}

/// <summary>
/// AI 消息模型
/// </summary>
public class AiMessageModel : INotifyPropertyChanged
{
    private string _content = string.Empty;
    private string _code = string.Empty;
    private bool _isUser;
    private DateTime _timestamp;
    private AiOperationType _operationType = AiOperationType.None;
    private string? _currentStrategyId; // 当前策略ID（用于场景2和3）
    
    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 代码内容（如果有）
    /// </summary>
    public string Code
    {
        get => _code;
        set
        {
            if (_code != value)
            {
                _code = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCode));
            }
        }
    }
    
    /// <summary>
    /// 是否有代码
    /// </summary>
    public bool HasCode => !string.IsNullOrWhiteSpace(Code);
    
    /// <summary>
    /// 是否为用户消息
    /// </summary>
    public bool IsUser
    {
        get => _isUser;
        set
        {
            if (_isUser != value)
            {
                _isUser = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set
        {
            if (_timestamp != value)
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 操作类型
    /// </summary>
    public AiOperationType OperationType
    {
        get => _operationType;
        set
        {
            if (_operationType != value)
            {
                _operationType = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 当前策略ID（用于场景2和3）
    /// </summary>
    public string? CurrentStrategyId
    {
        get => _currentStrategyId;
        set
        {
            if (_currentStrategyId != value)
            {
                _currentStrategyId = value;
                OnPropertyChanged();
            }
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// AI 会话模型
/// </summary>
public class AiChatSession : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _title = string.Empty;
    private DateTime _createdTime;
    private DateTime _lastMessageTime;
    private bool _isActive;
    
    /// <summary>
    /// 会话ID
    /// </summary>
    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 会话标题
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime
    {
        get => _createdTime;
        set
        {
            if (_createdTime != value)
            {
                _createdTime = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 最后消息时间
    /// </summary>
    public DateTime LastMessageTime
    {
        get => _lastMessageTime;
        set
        {
            if (_lastMessageTime != value)
            {
                _lastMessageTime = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 是否为当前激活会话
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 会话消息列表
    /// </summary>
    public ObservableCollection<AiMessageModel> Messages { get; set; } = new();
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

