namespace CommitDownload.App.Models;

public class CommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Committer { get; set; } = string.Empty;
}
