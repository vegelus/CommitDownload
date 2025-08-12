using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CommitDownload.App.Configurations;
using CommitDownload.App.Models;
using CommitDownload.App.Ports;
using Microsoft.Extensions.Options;

namespace CommitDownload.App.Adapters;

public class GitHubClient : IRepoClient
{
    private readonly HttpClient _httpClient;

    public GitHubClient(HttpClient httpClient, IOptions<GitHubSettings> options)
    {
        _httpClient = httpClient;
        var settings = options.Value;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CommitDownload");
        _httpClient.BaseAddress = new Uri(settings.BaseUrl);

        if (!string.IsNullOrWhiteSpace(settings.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.Token);
        }
    }

    public async Task<bool> RepositoryExistsAsync(string user, string repo)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, $"{user}/{repo}");
        using var resp = await _httpClient.SendAsync(req);

        if (resp.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException(
                "GitHub rate limit exceeded. Consider setting the token in the configuration.");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return false;

        resp.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<List<CommitInfo>> GetCommitsAsync(string user, string repo, int pageNumber, int perPage)
    {
        using var resp = await _httpClient.GetAsync($"{user}/{repo}/commits?per_page={perPage}&page={pageNumber}");

        if (resp.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException(
                "GitHub rate limit exceeded. Consider setting the token in the configuration.");

        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var arr = doc.RootElement.EnumerateArray();
        var list = new List<CommitInfo>();

        foreach (var el in arr)
        {
            var sha = el.GetProperty("sha").GetString() ?? "";
            var cObj = el.GetProperty("commit");
            list.Add(new CommitInfo
            {
                Sha = sha,
                Message = cObj.GetProperty("message").GetString() ?? "",
                Committer = cObj.GetProperty("committer").GetProperty("name").GetString() ?? ""
            });
        }

        return list;
    }
}