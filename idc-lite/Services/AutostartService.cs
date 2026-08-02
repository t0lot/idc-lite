using Microsoft.Win32;

namespace idc_lite.Services;

/// <summary>
/// Управление автозапуском приложения через реестр Windows.
/// </summary>
public static class AutostartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "idc-lite";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) != null;
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);

        var exePath = Environment.ProcessPath ?? System.AppContext.BaseDirectory;
        // Добавляем флаги: минимизированный запуск
        key.SetValue(AppName, $"\"{exePath}\" --minimized");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key != null)
        {
            try { key.DeleteValue(AppName); } catch { }
        }
    }
}
