using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Lumen.Core;

namespace Lumen.Infrastructure;

public sealed class WindowsApplicationDiscoveryService(IShortcutResolver shortcuts, ILogger<WindowsApplicationDiscoveryService> logger) : IApplicationDiscoveryService
{
    public Task<IReadOnlyList<ApplicationEntry>> DiscoverAsync(CancellationToken ct) => Task.Run(() =>
    {
        var items = new Dictionary<string, ApplicationEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) })
            AddFiles(root, items, shortcuts, ct);
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine }) foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) try { using var key = RegistryKey.OpenBaseKey(hive == Registry.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, view).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths"); if(key is null) continue; foreach(var name in key.GetSubKeyNames()){ using var child=key.OpenSubKey(name); var value=child?.GetValue(null) as string; if(!string.IsNullOrWhiteSpace(value) && File.Exists(value)) Add(value, "app-path", items); } } catch (UnauthorizedAccessException ex) { logger.LogDebug(ex,"Registry view unavailable"); }
        AddPathApplications(items, ct);
        AddWindowsBuiltIns(items);
        return (IReadOnlyList<ApplicationEntry>)items.Values.ToList();
    }, ct);
    private static void AddFiles(string root, Dictionary<string,ApplicationEntry> items,IShortcutResolver shortcuts,CancellationToken ct) { if(!Directory.Exists(root)) return; try { foreach(var file in Directory.EnumerateFiles(root,"*.*",SearchOption.AllDirectories)){ct.ThrowIfCancellationRequested();if(Path.GetExtension(file).Equals(".exe",StringComparison.OrdinalIgnoreCase)) Add(file,"start-menu",items); else if(Path.GetExtension(file).Equals(".lnk",StringComparison.OrdinalIgnoreCase) && shortcuts.TryResolve(file,out var target) && File.Exists(target.TargetPath)) AddShortcut(file,target,items); } } catch(IOException){} catch(UnauthorizedAccessException){} }
    private static void Add(string path,string source,Dictionary<string,ApplicationEntry> items){var full=Path.GetFullPath(path);items.TryAdd(full,new($"app:{full.ToLowerInvariant()}",source,Path.GetFileNameWithoutExtension(full),full,null,Path.GetDirectoryName(full)!,full,50));}
    private static void AddShortcut(string shortcutPath,ShortcutTarget target,Dictionary<string,ApplicationEntry> items){var id=$"shortcut:{Path.GetFullPath(shortcutPath).ToLowerInvariant()}";items.TryAdd(id,new(id,"start-menu",Path.GetFileNameWithoutExtension(shortcutPath),target.TargetPath,target.Arguments,string.IsNullOrWhiteSpace(target.WorkingDirectory)?Path.GetDirectoryName(target.TargetPath)!:target.WorkingDirectory,target.IconPath??target.TargetPath,65));}
    private static void AddPathApplications(Dictionary<string,ApplicationEntry> items,CancellationToken ct){var extensions=(Environment.GetEnvironmentVariable("PATHEXT")??".EXE;.COM;.BAT;.CMD").Split(';',StringSplitOptions.RemoveEmptyEntries);var paths=(Environment.GetEnvironmentVariable("PATH")??string.Empty).Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase);foreach(var path in paths){if(!Directory.Exists(path))continue;try{foreach(var file in Directory.EnumerateFiles(path)){ct.ThrowIfCancellationRequested();if(extensions.Contains(Path.GetExtension(file),StringComparer.OrdinalIgnoreCase))Add(file,"path",items);}}catch(IOException){}catch(UnauthorizedAccessException){}}}
    private static void AddWindowsBuiltIns(Dictionary<string,ApplicationEntry> items)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system = Path.Combine(windows, "System32");
        AddBuiltIn("calculator", "计算器", Path.Combine(system, "calc.exe"));
        AddBuiltIn("explorer", "文件资源管理器", Path.Combine(windows, "explorer.exe"));
        AddBuiltIn("control-panel", "控制面板", Path.Combine(system, "control.exe"), null, "shell:ControlPanelFolder");
        AddBuiltIn("notepad", "记事本", Path.Combine(system, "notepad.exe"));
        AddBuiltIn("paint", "画图", Path.Combine(system, "mspaint.exe"));
        AddBuiltIn("recycle-bin", "回收站", Path.Combine(windows, "explorer.exe"), "shell:RecycleBinFolder", "shell:RecycleBinFolder");
        items.TryAdd("windows:settings", new("windows:settings", "windows-system", "设置", "ms-settings:", null, string.Empty, null, 70));

        void AddBuiltIn(string id, string name, string executable, string? arguments = null, string? icon = null)
        {
            if (!File.Exists(executable)) return;
            items.TryAdd($"windows:{id}", new($"windows:{id}", "windows-system", name, executable, arguments, Path.GetDirectoryName(executable)!, icon ?? executable, 70));
        }
    }
}
public sealed class WindowsShortcutResolver(ILogger<WindowsShortcutResolver> logger) : IShortcutResolver
{
    public bool TryResolve(string shortcutPath, out ShortcutTarget target)
    {
        target = default!;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return false;
            var shell = Activator.CreateInstance(type)!;
            var shortcut = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath])!;
            var shortcutType = shortcut.GetType();
            var path = shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            if (string.IsNullOrWhiteSpace(path)) return false;
            var arguments = shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            var workingDirectory = shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            var iconLocation = shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            target = new(path, arguments, workingDirectory ?? string.Empty, NormalizeIconLocation(iconLocation));
            return true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or System.Reflection.TargetInvocationException)
        {
            logger.LogDebug(ex, "Could not resolve shortcut {Shortcut}", shortcutPath);
            return false;
        }
    }
    private static string? NormalizeIconLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        var value = location.Trim();
        var separator = value.LastIndexOf(',');
        if (separator >= 0 && int.TryParse(value[(separator + 1)..].Trim(), out _))
            return string.IsNullOrWhiteSpace(value[..separator].Trim().Trim('"')) ? null : value;
        return string.IsNullOrWhiteSpace(value.Trim('"')) ? null : value;
    }
}
public sealed class MsixApplicationDiscoveryService(ILogger<MsixApplicationDiscoveryService> logger) : IMsixApplicationDiscoveryService
{
    public async Task<IReadOnlyList<ApplicationEntry>> DiscoverAsync(CancellationToken ct)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"Get-StartApps | Select-Object Name,AppID | ConvertTo-Json -Compress\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            if (process is null) return [];
            var json = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json)) return [];
            using var doc = JsonDocument.Parse(json); var apps = new List<ApplicationEntry>();
            IEnumerable<JsonElement> values = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToArray() : [doc.RootElement];
            foreach (var item in values)
            {
                ct.ThrowIfCancellationRequested();
                var name = item.TryGetProperty("Name", out var n) ? n.GetString() : null;
                var id = item.TryGetProperty("AppID", out var a) ? a.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) || !id.Contains('!')) continue;
                apps.Add(new($"msix:{id.ToLowerInvariant()}", "msix", name, id, null, string.Empty, $"shell:AppsFolder\\{id}", 55));
            }
            return apps;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or JsonException)
        {
            logger.LogDebug(ex, "Could not enumerate MSIX applications");
            return [];
        }
    }
}
public sealed class PortableApplicationScanner(ISettingsService settings,IExecutableCandidateScorer scorer,ILogger<PortableApplicationScanner> logger) : IPortableScanner
{
    public Task<IReadOnlyList<ApplicationEntry>> ScanAsync(CancellationToken ct) => Task.Run(() => { var list=new List<ApplicationEntry>(); foreach(var root in settings.Current.PortableApplicationDirectories.Distinct(StringComparer.OrdinalIgnoreCase)){if(!Directory.Exists(root)){logger.LogWarning("Portable directory does not exist: {Directory}",root);continue;} foreach(var file in Enumerate(root,settings.Current.PortableScanMaxDepth,ct)){try{var info=FileVersionInfo.GetVersionInfo(file);var f=new FileInfo(file);var candidate=new ExecutableCandidate(file,f.Name,info.ProductName,info.FileDescription,info.CompanyName,info.FileVersion,true?f.LastWriteTimeUtc:DateTime.UtcNow,f.Length,true,new DirectoryInfo(f.DirectoryName!).Name,Depth(root,file),false);var score=scorer.Score(candidate);if(score>=settings.Current.MinimumExecutableCandidateScore) list.Add(new($"portable:{file.ToLowerInvariant()}","portable",First(info.ProductName,info.FileDescription,Path.GetFileNameWithoutExtension(file)),file,null,f.DirectoryName!,file,score));}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){logger.LogDebug(ex,"Skipped executable {Path}",file);}}}return (IReadOnlyList<ApplicationEntry>)list;},ct);
    private static IEnumerable<string> Enumerate(string root,int max,CancellationToken ct){var stack=new Stack<(string,int)>();stack.Push((root,0));while(stack.Count>0){var (dir,depth)=stack.Pop();IEnumerable<string> files;try{files=Directory.EnumerateFiles(dir,"*.exe");}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){continue;}foreach(var f in files){ct.ThrowIfCancellationRequested();yield return f;}if(depth>=max)continue;IEnumerable<string> dirs;try{dirs=Directory.EnumerateDirectories(dir);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){continue;}foreach(var d in dirs)stack.Push((d,depth+1));}}
    private static int Depth(string root,string file)=>Path.GetRelativePath(root,Path.GetDirectoryName(file)!).Split(Path.DirectorySeparatorChar).Length-1;
    private static string First(params string?[] values)=>values.FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x))??"Application";
}
public sealed class ApplicationSearchProvider(IApplicationStore store) : ISearchProvider { public string ProviderId=>"applications"; public async Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery q,CancellationToken ct){if(q.IsEmpty)return await store.GetRecentAsync(q.Limit,ct);var a=await store.SearchApplicationsAsync(q.Text,null,q.Limit,ct);return a.Select(x=>ToResult(x,q)).ToList();} internal static SearchResult ToResult(ApplicationEntry x,SearchQuery q){var type=x.Source=="portable"?SearchResultType.PortableApplication:SearchResultType.Application;var action=x.Source=="msix"?new SearchAction("appsfolder",x.ExecutablePath):new SearchAction("process",x.ExecutablePath,x.Arguments,x.WorkingDirectory);return new(x.Id,type,x.DisplayName,x.ExecutablePath,x.IconKey,Math.Max(TextMatcher.Score(x.DisplayName,q.Text),TextMatcher.Score(x.SearchText,q.Text))+x.CandidateScore,action);} }
public sealed class PortableApplicationSearchProvider(IApplicationStore store) : ISearchProvider { public string ProviderId=>"portable";public async Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery q,CancellationToken ct){if(q.IsEmpty)return [];var a=await store.SearchApplicationsAsync(q.Text,"portable",q.Limit,ct);return a.Select(x=>ApplicationSearchProvider.ToResult(x,q) with { Type=SearchResultType.PortableApplication}).ToList();}}
public sealed class FolderScanner(ISettingsService settings, ILogger<FolderScanner> logger) : IFolderScanner
{
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase) { ".git", ".svn", "node_modules", "bin", "obj", "$RECYCLE.BIN", "System Volume Information" };
    public Task<IReadOnlyList<FolderEntry>> ScanAsync(CancellationToken ct) => Task.Run(() =>
    {
        var entries = new Dictionary<string, FolderEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredRoot in settings.Current.FolderIndexDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(configuredRoot)) { logger.LogWarning("Folder index root does not exist: {Directory}", configuredRoot); continue; }
            var root = Path.GetFullPath(configuredRoot);
            Add(root, root, 0);
            var stack = new Stack<(string Path, int Depth)>(); stack.Push((root, 0));
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (directory, depth) = stack.Pop();
                if (depth >= Math.Max(0, settings.Current.FolderIndexMaxDepth)) continue;
                IEnumerable<string> children;
                try { children = Directory.EnumerateDirectories(directory); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                foreach (var child in children)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new DirectoryInfo(child);
                        if (ExcludedNames.Contains(info.Name) || (info.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0) continue;
                        var full = Path.GetFullPath(child); Add(full, root, depth + 1); stack.Push((full, depth + 1));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogDebug(ex, "Skipped folder {Path}", child); }
                }
            }
        }
        return (IReadOnlyList<FolderEntry>)entries.Values.ToList();

        void Add(string path, string root, int depth)
        {
            var id = $"folder:{path.ToLowerInvariant()}";
            entries.TryAdd(id, new FolderEntry(id, new DirectoryInfo(path).Name is { Length: > 0 } name ? name : path, path, root, depth));
        }
    }, ct);
}
public sealed class FolderSearchProvider(IApplicationStore store) : ISearchProvider
{
    public string ProviderId => "folders";
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        if (query.IsEmpty) return [];
        var folders = await store.SearchFoldersAsync(query.Text, query.Limit, ct);
        return folders.Select(folder => new SearchResult(folder.Id, SearchResultType.Folder, folder.Name, folder.Path, "shell:Folder", Math.Max(TextMatcher.Score(folder.Name, query.Text), TextMatcher.Score(folder.Path, query.Text)) + 40 - folder.Depth, new SearchAction("folder", folder.Path))).ToList();
    }
}
public sealed class QuicklinkSearchProvider(ISettingsService settings) : ISearchProvider
{
    public string ProviderId => "quicklinks";

