namespace Lumen.Core;

public enum SearchResultType { Application, PortableApplication, Command }
public sealed record SearchAction(string Kind, string Target, string? Arguments = null, string? WorkingDirectory = null);
public sealed record SearchResult(string Id, SearchResultType Type, string Title, string? Subtitle, string? IconKey, double Score, SearchAction PrimaryAction);
public sealed record SearchQuery(string Text, int Limit = 20)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}
public sealed record ExecutableCandidate(string Path, string FileName, string? ProductName, string? FileDescription, string? CompanyName, string? Version, DateTimeOffset LastWriteTime, long Size, bool HasIcon, string ParentDirectory, int Depth, bool WasLaunched);
public sealed record ApplicationEntry(string Id, string Source, string DisplayName, string ExecutablePath, string? Arguments, string WorkingDirectory, string? IconKey, int CandidateScore, bool Enabled = true, string SearchText = "");
public sealed record UsageEntry(string ResultId, SearchResultType ResultType, int LaunchCount, DateTimeOffset? LastLaunchedAt);
