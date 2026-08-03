using System.Diagnostics;

namespace idc_lite.Services;

/// <summary>
/// Управление автозапуском через Планировщик задач Windows (schtasks.exe).
/// Не требует внешних NuGet-пакетов — использует встроенную утилиту Windows.
/// </summary>
public static class TaskSchedulerService
{
    private const string TaskName = "IDC-Lite Autostart";

    /// <summary>
    /// Проверяет, существует ли задача автозапуска.
    /// </summary>
    public static bool IsTaskExists()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Создаёт задачу автозапуска при входе в Windows.
    /// </summary>
    public static bool CreateTask(string exePath, string arguments = "--minimized")
    {
        try
        {
            // Удаляем старую задачу, если есть (игнорируем ошибку)
            DeleteTask();

            // Создаём задачу через schtasks.exe /Create
            // /SC ONLOGON — триггер при входе в систему
            // /RL HIGHEST — максимальные привилегии
            var args = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" {arguments}\" /SC ONLOGON /RL HIGHEST /F";

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(10000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Удаляет задачу автозапуска.
    /// </summary>
    public static bool DeleteTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{TaskName}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}