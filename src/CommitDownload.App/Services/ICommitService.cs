using CommitDownload.App.Models;

namespace CommitDownload.App.Services;

public interface ICommitService
{
    Task<List<CommitInfo>> FetchCommitsAsync(string user, string repo);
}