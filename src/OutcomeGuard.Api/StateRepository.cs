using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace OutcomeGuard.Api;

/// <summary>Single-process MVP store. An atomic SQLite snapshot keeps related events, obligations and audit records consistent.</summary>
public sealed class StateRepository
{
    private readonly string _connectionString;

    public StateRepository(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath, Pooling = false }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS state (id INTEGER PRIMARY KEY CHECK(id=1), body TEXT NOT NULL);";
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public StoredState Load()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT body FROM state WHERE id=1";
        return command.ExecuteScalar() is string body
            ? JsonSerializer.Deserialize<StoredState>(body, JsonDefaults.Options) ?? throw new InvalidDataException("Stored state is empty.")
            : new StoredState();
    }

    public void Save(StoredState state)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO state(id,body) VALUES(1,$body) ON CONFLICT(id) DO UPDATE SET body=excluded.body";
        command.Parameters.AddWithValue("$body", JsonSerializer.Serialize(state, JsonDefaults.Options));
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
