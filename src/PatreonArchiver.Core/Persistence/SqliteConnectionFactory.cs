using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Configuration;

namespace PatreonArchiver.Core.Persistence;

/// <summary>
/// Creates opened, PRAGMA-configured SQLite connections. WAL keeps readers concurrent with the
/// single writer; the writer is serialized by <see cref="SqliteArchiveRepository"/>'s gate.
/// </summary>
internal sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IOptions<CoreOptions> options)
    {
        var dbPath = options.Value.DatabasePath;
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    public SqliteConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
