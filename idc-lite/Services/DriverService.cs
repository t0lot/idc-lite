using System.IO.Compression;
using LibreHardwareMonitor.Hardware;

namespace idc_lite.Services;

/// <summary>
/// Извлекает встроенный драйвер WinRing0x64.sys из LibreHardwareMonitorLib
/// в папку %AppData%/idc-lite/ с оригинальным именем.
/// </summary>
public static class DriverService
{
    private const string DriverFileName = "WinRing0x64.sys";
    private const string ResourceName = "LibreHardwareMonitor.Resources.WinRing0x64.gz";

    /// <summary>
    /// Извлекает драйвер в папку AppData, если его ещё нет там.
    /// </summary>
    public static void EnsureDriverExtracted(string appDataPath)
    {
        try
        {
            var driverPath = Path.Combine(appDataPath, DriverFileName);

            // Если файл уже существует и не пустой — не перезаписываем
            if (File.Exists(driverPath) && new FileInfo(driverPath).Length > 0)
                return;

            Directory.CreateDirectory(appDataPath);

            // Получаем сборку LibreHardwareMonitorLib через тип Computer
            var lhmAssembly = typeof(Computer).Assembly;
            var resourceStream = lhmAssembly.GetManifestResourceStream(ResourceName);

            if (resourceStream == null)
            {
                // Пробуем найти ресурс по части имени
                var allResources = lhmAssembly.GetManifestResourceNames();
                var match = Array.Find(allResources,
                    r => r.Contains("WinRing0x64", StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    resourceStream = lhmAssembly.GetManifestResourceStream(match);
            }

            if (resourceStream == null)
                return;

            // Читаем все байты ресурса
            using var ms = new MemoryStream();
            resourceStream.CopyTo(ms);
            ms.Position = 0;
            resourceStream.Dispose();

            var rawBytes = ms.ToArray();

            // Ресурс имеет 1-байтный префикс (0xFF) перед GZip-данными
            // GZip magic: 1F 8B 08
            int skip = 0;
            for (int i = 0; i < Math.Min(4, rawBytes.Length - 2); i++)
            {
                if (rawBytes[i] == 0x1F && rawBytes[i + 1] == 0x8B)
                {
                    skip = i;
                    break;
                }
            }

            // Декомпрессия GZip
            using var gzipStream = new MemoryStream(rawBytes, skip, rawBytes.Length - skip);
            using var gz = new GZipStream(gzipStream, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);

            File.WriteAllBytes(driverPath, outMs.ToArray());
        }
        catch
        {
            // Молча игнорируем — драйвер не критичен, LHM загрузит свой
        }
    }
}