    public Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        if (query.IsEmpty) return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        var results = settings.Current.Quicklinks
            .Where(link => TryNormalize(link, out _))
            .Select(link => CreateResult(link, query))
            .Where(result => result is not null)
            .Cast<SearchResult>()
            .ToList();
        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    private static SearchResult? CreateResult(Quicklink link, SearchQuery query)
    {
        TryNormalize(link, out var url);
        if (url.Contains("{query}", StringComparison.Ordinal))
        {
            if (!TryExtractSearchText(link, query.Text, out var searchText, out var dynamicScore)) return null;
            var target = ExpandTemplate(url, searchText);
            return new SearchResult($"quicklink:{url.ToLowerInvariant()}", SearchResultType.Quicklink, $"{link.Name.Trim()}: {searchText}", target, null, dynamicScore + 60, new SearchAction("url", target));
        }
        var score = Math.Max(TextMatcher.Score(link.Name, query.Text), Math.Max(TextMatcher.Score(link.Alias ?? string.Empty, query.Text), TextMatcher.Score(url, query.Text))) + 60;
        return score > 60 ? new SearchResult($"quicklink:{url.ToLowerInvariant()}", SearchResultType.Quicklink, link.Name.Trim(), url, null, score, new SearchAction("url", url)) : null;
    }

