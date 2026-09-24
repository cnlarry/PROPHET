using Prophet.Client.Models;
using Prophet.Client.Views.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prophet.Client.Services.Settings;

/// <summary>
/// 指标设置存储服务 - 负责保存和加载用户的指标配置
/// </summary>
public class IndicatorSettingsStorage
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Prophet",
        "Settings"
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "IndicatorSettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 保存指标设置到本地
    /// </summary>
    public static bool SaveSettings(IndicatorDialog.IndicatorSettings settings)
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
            System.Diagnostics.Debug.WriteLine($"保存指标设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从本地加载指标设置
    /// </summary>
    public static IndicatorDialog.IndicatorSettings? LoadSettings()
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
            var settings = JsonSerializer.Deserialize<IndicatorDialog.IndicatorSettings>(json, JsonOptions);

            if (settings != null)
            {
            }

            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载指标设置失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 清除本地保存的指标设置
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
            System.Diagnostics.Debug.WriteLine($"保存指标设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查本地是否存在指标设置文件
    /// </summary>
    public static bool SettingsExists()
    {
        return File.Exists(SettingsFilePath);
    }
}

