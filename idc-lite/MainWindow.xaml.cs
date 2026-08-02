using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using idc_lite.Models;
using idc_lite.Services;

using DisplayAnimation = idc_lite.Models.DisplayAnimation;

namespace idc_lite;

public partial class MainWindow : Window
{
    private readonly HidService _hidService;
    private readonly HardwareService _hardwareService;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _freqTimer;
    private readonly DispatcherTimer _usageTimer;

    private bool _deviceConnected;
    private bool _isSettingsMode;
    private bool _initializing = true;

    // Кэшированные кисти — ОДИН объект на всё время жизни
    private static readonly WpfBrush BrushGreen  = new(WpfColor.FromRgb(0xa6, 0xe3, 0xa1));
    private static readonly WpfBrush BrushRed    = new(WpfColor.FromRgb(0xf3, 0x8b, 0xa8));
    private static readonly WpfBrush BrushGray   = new(WpfColor.FromRgb(0x6c, 0x70, 0x86));
    private static readonly WpfBrush BrushBtnHov = new(WpfColor.FromRgb(0xcd, 0xd6, 0xf4));
    private static readonly WpfBrush BrushClsHov = new(WpfColor.FromRgb(0xf3, 0x8b, 0xa8));

    private static readonly WpfBrush BrushCold = new(WpfColor.FromRgb(0xa6, 0xe3, 0xa1)); // <50
    private static readonly WpfBrush BrushWarm = new(WpfColor.FromRgb(0xf9, 0xe2, 0xaf)); // <70
    private static readonly WpfBrush BrushHot  = new(WpfColor.FromRgb(0xfa, 0xb3, 0x87)); // <82
    private static readonly WpfBrush BrushCrit = new(WpfColor.FromRgb(0xf3, 0x8b, 0xa8)); // ≥82

    private WpfBrush _lastTempBrush = BrushGray;

    // Кэш последних значений
    private float? _lastTemp;
    private int? _lastSentTemp;
    private int? _lastSentFreq;
    private int? _lastSentUsage;
    private int _reconnectAttempts;

    // --- Анимация температуры (easing-интерполяция) ---
    private DisplayAnimation _animation = DisplayAnimation.None;
    private int _displayedTemp = -999;
    private int _targetTemp;
    private int _animStartTemp;
    private DateTime _animStartTime;
    private double _animDurationMs;
    private readonly DispatcherTimer _animTimer;
    private const int AnimFps = 16; // ~60fps

    // Rolling average для Smooth режима
    private readonly Queue<float> _tempHistory = new();
    private const int SmoothWindowSize = 5;

    // Для передачи ссылок в трей
    private static WeakReference<MainWindow>? _instance;

