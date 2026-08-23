using System.Drawing;
using System.Windows;
using idc_lite.Models;
using idc_lite.Services;

namespace idc_lite;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private IntPtr _hIcon = IntPtr.Zero;
    private MainWindow? _mainWindow;
    private HidService? _hidService;
    private HardwareService? _hardwareService;
    private SettingsService? _settingsService;
    private AppSettings? _settings;
    private readonly Mutex? _mutex;

    public App()
    {
        _mutex = new Mutex(true, "IDC-Lite-SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Глобальный перехват необработанных исключений — предотвращает краш
        this.DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true; // Не даём приложению умереть
        };

        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            // Последний рубеж — логирование не делаем, просто не падаем
        };

        System.Windows.Threading.Dispatcher.CurrentDispatcher.Hooks.DispatcherInactive
            += (_, _) => { }; // Держим dispatcher живым

        base.OnStartup(e);

        _hidService = new HidService();
        _hardwareService = new HardwareService();
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        TranslationService.SetLanguage(_settings.Language);

        _mainWindow = new MainWindow(_hidService, _hardwareService, _settingsService);

        CreateTrayIcon();

        _mainWindow.Show();

        this.Exit += OnApplicationExit;
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = TranslationService.Get(TranslationService.Keys.TrayTooltip),
            Icon = SystemIcons.Application,
            Visible = true
        };

        try
        {
            // Загружаем логотип из встроенного ресурса (работает в single-file publish)
            var resourceUri = new Uri("pack://application:,,,/logo.png");
            var resInfo = System.Windows.Application.GetResourceStream(resourceUri);
            if (resInfo != null)
            {
                using var ms = new System.IO.MemoryStream();
                resInfo.Stream.CopyTo(ms);
                using var bmp = new System.Drawing.Bitmap(ms);
                _hIcon = bmp.GetHicon();
                _trayIcon.Icon = System.Drawing.Icon.FromHandle(_hIcon);
            }
        }
        catch { }

        BuildTrayMenu();

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public void RebuildTrayMenu()
    {
        if (_trayIcon == null) return;

        if (_trayIcon.ContextMenuStrip != null)
        {
            _trayIcon.ContextMenuStrip.Dispose();
            _trayIcon.ContextMenuStrip = null;
        }

        _trayIcon.Text = TranslationService.Get(TranslationService.Keys.TrayTooltip);
        BuildTrayMenu();
    }

    private void BuildTrayMenu()
    {
        if (_trayIcon == null) return;

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Renderer = new CatppuccinRenderer();
        contextMenu.BackColor = Color.FromArgb(0x1e, 0x1e, 0x2e);
        contextMenu.ForeColor = Color.FromArgb(0xcd, 0xd6, 0xf4);
        contextMenu.ShowImageMargin = false;

        var menuFont = new Font("Segoe UI", 10F);
        var textColor = Color.FromArgb(0xcd, 0xd6, 0xf4);
        var exitColor = Color.FromArgb(0xf3, 0x8b, 0xa8);

        var showItem = new System.Windows.Forms.ToolStripMenuItem(
            TranslationService.Get(TranslationService.Keys.TrayShow));
        showItem.ForeColor = textColor;
        showItem.Font = menuFont;
        showItem.Click += (_, _) => ShowMainWindow();

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem(
            TranslationService.Get(TranslationService.Keys.TraySettings));
        settingsItem.ForeColor = textColor;
        settingsItem.Font = menuFont;
        settingsItem.Click += (_, _) =>
        {
            ShowMainWindow();
            _mainWindow?.OpenSettings();
        };

        var exitItem = new System.Windows.Forms.ToolStripMenuItem(
            TranslationService.Get(TranslationService.Keys.TrayExit));
        exitItem.ForeColor = exitColor;
        exitItem.Font = menuFont;
        exitItem.Click += (_, _) =>
        {
            CleanupTray();
            _mainWindow?.Shutdown();
            Shutdown();
        };

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = contextMenu;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _mainWindow.Activate();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
    }

    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        _mainWindow?.Shutdown();
        CleanupTray();
        _mutex?.Dispose();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CleanupTray();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void CleanupTray()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.ContextMenuStrip?.Dispose();
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_hIcon != IntPtr.Zero)
        {
            try { NativeMethods.DestroyIcon(_hIcon); } catch { }
            _hIcon = IntPtr.Zero;
        }
    }

    private static void ActivateExistingInstance()
    {
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var processes = System.Diagnostics.Process.GetProcessesByName(
            currentProcess.ProcessName);
        foreach (var process in processes)
        {
            if (process.Id != currentProcess.Id)
            {
                NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                break;
            }
        }
    }
}

// ===== Catppuccin Mocha color table & renderer for ToolStrip =====

internal sealed class CatppuccinMochaColorTable : System.Windows.Forms.ProfessionalColorTable
{
    private static readonly Color Base     = Color.FromArgb(0x1e, 0x1e, 0x2e);
    private static readonly Color Mantle  = Color.FromArgb(0x18, 0x18, 0x25);
    private static readonly Color Surface0 = Color.FromArgb(0x31, 0x32, 0x44);

    public override Color MenuBorder => Surface0;
    public override Color MenuItemBorder => Surface0;
    public override Color MenuItemSelected => Surface0;
    public override Color MenuItemSelectedGradientBegin => Surface0;
    public override Color MenuItemSelectedGradientEnd => Surface0;
    public override Color MenuItemPressedGradientBegin => Surface0;
    public override Color MenuItemPressedGradientEnd => Surface0;
    public override Color MenuItemPressedGradientMiddle => Surface0;
    public override Color ImageMarginGradientBegin => Base;
    public override Color ImageMarginGradientEnd => Base;
    public override Color ImageMarginGradientMiddle => Base;
    public override Color SeparatorDark => Surface0;
    public override Color SeparatorLight => Mantle;
    public override Color ToolStripBorder => Surface0;
    public override Color ToolStripGradientBegin => Base;
    public override Color ToolStripGradientEnd => Base;
    public override Color ToolStripGradientMiddle => Base;
    public override Color ToolStripContentPanelGradientBegin => Base;
    public override Color ToolStripContentPanelGradientEnd => Base;
    public override Color ToolStripPanelGradientBegin => Base;
    public override Color ToolStripPanelGradientEnd => Base;
    public override Color StatusStripBorder => Surface0;
    public override Color StatusStripGradientBegin => Base;
    public override Color StatusStripGradientEnd => Base;
    
}

internal sealed class CatppuccinRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    public CatppuccinRenderer() : base(new CatppuccinMochaColorTable()) { }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}