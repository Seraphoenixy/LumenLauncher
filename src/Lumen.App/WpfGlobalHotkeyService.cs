using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Lumen.Core;

namespace Lumen.App;

public sealed class WpfGlobalHotkeyService(ILogger<WpfGlobalHotkeyService> logger) : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModWin = 0x0008, ModNoRepeat = 0x4000;
    private const int Id = 0x4C55;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        if (!TryParse(gesture, out var modifiers, out var virtualKey))
        {
            logger.LogWarning("Invalid global hotkey: {Modifiers}+{Key}", string.Join('+', gesture.Modifiers), gesture.Key);
            return false;
        }

        _registered = RegisterHotKey(IntPtr.Zero, Id, modifiers | ModNoRepeat, virtualKey);
        if (_registered) ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        else logger.LogWarning("Windows rejected global hotkey: {Modifiers}+{Key} (error {Error})", string.Join('+', gesture.Modifiers), gesture.Key, Marshal.GetLastWin32Error());
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
        UnregisterHotKey(IntPtr.Zero, Id);
        _registered = false;
    }

    public void Dispose() => Unregister();

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message != WmHotkey || msg.wParam.ToInt32() != Id) return;
        handled = true;
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryParse(HotkeyGesture gesture, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        foreach (var modifier in gesture.Modifiers)
        {
            if (modifier.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModAlt;
            else if (modifier.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= ModControl;
            else if (modifier.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModShift;
            else if (modifier.Equals("Win", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Windows", StringComparison.OrdinalIgnoreCase)) modifiers |= ModWin;
            else { virtualKey = 0; return false; }
        }
        if (!Enum.TryParse<Key>(gesture.Key, true, out var key) || key is Key.None or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            virtualKey = 0;
            return false;
        }
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
