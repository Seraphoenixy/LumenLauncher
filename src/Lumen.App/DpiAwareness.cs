using System.Runtime.InteropServices;

namespace Lumen.App;

internal static class DpiAwareness
{
    private static readonly IntPtr PerMonitorV2 = new(-4);
    public static void EnablePerMonitorV2()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063)) SetProcessDpiAwarenessContext(PerMonitorV2);
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
