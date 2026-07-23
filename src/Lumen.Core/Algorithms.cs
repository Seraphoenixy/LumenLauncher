using Microsoft.Extensions.Logging;

namespace Lumen.Core;

public static class TextMatcher
{
    public static double Score(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0;
        var value = text ?? string.Empty; var q = query.Trim();
        var index = value.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return 0;
        var score = 50d + (index == 0 ? 50 : 0);
        if (index > 0 && !char.IsLetterOrDigit(value[index - 1])) score += 25;
        return score - Math.Min(index, 30);
    }
}
public sealed class DefaultExecutableCandidateScorer : IExecutableCandidateScorer
{
    private static readonly (string Word, int Weight)[] Negative = [("uninstall", -100), ("crashpad", -80), ("updater", -50), ("update", -50), ("helper", -30), ("service", -30), ("runtime", -25), ("redist", -25), ("plugins", -25), ("resources", -25), ("lib", -25)];
    public int Score(ExecutableCandidate c)
    {
        var score = (string.IsNullOrWhiteSpace(c.ProductName) ? 0 : 20) + (string.IsNullOrWhiteSpace(c.FileDescription) ? 0 : 15) + (c.HasIcon ? 15 : 0) + (c.Depth <= 1 ? 15 : 0) + (c.WasLaunched ? 40 : 0);
        if (string.Equals(Path.GetFileNameWithoutExtension(c.FileName), c.ParentDirectory, StringComparison.OrdinalIgnoreCase)) score += 10;
        var all = $"{c.FileName} {c.Path}".ToLowerInvariant(); foreach (var rule in Negative) if (all.Contains(rule.Word)) score += rule.Weight;
        return score;
    }
}
public sealed class SearchAggregator(IEnumerable<ISearchProvider> providers, Microsoft.Extensions.Logging.ILogger<SearchAggregator> logger)
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery query, CancellationToken token)
    {
        var calls = providers.Select(async p => { try { return await p.SearchAsync(query, token); } catch (OperationCanceledException) { throw; } catch (Exception ex) { logger.LogWarning(ex, "Search provider {Provider} failed", p.ProviderId); return (IReadOnlyList<SearchResult>)[]; } });
        var results = (await Task.WhenAll(calls)).SelectMany(x => x)
            .GroupBy(x => $"{x.Type}:{x.PrimaryAction.Target}", StringComparer.OrdinalIgnoreCase).Select(x => x.OrderByDescending(r => r.Score).First())
            .OrderByDescending(x => x.Score).ThenBy(x => x.Title).Take(query.Limit).ToList();
        return results;
    }
}
