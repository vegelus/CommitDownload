using CommitDownload.App.Models;

namespace CommitDownload.App.Ports;

public interface IRepoClient
{
    Task<bool> RepositoryExistsAsync(string user, string repo);
    Task<List<CommitInfo>> GetCommitsAsync(string user, string repo, int pageNumber = 1);

}