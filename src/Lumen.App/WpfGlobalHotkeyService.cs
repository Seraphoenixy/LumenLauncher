using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Lumen.Core;

namespace Lumen.App;

public sealed class WpfGlobalHotkeyService(ILogger<WpfGlobalHotkeyService> logger) : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const int WmKeyUp = 0x0101, WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12, VkLWin = 0x5B, VkRWin = 0x5C;
    private const int VkLShift = 0xA0, VkRShift = 0xA1, VkLControl = 0xA2, VkRControl = 0xA3, VkLMenu = 0xA4, VkRMenu = 0xA5;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModWin = 0x0008, ModNoRepeat = 0x4000;
    private const int Id = 0x4C55;
    private bool _registered;
    private uint _registeredModifiers;
    private uint _pendingModifierKeyUps;
    private long _suppressKeyUpsUntil;

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
        if (_registered)
        {
            _registeredModifiers = modifiers;
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        }
        else logger.LogWarning("Windows rejected global hotkey: {Modifiers}+{Key} (error {Error})", string.Join('+', gesture.Modifiers), gesture.Key, Marshal.GetLastWin32Error());
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
        UnregisterHotKey(IntPtr.Zero, Id);
        _registeredModifiers = 0;
        _pendingModifierKeyUps = 0;
        _registered = false;
    }

    public void Dispose() => Unregister();

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (_pendingModifierKeyUps != 0 && Environment.TickCount64 > _suppressKeyUpsUntil)
        {
            _pendingModifierKeyUps = 0;
        }

        if (msg.message is WmKeyUp or WmSysKeyUp
            && TryGetModifier(msg.wParam.ToInt32(), out var releasedModifier)
            && (_pendingModifierKeyUps & releasedModifier) != 0)
        {
            _pendingModifierKeyUps &= ~releasedModifier;
            handled = true;
            return;
        }

        if (msg.message != WmHotkey || msg.wParam.ToInt32() != Id) return;
        handled = true;
        _pendingModifierKeyUps = _registeredModifiers;
        _suppressKeyUpsUntil = Environment.TickCount64 + 1000;
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryGetModifier(int virtualKey, out uint modifier)
    {
        modifier = virtualKey switch
        {
            VkMenu or VkLMenu or VkRMenu => ModAlt,
            VkControl or VkLControl or VkRControl => ModControl,
            VkShift or VkLShift or VkRShift => ModShift,
            VkLWin or VkRWin => ModWin,
            _ => 0
        };
        return modifier != 0;
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