    public MainWindow(HidService hidService, HardwareService hardwareService, SettingsService settingsService)
    {
        InitializeComponent();

        _hidService = hidService;
        _hardwareService = hardwareService;
        _settingsService = settingsService;
        _settings = settingsService.Load();

        _updateTimer = new DispatcherTimer(DispatcherPriority.Background);
        _updateTimer.Tick += OnMainTick;

        _freqTimer = new DispatcherTimer(DispatcherPriority.Background);
        _freqTimer.Tick += (_, _) => { try { _freqTimer.Stop(); SendFrequencyToDisplay(); } catch { } };

        _usageTimer = new DispatcherTimer(DispatcherPriority.Background);
        _usageTimer.Tick += (_, _) => { try { _usageTimer.Stop(); SendUsageToDisplay(); } catch { } };

        _animTimer = new DispatcherTimer(DispatcherPriority.Background);
        _animTimer.Tick += OnAnimTick;

        _instance = new WeakReference<MainWindow>(this);

        TranslationService.SetLanguage(_settings.Language);
        ApplyLanguage();
        ApplySettings();

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public static MainWindow? GetInstance() => _instance?.TryGetTarget(out var w) == true ? w : null;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_settings.HighPriority)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; }
            catch { }
        }

        _hardwareService.Open();
        TryConnectDevice();
        StartUpdateTimer();

        _initializing = false;

        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--minimized") && _settings.StartMinimized)
        {
            Hide();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settingsService.Save(_settings);
    }

    public void Shutdown()
    {
        _updateTimer.Stop();
        _freqTimer.Stop();
        _usageTimer.Stop();
        _animTimer.Stop();
        _hidService.SendShow(false);
        _hardwareService.Close();
        _hidService.CloseDevice();
        _settingsService.Save(_settings);
    }

    private void TryConnectDevice()
    {
        _deviceConnected = _hidService.OpenDevice();
        if (_deviceConnected)
        {
            _hidService.SendShow(true);
            _reconnectAttempts = 0;
        }
        UpdateStatusUI();
    }

    private void StartUpdateTimer()
    {
        _updateTimer.Stop();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, _settings.UpdateIntervalMs));
        _updateTimer.Start();
    }

    // ===== Главный тик =====

    private void OnMainTick(object? sender, EventArgs e)
    {
        try { DoMainTick(); }
        catch { /* Не падаем */ }
    }

    private void DoMainTick()
    {
        var temp = _hardwareService.GetCpuTemperature();

        // --- Дисплей: частота (через 100мс) ---
        _freqTimer.Interval = TimeSpan.FromMilliseconds(100);
        _freqTimer.Start();

        // --- Дисплей: загрузка (через 200мс) ---
        _usageTimer.Interval = TimeSpan.FromMilliseconds(200);
        _usageTimer.Start();

        // --- Температура (с анимацией или без) ---
        if (!temp.HasValue || float.IsNaN(temp.Value))
        {
            _animTimer.Stop();
            _displayedTemp = -999;
            _tempHistory.Clear();
            TemperatureValue.Text = "—";
            if (_lastTempBrush != BrushGray)
            {
                TemperatureValue.Foreground = BrushGray;
                _lastTempBrush = BrushGray;
            }
        }
        else
        {
            int newTemp;

            if (_animation == DisplayAnimation.Smooth)
            {
                // Rolling average — сглаживает скачки
                _tempHistory.Enqueue(temp.Value);
                while (_tempHistory.Count > SmoothWindowSize)
                    _tempHistory.Dequeue();
                newTemp = (int)Math.Round(_tempHistory.Average());
            }
            else
            {
                newTemp = (int)Math.Round(temp.Value);
            }

            _lastTemp = temp;

            if (_animation == DisplayAnimation.None || _displayedTemp < 0)
            {
                _displayedTemp = newTemp;
                SetTemperatureDisplay(newTemp);
                SendTemperatureRaw(newTemp);
            }
            else if (newTemp == _displayedTemp)
            {
                SetTemperatureDisplay(newTemp);
            }
            else
            {
                StartAnimation(newTemp);
            }
        }

        // --- Переподключение если устройство пропало ---
        if (!_deviceConnected)
        {
            _reconnectAttempts++;
            if (_reconnectAttempts % 5 == 0)
                TryConnectDevice();
        }
    }

    private void StartAnimation(int targetTemp)
    {
        _animTimer.Stop();
        _animStartTemp = _displayedTemp;
        _targetTemp = targetTemp;
        _animStartTime = DateTime.UtcNow;

        int diff = Math.Abs(_targetTemp - _animStartTemp);

        if (_animation == DisplayAnimation.Smooth)
        {
            double baseDuration = _settings.UpdateIntervalMs;
            _animDurationMs = diff > 15 ? baseDuration * 0.5 : baseDuration;
        }
        else // Roller — быстрая плавная прокрутка
        {
            _animDurationMs = Math.Max(100, Math.Min(_settings.UpdateIntervalMs, (double)diff * 30));
        }

        _animTimer.Interval = TimeSpan.FromMilliseconds(AnimFps);
        _animTimer.Start();
    }

    private static double EaseInOutCubic(double t)
    {
        return t < 0.5
            ? 4 * t * t * t
            : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        try
        {
            double elapsed = (DateTime.UtcNow - _animStartTime).TotalMilliseconds;

            if (elapsed >= _animDurationMs)
            {
                _animTimer.Stop();
                _displayedTemp = _targetTemp;
                SetTemperatureDisplay(_displayedTemp);
                SendTemperatureRaw(_displayedTemp);
                return;
            }

            double progress = elapsed / _animDurationMs;
            double eased = EaseInOutCubic(progress);
            double current = _animStartTemp + (_targetTemp - _animStartTemp) * eased;
            int displayTemp = (int)Math.Round(current);

            if (displayTemp != _displayedTemp)
            {
                _displayedTemp = displayTemp;
                SetTemperatureDisplay(_displayedTemp);
                SendTemperatureRaw(_displayedTemp);
            }
        }
        catch { }
    }

    private void SetTemperatureDisplay(int temp)
    {
        TemperatureValue.Text = $"{temp}";

        var brush = temp switch
        {
            < 50  => BrushCold,
            < 70  => BrushWarm,
            < 82  => BrushHot,
            _     => BrushCrit,
        };

        if (!ReferenceEquals(_lastTempBrush, brush))
        {
            TemperatureValue.Foreground = brush;
            _lastTempBrush = brush;
        }
    }

    // ===== Отправка на LCD-дисплей =====

    private void SendTemperatureRaw(int tempC)
    {
        if (!_deviceConnected) return;
        if (_lastSentTemp.HasValue && _lastSentTemp.Value == tempC) return;
        _lastSentTemp = tempC;
        _hidService.SendTemperature(tempC);
    }

    private void SendFrequencyToDisplay()
    {
        if (!_deviceConnected) return;

        var freq = _hardwareService.GetCpuFrequency();
        if (!freq.HasValue || float.IsNaN(freq.Value) || freq.Value <= 0)
            return;

        int freqMHz = (int)Math.Round(freq.Value);
        if (_lastSentFreq.HasValue && _lastSentFreq.Value == freqMHz)
            return;

        _lastSentFreq = freqMHz;
        _hidService.SendFrequency(freqMHz);
    }

    private void SendUsageToDisplay()
    {
        if (!_deviceConnected) return;

        var load = _hardwareService.GetCpuLoad();
        if (!load.HasValue || float.IsNaN(load.Value))
            return;

        int usage = (int)Math.Round(load.Value);
        if (_lastSentUsage.HasValue && _lastSentUsage.Value == usage)
            return;

        _lastSentUsage = usage;
        _hidService.SendUsage(usage);
    }

    // ===== UI Status =====

    private void UpdateStatusUI()
    {
        if (_deviceConnected)
        {
            StatusText.Text = TranslationService.Get(TranslationService.Keys.StatusConnected);
            StatusDot.Fill = BrushGreen;
        }
        else
        {
            StatusText.Text = TranslationService.Get(TranslationService.Keys.StatusNotFound);
            StatusDot.Fill = BrushRed;
        }

        UpdateIntervalText.Text = TranslationService.Get(TranslationService.Keys.IntervalMs, _settings.UpdateIntervalMs);
    }

    // ===== Локализация =====

    private void ApplyLanguage()
    {
        Title = TranslationService.Get(TranslationService.Keys.AppTitle);
        HeaderTitle.Text = TranslationService.Get(TranslationService.Keys.AppTitle);
        SettingsHeaderTitle.Text = TranslationService.Get(TranslationService.Keys.Settings);
        VersionLabel.Text = TranslationService.Get(TranslationService.Keys.VersionInfo);

        // Settings labels
        LanguageSectionLabel.Text = TranslationService.Get(TranslationService.Keys.LanguageSection);
        LangRussian.Content = TranslationService.Get(TranslationService.Keys.LangRussian);
        LangEnglish.Content = TranslationService.Get(TranslationService.Keys.LangEnglish);
        LangChinese.Content = TranslationService.Get(TranslationService.Keys.LangChinese);

        UpdateIntervalLabel.Text = TranslationService.Get(TranslationService.Keys.UpdateInterval);
        AnimationLabel.Text = TranslationService.Get(TranslationService.Keys.AnimationSection);
        AnimNone.Content = TranslationService.Get(TranslationService.Keys.AnimNone);
        AnimSmooth.Content = TranslationService.Get(TranslationService.Keys.AnimSmooth);
        AnimRoller.Content = TranslationService.Get(TranslationService.Keys.AnimRoller);
        UpdateAnimDescription();
        TempSourceLabel.Text = TranslationService.Get(TranslationService.Keys.TempSource);
        TempSourceAuto.Content = TranslationService.Get(TranslationService.Keys.TempAuto);
        TempSourceCoreAvg.Content = TranslationService.Get(TranslationService.Keys.TempCoreAvg);
        TempSourceHotspot.Content = TranslationService.Get(TranslationService.Keys.TempHotspot);
        TempSourcePackage.Content = TranslationService.Get(TranslationService.Keys.TempPackage);

        SystemLabel.Text = TranslationService.Get(TranslationService.Keys.System);
        AutoStartCheck.Content = TranslationService.Get(TranslationService.Keys.AutoStart);
        MinimizeToTrayCheck.Content = TranslationService.Get(TranslationService.Keys.MinimizeToTray);
        StartMinimizedCheck.Content = TranslationService.Get(TranslationService.Keys.StartMinimized);
        HighPriorityCheck.Content = TranslationService.Get(TranslationService.Keys.HighPriority);

        AppDescriptionLabel.Text = TranslationService.Get(TranslationService.Keys.AppDescription);

        // Update interval text
        if (IntervalValue != null)
            IntervalValue.Text = TranslationService.Get(TranslationService.Keys.IntervalMs, _settings.UpdateIntervalMs);

        try
        {
            AppDataPath.Text = TranslationService.Get(TranslationService.Keys.SettingsPath, SettingsService.GetAppDataPath());
        }
        catch { }

        // Status text
        UpdateStatusUI();
    }

    public void ApplySettings()
    {
        StartUpdateTimer();

        // Apply language changes
        TranslationService.SetLanguage(_settings.Language);
        ApplyLanguage();

        // Apply animation mode + reset
        _animation = _settings.DisplayAnimation;
        _tempHistory.Clear();
        _animTimer.Stop();

        // Apply temperature source
        _hardwareService.SetTemperatureSource(_settings.TempSource);

        // Rebuild tray menu if language changed
        if (WpfApplication.Current is App app)
            app.RebuildTrayMenu();

        try
        {
            Process.GetCurrentProcess().PriorityClass = _settings.HighPriority
                ? ProcessPriorityClass.High
                : ProcessPriorityClass.Normal;
        }
        catch { }
    }

    // ===== Настройки: загрузка/сохранение =====

    private void LoadSettings()
    {
        _initializing = true;

        // Language
        LangRussian.IsChecked = _settings.Language == idc_lite.Models.Language.Russian;
        LangEnglish.IsChecked = _settings.Language == idc_lite.Models.Language.English;
        LangChinese.IsChecked = _settings.Language == idc_lite.Models.Language.Chinese;

        if (IntervalSlider != null)
            IntervalSlider.Value = _settings.UpdateIntervalMs;

        if (AutoStartCheck != null)
            AutoStartCheck.IsChecked = AutostartService.IsEnabled();
        if (MinimizeToTrayCheck != null)
            MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        if (StartMinimizedCheck != null)
            StartMinimizedCheck.IsChecked = _settings.StartMinimized;
        if (HighPriorityCheck != null)
            HighPriorityCheck.IsChecked = _settings.HighPriority;

        // Animation
        AnimNone.IsChecked     = _settings.DisplayAnimation == DisplayAnimation.None;
        AnimSmooth.IsChecked   = _settings.DisplayAnimation == DisplayAnimation.Smooth;
        AnimRoller.IsChecked   = _settings.DisplayAnimation == DisplayAnimation.Roller;

        TempSourceAuto.IsChecked = _settings.TempSource == TemperatureSource.Auto;
        TempSourceCoreAvg.IsChecked = _settings.TempSource == TemperatureSource.CoreAverage;
        TempSourceHotspot.IsChecked = _settings.TempSource == TemperatureSource.Hotspot;
        TempSourcePackage.IsChecked = _settings.TempSource == TemperatureSource.Package;

        _initializing = false;
    }

    private void SaveAll()
    {
        if (_initializing) return;
        _settingsService.Save(_settings);
        ApplySettings();
    }

    // ===== Настройки: обработчики =====

    private void Language_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        if (LangRussian.IsChecked == true) _settings.Language = idc_lite.Models.Language.Russian;
        else if (LangEnglish.IsChecked == true) _settings.Language = idc_lite.Models.Language.English;
        else if (LangChinese.IsChecked == true) _settings.Language = idc_lite.Models.Language.Chinese;

        TranslationService.SetLanguage(_settings.Language);
        SaveAll();
        ApplyLanguage();
    }

    private void IntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing || IntervalValue == null) return;

        var val = (int)e.NewValue;
        _settings.UpdateIntervalMs = val;
        IntervalValue.Text = TranslationService.Get(TranslationService.Keys.IntervalMs, val);
        SaveAll();
    }

    private void AutoStartCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        bool enabled = AutoStartCheck.IsChecked ?? false;
        _settings.AutoStart = enabled;

        if (enabled)
            AutostartService.Enable();
        else
            AutostartService.Disable();

        // Синхронизируем чекбокс с реальным результатом
        AutoStartCheck.IsChecked = AutostartService.IsEnabled();

        SaveAll();
    }

    private void MinimizeToTrayCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked ?? true;
        SaveAll();
    }

    private void StartMinimizedCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.StartMinimized = StartMinimizedCheck.IsChecked ?? true;
        SaveAll();
    }

    private void HighPriorityCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.HighPriority = HighPriorityCheck.IsChecked ?? true;
        SaveAll();
    }

    private void Anim_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        if (AnimNone.IsChecked == true) _settings.DisplayAnimation = DisplayAnimation.None;
        else if (AnimSmooth.IsChecked == true) _settings.DisplayAnimation = DisplayAnimation.Smooth;
        else if (AnimRoller.IsChecked == true) _settings.DisplayAnimation = DisplayAnimation.Roller;

        UpdateAnimDescription();
        SaveAll();
    }

    private void UpdateAnimDescription()
    {
        if (AnimDescription == null) return;

        string key = _settings.DisplayAnimation switch
        {
            DisplayAnimation.Smooth => TranslationService.Keys.AnimSmoothDesc,
            DisplayAnimation.Roller  => TranslationService.Keys.AnimRollerDesc,
            _                          => TranslationService.Keys.AnimNoneDesc,
        };

        AnimDescription.Text = TranslationService.Get(key);
    }

    private void TempSource_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        if (TempSourceAuto.IsChecked == true) _settings.TempSource = TemperatureSource.Auto;
        else if (TempSourceCoreAvg.IsChecked == true) _settings.TempSource = TemperatureSource.CoreAverage;
        else if (TempSourceHotspot.IsChecked == true) _settings.TempSource = TemperatureSource.Hotspot;
        else if (TempSourcePackage.IsChecked == true) _settings.TempSource = TemperatureSource.Package;

        SaveAll();
    }

    // ===== Переключение панелей =====

    private void ShowSettings()
    {
        _isSettingsMode = true;

        // Скрываем главную панель
        MainHeader.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Collapsed;

        // Показываем панель настроек
        SettingsHeader.Visibility = Visibility.Visible;
        SettingsContent.Visibility = Visibility.Visible;

        // Принудительный layout — гарантирует видимость
        UpdateLayout();

        LoadSettings();
        ApplyLanguage();
    }

    private void ShowMain()
    {
        _isSettingsMode = false;

        // Скрываем панель настроек
        SettingsHeader.Visibility = Visibility.Collapsed;
        SettingsContent.Visibility = Visibility.Collapsed;

        // Показываем главную панель
        MainHeader.Visibility = Visibility.Visible;
        MainContent.Visibility = Visibility.Visible;

        // Принудительный layout
        UpdateLayout();
    }

    // ===== Window Controls =====

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSettingsMode)
        {
            SaveAll();
            ShowMain();
        }
        else
        {
            // Всегда сворачиваем в трей при нажатии на крестик
            Hide();
        }
    }

    private void WindowBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is System.Windows.Shapes.Path path)
        {
            path.Fill = BrushBtnHov;
            path.Stroke = BrushBtnHov;
        }
    }

    private void WindowBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is System.Windows.Shapes.Path path)
        {
            path.Fill = BrushGray;
            path.Stroke = BrushGray;
        }
    }

    private void CloseBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (CloseBtn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushClsHov;
    }

    private void CloseBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (CloseBtn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushGray;
    }

    private void SettingsCloseBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushClsHov;
    }

    private void SettingsCloseBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushGray;
    }

    private void BackBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (BackBtn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushBtnHov;
    }

    private void BackBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (BackBtn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushGray;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAll();
        ShowMain();
    }

    // ===== Открытие настроек =====

    public void OpenSettings()
    {
        if (!_isSettingsMode)
            ShowSettings();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettings();
    }
}