using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lumen.Core;
using Lumen.Infrastructure;
namespace Lumen.App;
public partial class App : System.Windows.Application
{
 private ServiceProvider? _services;
 protected override async void OnStartup(StartupEventArgs e)
 {
  DpiAwareness.EnablePerMonitorV2(); base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
  var services = new ServiceCollection();
  services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
  services.AddSingleton<ISettingsService, JsonSettingsService>(); services.AddSingleton<IApplicationStore, SqliteApplicationStore>();
  services.AddSingleton<IExecutableCandidateScorer, DefaultExecutableCandidateScorer>(); services.AddSingleton<IShortcutResolver, WindowsShortcutResolver>();
  services.AddSingleton<IApplicationDiscoveryService, WindowsApplicationDiscoveryService>(); services.AddSingleton<IMsixApplicationDiscoveryService, MsixApplicationDiscoveryService>();
  services.AddSingleton<IPortableScanner, PortableApplicationScanner>(); services.AddSingleton<IFolderScanner, FolderScanner>(); services.AddSingleton<IResultExecutor, ResultExecutor>();
  services.AddSingleton<ISearchProvider, ApplicationSearchProvider>(); services.AddSingleton<ISearchProvider, PortableApplicationSearchProvider>(); services.AddSingleton<ISearchProvider, FolderSearchProvider>(); services.AddSingleton<ISearchProvider, QuicklinkSearchProvider>(); services.AddSingleton<ISearchProvider, BuiltInCommandSearchProvider>();
  services.AddSingleton<SearchAggregator>(); services.AddSingleton<IconCacheService>(); services.AddSingleton<MainWindowViewModel>(); services.AddSingleton<MainWindow>();
  services.AddSingleton<ILauncherWindowService>(provider => provider.GetRequiredService<MainWindow>()); services.AddSingleton<IGlobalHotkeyService, WpfGlobalHotkeyService>(); services.AddSingleton<IndexRefreshService>(); services.AddSingleton<StartupService>(); services.AddSingleton<TrayIconService>(); services.AddTransient<SettingsWindow>();
  _services = services.BuildServiceProvider();
  try { await _services.GetRequiredService<ISettingsService>().LoadAsync(); await _services.GetRequiredService<IApplicationStore>().InitializeAsync(); }
  catch (Exception ex) { _services.GetRequiredService<ILogger<App>>().LogError(ex, "Startup initialization failed"); }
  var tray = _services.GetRequiredService<TrayIconService>(); var window = _services.GetRequiredService<MainWindow>(); var hotkey = _services.GetRequiredService<IGlobalHotkeyService>();
  hotkey.HotkeyPressed += (_, _) => window.ToggleLauncher();
  if (!hotkey.Register(_services.GetRequiredService<ISettingsService>().Current.Hotkey)) tray.ShowWarning("快捷键不可用", "默认快捷键已被其他程序占用；请在设置中更换。");
  window.ShowLauncher();
  _ = _services.GetRequiredService<IndexRefreshService>().RebuildAsync();
 }
 protected override void OnExit(ExitEventArgs e){_services?.Dispose();base.OnExit(e);}
}
