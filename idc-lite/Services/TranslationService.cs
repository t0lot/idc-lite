using idc_lite.Models;

namespace idc_lite.Services;

/// <summary>
/// Централизованный сервис локализации — все UI строки на 3 языках.
/// </summary>
public static class TranslationService
{
    private static Language _current = Language.Russian;

    public static Language Current
    {
        get => _current;
        set => _current = value;
    }

    public static void SetLanguage(Language lang) => _current = lang;

    // ===== Ключи строк =====
    public static class Keys
    {
        // MainWindow
        public const string AppTitle = "AppTitle";
        public const string StatusSearching = "StatusSearching";
        public const string StatusConnected = "StatusConnected";
        public const string StatusNotFound = "StatusNotFound";
        public const string Celsius = "Celsius";
        public const string VersionInfo = "VersionInfo";
        public const string IntervalMs = "IntervalMs";

        // SettingsWindow
        public const string Settings = "Settings";
        public const string UpdateInterval = "UpdateInterval";
        public const string TempSource = "TempSource";
        public const string TempAuto = "TempAuto";
        public const string TempCoreAvg = "TempCoreAvg";
        public const string TempHotspot = "TempHotspot";
        public const string TempPackage = "TempPackage";
        public const string System = "System";
        public const string AutoStart = "AutoStart";
        public const string MinimizeToTray = "MinimizeToTray";
        public const string StartMinimized = "StartMinimized";
        public const string HighPriority = "HighPriority";
        public const string AppDescription = "AppDescription";
        public const string SettingsPath = "SettingsPath";

        // Language section
        public const string LanguageSection = "LanguageSection";
        public const string LangRussian = "LangRussian";
        public const string LangEnglish = "LangEnglish";
        public const string LangChinese = "LangChinese";

        // Animation
        public const string AnimationSection = "AnimationSection";
        public const string AnimNone = "AnimNone";
        public const string AnimSmooth = "AnimSmooth";
        public const string AnimRoller = "AnimRoller";
        public const string AnimNoneDesc = "AnimNoneDesc";
        public const string AnimSmoothDesc = "AnimSmoothDesc";
        public const string AnimRollerDesc = "AnimRollerDesc";

        // Tray
        public const string TrayTooltip = "TrayTooltip";
        public const string TrayShow = "TrayShow";
        public const string TraySettings = "TraySettings";
        public const string TrayExit = "TrayExit";
    }

