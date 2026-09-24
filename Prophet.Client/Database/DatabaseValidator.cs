using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Prophet.Client.Database;

/// <summary>
/// 数据库验证工具
/// 用于启动时检查关键表是否存在，确保数据库结构完整
/// </summary>
public static class DatabaseValidator
{
    /// <summary>
    /// 关键表清单（必须存在，否则应用无法正常运行）
    /// </summary>
    private static readonly string[] CriticalTables =
    {
        // 核心配置表
        "app_settings",
        "schema_migrations",
        
        // 标的与策略表
        "instruments",
        "strategies",
        "strategy_versions",
        
        // 回测核心表
        "backtest_configs",
        "backtest_runs",
        "backtest_orders",
        
        // 市场数据表
        "klines",
        "fundingrate",
    };

    /// <summary>
    /// 可选表清单（不存在不影响核心功能，但会影响特定功能）
    /// </summary>
    private static readonly string[] OptionalTables =
    {
        "backtest_signals",
        "backtest_order_signals",
        "backtest_drawdown_periods",
        "backtest_equity_curve",
        "fear_greed_index",
        "longshortratio",
        "trading_instances",
        "instance_snapshots",
        "live_orders",
        "live_sessions",
    };

    /// <summary>
    /// 验证关键表是否存在
    /// </summary>
    /// <returns>验证结果</returns>
    public static async Task<ValidationResult> ValidateCriticalTablesAsync()
    {
        try
        {
            using var connection = DBHelper.CreateConnection();
            
            var existingTables = await GetExistingTablesAsync(connection);
            var missingTables = CriticalTables.Where(t => !existingTables.Contains(t)).ToList();
            
            if (missingTables.Count > 0)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"关键表缺失: {string.Join(", ", missingTables)}",
                    MissingCriticalTables = missingTables
                };
            }
            
            return new ValidationResult
            {
                IsValid = true,
                Message = $"所有关键表已就绪（{CriticalTables.Length} 张表）"
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"数据库验证失败: {ex.Message}",
                Exception = ex
            };
        }
    }

    /// <summary>
    /// 验证所有表（包括可选表）
    /// </summary>
    public static async Task<ValidationResult> ValidateAllTablesAsync()
    {
        try
        {
            using var connection = DBHelper.CreateConnection();
            
            var existingTables = await GetExistingTablesAsync(connection);
            
            var allTables = CriticalTables.Concat(OptionalTables).ToArray();
            var missingCriticalTables = CriticalTables.Where(t => !existingTables.Contains(t)).ToList();
            var missingOptionalTables = OptionalTables.Where(t => !existingTables.Contains(t)).ToList();
            
            if (missingCriticalTables.Count > 0)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"关键表缺失: {string.Join(", ", missingCriticalTables)}",
                    MissingCriticalTables = missingCriticalTables,
                    MissingOptionalTables = missingOptionalTables
                };
            }
            
            var message = missingOptionalTables.Count > 0
                ? $"关键表完整，可选表缺失 {missingOptionalTables.Count} 张: {string.Join(", ", missingOptionalTables)}"
                : $"所有表已就绪（{allTables.Length} 张表）";
            
            return new ValidationResult
            {
                IsValid = true,
                Message = message,
                MissingOptionalTables = missingOptionalTables
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"数据库验证失败: {ex.Message}",
                Exception = ex
            };
        }
    }

    /// <summary>
    /// 获取数据库中现有的表列表
    /// </summary>
    private static async Task<HashSet<string>> GetExistingTablesAsync(System.Data.IDbConnection connection)
    {
        const string sql = @"
            SELECT name 
            FROM sqlite_master 
            WHERE type='table' 
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
        ";
        
        var tables = await connection.QueryAsync<string>(sql);
        return new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 打印数据库状态报告
    /// </summary>
    public static async Task<string> GenerateStatusReportAsync()
    {
        try
        {
            using var connection = DBHelper.CreateConnection();
            
            var existingTables = await GetExistingTablesAsync(connection);
            var allTables = CriticalTables.Concat(OptionalTables).ToArray();
            
            var report = new System.Text.StringBuilder();
            report.AppendLine("=".PadRight(60, '='));
            report.AppendLine("数据库状态报告");
            report.AppendLine("=".PadRight(60, '='));
            report.AppendLine();
            
            report.AppendLine($"数据库路径: {DBHelper.DatabasePath}");
            report.AppendLine($"已存在表数量: {existingTables.Count}");
            report.AppendLine();
            
            // 关键表状态
            report.AppendLine("【关键表】");
            foreach (var table in CriticalTables)
            {
                var status = existingTables.Contains(table) ? "✅" : "❌";
                report.AppendLine($"  {status} {table}");
            }
            report.AppendLine();
            
            // 可选表状态
            report.AppendLine("【可选表】");
            foreach (var table in OptionalTables)
            {
                var status = existingTables.Contains(table) ? "✅" : "⚠️";
                report.AppendLine($"  {status} {table}");
            }
            report.AppendLine();
            
            // 额外的表（不在预期清单中）
            var extraTables = existingTables
                .Where(t => !allTables.Contains(t, StringComparer.OrdinalIgnoreCase))
                .ToList();
            
            if (extraTables.Count > 0)
            {
                report.AppendLine("【额外的表】");
                foreach (var table in extraTables)
                {
                    report.AppendLine($"  ℹ️ {table}");
                }
                report.AppendLine();
            }
            
            // 应用迁移状态
            var migrationCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM schema_migrations"
            );
            report.AppendLine($"已应用迁移数量: {migrationCount}");
            
            report.AppendLine("=".PadRight(60, '='));
            
            return report.ToString();
        }
        catch (Exception ex)
        {
            return $"生成报告失败: {ex.Message}";
        }
    }
}

/// <summary>
/// 数据库验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 是否验证通过
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 成功消息
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 缺失的关键表
    /// </summary>
    public List<string> MissingCriticalTables { get; set; } = new();
    
    /// <summary>
    /// 缺失的可选表
    /// </summary>
    public List<string> MissingOptionalTables { get; set; } = new();
    
    /// <summary>
    /// 异常信息
    /// </summary>
    public Exception? Exception { get; set; }
}

