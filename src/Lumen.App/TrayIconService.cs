using System.Drawing;
using Forms = System.Windows.Forms;
using Lumen.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Lumen.App;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIconService(ILauncherWindowService launcher, IndexRefreshService index, IServiceProvider services)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 Lumen", null, (_, _) => launcher.ShowLauncher());
        menu.Items.Add("设置…", null, (_, _) => services.GetRequiredService<SettingsWindow>().ShowDialog());
        menu.Items.Add("重建索引", null, async (_, _) => await index.RebuildAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            if (launcher is MainWindow window) window.CloseForShutdown();
            System.Windows.Application.Current.Shutdown();
        });
        _icon = new Forms.NotifyIcon { Icon = SystemIcons.Application, Text = "Lumen 启动器", Visible = true, ContextMenuStrip = menu };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) launcher.ToggleLauncher(); };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
    public void ShowWarning(string title, string message) => _icon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Warning);
}
