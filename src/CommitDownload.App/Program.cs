using CommitDownload.App.Adapters;
using CommitDownload.App.Configurations;
using CommitDownload.App.Input;
using CommitDownload.App.Ports;
using CommitDownload.App.Services;
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
                services.AddScoped<ICommitService, CommitGitHubService>();
                services.AddTransient<App>();
            })
            .Build();

        var app = host.Services.GetRequiredService<App>();
        return await app.Run();
    }
}

internal class App(IInputHandler input, ICommitService commitService)
{
    public async Task<int> Run()
    {
        try
        {
            var user = input.ReadUsername();
            var repo = input.ReadRepository();

            var commits = await commitService.FetchCommitsAsync(user, repo);

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
        catch (InvalidOperationException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }
}