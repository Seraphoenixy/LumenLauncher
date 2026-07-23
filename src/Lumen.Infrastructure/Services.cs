using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Lumen.Core;

namespace Lumen.Infrastructure;

public sealed class JsonSettingsService(ILogger<JsonSettingsService> logger) : ISettingsService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "settings.json");
    public LumenSettings Current { get; private set; } = new();
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        try { Current = File.Exists(_path) ? JsonSerializer.Deserialize<LumenSettings>(await File.ReadAllTextAsync(_path, cancellationToken), JsonOptions) ?? new() : new(); }
        catch (JsonException ex) { File.Move(_path, _path + ".broken-" + DateTime.UtcNow.Ticks, true); logger.LogWarning(ex, "Invalid settings backed up"); Current = new(); }
        if (!File.Exists(_path)) await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(Current, JsonOptions), cancellationToken);
    }
    public Task SaveAsync(CancellationToken cancellationToken = default) => File.WriteAllTextAsync(_path, JsonSerializer.Serialize(Current, JsonOptions), cancellationToken);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
}
public sealed class SqliteApplicationStore(ILogger<SqliteApplicationStore> logger) : IApplicationStore
{
    private static readonly bool ProviderInitialized = InitializeProvider();
    private readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "lumen.db") }.ToString();
    private static bool InitializeProvider() { Batteries_V2.Init(); return true; }
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _ = ProviderInitialized;
        Directory.CreateDirectory(Path.GetDirectoryName(new SqliteConnectionStringBuilder(_connectionString).DataSource)!);
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(ct);
        var sql = """CREATE TABLE IF NOT EXISTS schema_versions(version INTEGER NOT NULL); INSERT INTO schema_versions SELECT 1 WHERE NOT EXISTS(SELECT 1 FROM schema_versions); CREATE TABLE IF NOT EXISTS applications(id TEXT PRIMARY KEY, source TEXT NOT NULL, display_name TEXT NOT NULL, executable_path TEXT NOT NULL, arguments TEXT, working_directory TEXT NOT NULL, icon_cache_key TEXT, candidate_score INTEGER NOT NULL, enabled INTEGER NOT NULL, search_text TEXT NOT NULL DEFAULT '', updated_at TEXT NOT NULL); CREATE INDEX IF NOT EXISTS ix_apps_name ON applications(display_name); CREATE TABLE IF NOT EXISTS usage_history(result_id TEXT PRIMARY KEY, result_type INTEGER, launch_count INTEGER, last_launched_at TEXT);""";
        await using var cmd = db.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(ct);
        await EnsureApplicationSearchColumnAsync(db, ct);
        logger.LogInformation("Lumen database initialized");
    }
    public async Task UpsertApplicationsAsync(IEnumerable<ApplicationEntry> entries, CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        foreach (var e in entries) { await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "INSERT INTO applications(id,source,display_name,executable_path,arguments,working_directory,icon_cache_key,candidate_score,enabled,search_text,updated_at) VALUES($id,$s,$n,$p,$a,$w,$i,$c,$en,$search,$u) ON CONFLICT(id) DO UPDATE SET display_name=$n, executable_path=$p, arguments=$a, working_directory=$w, icon_cache_key=$i, candidate_score=$c, enabled=$en, search_text=CASE WHEN length($search)>0 THEN $search ELSE search_text END, updated_at=$u"; c.Parameters.AddWithValue("$id", e.Id); c.Parameters.AddWithValue("$s", e.Source); c.Parameters.AddWithValue("$n", e.DisplayName); c.Parameters.AddWithValue("$p", e.ExecutablePath); c.Parameters.AddWithValue("$a", (object?)e.Arguments ?? DBNull.Value); c.Parameters.AddWithValue("$w", e.WorkingDirectory); c.Parameters.AddWithValue("$i", (object?)e.IconKey ?? DBNull.Value); c.Parameters.AddWithValue("$c", e.CandidateScore); c.Parameters.AddWithValue("$en", e.Enabled ? 1 : 0); c.Parameters.AddWithValue("$search", e.SearchText); c.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("O")); await c.ExecuteNonQueryAsync(ct); }
        await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<ApplicationEntry>> SearchApplicationsAsync(string query, string? source, int limit, CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(ct); await using var c = db.CreateCommand(); c.CommandText = source is null ? "SELECT id,source,display_name,executable_path,arguments,working_directory,icon_cache_key,candidate_score,enabled,search_text FROM applications WHERE enabled=1 AND (display_name LIKE $q OR search_text LIKE $q) ORDER BY candidate_score DESC, display_name LIMIT $l" : "SELECT id,source,display_name,executable_path,arguments,working_directory,icon_cache_key,candidate_score,enabled,search_text FROM applications WHERE enabled=1 AND source=$s AND (display_name LIKE $q OR search_text LIKE $q) ORDER BY candidate_score DESC, display_name LIMIT $l"; c.Parameters.AddWithValue("$q", "%" + query + "%"); if(source is not null)c.Parameters.AddWithValue("$s", source); c.Parameters.AddWithValue("$l", limit); var list = new List<ApplicationEntry>(); await using var r = await c.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetInt32(7), r.GetInt32(8) != 0, r.IsDBNull(9) ? string.Empty : r.GetString(9))); return list;
    }
    private static async Task EnsureApplicationSearchColumnAsync(SqliteConnection db, CancellationToken ct)
    {
        await using var columns = db.CreateCommand(); columns.CommandText = "PRAGMA table_info(applications)";
        await using var reader = await columns.ExecuteReaderAsync(ct);
        var exists = false;
        while (await reader.ReadAsync(ct)) exists |= string.Equals(reader.GetString(1), "search_text", StringComparison.OrdinalIgnoreCase);
        if (!exists) { await using var add = db.CreateCommand(); add.CommandText = "ALTER TABLE applications ADD COLUMN search_text TEXT NOT NULL DEFAULT ''"; await add.ExecuteNonQueryAsync(ct); }
    }
    public async Task RecordUsageAsync(SearchResult r, CancellationToken ct=default) { await using var db=new SqliteConnection(_connectionString);await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="INSERT INTO usage_history(result_id,result_type,launch_count,last_launched_at) VALUES($id,$t,1,$d) ON CONFLICT(result_id) DO UPDATE SET launch_count=launch_count+1,last_launched_at=$d";c.Parameters.AddWithValue("$id",r.Id);c.Parameters.AddWithValue("$t",(int)r.Type);c.Parameters.AddWithValue("$d",DateTimeOffset.UtcNow.ToString("O"));await c.ExecuteNonQueryAsync(ct); }
    public async Task<IReadOnlyList<SearchResult>> GetRecentAsync(int limit,CancellationToken ct=default){ await using var db=new SqliteConnection(_connectionString);await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT a.id,a.source,a.display_name,a.executable_path,a.arguments,a.working_directory,a.icon_cache_key,u.launch_count FROM applications a JOIN usage_history u ON a.id=u.result_id ORDER BY u.last_launched_at DESC LIMIT $l";c.Parameters.AddWithValue("$l",limit);var items=new List<SearchResult>();await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){var type=r.GetString(1)=="portable"?SearchResultType.PortableApplication:SearchResultType.Application;items.Add(new(r.GetString(0),type,r.GetString(2),r.GetString(3),r.IsDBNull(6)?null:r.GetString(6),r.GetInt32(7),new("process",r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.GetString(5))));}return items; }
}

