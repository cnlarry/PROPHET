using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Prophet.Client.Backtest.Models;

namespace Prophet.Client.ViewModels;

/// <summary>
/// 分步回测配置向导 ViewModel
/// 继承自 BacktestConfigDialogViewModel，增加分步导航功能
/// </summary>
public class StepWizardBacktestConfigViewModel : BacktestConfigDialogViewModel
{
    private int _currentStep = 1;
    
    /// <summary>
    /// 当前步骤（1-3）
    /// </summary>
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep != value)
            {
                _currentStep = value;
                OnPropertyChanged();
                CurrentStepChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    /// <summary>
    /// 当前步骤变化事件
    /// </summary>
    public event EventHandler? CurrentStepChanged;
    
    /// <summary>
    /// 前往下一步
    /// </summary>
    public void GoToNextStep()
    {
        if (!ValidateCurrentStep())
        {
            return;
        }
        
        if (CurrentStep < 3)
        {
            CurrentStep++;
            ValidationMessage = string.Empty; // 清空验证消息
        }
    }
    
    /// <summary>
    /// 返回上一步
    /// </summary>
    public void GoToPreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            ValidationMessage = string.Empty; // 清空验证消息
        }
    }
    
    /// <summary>
    /// 验证当前步骤的参数
    /// </summary>
    private bool ValidateCurrentStep()
    {
        switch (CurrentStep)
        {
            case 1:
                return ValidateStep1();
            case 2:
                return ValidateStep2();
            case 3:
                return ValidateStep3();
            default:
                return true;
        }
    }
    
    /// <summary>
    /// 验证第一步：策略与标的
    /// </summary>
    private bool ValidateStep1()
    {
        // 验证策略和版本
        if (SelectedStrategy == null)
        {
            ValidationMessage = "请选择策略";
            return false;
        }
        
        if (SelectedVersion == null)
        {
            ValidationMessage = "请选择策略版本";
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(SelectedSymbolKey))
        {
            ValidationMessage = "请选择标的";
            return false;
        }
        
        // 验证日期范围
        if (EndDate <= StartDate)
        {
            ValidationMessage = "结束日期必须晚于开始日期";
            return false;
        }
        
        // 验证结束日期不能是今天或未来
        if (EndDate.DateTime.Date >= DateTime.UtcNow.Date)
        {
            ValidationMessage = "结束日期不能选择今天或未来日期（历史数据未生成）";
            return false;
        }
        
        // 验证日期跨度（至少1天）
        var dateSpan = (EndDate.DateTime - StartDate.DateTime).TotalDays;
        if (dateSpan < 1)
        {
            ValidationMessage = "回测时间跨度至少需要1天";
            return false;
        }
        
        // 验证日期跨度不要太长（避免性能问题）
        if (dateSpan > 365)
        {
            ValidationMessage = "回测时间跨度不能超过1年（365天）";
            return false;
        }
        
        ValidationMessage = string.Empty;
        return true;
    }
    
    /// <summary>
    /// 验证第二步：资金与风控
    /// </summary>
    private bool ValidateStep2()
    {
        // 验证初始资金
        if (InitialCapital < 100)
        {
            ValidationMessage = "初始资金不能低于100 USDT";
            return false;
        }
        
        if (InitialCapital > 10000000)
        {
            ValidationMessage = "初始资金不能超过1000万 USDT";
            return false;
        }
        
        // 验证杠杆倍数
        if (Leverage < 1 || Leverage > 125)
        {
            ValidationMessage = "杠杆倍数必须在1-125倍之间";
            return false;
        }
        
        // 验证仓位比例
        if (PositionSizePercent <= 0 || PositionSizePercent > 1)
        {
            ValidationMessage = "仓位比例必须在0-100%之间";
            return false;
        }
        
        // 验证手续费率
        if (FeeRate < 0 || FeeRate > 0.01m)
        {
            ValidationMessage = "手续费率必须在0-1%之间";
            return false;
        }
        
        // 验证滑点率
        if (SlippageRate < 0 || SlippageRate > 0.01m)
        {
            ValidationMessage = "滑点率必须在0-1%之间";
            return false;
        }
        
        // 验证止盈止损比例
        if (DefaultTakeProfitPercent <= 0 || DefaultTakeProfitPercent > 1)
        {
            ValidationMessage = "默认止盈比例必须在0-100%之间";
            return false;
        }
        
        if (DefaultStopLossPercent <= 0 || DefaultStopLossPercent > 1)
        {
            ValidationMessage = "默认止损比例必须在0-100%之间";
            return false;
        }
        
        // 止盈必须大于止损
        if (DefaultTakeProfitPercent <= DefaultStopLossPercent)
        {
            ValidationMessage = "默认止盈比例必须大于止损比例";
            return false;
        }
        
        ValidationMessage = string.Empty;
        return true;
    }
    
    /// <summary>
    /// 验证第三步：高级参数
    /// </summary>
    private bool ValidateStep3()
    {
        // 验证ATR参数
        if (ATRPeriod < 5 || ATRPeriod > 100)
        {
            ValidationMessage = "ATR周期必须在5-100之间";
            return false;
        }
        
        if (ATRMultiplier < 0.1m || ATRMultiplier > 10)
        {
            ValidationMessage = "ATR止损倍数必须在0.1-10之间";
            return false;
        }
        
        if (RiskPercentPerTrade < 0.001m || RiskPercentPerTrade > 0.1m)
        {
            ValidationMessage = "每笔交易风险必须在0.1%-10%之间";
            return false;
        }
        
        // 验证Kelly参数
        if (MaxKellyFraction < 0.05m || MaxKellyFraction > 1)
        {
            ValidationMessage = "最大Kelly比例必须在5%-100%之间";
            return false;
        }
        
        ValidationMessage = string.Empty;
        return true;
    }
    
    /// <summary>
    /// 重写验证方法，调用完整验证
    /// </summary>
    public new bool TryBuildRequest(out BacktestConfigRequest? request)
    {
        // 验证所有步骤
        if (!ValidateStep1())
        {
            request = null;
            return false;
        }
        
        if (!ValidateStep2())
        {
            request = null;
            return false;
        }
        
        if (!ValidateStep3())
        {
            request = null;
            return false;
        }
        
        // 调用基类方法构建请求
        return base.TryBuildRequest(out request);
    }
    
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
    }
}

