using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AlAnsariMacChanger.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;

namespace AlAnsariMacChanger;

public partial class MainWindow : Window
{
    private readonly TextBox[] _macBoxes;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();

        _macBoxes = new[] { Mac1, Mac2, Mac3, Mac4, Mac5, Mac6 };

        foreach (var box in _macBoxes)
        {
            box.PreviewTextInput += MacBox_PreviewTextInput;
            box.TextChanged += MacBox_TextChanged;
            box.PreviewKeyDown += MacBox_PreviewKeyDown;
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
            DataObject.AddPastingHandler(box, MacBox_Pasting);
        }

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            await viewModel.InitializeAsync();
    }

    public void PrepareForExit() => _exitRequested = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            Hide();

            if (System.Windows.Application.Current is App app)
                app.HideToTray();

            return;
        }

        base.OnClosing(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
            app.ExitApplication();
        else
            Close();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Al-Ansari MAC Changer v1.0\n" +
            "MAC address and system identity changer for Windows\n\n" +
            "• MAC addresses: local, universal and vendor OUI types\n" +
            "• Computer name, NetBIOS, workgroup and MachineGuid\n" +
            "• DNS resolvers, IPv6 privacy and advertising ID\n" +
            "• Automatic backup, restore and change log\n\n" +
            "Runs with administrator privileges — every change is reversible.",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MacBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(Uri.IsHexDigit);
    }

    private void MacBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;

        if (box.Text.Length > 0 && box.Text.Any(c => !Uri.IsHexDigit(c)))
        {
            box.Text = new string(box.Text.Where(Uri.IsHexDigit).ToArray());
            box.CaretIndex = box.Text.Length;
            return;
        }

        if (box.Text.Length == 2 && box.IsKeyboardFocused)
            MoveFocus(box, 1);
    }

    private void MacBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        if (e.Key == Key.Back && box.Text.Length == 0)
        {
            MoveFocus(box, -1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right && box.CaretIndex >= box.Text.Length)
        {
            MoveFocus(box, 1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && box.CaretIndex == 0)
        {
            MoveFocus(box, -1);
            e.Handled = true;
        }
    }

    private void MacBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();

        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)) return;

        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        var hex = new string(text.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();

        if (hex.Length == 0) return;

        if (hex.Length == 12)
        {
            if (DataContext is MainViewModel viewModel) viewModel.SetMacAddress(hex);
            _macBoxes[5].Focus();
            _macBoxes[5].CaretIndex = 2;
            return;
        }

        if (sender is TextBox box && hex.Length <= 2)
        {
            box.Text = hex;
            box.CaretIndex = hex.Length;
        }
    }

    private void MoveFocus(TextBox current, int direction)
    {
        var index = Array.IndexOf(_macBoxes, current);
        if (index < 0) return;

        var target = index + direction;
        if (target < 0 || target >= _macBoxes.Length) return;

        var next = _macBoxes[target];
        next.Focus();
        next.SelectAll();
    }
}
