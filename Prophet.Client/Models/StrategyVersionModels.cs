using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Prophet.Client.Models;

/// <summary>
/// 策略版本
/// </summary>
public class StrategyVersion
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("strategy_id")]
    public string StrategyId { get; set; } = string.Empty;
    
    [JsonPropertyName("version_string")]
    public string VersionString { get; set; } = string.Empty;
    
    [JsonPropertyName("major_version")]
    public int MajorVersion { get; set; }
    
    [JsonPropertyName("minor_version")]
    public int MinorVersion { get; set; }
    
    [JsonPropertyName("patch_version")]
    public int PatchVersion { get; set; }
    
    [JsonPropertyName("change_type")]
    public string ChangeType { get; set; } = string.Empty;
    
    [JsonPropertyName("dsl")]
    public string Dsl { get; set; } = string.Empty;
    
    [JsonPropertyName("parameters")]
    public string? Parameters { get; set; }
    
    [JsonPropertyName("dsl_hash")]
    public string? DslHash { get; set; }
    
    // 版本状态相关
    [JsonPropertyName("version_status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("status_changed_at")]
    public string? StatusChangedAt { get; set; }
    
    // 验证相关
    [JsonPropertyName("validation_status")]
    public string? ValidationStatus { get; set; }
    
    [JsonPropertyName("validation_message")]
    public string? ValidationMessage { get; set; }
    
    [JsonPropertyName("validated_at")]
    public string? ValidatedAt { get; set; }
    
    // 回测相关
    [JsonPropertyName("backtest_data")]
    public string? BacktestData { get; set; }
    
    [JsonPropertyName("backtest_completed_at")]
    public string? BacktestCompletedAt { get; set; }
    
    // 变更说明
    [JsonPropertyName("change_description")]
    public string? ChangeDescription { get; set; }
    
    [JsonPropertyName("breaking_changes")]
    public string? BreakingChanges { get; set; }
    
    // 风险和标签
    [JsonPropertyName("risk_disclosure")]
    public string? RiskDisclosure { get; set; }
    
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }
    
    // 废弃和升级
    [JsonPropertyName("deprecation_reason")]
    public string? DeprecationReason { get; set; }
    
    [JsonPropertyName("force_upgrade")]
    public bool ForceUpgrade { get; set; }
    
    [JsonPropertyName("upgrade_deadline")]
    public string? UpgradeDeadline { get; set; }
    
    // 审计字段
    [JsonPropertyName("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("activated_at")]
    public string? ActivatedAt { get; set; }
    
    [JsonPropertyName("active_users")]
    public int? ActiveUsers { get; set; }

    // UI辅助属性
    public string StatusDisplay => Status switch
    {
        "active" => "激活",
        "inactive" => "非活跃",
        "draft" => "草稿",
        _ => Status
    };

    public string ChangeTypeDisplay => ChangeType switch
    {
        "major" => "重大变更",
        "minor" => "功能增强",
        "patch" => "问题修复",
        "initial" => "初始版本",
        _ => ChangeType
    };
    
    public string ValidationStatusDisplay => ValidationStatus switch
    {
        "valid" => "✓ 验证通过",
        "invalid" => "✗ 验证失败",
        "pending" => "⏳ 待验证",
        _ => "未知"
    };
}

/// <summary>
/// 版本列表项（简化版）
/// </summary>
public class VersionListItem
{
    [JsonPropertyName("version_string")]
    public string VersionString { get; set; } = string.Empty;
    
    [JsonPropertyName("change_type")]
    public string ChangeType { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("change_description")]
    public string? ChangeDescription { get; set; }
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("active_users")]
    public int ActiveUsers { get; set; }

    // UI辅助属性
    public string StatusDisplay => Status switch
    {
        "active" => "✓ 激活",
        "deprecated" => "⚠ 已废弃",
        "archived" => "✗ 已归档",
        "draft" => "📝 草稿",
        _ => Status
    };
}

/// <summary>
/// 策略实例
/// </summary>
public class StrategyInstance
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("strategy_id")]
    public string StrategyId { get; set; } = string.Empty;
    
    [JsonPropertyName("strategy_name")]
    public string? StrategyName { get; set; }
    
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("locked_version")]
    public string LockedVersion { get; set; } = string.Empty;
    
    [JsonPropertyName("current_version")]
    public string? CurrentVersion { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;
    
    [JsonPropertyName("has_newer_version")]
    public bool HasNewerVersion { get; set; }
    
    [JsonPropertyName("force_upgrade_required")]
    public bool ForceUpgradeRequired { get; set; }
    
    [JsonPropertyName("upgrade_notified_at")]
    public string? UpgradeNotifiedAt { get; set; }
    
    [JsonPropertyName("version_status")]
    public string? VersionStatus { get; set; }
    
    [JsonPropertyName("dsl")]
    public string? Dsl { get; set; }
    
    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("last_executed_at")]
    public string? LastExecutedAt { get; set; }

    // UI辅助属性
    public bool ShowUpgradeNotice => HasNewerVersion || ForceUpgradeRequired;
    public string UpgradeNoticeMessage
    {
        get
        {
            if (ForceUpgradeRequired)
                return $"⚠ 强制升级：当前版本已不可用，请升级到 {CurrentVersion}";
            if (HasNewerVersion)
                return $"💡 有新版本 {CurrentVersion} 可用";
            return string.Empty;
        }
    }
}

/// <summary>
/// 创建版本请求
/// </summary>
public class CreateVersionRequest
{
    [JsonPropertyName("change_type")]
    public string ChangeType { get; set; } = "patch";
    
