using Microsoft.Extensions.Logging;
using Lumen.Core;
using System.Diagnostics;
using System.IO;

namespace Lumen.App;

public sealed class IndexRefreshService(IApplicationDiscoveryService discovery, IMsixApplicationDiscoveryService msixDiscovery, IPortableScanner portableScanner, IFolderScanner folderScanner, IApplicationStore store, ILogger<IndexRefreshService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task RebuildAsync(CancellationToken cancellationToken = default) => RebuildAsync(includePortable: true, startPinyinIndexer: true, cancellationToken);
    public Task RebuildStartupApplicationsAsync(CancellationToken cancellationToken = default) => RebuildAsync(includePortable: false, startPinyinIndexer: false, cancellationToken);
    public Task RebuildPortableApplicationsAsync(CancellationToken cancellationToken = default) => RebuildPortableAsync(cancellationToken);
    public Task RebuildFolderIndexAsync(CancellationToken cancellationToken = default) => RebuildFoldersAsync(cancellationToken);

    private async Task RebuildAsync(bool includePortable, bool startPinyinIndexer, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var discovered = await DiscoverSafely("Windows", discovery.DiscoverAsync, cancellationToken);
            var msix = await DiscoverSafely("MSIX", msixDiscovery.DiscoverAsync, cancellationToken);
            var portable = includePortable ? await DiscoverSafely("PATH/portable", portableScanner.ScanAsync, cancellationToken) : [];
            var folders = await DiscoverFoldersSafely(cancellationToken);
            await store.UpsertApplicationsAsync(discovered.Concat(msix).Concat(portable), cancellationToken);
            await store.ReplaceFoldersAsync(folders, cancellationToken);
            if (startPinyinIndexer) StartPinyinIndexer();
            logger.LogInformation("Index rebuilt: {Applications} applications", discovered.Count + msix.Count + portable.Count);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogError(ex, "Index rebuild failed"); }
        finally { _gate.Release(); }
    }

    private async Task RebuildPortableAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var portable = await DiscoverSafely("PATH/portable", portableScanner.ScanAsync, cancellationToken);
            await store.UpsertApplicationsAsync(portable, cancellationToken);
            StartPinyinIndexer();
            logger.LogInformation("Portable application index rebuilt: {Applications} applications", portable.Count);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogError(ex, "Portable application index rebuild failed"); }
        finally { _gate.Release(); }
    }

    private async Task RebuildFoldersAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var folders = await DiscoverFoldersSafely(cancellationToken);
            await store.ReplaceFoldersAsync(folders, cancellationToken);
            logger.LogInformation("Folder index rebuilt: {Folders} folders", folders.Count);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogError(ex, "Folder index rebuild failed"); }
        finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<ApplicationEntry>> DiscoverSafely(string source, Func<CancellationToken, Task<IReadOnlyList<ApplicationEntry>>> action, CancellationToken cancellationToken)
    {
        try { return await action(cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "{Source} discovery failed; continuing with other sources", source); return []; }
    }
    private async Task<IReadOnlyList<FolderEntry>> DiscoverFoldersSafely(CancellationToken cancellationToken)
    {
        try { return await folderScanner.ScanAsync(cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Folder discovery failed"); return []; }
    }
    private void StartPinyinIndexer()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Lumen.Indexer.exe");
        if (!File.Exists(path)) { logger.LogWarning("Pinyin indexer is not available at {Path}", path); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or UnauthorizedAccessException) { logger.LogWarning(ex, "Could not start pinyin indexer"); }
    }
}
