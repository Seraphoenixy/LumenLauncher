using System.Windows;
using Lumen.Core;

namespace Lumen.App;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly IGlobalHotkeyService _hotkey;

    public SettingsWindow(ISettingsService settings, IGlobalHotkeyService hotkey)
    {
        _settings = settings; _hotkey = hotkey; InitializeComponent();
        HotkeyModifiers.Text = string.Join('+', settings.Current.Hotkey.Modifiers);
        HotkeyKey.Text = settings.Current.Hotkey.Key;
        PortableDirectories.Text = string.Join(Environment.NewLine, settings.Current.PortableApplicationDirectories);
        HideOnDeactivated.IsChecked = settings.Current.Window.HideOnDeactivated;
    }

    private async void Save(object sender, RoutedEventArgs e)
    {
        var modifiers = HotkeyModifiers.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var candidate = new HotkeyGesture(modifiers, HotkeyKey.Text.Trim());
        var previous = _settings.Current.Hotkey;
        if (!_hotkey.Register(candidate))
        {
            _hotkey.Register(previous);
            System.Windows.MessageBox.Show(this, "该快捷键无效，或已被其他程序占用。请换一个组合键。", "Lumen 快捷键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _settings.Current.Hotkey = candidate;
        _settings.Current.PortableApplicationDirectories = PortableDirectories.Text.Split(["\r\n", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _settings.Current.Window.HideOnDeactivated = HideOnDeactivated.IsChecked == true;
        await _settings.SaveAsync();
        DialogResult = true;
    }
}
