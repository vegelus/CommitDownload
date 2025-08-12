using System.Collections.Concurrent;
using CommitDownload.App.Configurations;
using CommitDownload.App.Models;
using CommitDownload.App.Ports;
using Microsoft.Extensions.Options;

namespace CommitDownload.App.Adapters;

public class CommitGitHubService(IRepoClient repoClient, IOptions<GitHubSettings> options) : ICommitService
{
    private readonly GitHubSettings _settings = options.Value;

    public async Task<IEnumerable<CommitInfo>> FetchCommitsAsync(string user, string repo, CancellationToken cancellationToken = default)
    {
        if (!await repoClient.RepositoryExistsAsync(user, repo, cancellationToken))
            throw new InvalidOperationException($"Repository '{user}/{repo}' not found.");

        var allCommits = new ConcurrentBag<CommitInfo>();
        var perPage    = _settings.PerPage;
        var nextPage   = 1;

        var workerCount = Environment.ProcessorCount; 

        var workers = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(async () =>
            {
                while (true)
                {
                    var page = Interlocked.Increment(ref nextPage) - 1;
                    var commits = await repoClient.GetCommitsAsync(user, repo, page, perPage, cancellationToken);
                    if (commits.Count == 0)
                        break;

                    foreach (var c in commits)
                        allCommits.Add(c);
                }
            }, cancellationToken))
            .ToArray();

        await Task.WhenAll(workers);

        return allCommits.ToList();
    }
}
