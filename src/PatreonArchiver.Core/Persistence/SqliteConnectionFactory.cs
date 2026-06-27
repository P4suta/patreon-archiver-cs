using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Configuration;

namespace PatreonArchiver.Core.Persistence;

/// <summary>
/// Points Microsoft.Data.Sqlite.Core at the OS-provided <c>winsqlite3.dll</c> instead of the
/// bundled <c>e_sqlite3</c> native lib (which carries CVE-2025-6965 with no patched upstream
/// build). Runs once on assembly load — before any <see cref="SqliteConnection"/> is created —
/// so the App and the test host both use winsqlite3 without any per-entry-point wiring.
/// </summary>
internal static class SqliteProvider
{
    [ModuleInitializer]
    internal static void Init() =>
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
}

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
