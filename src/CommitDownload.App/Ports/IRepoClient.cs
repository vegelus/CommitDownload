using CommitDownload.App.Models;

namespace CommitDownload.App.Ports;

public interface IRepoClient
{
    Task<bool> RepositoryExistsAsync(string user, string repo, CancellationToken cancellationToken = default);
    Task<List<CommitInfo>> GetCommitsAsync(string user, string repo, int pageNumber, int perPage, CancellationToken cancellationToken = default);

}