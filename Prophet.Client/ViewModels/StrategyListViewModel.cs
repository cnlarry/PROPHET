using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Services.Strategy;

namespace Prophet.Client.ViewModels;

/// <summary>
/// 策略列表ViewModel（离线版）
/// 使用本地SQLite数据库，无需API
/// </summary>
public class StrategyListViewModel : INotifyPropertyChanged
{
    private readonly LocalStrategyService _strategyService;
    private bool _isLoading;
    private string? _errorMessage;
    private StrategyInfo? _selectedStrategy;
    private string _searchText = string.Empty;

    public StrategyListViewModel(LocalStrategyService strategyService)
    {
        _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
        AllStrategies = new ObservableCollection<StrategyInfo>();
        ActiveStrategies = new ObservableCollection<StrategyInfo>();
        DraftStrategies = new ObservableCollection<StrategyInfo>();
        DeprecatedStrategies = new ObservableCollection<StrategyInfo>();
        ArchivedStrategies = new ObservableCollection<StrategyInfo>();
        AnomalyStrategies = new ObservableCollection<StrategyInfo>();
    }

    #region 属性

    /// <summary>
    /// 所有策略
    /// </summary>
    public ObservableCollection<StrategyInfo> AllStrategies { get; }

    /// <summary>
    /// 激活的策略
    /// </summary>
    public ObservableCollection<StrategyInfo> ActiveStrategies { get; }

    /// <summary>
    /// 草稿策略
    /// </summary>
    public ObservableCollection<StrategyInfo> DraftStrategies { get; }

    /// <summary>
    /// 已废弃的策略
    /// </summary>
    public ObservableCollection<StrategyInfo> DeprecatedStrategies { get; }

    /// <summary>
    /// 已归档的策略
    /// </summary>
    public ObservableCollection<StrategyInfo> ArchivedStrategies { get; }

    /// <summary>
    /// 异常状态的策略
    /// </summary>
    public ObservableCollection<StrategyInfo> AnomalyStrategies { get; }

    /// <summary>
    /// 当前选中的策略
    /// </summary>
    public StrategyInfo? SelectedStrategy
    {
        get => _selectedStrategy;
        set
        {
            if (_selectedStrategy != value)
            {
                // 清除旧的选中状态
                if (_selectedStrategy != null)
                {
                    _selectedStrategy.IsSelected = false;
                }
                
                _selectedStrategy = value;
                
                // 设置新的选中状态
                if (_selectedStrategy != null)
                {
                    _selectedStrategy.IsSelected = true;
                }
                
                OnPropertyChanged();
                OnSelectedStrategyChanged?.Invoke(value);
            }
        }
    }
    
    /// <summary>
    /// 判断策略是否被选中
    /// </summary>
    public bool IsStrategySelected(string strategyId)
    {
        return SelectedStrategy?.Id == strategyId;
    }

