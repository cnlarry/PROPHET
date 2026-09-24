using Prophet.Client.Models;
using System;

namespace Prophet.Client.Services.Settings;

/// <summary>
/// 应用程序设置服务 - 提供全局访问设置
/// </summary>
public class AppSettingsService
{
    private AppSettings _settings;

    /// <summary>
    /// 设置变更事件
    /// </summary>
    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettingsService()
    {
        // 加载设置
        _settings = AppSettingsStorage.LoadSettings();
    }

    /// <summary>
    /// 获取当前设置
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// 更新设置
    /// </summary>
    public bool UpdateSettings(AppSettings newSettings)
    {
        try
        {
            _settings = newSettings;
            
            // 保存到文件
            if (AppSettingsStorage.SaveSettings(_settings))
            {
                // 触发设置变更事件
                SettingsChanged?.Invoke(this, _settings);
                Console.WriteLine($"✅ [AppSettingsService] 设置已更新");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AppSettingsService] 更新设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 重新加载设置
    /// </summary>
    public void ReloadSettings()
    {
        _settings = AppSettingsStorage.LoadSettings();
        SettingsChanged?.Invoke(this, _settings);
    }

    /// <summary>
    /// 获取涨的颜色
    /// </summary>
    public string GetRisingColor() => _settings.GetRisingColor();

    /// <summary>
    /// 获取跌的颜色
    /// </summary>
    public string GetFallingColor() => _settings.GetFallingColor();

    /// <summary>
    /// 获取时区
    /// </summary>
    public TimeZoneInfo GetTimeZone() => _settings.GetTimeZone();
}
