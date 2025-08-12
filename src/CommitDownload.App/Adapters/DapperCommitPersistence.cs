using System.Data;
using CommitDownload.App.Configurations;
using CommitDownload.App.Data;
using CommitDownload.App.Exceptions;
using CommitDownload.App.Ports;
using Dapper;
using Microsoft.Extensions.Options;

namespace CommitDownload.App.Adapters;

public class DapperCommitPersistence(IDbConnection connection, IOptions<AppSettings> options) : ICommitPersistence
{
    private readonly AppSettings _settings = options.Value;

    public async Task BulkInsertAsync(IReadOnlyCollection<Commit> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        
        var batchSize = Math.Max(1, _settings.BatchSize);

        if (commits.Count == 0) return;

        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var tx = connection.BeginTransaction();
        try
        {
            for (var i = 0; i < commits.Count; i += batchSize)
            {
                var count = Math.Min(batchSize, commits.Count - i);
                var batch = commits.Skip(i).Take(count).ToList();

                var parameters = new DynamicParameters();
                var values = new List<string>(batch.Count);

                for (var j = 0; j < batch.Count; j++)
                {
                    var c = batch[j];
                    values.Add($"(@RepoOwner{j}, @RepoName{j}, @Sha{j}, @Message{j}, @Committer{j})");
                    parameters.Add($"@RepoOwner{j}", c.RepoOwner);
                    parameters.Add($"@RepoName{j}",  c.RepoName);
                    parameters.Add($"@Sha{j}",       c.Sha);
                    parameters.Add($"@Message{j}",   c.Message);
                    parameters.Add($"@Committer{j}", c.Committer);
                }

                var sql = $@"
                    INSERT INTO Commits (RepoOwner, RepoName, Sha, Message, Committer)
                    VALUES {string.Join(", ", values)}
                    ON CONFLICT(Sha) DO NOTHING;";

                await connection.ExecuteAsync(sql, parameters, tx);
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); }
            catch
            {
                // ignored
            }

            throw new PersistenceException("BulkInsert failed.", ex);
        }
    }
}