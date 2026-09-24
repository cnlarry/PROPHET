using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Prophet.Client.Models;

namespace Prophet.Client.ViewModels;

/// <summary>
/// 策略版本管理ViewModel（离线版 - 已禁用）
/// </summary>
public class StrategyVersionViewModel : INotifyPropertyChanged
{
    // 离线版暂不支持版本管理
    private string _strategyId = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;
    private VersionListItem? _selectedVersion;

    public StrategyVersionViewModel()
    {
        Versions = new ObservableCollection<VersionListItem>();
    }

    #region 属性

    public string StrategyId
    {
        get => _strategyId;
        set
        {
            if (_strategyId != value)
            {
                _strategyId = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<VersionListItem> Versions { get; }

    public VersionListItem? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (_selectedVersion != value)
            {
                _selectedVersion = value;
                OnPropertyChanged();
            }
        }
    }

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

    #endregion

    #region 方法

    /// <summary>
    /// 加载版本列表
    /// </summary>
    public async Task LoadVersionsAsync()
    {
        if (string.IsNullOrEmpty(StrategyId))
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // 离线版：暂不支持版本管理
            ErrorMessage = "离线版暂不支持版本管理功能";
            await Task.CompletedTask; // 消除警告
            // var versions = await _apiClient.GetStrategyVersionsAsync(StrategyId);
            // 
            // Versions.Clear();
            // foreach (var version in versions)
            // {
            //     Versions.Add(version);
            // }
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
    /// 创建新版本
    /// </summary>
    public async Task<bool> CreateVersionAsync(
        string changeType,
        string dsl,
        string? changeDescription,
        string? breakingChanges = null)
    {
        if (string.IsNullOrEmpty(StrategyId))
            return false;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var request = new CreateVersionRequest
            {
                ChangeType = changeType,
                Dsl = dsl,
                Status = "active",
                ChangeDescription = changeDescription,
                BreakingChanges = breakingChanges
            };

            // 离线版：暂不支持版本管理
            ErrorMessage = "离线版暂不支持创建版本";
            await Task.CompletedTask; // 消除警告
            return false;
            
            // var result = await _apiClient.CreateStrategyVersionAsync(StrategyId, request);
            // if (result != null)
            // {
            //     await LoadVersionsAsync(); // 刷新列表
            //     return true;
            // }
            //
            // ErrorMessage = "创建版本失败";
            // return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"创建失败: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
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