    private static bool TryExtractSearchText(Quicklink link, string input, out string searchText, out double score)
    {
        searchText = string.Empty;
        score = 0;
        foreach (var trigger in new[] { link.Alias, link.Name }.Where(value => !string.IsNullOrWhiteSpace(value)).OrderByDescending(value => value!.Length))
        {
            var value = trigger!.Trim();
            if (!input.StartsWith(value, StringComparison.OrdinalIgnoreCase) || input.Length <= value.Length || !char.IsWhiteSpace(input[value.Length])) continue;
            searchText = input[value.Length..].Trim();
            if (searchText.Length == 0) continue;
            score = 100 + (string.Equals(value, link.Alias, StringComparison.OrdinalIgnoreCase) ? 25 : 0);
            return true;
        }
        return false;
    }

    public static string ExpandTemplate(string url, string query) => url.Replace("{query}", Uri.EscapeDataString(query.Trim()), StringComparison.Ordinal);

    public static bool TryNormalize(Quicklink? link, out string url)
    {
        url = string.Empty;
        if (link is null || string.IsNullOrWhiteSpace(link.Name) || string.IsNullOrWhiteSpace(link.Url)) return false;
        var value = link.Url.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var suppliedUri) && suppliedUri.Scheme is not ("http" or "https")) return false;
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) value = "https://" + value;
        var validationValue = value.Replace("{query}", "lumen", StringComparison.Ordinal);
        if (!Uri.TryCreate(validationValue, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;
        url = value.Contains("{query}", StringComparison.Ordinal) ? value : uri.AbsoluteUri;
        return true;
    }
}
public sealed class BuiltInCommandSearchProvider : ISearchProvider { public string ProviderId=>"commands"; public Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery q,CancellationToken ct){var cmds=new[]{new SearchResult("command:rebuild",SearchResultType.Command,"Rebuild index","Scan applications and import Chrome history",null,TextMatcher.Score("Rebuild index",q.Text),new("command","rebuild")),new SearchResult("command:exit",SearchResultType.Command,"Exit Lumen",null,null,TextMatcher.Score("Exit Lumen",q.Text),new("command","exit"))};return Task.FromResult<IReadOnlyList<SearchResult>>(cmds.Where(x=>x.Score>0).ToList());}}
