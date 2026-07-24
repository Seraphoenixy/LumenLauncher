using System.Drawing;
using System.Reflection;
using Forms = System.Windows.Forms;
using Lumen.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Lumen.App;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIconService(ILauncherWindowService launcher, IndexRefreshService index, StartupService startup, IServiceProvider services)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 Lumen", null, (_, _) => launcher.ShowLauncher());
        menu.Items.Add("设置…", null, (_, _) => services.GetRequiredService<SettingsWindow>().ShowDialog());
        menu.Items.Add("重建索引", null, async (_, _) => await index.RebuildAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        var autoStart = new Forms.ToolStripMenuItem("开机自启动");
        autoStart.Click += (_, _) =>
        {
            try { startup.SetEnabled(!startup.IsEnabled()); autoStart.Checked = startup.IsEnabled(); }
            catch (Exception ex) { autoStart.Checked = startup.IsEnabled(); ShowWarning("开机自启动设置失败", ex.Message); }
        };
        menu.Opening += (_, _) =>
        {
            try { autoStart.Checked = startup.IsEnabled(); }
            catch (Exception ex) { autoStart.Checked = false; ShowWarning("无法读取开机自启动状态", ex.Message); }
        };
        menu.Items.Add(autoStart);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            if (launcher is MainWindow window) window.CloseForShutdown();
            System.Windows.Application.Current.Shutdown();
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem($"Lumen v{GetVersion()}") { Enabled = false });
        _icon = new Forms.NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application, Text = "Lumen 启动器", Visible = true, ContextMenuStrip = menu };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) launcher.ToggleLauncher(); };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
    private static string GetVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "未知";
    public void ShowWarning(string title, string message) => _icon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Warning);
}