    [JsonPropertyName("dsl")]
    public string Dsl { get; set; } = string.Empty;
    
    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";
    
    [JsonPropertyName("change_description")]
    public string? ChangeDescription { get; set; }
    
    [JsonPropertyName("breaking_changes")]
    public string? BreakingChanges { get; set; }
}

/// <summary>
/// 订阅策略请求
/// </summary>
public class SubscribeStrategyRequest
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "paper";
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// 升级版本请求
/// </summary>
public class UpgradeVersionRequest
{
    [JsonPropertyName("target_version")]
    public string TargetVersion { get; set; } = string.Empty;
}

/// <summary>
/// API响应包装
/// </summary>
public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("data")]
    public T? Data { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// 策略信息（用于列表显示）
/// </summary>
public class StrategyInfo : INotifyPropertyChanged
{
    private bool _isSelected;
    
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Dsl { get; set; } = string.Empty;
    public string? Remarks { get; set; }  // 备注信息
    public string? Description { get; set; }  // 策略描述
    public string? StrategyType { get; set; }  // 策略类型（趋势跟踪、网格等）
    public string? RiskLevel { get; set; }  // 风险等级（low/medium/high）
    public int? QualityScore { get; set; }  // 质量评分 (0-100)，可空
    public string Status { get; set; } = "draft"; // draft, active, deprecated, archived, anomaly
    public string? StatusChangedAt { get; set; }  // 状态变更时间
    public string? AnomalyReason { get; set; }  // 异常原因
    public bool Enabled { get; set; } = false;  // 是否已激活（保留兼容）
    public bool Public { get; set; } = false;  // 是否公开共享
    public int TotalSubscribers { get; set; }  // 订阅人数
    public int TotalBacktestCount { get; set; }  // 回测次数
    public string? LastBacktestAt { get; set; }  // 最后回测时间
    public string UpdatedAt { get; set; } = string.Empty;
    public string Version { get; set; } = "v1.0.0";
    public string LastCompiledAt { get; set; } = string.Empty;  // 最后编译时间（版本创建时间）
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    // UI辅助属性
    public string StatusDisplay => Status switch
    {
        "active" => "✅ 激活",
        "draft" => "📝 草稿",
        "deprecated" => "⚠️ 废弃",
        "archived" => "📦 归档",
        "anomaly" => "🔴 异常",
        _ => "📝 草稿"
    };
    
    public string StatusIcon => Status switch
    {
        "active" => "✅",
        "draft" => "📝",
        "deprecated" => "⚠️",
        "archived" => "📦",
        "anomaly" => "🔴",
        _ => "📝"
    };
    
    public string StatusColor => Status switch
    {
        "active" => "#4CAF50",      // 绿色
        "draft" => "#9E9E9E",       // 灰色
        "deprecated" => "#FF9800",  // 橙色
        "archived" => "#616161",    // 深灰
        "anomaly" => "#F44336",     // 红色
        _ => "#9E9E9E"
    };
    
    /// <summary>
    /// 风险等级显示
    /// </summary>
    public string RiskLevelDisplay => RiskLevel switch
    {
        "low" => "低风险",
        "medium-low" => "中低风险",
        "medium" => "中等风险",
        "medium-high" => "中高风险",
        "high" => "高风险",
        _ => "未评估"
    };
    
    /// <summary>
    /// 风险等级颜色
    /// </summary>
    public string RiskLevelColor => RiskLevel switch
    {
        "low" => "#4CAF50",
        "medium-low" => "#8BC34A",
        "medium" => "#FF9800",
        "medium-high" => "#FF5722",
        "high" => "#F44336",
        _ => "#9E9E9E"
    };
    
    /// <summary>
    /// 质量评分显示
    /// </summary>
    public string QualityScoreDisplay => QualityScore.HasValue ? $"{QualityScore.Value}/100" : "未评分";
    
    /// <summary>
    /// 是否可以编辑
    /// </summary>
    public bool CanEdit => Status == "draft" || Status == "active" || Status == "anomaly";
    
    /// <summary>
    /// 是否可以删除
    /// </summary>
    public bool CanDelete => Status == "draft" || Status == "archived";
    
    /// <summary>
    /// 是否可以激活
    /// </summary>
    public bool CanActivate => Status == "draft";
    
    /// <summary>
    /// 是否可以废弃
    /// </summary>
    public bool CanDeprecate => Status == "active";
    
    /// <summary>
    /// 是否可以归档
    /// </summary>
    public bool CanArchive => Status != "archived";
    
    /// <summary>
    /// 是否可以公开分享
    /// </summary>
    public bool CanShare => Status == "active";
    
    /// <summary>
    /// 是否可以回测
    /// </summary>
    public bool CanBacktest => Status != "archived";

    public string DisplayName => $"{StatusIcon} {Name}";
    
    /// <summary>
    /// 是否可以重命名（只有草稿状态可以重命名）
    /// </summary>
    public bool CanRename => Status == "draft";
    
    /// <summary>
    /// 是否可以取消共享（只有已激活且已公开的策略可以取消共享）
    /// </summary>
    public bool CanUnshare => Status == "active" && Public;
    
    public string TimeAgo
    {
        get
        {
            // 优先使用最后编译时间，如果没有则使用更新时间
            var timeString = !string.IsNullOrEmpty(LastCompiledAt) ? LastCompiledAt : UpdatedAt;
            
            if (DateTime.TryParse(timeString, out var date))
            {
                var diff = DateTime.Now - date;
                if (diff.TotalMinutes < 1) return "刚刚";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}分钟前";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}小时前";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}天前";
                return date.ToString("yyyy-MM-dd");
            }
            return timeString;
        }
    }
}

