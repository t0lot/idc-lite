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
    private SettingsWindow? _settingsWindow;

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
            // Вся длительность интервала — плавно
            // Для больших скачков (>15°) ускоряем в 2 раза
            double baseDuration = _settings.UpdateIntervalMs;
            _animDurationMs = diff > 15 ? baseDuration * 0.5 : baseDuration;
        }
        else // Roller — быстрая плавная прокрутка
        {
            // 30мс на градус, минимум 100мс, максимум интервал
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
                // Анимация завершена — фиксируем целевое значение
                _animTimer.Stop();
                _displayedTemp = _targetTemp;
                SetTemperatureDisplay(_displayedTemp);
                SendTemperatureRaw(_displayedTemp);
                return;
            }

            // Easing-интерполяция: плавное ускорение и замедление
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

    private void ApplyLanguage()
    {
        Title = TranslationService.Get(TranslationService.Keys.AppTitle);
        HeaderTitle.Text = TranslationService.Get(TranslationService.Keys.AppTitle);
        VersionLabel.Text = TranslationService.Get(TranslationService.Keys.VersionInfo);

        // Update status text and interval text with current language
        UpdateStatusUI();
    }

    public void ApplySettings()
    {
        StartUpdateTimer();

        // Apply language changes (in case settings window changed it)
        TranslationService.SetLanguage(_settings.Language);
        ApplyLanguage();

        // Apply animation mode + сброс
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

    // ===== Window Controls =====

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Всегда сворачиваем в трей при нажатии на крестик
        Hide();
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

    // ===== Окно настроек: заменяет главное на той же позиции =====

    public void OpenSettings()
    {
        SettingsButton_Click(this, new RoutedEventArgs());
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _settingsService, this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        this.Hide();
        _settingsWindow.Show();
    }

    /// <summary>Возвращает позицию/размер главного окна для оверлея настроек.</summary>
    public Rect GetWindowBounds()
    {
        return new Rect(Left, Top, Width, Height);
    }

    /// <summary>Восстанавливает позицию/размер после закрытия настроек.</summary>
    public void RestoreWindowBounds(double left, double top)
    {
        Left = left;
        Top = top;
        Show();
        Activate();
    }

    }