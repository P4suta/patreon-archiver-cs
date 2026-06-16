using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace PatreonArchiver.Core.Persistence;

/// <summary>
/// Applies ordered, embedded <c>NNN_*.sql</c> migrations, tracking the applied version in
/// the <c>schema_version</c> table. Each migration runs in its own transaction.
/// </summary>
internal sealed class MigrationRunner(SqliteConnectionFactory factory)
{
    private const string ResourceMarker = ".Persistence.Migrations.";

    public void Apply()
    {
        using var connection = factory.Create();
        var current = GetCurrentVersion(connection);

        foreach (var (version, sql) in LoadMigrations().Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            using var tx = connection.BeginTransaction();

            using (var migrate = connection.CreateCommand())
            {
                migrate.Transaction = tx;
                migrate.CommandText = sql;
                migrate.ExecuteNonQuery();
            }

            using (var stamp = connection.CreateCommand())
            {
                stamp.Transaction = tx;
                stamp.CommandText = "DELETE FROM schema_version; INSERT INTO schema_version(version) VALUES($v);";
                stamp.Parameters.AddWithValue("$v", version);
                stamp.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    private static int GetCurrentVersion(SqliteConnection connection)
    {
        using var exists = connection.CreateCommand();
        exists.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_version';";
        if (Convert.ToInt64(exists.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
        {
            return 0;
        }

        using var read = connection.CreateCommand();
        read.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        return Convert.ToInt32(read.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static IEnumerable<(int Version, string Sql)> LoadMigrations()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.Contains(ResourceMarker, StringComparison.Ordinal) ||
                !resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = resource[(resource.IndexOf(ResourceMarker, StringComparison.Ordinal) + ResourceMarker.Length)..];
            var prefix = fileName.Split('_', 2)[0];
            if (!int.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            yield return (version, reader.ReadToEnd());
        }
    }
}
