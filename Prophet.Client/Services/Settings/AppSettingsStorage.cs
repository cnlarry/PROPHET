using Prophet.Client.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prophet.Client.Services.Settings;

/// <summary>
/// 应用程序设置存储服务 - 负责保存和加载全局配置
/// </summary>
public static class AppSettingsStorage
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Prophet",
        "Settings"
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "AppSettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 保存应用程序设置到本地
    /// </summary>
    public static bool SaveSettings(AppSettings settings)
    {
        try
        {
            // 确保目录存在
            Directory.CreateDirectory(SettingsDirectory);

            // 序列化为JSON
            var json = JsonSerializer.Serialize(settings, JsonOptions);

            // 写入文件
            File.WriteAllText(SettingsFilePath, json);

            Console.WriteLine($"✅ [AppSettingsStorage] 设置已保存到: {SettingsFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AppSettingsStorage] 保存设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从本地加载应用程序设置
    /// </summary>
    public static AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                Console.WriteLine($"ℹ️ [AppSettingsStorage] 设置文件不存在，创建默认设置文件");
                // 首次启动时，创建默认设置文件，避免每次启动都提示
                var defaultSettings = new AppSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }

            // 读取文件
            var json = File.ReadAllText(SettingsFilePath);

            // 反序列化
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

            if (settings != null)
            {
                Console.WriteLine($"✅ [AppSettingsStorage] 设置已加载: {SettingsFilePath}");
                return settings;
            }

            // 如果反序列化失败，创建默认设置文件
            Console.WriteLine($"⚠️ [AppSettingsStorage] 设置文件格式错误，使用默认设置并重新创建文件");
            var fallbackSettings = new AppSettings();
            SaveSettings(fallbackSettings);
            return fallbackSettings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AppSettingsStorage] 加载设置失败: {ex.Message}");
            // 发生异常时，尝试创建默认设置文件
            try
            {
                var defaultSettings = new AppSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }
            catch
            {
                // 如果保存也失败，直接返回默认设置
                return new AppSettings();
            }
        }
    }

    /// <summary>
    /// 清除本地保存的设置
    /// </summary>
    public static bool ClearSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
                Console.WriteLine($"✅ [AppSettingsStorage] 设置已清除");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AppSettingsStorage] 清除设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查本地是否存在设置文件
    /// </summary>
    public static bool SettingsExists()
    {
        return File.Exists(SettingsFilePath);
    }
}