    // ===== Словарь переводов =====
    private static readonly Dictionary<string, (string ru, string en, string zh)> Translations = new()
    {
        // MainWindow
        [Keys.AppTitle]        = ("IDC-Lite", "IDC-Lite", "IDC-Lite"),
        [Keys.StatusSearching] = ("Поиск устройства...", "Searching for device...", "正在搜索设备..."),
        [Keys.StatusConnected] = ("Дисплей подключён", "Display connected", "显示屏已连接"),
        [Keys.StatusNotFound]  = ("Устройство не найдено", "Device not found", "未找到设备"),
        [Keys.Celsius]          = ("°C", "°C", "°C"),
        [Keys.VersionInfo]      = ("IDC-Lite v1.0", "IDC-Lite v1.0", "IDC-Lite v1.0"),
        [Keys.IntervalMs]       = ("{0} мс", "{0} ms", "{0} 毫秒"),

        // SettingsWindow
        [Keys.Settings]          = ("Настройки", "Settings", "设置"),
        [Keys.UpdateInterval]    = ("ИНТЕРВАЛ ОБНОВЛЕНИЯ", "UPDATE INTERVAL", "更新间隔"),
        [Keys.TempSource]        = ("ИСТОЧНИК ТЕМПЕРАТУРЫ", "TEMPERATURE SOURCE", "温度来源"),
        [Keys.TempAuto]          = ("Авто (рекомендуется)", "Auto (recommended)", "自动 (推荐)"),
        [Keys.TempCoreAvg]       = ("Средняя ядер", "Core average", "核心平均"),
        [Keys.TempHotspot]      = ("Хотспот (самая горячая)", "Hotspot (hottest)", "热点 (最热)"),
        [Keys.TempPackage]       = ("CPU Package", "CPU Package", "CPU 封装"),
        [Keys.System]            = ("СИСТЕМА", "SYSTEM", "系统"),
        [Keys.AutoStart]         = ("Запуск при старте Windows", "Launch with Windows", "随 Windows 启动"),
        [Keys.MinimizeToTray]   = ("Сворачивать в трей при закрытии", "Minimize to tray on close", "关闭时最小化到托盘"),
        [Keys.StartMinimized]    = ("Запускать свёрнутым в трей", "Start minimized to tray", "启动时最小化到托盘"),
        [Keys.HighPriority]      = ("Высокий приоритет процесса", "High process priority", "高进程优先级"),
        [Keys.AppDescription]    = ("Мониторинг CPU для USB HID LCD-дисплеев",
                                    "CPU monitoring for USB HID LCD displays",
                                    "用于 USB HID LCD 显示屏的 CPU 监控"),
        [Keys.SettingsPath]      = ("Настройки: {0}", "Settings: {0}", "设置: {0}"),

        // Language section
        [Keys.LanguageSection]   = ("ЯЗЫК", "LANGUAGE", "语言"),
        [Keys.LangRussian]       = ("Русский", "Russian", "俄语"),
        [Keys.LangEnglish]       = ("Английский", "English", "英语"),
        [Keys.LangChinese]       = ("Китайский", "Chinese", "中文"),

        // Animation
        [Keys.AnimationSection] = ("АНИМАЦИЯ ДИСПЛЕЯ", "DISPLAY ANIMATION", "显示动画"),
        [Keys.AnimNone]         = ("Отсутствует", "None", "无"),
        [Keys.AnimSmooth]       = ("Плавная", "Smooth", "平滑"),
        [Keys.AnimRoller]       = ("Роллер", "Roller", "滚动"),
        [Keys.AnimNoneDesc]     = ("Мгновенное обновление. Максимальная точность.",
                                    "Instant update. Maximum accuracy.",
                                    "即时更新。最高精度。"),
        [Keys.AnimSmoothDesc]   = ("Плавная интерполяция с усреднением. Менее точная — для красоты. Рекомендуется задержка ≥ 1000 мс.",
                                    "Smooth interpolation with averaging. Less accurate — for beauty. Recommended delay ≥ 1000 ms.",
                                    "带平均值的平滑插值。精度较低 — 追求美感。建议延迟 ≥ 1000 毫秒。"),
        [Keys.AnimRollerDesc]   = ("Быстрая плавная прокрутка. Короткий переход, но без рывков.",
                                    "Fast smooth scrolling. Short transition, but no jumps.",
                                    "快速平滑滚动。短暂过渡，无跳跃。"),

        // Tray
        [Keys.TrayTooltip]    = ("IDC-Lite — мониторинг LCD", "IDC-Lite — LCD Monitor", "IDC-Lite — LCD 监控"),
        [Keys.TrayShow]       = ("Показать", "Show", "显示"),
        [Keys.TraySettings]   = ("Настройки", "Settings", "设置"),
        [Keys.TrayExit]       = ("Выход", "Exit", "退出"),
    };

    public static string Get(string key)
    {
        if (Translations.TryGetValue(key, out var t))
        {
            return _current switch
            {
                Language.English => t.en,
                Language.Chinese => t.zh,
                _ => t.ru
            };
        }
        return key;
    }

    public static string Get(string key, params object[] args)
    {
        var template = Get(key);
        return string.Format(template, args);
    }

    public static string Get(Language lang, string key)
    {
        if (Translations.TryGetValue(key, out var t))
        {
            return lang switch
            {
                Language.English => t.en,
                Language.Chinese => t.zh,
                _ => t.ru
            };
        }
        return key;
    }
}