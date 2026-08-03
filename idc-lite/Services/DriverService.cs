using System.IO.Compression;
using LibreHardwareMonitor.Hardware;

namespace idc_lite.Services;

/// <summary>
/// Управление драйвером WinRing0x64.sys:
/// 1. Копирует .sys из папки exe в %AppData%/idc-lite/ с оригинальным именем
/// 2. Удаляет .sys из папки exe (драйвер уже загружен в память ядра)
/// </summary>
public static class DriverService
{
    private const string DriverFileName = "WinRing0x64.sys";
    private const string LhmResourceName = "LibreHardwareMonitor.Resources.WinRing0x64.gz";

    /// <summary>
    /// После Computer.Open() драйвер загружен в память ядра.
    /// Копируем .sys из папки exe в AppData и удаляем оригинал.
    /// </summary>
    public static void MoveDriverToAppData(string appDataPath)
    {
        try
        {
            Directory.CreateDirectory(appDataPath);
            var destPath = Path.Combine(appDataPath, DriverFileName);

            // Имя .sys файла = имя процесса + ".sys"
            var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            var sourceName = processName + ".sys";

            // Ищем .sys в нескольких возможных местах (single-file publish м.б. ExtractToDirectory)
            string? sourcePath = null;
            var candidates = new List<string>();

            // 1. AppContext.BaseDirectory
            candidates.Add(Path.Combine(AppContext.BaseDirectory, sourceName));

            // 2. Папка из Environment.ProcessPath
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    candidates.Add(Path.Combine(Path.GetDirectoryName(exePath)!, sourceName));
            }
            catch { }

            // 3. Текущая директория
            try { candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), sourceName)); }
            catch { }

            foreach (var candidate in candidates.Distinct())
            {
                if (File.Exists(candidate))
                {
                    sourcePath = candidate;
                    break;
                }
            }

            if (sourcePath != null)
            {
                // Копируем в AppData с оригинальным именем
                File.Copy(sourcePath, destPath, overwrite: true);

                // Удаляем из папки exe — драйвер уже в памяти, файл не нужен
                try { File.Delete(sourcePath); }
                catch { /* Файл может быть залочен — оставляем */ }
            }
        }
        catch
        {
            // Не критично — драйвер уже работает
        }
    }

    /// <summary>
    /// Удаляет .sys из папки exe после остановки драйвера.
    /// Вызывать ПОСЛЕ Computer.Close().
    /// </summary>
    public static void RemoveDriverFromExeDir()
    {
        try
        {
            var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            var sourceName = processName + ".sys";

            var candidates = new List<string>();
            candidates.Add(Path.Combine(AppContext.BaseDirectory, sourceName));
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    candidates.Add(Path.Combine(Path.GetDirectoryName(exePath)!, sourceName));
            }
            catch { }
            try { candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), sourceName)); }
            catch { }

            foreach (var candidate in candidates.Distinct())
            {
                if (File.Exists(candidate))
                {
                    try { File.Delete(candidate); }
                    catch { /* может всё ещё быть залочен */ }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Предварительно извлекает WinRing0x64.sys из ресурсов LHM в AppData.
    /// </summary>
    public static void EnsureDriverExtracted(string appDataPath)
    {
        try
        {
            var destPath = Path.Combine(appDataPath, DriverFileName);

            if (File.Exists(destPath) && new FileInfo(destPath).Length > 0)
                return;

            Directory.CreateDirectory(appDataPath);

            var lhmAssembly = typeof(Computer).Assembly;
            var resourceStream = lhmAssembly.GetManifestResourceStream(LhmResourceName);

            if (resourceStream == null)
            {
                var allResources = lhmAssembly.GetManifestResourceNames();
                var match = Array.Find(allResources,
                    r => r.Contains("WinRing0x64", StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    resourceStream = lhmAssembly.GetManifestResourceStream(match);
            }

            if (resourceStream == null) return;

            using var ms = new MemoryStream();
            resourceStream.CopyTo(ms);
            ms.Position = 0;
            resourceStream.Dispose();

            var rawBytes = ms.ToArray();

            int skip = 0;
            for (int i = 0; i < Math.Min(4, rawBytes.Length - 2); i++)
            {
                if (rawBytes[i] == 0x1F && rawBytes[i + 1] == 0x8B)
                {
                    skip = i;
                    break;
                }
            }

            using var gzipStream = new MemoryStream(rawBytes, skip, rawBytes.Length - skip);
            using var gz = new GZipStream(gzipStream, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);

            File.WriteAllBytes(destPath, outMs.ToArray());
        }
        catch
        {
            // Не критично
        }
    }
}