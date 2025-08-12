namespace CommitDownload.App.Input;

public class ArgumentInputHandler : IInputHandler
{
    private readonly string[] _args;
    public ArgumentInputHandler(string[] args)
    {
        _args = args;
    }

    public string ReadUsername()
    {
        if (_args.Length < 1)
            throw new ArgumentException("No username. Use: GitPuller <user> <repo>");
        return _args[0];
    }

    public string ReadRepository()
    {
        if (_args.Length < 2)
            throw new ArgumentException("No repository name. Use: GitPuller <user> <repo>");
        return _args[1];
    }
}
