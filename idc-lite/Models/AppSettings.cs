namespace idc_lite.Models;

public enum DisplayAnimation
{
    None = 0,
    Smooth = 1,
    Roller = 2
}

public enum TemperatureSource
{
    Auto = 0,           // Авто-выбор (Package → Tctl/Tdie → Core avg)
    CoreAverage = 1,    // Средняя температура ядер
    Hotspot = 2,        // Самый горячий сенсор
    Package = 3         // CPU Package
}

public class AppSettings
{
    public int UpdateIntervalMs { get; set; } = 1000;

    public bool AutoStart { get; set; } = false;

    public bool MinimizeToTray { get; set; } = true;

    public bool StartMinimized { get; set; } = true;

    public bool HighPriority { get; set; } = true;

    public TemperatureSource TempSource { get; set; } = TemperatureSource.Auto;

    public Language Language { get; set; } = Language.Russian;

    public DisplayAnimation DisplayAnimation { get; set; } = DisplayAnimation.None;
}