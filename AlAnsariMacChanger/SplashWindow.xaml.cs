using System.Windows;
using System.Windows.Threading;

namespace AlAnsariMacChanger;

public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly string[] _steps =
    {
        "Loading configuration…",
        "Reading network adapters…",
        "Preparing the backup store…",
        "Loading system identity…",
        "Ready."
    };

    private double _progress;
    private int _step;

    public SplashWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _progress = Math.Min(96, _progress + 2.2);
        LoadingBar.Value = _progress;

        var index = Math.Min(_steps.Length - 1, (int)(_progress / (100.0 / _steps.Length)));
        if (index != _step)
        {
            _step = index;
            LoadingText.Text = _steps[index];
        }

        if (_progress >= 96)
            _timer.Stop();
    }
}
