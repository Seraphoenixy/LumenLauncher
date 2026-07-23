using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using Drawing = System.Drawing;

namespace Lumen.App;

public sealed class IconCacheService
{
    private readonly Dictionary<string, ImageSource> _memory = new(StringComparer.OrdinalIgnoreCase);

    public void Reset() => _memory.Clear();

    public ImageSource? GetIcon(string? iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return null;
        var icon = Parse(iconKey);
        var source = icon.Path;
        if (!icon.IsShellItem && !File.Exists(source)) return null;
        var key = BuildKey(icon);
        if (_memory.TryGetValue(key, out var cached)) return cached;
        try
        {
            var image = icon.IsShellItem ? ExtractShellItem(source) : Extract(source, icon.Index);
            if (image is not null) _memory[key] = image;
            return image;
        }
        catch (Exception) { return null; }
    }

    private static IconSource Parse(string iconKey)
    {
        var value = Environment.ExpandEnvironmentVariables(iconKey.Trim());
        if (value.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return new(value, 0, true);
        var index = 0;
        var separator = value.LastIndexOf(',');
        if (separator >= 0 && int.TryParse(value[(separator + 1)..].Trim(), out var parsed)) { index = parsed; value = value[..separator]; }
        return new(value.Trim().Trim('"'), index, false);
    }

    private static string BuildKey(IconSource icon)
    {
        return $"{icon.Path.ToLowerInvariant()}|{icon.Index}";
    }

    private static ImageSource? Extract(string source, int index)
    {
        var handles = new IntPtr[1];
        if (PrivateExtractIcons(source, index, 32, 32, handles, null, 1, 0) > 0 && handles[0] != IntPtr.Zero)
        {
            try { return CreateImage(handles[0]); }
            finally { DestroyIcon(handles[0]); }
        }
        using var icon = Drawing.Icon.ExtractAssociatedIcon(source);
        return icon is null ? null : CreateImage(icon.Handle);
    }

    private static ImageSource? ExtractShellItem(string parsingName)
    {
        var result = SHGetFileInfo(parsingName, 0, out var info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
        try { return CreateImage(info.hIcon); }
        finally { DestroyIcon(info.hIcon); }
    }

    private static BitmapSource CreateImage(IntPtr handle)
    {
        var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
        image.Freeze();
        return image;
    }

    private sealed record IconSource(string Path, int Index, bool IsShellItem);
    private const uint ShgfiIcon = 0x00000100, ShgfiLargeIcon = 0x00000000;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ShFileInfo { public IntPtr hIcon; public IntPtr iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attributes, out ShFileInfo info, uint cbFileInfo, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint PrivateExtractIcons(string fileName, int iconIndex, int cxIcon, int cyIcon, [Out] IntPtr[] iconHandles, [Out] uint[]? iconIds, uint iconCount, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
}
