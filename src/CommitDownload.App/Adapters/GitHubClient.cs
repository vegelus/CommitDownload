using System.Net;
using System.Text.Json;
using CommitDownload.App.Configurations;
using CommitDownload.App.Models;
using CommitDownload.App.Ports;
using Microsoft.Extensions.Options;

namespace CommitDownload.App.Adapters;

public class GitHubClient : IRepoClient
{
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;

    public GitHubClient(HttpClient httpClient, IOptions<GitHubSettings> options)
    {
        _httpClient = httpClient;
        _settings   = options.Value;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CommitDownload");
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    public async Task<bool> RepositoryExistsAsync(string user, string repo)
    {
        using var req  = new HttpRequestMessage(HttpMethod.Head, $"{user}/{repo}");
        using var resp = await _httpClient.SendAsync(req);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return false;

        resp.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<List<CommitInfo>> GetCommitsAsync(string user, string repo, int pageNumber = 1)
    {
        var all   = new List<CommitInfo>();
        int perPage = _settings.PerPage;

        while (true)
        {
            using var resp = await _httpClient.GetAsync($"{user}/{repo}/commits?per_page={perPage}&page={pageNumber}");
            resp.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var arr = doc.RootElement.EnumerateArray();
            int count = 0;

            foreach (var el in arr)
            {
                count++;
                var sha  = el.GetProperty("sha").GetString() ?? "";
                var cObj = el.GetProperty("commit");
                all.Add(new CommitInfo {
                    Sha       = sha,
                    Message   = cObj.GetProperty("message").GetString() ?? "",
                    Committer = cObj.GetProperty("committer").GetProperty("name").GetString() ?? ""
                });
            }

            if (count < perPage) break;
            pageNumber++;
        }

        return all;
    }
}