using System.IO;
using System.Text.RegularExpressions;

namespace idc_lite.Services;

/// <summary>
/// Кроссплатформенный сервис телеметрии Linux через sysfs/hwmon и /proc.
/// </summary>
public static class LinuxHardwareService
{
    private static long _prevIdleTime;
    private static long _prevTotalTime;
    private static readonly object _lock = new();

    public static float? GetCpuTemperature()
    {
        try
        {
            if (Directory.Exists("/sys/class/hwmon"))
            {
                var hwmonDirs = Directory.GetDirectories("/sys/class/hwmon");
                
                // Приоритетные имена драйверов CPU в ядре Linux
                string[] preferredCpuDrivers = ["coretemp", "k10temp", "zenpower", "cpu_thermal", "soc_thermal", "acpitz"];
                
                string? bestHwmonDir = null;
                foreach (var dir in hwmonDirs)
                {
                    var namePath = Path.Combine(dir, "name");
                    if (File.Exists(namePath))
                    {
                        var driverName = File.ReadAllText(namePath).Trim();
                        if (preferredCpuDrivers.Any(p => driverName.Contains(p, StringComparison.OrdinalIgnoreCase)))
                        {
                            bestHwmonDir = dir;
                            break;
                        }
                    }
                }

                var dirsToScan = bestHwmonDir != null 
                    ? new[] { bestHwmonDir }.Concat(hwmonDirs.Where(d => d != bestHwmonDir))
                    : hwmonDirs;

                foreach (var dir in dirsToScan)
                {
                    var tempInputs = Directory.GetFiles(dir, "temp*_input");
                    foreach (var tempFile in tempInputs)
                    {
                        var labelFile = tempFile.Replace("_input", "_label");
                        // Если есть метка (Tdie, Tctl, Package id 0, CPU), отдаем приоритет
                        if (File.Exists(labelFile))
                        {
                            var label = File.ReadAllText(labelFile).Trim();
                            if (label.Contains("Tdie", StringComparison.OrdinalIgnoreCase) || 
                                label.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                label.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                                label.Contains("Tctl", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(File.ReadAllText(tempFile).Trim(), out int val) && val > 0)
                                {
                                    float c = val > 1000 ? val / 1000.0f : val;
                                    if (c is >= 10 and <= 125)
                                        return c;
                                }
                            }
                        }

                        if (int.TryParse(File.ReadAllText(tempFile).Trim(), out int millidegrees) && millidegrees > 0)
                        {
                            float c = millidegrees > 1000 ? millidegrees / 1000.0f : millidegrees;
                            if (c is >= 10 and <= 125)
                                return c;
                        }
                    }
                }
            }

            // Fallback: /sys/class/thermal/thermal_zone*
            if (Directory.Exists("/sys/class/thermal"))
            {
                var zones = Directory.GetDirectories("/sys/class/thermal", "thermal_zone*");
                foreach (var zone in zones)
                {
                    var typePath = Path.Combine(zone, "type");
                    var tempPath = Path.Combine(zone, "temp");
                    
                    if (File.Exists(tempPath) && int.TryParse(File.ReadAllText(tempPath).Trim(), out int millidegrees))
                    {
                        float c = millidegrees > 1000 ? millidegrees / 1000.0f : millidegrees;
                        if (c is >= 10 and <= 125)
                            return c;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    public static float? GetCpuLoad()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists("/proc/stat"))
                    return null;

                var firstLine = File.ReadLines("/proc/stat").FirstOrDefault();
                if (string.IsNullOrWhiteSpace(firstLine) || !firstLine.StartsWith("cpu "))
                    return null;

                var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    return null;

                long user = long.Parse(parts[1]);
                long nice = long.Parse(parts[2]);
                long system = long.Parse(parts[3]);
                long idle = long.Parse(parts[4]);
                long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
                long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
                long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;
                long steal = parts.Length > 8 ? long.Parse(parts[8]) : 0;

                long idleAllTime = idle + iowait;
                long systemAllTime = system + irq + softirq;
                long totalTime = user + nice + systemAllTime + idleAllTime + steal;

                long totalDelta = totalTime - _prevTotalTime;
                long idleDelta = idleAllTime - _prevIdleTime;

                _prevTotalTime = totalTime;
                _prevIdleTime = idleAllTime;

                if (totalDelta <= 0)
                    return null;

                float usage = (float)(totalDelta - idleDelta) * 100.0f / totalDelta;
                return Math.Clamp(usage, 0f, 100f);
            }
            catch { }

            return null;
        }
    }

    public static float? GetCpuFrequency()
    {
        try
        {
            if (File.Exists("/proc/cpuinfo"))
            {
                var lines = File.ReadLines("/proc/cpuinfo");
                var mhzMatches = new List<float>();

                foreach (var line in lines)
                {
                    if (line.StartsWith("cpu MHz", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length == 2 && float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float mhz))
                        {
                            mhzMatches.Add(mhz);
                        }
                    }
                }

                if (mhzMatches.Count > 0)
                    return (float)Math.Round(mhzMatches.Average(), 0);
            }

            // sysfs scaling_cur_freq (KHz)
            if (Directory.Exists("/sys/devices/system/cpu/cpufreq"))
            {
                var policyDirs = Directory.GetDirectories("/sys/devices/system/cpu/cpufreq", "policy*");
                var freqs = new List<float>();
                foreach (var p in policyDirs)
                {
                    var curPath = Path.Combine(p, "scaling_cur_freq");
                    if (File.Exists(curPath) && long.TryParse(File.ReadAllText(curPath).Trim(), out long khz))
                    {
                        freqs.Add(khz / 1000.0f);
                    }
                }
                if (freqs.Count > 0)
                    return (float)Math.Round(freqs.Average(), 0);
            }
        }
        catch { }

        return null;
    }
}
