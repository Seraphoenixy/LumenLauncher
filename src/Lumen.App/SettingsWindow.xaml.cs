using System.Windows;
using Lumen.Core;
using Lumen.Infrastructure;

namespace Lumen.App;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly IGlobalHotkeyService _hotkey;
    private readonly IndexRefreshService _index;

    public SettingsWindow(ISettingsService settings, IGlobalHotkeyService hotkey, IndexRefreshService index)
    {
        _settings = settings; _hotkey = hotkey; _index = index; InitializeComponent();
        HotkeyModifiers.Text = string.Join('+', settings.Current.Hotkey.Modifiers);
        HotkeyKey.Text = settings.Current.Hotkey.Key;
        PortableDirectories.Text = string.Join(Environment.NewLine, settings.Current.PortableApplicationDirectories);
        FolderIndexDirectories.Text = string.Join(Environment.NewLine, settings.Current.FolderIndexDirectories);
        Quicklinks.Text = string.Join(Environment.NewLine, settings.Current.Quicklinks.Select(link => string.IsNullOrWhiteSpace(link.Alias) ? $"{link.Name} | {link.Url}" : $"{link.Name} | {link.Url} | {link.Alias}"));
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
        var folderDirectories = FolderIndexDirectories.Text.Split(["\r\n", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var foldersChanged = !_settings.Current.FolderIndexDirectories.SequenceEqual(folderDirectories, StringComparer.OrdinalIgnoreCase);
        _settings.Current.FolderIndexDirectories = folderDirectories;
        var quicklinks = new List<Quicklink>();
        var invalidLines = new List<int>();
        var lines = Quicklinks.Text.Split(["\r\n", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < lines.Length; index++)
        {
            var parts = lines[index].Split('|', StringSplitOptions.TrimEntries);
            var link = new Quicklink { Name = parts.ElementAtOrDefault(0) ?? string.Empty, Url = parts.ElementAtOrDefault(1) ?? string.Empty, Alias = parts.ElementAtOrDefault(2) };
            if (parts.Length is < 2 or > 3 || !QuicklinkSearchProvider.TryNormalize(link, out var normalizedUrl)) { invalidLines.Add(index + 1); continue; }
            link.Url = normalizedUrl;
            quicklinks.Add(link);
        }
        if (invalidLines.Count > 0)
        {
            _hotkey.Register(previous);
            _settings.Current.Hotkey = previous;
            System.Windows.MessageBox.Show(this, $"Quicklinks 第 {string.Join("、", invalidLines)} 行格式或网址无效。请使用：名称 | URL | 别名", "Lumen Quicklinks", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _settings.Current.Quicklinks = quicklinks;
        _settings.Current.Window.HideOnDeactivated = HideOnDeactivated.IsChecked == true;
        await _settings.SaveAsync();
        if (foldersChanged) _ = _index.RebuildFolderIndexAsync();
        DialogResult = true;
    }
}
