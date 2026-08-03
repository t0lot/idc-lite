using System.Diagnostics;

namespace idc_lite.Services;

/// <summary>
/// Управление автозапуском приложения через Планировщик задач Windows.
/// Надёжнее, чем реестр, и работает в современных версиях Windows.
/// </summary>
public static class AutostartService
{
    public static bool IsEnabled()
    {
        return TaskSchedulerService.IsTaskExists();
    }

    public static void Enable()
    {
        var exePath = GetExePath();
        if (exePath == null) return;

        TaskSchedulerService.CreateTask(exePath);
    }

    public static void Disable()
    {
        TaskSchedulerService.DeleteTask();
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