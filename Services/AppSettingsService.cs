using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using clipboard.Models;

namespace clipboard.Services;

/// <summary>
/// 应用设置服务，负责加载和保存应用配置
/// </summary>
public class AppSettingsService
{
    private readonly string _settingsFilePath;
    private AppSettings _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AppSettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipboardApp"
        );
        Directory.CreateDirectory(appDataPath);
        _settingsFilePath = Path.Combine(appDataPath, "app_settings.json");
        _settings = LoadSettings();
    }

    public AppSettings GetSettings()
    {
        return _settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _lock.WaitAsync();
        try
        {
            _settings = settings;
            var json = JsonSerializer.Serialize(_settings, AppSettingsJsonContext.Default.AppSettings);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
        
        // 返回默认设置
        return new AppSettings();
    }
}

