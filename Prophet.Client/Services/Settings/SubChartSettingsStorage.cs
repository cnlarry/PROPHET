using Prophet.Client.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prophet.Client.Services.Settings;

/// <summary>
/// 副图设置存储服务 - 负责保存和加载用户的副图配置
/// </summary>
public class SubChartSettingsStorage
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Prophet",
        "Settings"
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "SubChartSettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 保存副图设置到本地
    /// </summary>
    public static bool SaveSettings(SubChartSettings settings)
    {
        try
        {
            // 确保目录存在
            Directory.CreateDirectory(SettingsDirectory);

            // 序列化为JSON
            var json = JsonSerializer.Serialize(settings, JsonOptions);

            // 写入文件
            File.WriteAllText(SettingsFilePath, json);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存副图设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从本地加载副图设置
    /// </summary>
    public static SubChartSettings? LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return null;
            }

            // 读取文件
            var json = File.ReadAllText(SettingsFilePath);

            // 反序列化
            var settings = JsonSerializer.Deserialize<SubChartSettings>(json, JsonOptions);

            if (settings != null)
            {
            }

            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载副图设置失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 清除本地保存的副图设置
    /// </summary>
    public static bool ClearSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存副图设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查本地是否存在副图设置文件
    /// </summary>
    public static bool SettingsExists()
    {
        return File.Exists(SettingsFilePath);
    }
}

