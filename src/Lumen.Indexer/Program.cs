using Microsoft.Data.Sqlite;
using SQLitePCL;
using ToolGood.Words.Pinyin;

Batteries_V2.Init();
using var gate = new Mutex(true, @"Local\LumenPinyinIndexer", out var ownsGate);
if (!ownsGate) return;

var database = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "lumen.db");
if (!File.Exists(database)) return;
await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database }.ToString());
await connection.OpenAsync();
var entries = new List<(string Id, string Name)>();
await using (var read = connection.CreateCommand())
{
    read.CommandText = "SELECT id, display_name FROM applications WHERE enabled=1";
    await using var reader = await read.ExecuteReaderAsync();
    while (await reader.ReadAsync()) entries.Add((reader.GetString(0), reader.GetString(1)));
}
await using var transaction = await connection.BeginTransactionAsync();
foreach (var entry in entries)
{
    var full = WordsHelper.GetPinyin(entry.Name, false);
    var initials = WordsHelper.GetFirstPinyin(entry.Name);
    var aliases = string.Join(' ', new[] { entry.Name, full, initials }.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
    await using var update = connection.CreateCommand();
    update.Transaction = (SqliteTransaction)transaction;
    update.CommandText = "UPDATE applications SET search_text=$search WHERE id=$id";
    update.Parameters.AddWithValue("$search", aliases);
    update.Parameters.AddWithValue("$id", entry.Id);
    await update.ExecuteNonQueryAsync();
}
await transaction.CommitAsync();
