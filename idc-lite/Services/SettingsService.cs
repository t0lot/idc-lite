using System.Text.Json;
using idc_lite.Models;

namespace idc_lite.Services;

/// <summary>
/// Сервис сохранения/загрузки настроек в %AppData%/idc-lite/settings.json
/// </summary>
public sealed class SettingsService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "idc-lite");

    private static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // Файл повреждён — создаём новый
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataPath);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Молча игнорируем ошибки записи
        }
    }

    public static string GetAppDataPath() => AppDataPath;
}
