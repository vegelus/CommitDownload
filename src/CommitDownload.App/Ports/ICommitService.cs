using CommitDownload.App.Models;

namespace CommitDownload.App.Ports;

public interface ICommitService
{
    Task<IEnumerable<CommitInfo>> FetchCommitsAsync(string user, string repo, CancellationToken cancellationToken = default);
}