    /// <summary>
    /// 搜索文本
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                FilterStrategies();
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
            }
        }
    }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 激活策略数量
    /// </summary>
    public int ActiveCount => ActiveStrategies.Count;

    /// <summary>
    /// 草稿策略数量
    /// </summary>
    public int DraftCount => DraftStrategies.Count;

    /// <summary>
    /// 已废弃策略数量
    /// </summary>
    public int DeprecatedCount => DeprecatedStrategies.Count;

    /// <summary>
    /// 已归档策略数量
    /// </summary>
    public int ArchivedCount => ArchivedStrategies.Count;

    /// <summary>
    /// 异常策略数量
    /// </summary>
    public int AnomalyCount => AnomalyStrategies.Count;

    #endregion

    #region 事件

    /// <summary>
    /// 选中策略变更事件
    /// </summary>
    public event Action<StrategyInfo?>? OnSelectedStrategyChanged;

    #endregion

    #region 方法

    /// <summary>
    /// 加载策略列表
    /// </summary>
    public async Task LoadStrategiesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var strategies = await _strategyService.GetMyStrategiesAsync();

            AllStrategies.Clear();
            ActiveStrategies.Clear();
            DraftStrategies.Clear();
            DeprecatedStrategies.Clear();
            ArchivedStrategies.Clear();
            AnomalyStrategies.Clear();

            foreach (var strategy in strategies)
            {
                AllStrategies.Add(strategy);
                
                switch (strategy.Status.ToLower())
                {
                    case "active":
                        ActiveStrategies.Add(strategy);
                        break;
                    case "draft":
                        DraftStrategies.Add(strategy);
                        break;
                    case "deprecated":
                        DeprecatedStrategies.Add(strategy);
                        break;
                    case "archived":
                        ArchivedStrategies.Add(strategy);
                        break;
                    case "anomaly":
                        AnomalyStrategies.Add(strategy);
                        break;
                }
            }

            OnPropertyChanged(nameof(ActiveCount));
            OnPropertyChanged(nameof(DraftCount));
            OnPropertyChanged(nameof(DeprecatedCount));
            OnPropertyChanged(nameof(ArchivedCount));
            OnPropertyChanged(nameof(AnomalyCount));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 过滤策略
    /// </summary>
    private void FilterStrategies()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // 显示所有策略
            return;
        }

        // TODO: 实现搜索过滤逻辑
    }

    /// <summary>
    /// 刷新策略列表
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadStrategiesAsync();
    }

    /// <summary>
    /// 激活策略
    /// </summary>
    public async Task<bool> ActivateStrategyAsync(StrategyInfo strategy, string? description = null, string? strategyType = null, string? riskDisclosure = null)
    {
        try
        {
            Console.WriteLine($"🔄 激活策略: {strategy.Name}");
            
            // 调用本地服务激活策略
            var success = await _strategyService.ActivateStrategyAsync(strategy.Id);
            
            if (success)
            {
                Console.WriteLine($"✅ 策略激活成功: {strategy.Name}");
                await RefreshAsync();
                return true;
            }
            else
            {
                Console.WriteLine($"❌ 策略激活失败: {strategy.Name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 激活策略异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 废弃策略
    /// </summary>
    public async Task<bool> DeprecateStrategyAsync(StrategyInfo strategy, string reason)
    {
        try
        {
            Console.WriteLine($"🔄 废弃策略: {strategy.Name}, 原因: {reason}");
            
            var success = await _strategyService.DeprecateStrategyAsync(strategy.Id, reason);
            
            if (success)
            {
                Console.WriteLine($"✅ 策略废弃成功: {strategy.Name}");
                await RefreshAsync();
                return true;
            }
            else
            {
                Console.WriteLine($"❌ 策略废弃失败: {strategy.Name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 废弃策略异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 归档策略
    /// </summary>
    public async Task<bool> ArchiveStrategyAsync(StrategyInfo strategy, string reason)
    {
        try
        {
            Console.WriteLine($"🔄 归档策略: {strategy.Name}, 原因: {reason}");
            
            var success = await _strategyService.ArchiveStrategyAsync(strategy.Id, reason);
            
            if (success)
            {
                Console.WriteLine($"✅ 策略归档成功: {strategy.Name}");
                await RefreshAsync();
                return true;
            }
            else
            {
                Console.WriteLine($"❌ 策略归档失败: {strategy.Name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 归档策略异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从异常状态恢复策略
    /// </summary>
    public async Task<bool> RecoverStrategyAsync(StrategyInfo strategy)
    {
        try
        {
            Console.WriteLine($"🔄 恢复策略: {strategy.Name}");
            
            var success = await _strategyService.RecoverStrategyFromAnomalyAsync(strategy.Id);
            
            if (success)
            {
                Console.WriteLine($"✅ 策略恢复成功: {strategy.Name}");
                await RefreshAsync();
                return true;
            }
            else
            {
                Console.WriteLine($"❌ 策略恢复失败: {strategy.Name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 恢复策略异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    public async Task<bool> DeleteStrategyAsync(StrategyInfo strategy)
    {
        try
        {
            Console.WriteLine($"🔄 删除策略: {strategy.Name}");
            
            var success = await _strategyService.DeleteStrategyAsync(strategy.Id);
            
            if (success)
            {
                Console.WriteLine($"✅ 策略删除成功: {strategy.Name}");
                await RefreshAsync();
                return true;
            }
            else
            {
                Console.WriteLine($"❌ 策略删除失败: {strategy.Name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 删除策略异常: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

