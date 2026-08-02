using Microsoft.Win32;
using System.Diagnostics;

namespace idc_lite.Services;

/// <summary>
/// Управление автозапуском приложения через реестр Windows.
/// Надёжная реализация с множественными fallback-методами получения пути к exe.
/// </summary>
public static class AutostartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "idc-lite";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
    }

    public static void Enable()
    {
        var exePath = GetExePath();
        if (exePath == null) return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            // Кавычки обязательны — путь может содержать пробелы
            key.SetValue(AppName, $"\"{exePath}\" --minimized");
        }
        catch { }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { }
    }

    /// <summary>
    /// Получает путь к исполняемому файлу .exe с тремя fallback-методами.
    /// </summary>
    private static string? GetExePath()
    {
        // Метод 1: Environment.ProcessPath (.NET 6+, работает в single-file)
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;
        }
        catch { }

        // Метод 2: Process.MainModule.FileName
        try
        {
            var module = Process.GetCurrentProcess().MainModule;
            if (module != null && !string.IsNullOrEmpty(module.FileName) && File.Exists(module.FileName))
                return module.FileName;
        }
        catch { }

        // Метод 3: AppContext.BaseDirectory + имя сборки
        try
        {
            var baseDir = AppContext.BaseDirectory
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var exeName = AppDomain.CurrentDomain.FriendlyName;
            if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                exeName += ".exe";

            var fullPath = Path.Combine(baseDir, exeName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        catch { }

        return null;
    }
}