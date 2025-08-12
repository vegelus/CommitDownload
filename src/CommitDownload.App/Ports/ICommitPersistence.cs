using CommitDownload.App.Data;

namespace CommitDownload.App.Ports;

public interface ICommitPersistence
{
    Task BulkInsertAsync(IReadOnlyCollection<Commit> commits);
}