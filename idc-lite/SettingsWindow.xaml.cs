using System.Windows;
using System.Windows.Input;
using idc_lite.Models;
using idc_lite.Services;

using DisplayAnimation = idc_lite.Models.DisplayAnimation;

namespace idc_lite;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly MainWindow _mainWindow;
    private bool _initializing = true;

    // Cached brushes
    private static readonly WpfBrush BrushDef = new(WpfColor.FromRgb(0x6c, 0x70, 0x86));
    private static readonly WpfBrush BrushCloseHov = new(WpfColor.FromRgb(0xf3, 0x8b, 0xa8));
    private static readonly WpfBrush BrushBackHov = new(WpfColor.FromRgb(0xcd, 0xd6, 0xf4));

    public SettingsWindow(AppSettings settings, SettingsService settingsService, MainWindow mainWindow)
    {
        InitializeComponent();

        _settings = settings;
        _settingsService = settingsService;
        _mainWindow = mainWindow;

        // Встать ровно на место главного окна
        var bounds = _mainWindow.GetWindowBounds();
        Left = bounds.X;
        Top = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;

        Language_Changed_Internal();
        ApplyLanguage();

        LoadSettings();

        try
        {
            AppDataPath.Text = TranslationService.Get(TranslationService.Keys.SettingsPath, SettingsService.GetAppDataPath());
        }
        catch { }

        _initializing = false;
    }

    private void ApplyLanguage()
    {
        Title = TranslationService.Get(TranslationService.Keys.Settings);
        HeaderTitle.Text = TranslationService.Get(TranslationService.Keys.Settings);

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

        // Update interval text with current language
        if (IntervalValue != null)
            IntervalValue.Text = TranslationService.Get(TranslationService.Keys.IntervalMs, _settings.UpdateIntervalMs);

        if (!_initializing)
        {
            try
            {
                AppDataPath.Text = TranslationService.Get(TranslationService.Keys.SettingsPath, SettingsService.GetAppDataPath());
            }
            catch { }
        }
    }

    private void Language_Changed_Internal()
    {
        TranslationService.SetLanguage(_settings.Language);
    }

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

    private void LoadSettings()
    {
        // Language
        LangRussian.IsChecked = _settings.Language == idc_lite.Models.Language.Russian;
        LangEnglish.IsChecked = _settings.Language == idc_lite.Models.Language.English;
        LangChinese.IsChecked = _settings.Language == idc_lite.Models.Language.Chinese;

        if (IntervalSlider != null)
            IntervalSlider.Value = _settings.UpdateIntervalMs;

        if (AutoStartCheck != null)
            AutoStartCheck.IsChecked = _settings.AutoStart;
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
    }

    private void SaveAll()
    {
        if (_initializing) return;
        _settingsService.Save(_settings);
        _mainWindow.ApplySettings();
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
        _settings.AutoStart = AutoStartCheck.IsChecked ?? false;
        if (_settings.AutoStart)
            AutostartService.Enable();
        else
            AutostartService.Disable();
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

    // ===== Navigation =====

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAll();
        var left = Left;
        var top = Top;
        Close();
        _mainWindow.RestoreWindowBounds(left, top);
    }

    // ===== Window dragging =====

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAll();
        var left = Left;
        var top = Top;
        Close();
        _mainWindow.RestoreWindowBounds(left, top);
    }

    // ===== Hover effects =====

    private void BackBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (BackBtn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushBackHov;
    }

    private void BackBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (BackBtn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushDef;
    }

    private void CloseBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushCloseHov;
    }

    private void CloseBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is System.Windows.Shapes.Path path)
            path.Stroke = BrushDef;
    }

    }