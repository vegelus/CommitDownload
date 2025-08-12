using System.Data;
using Dapper;

namespace CommitDownload.App.Adapters;

public class DapperMigrationRunner(IDbConnection connection)
{
    private readonly string _migrationsPath = Path.Combine(AppContext.BaseDirectory, "Migrations");

    public void Migrate()
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS __MigrationsHistory (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                MigrationId  TEXT    NOT NULL,
                AppliedOn    DATETIME NOT NULL
            );
        ");

        var applied = connection
            .Query<string>("SELECT MigrationId FROM __MigrationsHistory")
            .ToHashSet();

        var scripts = Directory
            .GetFiles(_migrationsPath, "*.sql")
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n)
            .ToList();

        foreach (var file in scripts)
        {
            if (applied.Contains(file))
                continue;

            var sql = File.ReadAllText(Path.Combine(_migrationsPath, file));
            using var tx = connection.BeginTransaction();
            connection.Execute(sql, transaction: tx);

            connection.Execute(
                "INSERT INTO __MigrationsHistory (MigrationId, AppliedOn) VALUES (@id, @time);",
                new { id = file, time = DateTime.UtcNow },
                tx
            );

            tx.Commit();
        }
    }
}