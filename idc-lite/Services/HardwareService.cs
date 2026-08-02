using idc_lite.Models;
using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Management;
using IHardware = LibreHardwareMonitor.Hardware.IHardware;

namespace idc_lite.Services;

/// <summary>
/// Мониторинг температуры, загрузки и частоты CPU.
/// Оптимизация: один hardware.Update() на цикл, кэширование 1.5с.
/// </summary>
public sealed class HardwareService : IDisposable
{
    private readonly Computer _computer;
    private bool _isOpen;

    private TemperatureSource _tempSource = TemperatureSource.Auto;

    public void SetTemperatureSource(TemperatureSource source)
    {
        _tempSource = source;
    }

    public TemperatureSource TempSource => _tempSource;

    private float? _cachedTemperature;
    private float? _cachedLoad;
    private float? _cachedFrequency;
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private readonly object _lock = new();
    private const double CacheSeconds = 0.5;

    public HardwareService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = false,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false,
            IsNetworkEnabled = false,
            IsStorageEnabled = false,
            IsBatteryEnabled = false,
            IsPsuEnabled = false
        };
    }

    public void Open()
    {
        if (!_isOpen)
        {
            try
            {
                // Извлекаем драйвер WinRing0x64.sys в AppData до загрузки
                var appDataPath = SettingsService.GetAppDataPath();
                DriverService.EnsureDriverExtracted(appDataPath);

                _computer.Open();
                _isOpen = true;
            }
            catch
            {
                _isOpen = false;
            }
        }
    }

    public float? GetCpuTemperature()
    {
        RefreshIfNeeded();
        return _cachedTemperature;
    }

    public float? GetCpuLoad()
    {
        RefreshIfNeeded();
        return _cachedLoad;
    }

    public float? GetCpuFrequency()
    {
        RefreshIfNeeded();
        return _cachedFrequency;
    }

    private void RefreshIfNeeded()
    {
        lock (_lock)
        {
            if ((DateTime.UtcNow - _lastUpdateTime).TotalSeconds < CacheSeconds)
                return;

            _lastUpdateTime = DateTime.UtcNow;
            _cachedTemperature = null;
            _cachedLoad = null;
            _cachedFrequency = null;

            if (!_isOpen) return;

            try
            {
                // Перебираем копию коллекции — оригинал может измениться
                var hardwareList = new List<IHardware>(_computer.Hardware);
                foreach (var hardware in hardwareList)
                {
                    if (hardware.HardwareType != HardwareType.Cpu)
                        continue;

                    hardware.Update();

                    // === Температура ===
                    if (!_cachedTemperature.HasValue)
                    {
                        _cachedTemperature = ReadTemperature(hardware.Sensors, _tempSource);
                    }

                    // === Загрузка ===
                    if (!_cachedLoad.HasValue)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                            {
                                var name = (sensor.Name ?? "").ToUpperInvariant();
                                if (name.Contains("TOTAL"))
                                {
                                    _cachedLoad = sensor.Value;
                                    break;
                                }
                            }
                        }
                        // Fallback: любой Load сенсор
                        if (!_cachedLoad.HasValue)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                                {
                                    _cachedLoad = sensor.Value;
                                    break;
                                }
                            }
                        }
                    }

                    // === Частота ===
                    if (!_cachedFrequency.HasValue)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                            {
                                var name = (sensor.Name ?? "").ToUpperInvariant();
                                if (name.Contains("BUS"))
                                {
                                    _cachedFrequency = sensor.Value;
                                    break;
                                }
                            }
                        }
                        // Fallback: первый Clock сенсор
                        if (!_cachedFrequency.HasValue)
                        {
                            float? maxClock = null;
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                                {
                                    if (!maxClock.HasValue || sensor.Value > maxClock)
                                        maxClock = sensor.Value;
                                }
                            }
                            _cachedFrequency = maxClock;
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем
            }

            // WMI фоллбэк для температуры
            if (!_cachedTemperature.HasValue)
                _cachedTemperature = GetTemperatureWmi();

            if (!_cachedTemperature.HasValue)
                _cachedTemperature = float.NaN;
        }
    }

    /// <summary>
    /// Чтение температуры CPU по выбранному источнику.
    /// </summary>
    private static float? ReadTemperature(
        System.Collections.Generic.IEnumerable<ISensor> sensors,
        TemperatureSource source)
    {
        float? packageTemp = null;
        float? tctlTemp = null;
        float? maxCoreTemp = null;
        float sumCoreTemps = 0;
        int coreCount = 0;

        foreach (var sensor in sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                continue;

            var name = (sensor.Name ?? "").ToUpperInvariant();

            if (name.Contains("PACKAGE"))
                packageTemp ??= sensor.Value;
            else if (name.Contains("TCTL") || name.Contains("TDIE"))
                tctlTemp ??= sensor.Value;
            else if (name.Contains("CORE"))
            {
                sumCoreTemps += sensor.Value.Value;
                coreCount++;
                if (!maxCoreTemp.HasValue || sensor.Value > maxCoreTemp)
                    maxCoreTemp = sensor.Value;
            }
        }

        float? coreAvg = coreCount > 0 ? sumCoreTemps / coreCount : null;

        return source switch
        {
            TemperatureSource.Package => packageTemp ?? tctlTemp ?? coreAvg ?? maxCoreTemp,
            TemperatureSource.Hotspot => maxCoreTemp ?? tctlTemp ?? packageTemp ?? coreAvg,
            TemperatureSource.CoreAverage => coreAvg ?? maxCoreTemp ?? packageTemp ?? tctlTemp,
            _ => packageTemp ?? tctlTemp ?? coreAvg ?? maxCoreTemp, // Auto
        };
    }

            private static float? GetTemperatureWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");

            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = obj["CurrentTemperature"];
                if (raw != null)
                {
                    double celsius = Convert.ToDouble(raw) / 10.0 - 273.15;
                    if (celsius > 0 && celsius < 150)
                        return (float)celsius;
                }
            }
        }
        catch { }

        return null;
    }

    public void Close()
    {
        if (_isOpen)
        {
            try { _computer.Close(); } catch { }
            _isOpen = false;
        }
    }

    public void Dispose()
    {
        Close();
    }
}