using System.Data;
using CommitDownload.App.Adapters;
using CommitDownload.App.Configurations;
using CommitDownload.App.Data;
using CommitDownload.App.Exceptions;
using CommitDownload.App.Input;
using CommitDownload.App.Ports;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommitDownload.App;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, builder) =>
            {
                builder.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddCommandLine(args);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<GitHubSettings>(ctx.Configuration.GetSection("GitHub"));
                services.Configure<AppSettings>(ctx.Configuration.GetSection("App"));

                var connString = ctx.Configuration.GetConnectionString("DefaultConnection");
                services.AddTransient<IDbConnection>(_ => new SqliteConnection(connString));
                services.AddScoped<ICommitPersistence, DapperCommitPersistence>();


                services.AddSingleton<IInputHandler>(sp => new ArgumentInputHandler(args));
                services.AddHttpClient<IRepoClient, GitHubClient>();
                services.AddScoped<ICommitService, CommitGitHubService>();
                services.AddTransient<App>();
            })
            .Build();

        using (var scope = host.Services.CreateScope())
        {
            var conn = scope.ServiceProvider.GetRequiredService<IDbConnection>();
            var migrator = new DapperMigrationRunner(conn);
            migrator.Migrate();
        }

        var app = host.Services.GetRequiredService<App>();
        return await app.Run();
    }
}

internal class App(
    IInputHandler input, 
    ICommitService commitService, 
    ICommitPersistence commitPersistence)
{
    public async Task<int> Run()
    {
        try
        {
            var user = input.ReadUsername();
            var repo = input.ReadRepository();

            var commitsInfo = await commitService.FetchCommitsAsync(user, repo);

            var commits = commitsInfo
                .Select(x => Commit.Create(user, repo, x.Sha, x.Message, x.Committer))
                .ToList();
            
            await commitPersistence.BulkInsertAsync(commits);

            Console.WriteLine("Commits:");
            foreach (var c in commits)
                Console.WriteLine($"{repo}/{c.Sha}: {c.Message} [{c.Committer}]");

            return 0;
        }
        catch (RateLimitExceededException ex)
        {
            await Console.Error.WriteLineAsync(ex.ResetAt is { } ts
                ? $"Github limit has been exceeded. Try again after: {ts:O}"
                : "Github limit has been exceeded.");
            return 2;
        }
        catch (PersistenceException ex)
        {
            await Console.Error.WriteLineAsync($"Error of personalities: {ex.Message}");
            return 3;
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"Network error/API: {ex.Message}");
            return 4;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Unexpected error: {ex}");
            return 1;
        }

    }
}