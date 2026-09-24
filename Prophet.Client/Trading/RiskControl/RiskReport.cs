using System;

namespace Prophet.Client.Trading.RiskControl;

/// <summary>
/// 风险报告
/// 包含完整的风险评估信息
/// </summary>
public class RiskReport
{
    // 基本信息
    public DateTime GeneratedAt { get; set; }
    public bool EmergencyStopActivated { get; set; }
    public RiskLevel RiskLevel { get; set; }
    
    // 账户信息
    public decimal CurrentEquity { get; set; }
    public decimal PeakEquity { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal TotalMargin { get; set; }
    
    // 回撤指标
    public decimal CurrentDrawdown { get; set; }
    public decimal MaxDrawdown { get; set; }
    
    // 仓位指标
    public int ActivePositions { get; set; }
    public decimal TotalRiskPercent { get; set; }
    
    // 交易统计
    public int TodayTrades { get; set; }
    public int ConsecutiveLosses { get; set; }
    public decimal WinRate { get; set; }
    
    /// <summary>
    /// 生成报告文本
    /// </summary>
    public string GenerateReport()
    {
        return $@"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    风险评估报告
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📅 生成时间: {GeneratedAt:yyyy-MM-dd HH:mm:ss}
⚡ 风险等级: {GetRiskLevelEmoji()} {RiskLevel}
🚨 紧急停止: {(EmergencyStopActivated ? "已激活" : "未激活")}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    账户状态
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

💰 当前权益: {CurrentEquity:F2} USDT
📈 峰值权益: {PeakEquity:F2} USDT
💵 可用余额: {AvailableBalance:F2} USDT
🔒 占用保证金: {TotalMargin:F2} USDT

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    回撤指标
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📉 当前回撤: {CurrentDrawdown:P2} {GetDrawdownWarning(CurrentDrawdown)}
📊 最大回撤: {MaxDrawdown:P2}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    仓位状态
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📦 活跃仓位: {ActivePositions}
⚖️ 总风险占比: {TotalRiskPercent:P2}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    交易统计
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📅 今日交易: {TodayTrades}
❌ 连续亏损: {ConsecutiveLosses} {GetConsecutiveLossWarning(ConsecutiveLosses)}
✅ 胜率: {WinRate:P2}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    风险建议
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

{GetRiskAdvice()}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
";
    }
    
    private string GetRiskLevelEmoji()
    {
        return RiskLevel switch
        {
            RiskLevel.Low => "🟢",
            RiskLevel.Medium => "🟡",
            RiskLevel.High => "🟠",
            RiskLevel.Critical => "🔴",
            _ => "⚪"
        };
    }
    
    private string GetDrawdownWarning(decimal drawdown)
    {
        if (drawdown > 0.12m)
            return "⚠️ 回撤过高！";
        if (drawdown > 0.08m)
            return "⚠️ 注意风险";
        return "";
    }
    
    private string GetConsecutiveLossWarning(int losses)
    {
        if (losses >= 5)
            return "⚠️ 建议暂停交易";
        if (losses >= 3)
            return "⚠️ 注意连续亏损";
        return "";
    }
    
    private string GetRiskAdvice()
    {
        var advice = new System.Text.StringBuilder();
        
        if (RiskLevel == RiskLevel.Critical)
        {
            advice.AppendLine("🚨 风险等级严重！建议立即采取以下措施：");
            advice.AppendLine("   • 停止所有新开仓操作");
            advice.AppendLine("   • 考虑平掉部分或全部仓位");
            advice.AppendLine("   • 检查策略是否需要调整");
        }
        else if (RiskLevel == RiskLevel.High)
        {
            advice.AppendLine("⚠️ 风险等级较高，建议：");
            advice.AppendLine("   • 减少仓位大小");
            advice.AppendLine("   • 严格执行止损");
            advice.AppendLine("   • 避免过度交易");
        }
        else if (RiskLevel == RiskLevel.Medium)
        {
            advice.AppendLine("ℹ️ 风险等级中等，建议：");
            advice.AppendLine("   • 保持当前风险控制策略");
            advice.AppendLine("   • 密切监控市场变化");
            advice.AppendLine("   • 适度控制仓位");
        }
        else
        {
            advice.AppendLine("✅ 风险等级较低，可以正常交易");
            advice.AppendLine("   • 继续执行当前策略");
            advice.AppendLine("   • 保持良好的风险管理习惯");
        }
        
        // 特定建议
        if (ConsecutiveLosses >= 3)
        {
            advice.AppendLine();
            advice.AppendLine("💡 连续亏损提醒：");
            advice.AppendLine($"   已连续亏损 {ConsecutiveLosses} 次，建议暂停交易，重新评估策略");
        }
        
        if (CurrentDrawdown > 0.10m)
        {
            advice.AppendLine();
            advice.AppendLine("💡 回撤提醒：");
            advice.AppendLine($"   当前回撤 {CurrentDrawdown:P2}，建议控制风险，避免进一步扩大");
        }
        
        if (ActivePositions >= 3)
        {
            advice.AppendLine();
            advice.AppendLine("💡 仓位提醒：");
            advice.AppendLine($"   当前有 {ActivePositions} 个活跃仓位，注意分散风险");
        }
        
        return advice.ToString();
    }
}

