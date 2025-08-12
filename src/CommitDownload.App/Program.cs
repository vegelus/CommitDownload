using CommitDownload.App.Adapters;
using CommitDownload.App.Configurations;
using CommitDownload.App.Input;
using CommitDownload.App.Ports;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommitDownload.App;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, builder) =>
            {
                builder.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddCommandLine(args);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<GitHubSettings>(ctx.Configuration.GetSection("GitHub"));
                services.AddSingleton<IInputHandler>(sp => new ArgumentInputHandler(args));
                services.AddHttpClient<IRepoClient, GitHubClient>();
                services.AddTransient<App>();
            })
            .Build();

        var app = host.Services.GetRequiredService<App>();
        return await app.Run();
    }
}

internal class App
{
    private readonly IInputHandler _input;
    private readonly IRepoClient _client;

    public App(IInputHandler input, IRepoClient client)
    {
        _input = input;
        _client = client;
    }

    public async Task<int> Run()
    {
        try
        {
            var user = _input.ReadUsername();
            var repo = _input.ReadRepository();

            if (!await _client.RepositoryExistsAsync(user, repo))
            {
                await Console.Error.WriteLineAsync("The repository does not exist or no access.");
                return 1;
            }

            var commits = await _client.GetCommitsAsync(user, repo);
            Console.WriteLine("Commits:");
            foreach (var c in commits)
                Console.WriteLine($"{repo}/{c.Sha}: {c.Message} [{c.Committer}]");

            return 0;
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"There was a error: {ex.Message}");
            return 1;
        }
    }
}