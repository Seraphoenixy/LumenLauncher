namespace Lumen.Core;

public interface ISearchProvider
{
    string ProviderId { get; }
    Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery query, CancellationToken cancellationToken);
}
public interface IExecutableCandidateScorer { int Score(ExecutableCandidate candidate); }
public interface IApplicationStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task UpsertApplicationsAsync(IEnumerable<ApplicationEntry> entries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationEntry>> SearchApplicationsAsync(string query, string? source, int limit, CancellationToken cancellationToken = default);
    Task RecordUsageAsync(SearchResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
public interface IApplicationDiscoveryService { Task<IReadOnlyList<ApplicationEntry>> DiscoverAsync(CancellationToken cancellationToken); }
public interface IMsixApplicationDiscoveryService { Task<IReadOnlyList<ApplicationEntry>> DiscoverAsync(CancellationToken cancellationToken); }
public interface IShortcutResolver { bool TryResolve(string shortcutPath, out ShortcutTarget target); }
public interface IPortableScanner { Task<IReadOnlyList<ApplicationEntry>> ScanAsync(CancellationToken cancellationToken); }
public interface IResultExecutor { Task ExecuteAsync(SearchResult result, CancellationToken cancellationToken = default); }
public interface ISettingsService { LumenSettings Current { get; } Task LoadAsync(CancellationToken cancellationToken = default); Task SaveAsync(CancellationToken cancellationToken = default); }
public interface IGlobalHotkeyService : IDisposable { event EventHandler? HotkeyPressed; bool Register(HotkeyGesture gesture); void Unregister(); }
public interface ILauncherWindowService { void ShowLauncher(); void HideLauncher(); void ToggleLauncher(); }
public sealed record HotkeyGesture(IReadOnlyList<string> Modifiers, string Key);
public sealed record ShortcutTarget(string TargetPath, string? Arguments, string WorkingDirectory, string? IconPath);
public sealed class LumenSettings
{
    public HotkeyGesture Hotkey { get; set; } = new(["Alt"], "Space");
    public List<string> PortableApplicationDirectories { get; set; } = [];
    public int PortableScanMaxDepth { get; set; } = 4;
    public int MinimumExecutableCandidateScore { get; set; } = 20;
    public WindowSettings Window { get; set; } = new();
}
public sealed class WindowSettings { public double Width { get; set; } = 720; public int MaxVisibleResults { get; set; } = 12; public bool HideOnDeactivated { get; set; } = true; public double? Left { get; set; } public double? Top { get; set; } }
