namespace CommitDownload.App.Data;

public class Commit 
{
    public int Id { get; private set; }
    public string RepoOwner { get; }
    public string RepoName { get; }
    public string Sha { get; }
    public string Message { get; }
    public string Committer { get; }

    private Commit()
    {
    }

    private Commit(int? id, string repoOwner, string repoName, string sha, string message, string committer)
    {
        if (id != null) Id = id.Value;
        RepoOwner = repoOwner;
        RepoName = repoName;
        Sha = sha;
        Message = message;
        Committer = committer;
    }

    public static Commit Create(string repoOwner, string repoName, string sha, string message, string committer)
    {
        return new Commit(null, repoOwner, repoName, sha, message, committer);
    }
}