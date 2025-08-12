using CommitDownload.App.Configurations;
using CommitDownload.App.Models;
using CommitDownload.App.Ports;
using Microsoft.Extensions.Options;

namespace CommitDownload.App.Services;

public class CommitGitHubService(IRepoClient repoClient, IOptions<GitHubSettings> options) : ICommitService
{
    private readonly GitHubSettings _settings = options.Value;

    public async Task<List<CommitInfo>> FetchCommitsAsync(string user, string repo)
    {
        if (!await repoClient.RepositoryExistsAsync(user, repo))
            throw new InvalidOperationException($"Repository '{user}/{repo}' not found.");

        var all        = new List<CommitInfo>();
        var pageNumber = 1;
        var perPage    = _settings.PerPage;

        while (true)
        {
            var commits = await repoClient.GetCommitsAsync(user, repo, pageNumber, perPage);
            all.AddRange(commits);             
            if (commits.Count < perPage || commits.Count > 400) //TODO usunąć warunek
                break;
            pageNumber++;
        }

        return all;
    }
}