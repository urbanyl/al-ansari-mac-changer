using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace AlAnsariMacChanger;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        CreateTrayIcon();

        var splash = new SplashWindow();
        splash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2800) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ShowMainWindow();
            splash.Close();
        };
        timer.Start();
    }

    private void CreateTrayIcon()
    {
        var icon = Environment.ProcessPath is { } processPath
            ? Icon.ExtractAssociatedIcon(processPath)
            : null;

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Al-Ansari MAC Changer",
            Icon = icon ?? SystemIcons.Application,
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Al-Ansari MAC Changer", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitApplication());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            _mainWindow.Show();
            _mainWindow.Activate();
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    }

    public void HideToTray()
    {
        _mainWindow?.Hide();
        _trayIcon?.ShowBalloonTip(
            2500,
            "Al-Ansari MAC Changer",
            "The application keeps running in the background. Click the tray icon to reopen it.",
            Forms.ToolTipIcon.Info);
    }

    public void ExitApplication()
    {
        _mainWindow?.PrepareForExit();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        Shutdown();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "Al-Ansari MAC Changer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
