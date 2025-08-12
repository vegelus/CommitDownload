using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CommitDownload.App.Configurations;
using CommitDownload.App.Exceptions;
using CommitDownload.App.Models;
using CommitDownload.App.Ports;
using Microsoft.Extensions.Options;

namespace CommitDownload.App.Adapters;

public class GitHubClient : IRepoClient
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _appSettings;

    public GitHubClient(HttpClient httpClient, IOptions<GitHubSettings> options, IOptions<AppSettings> appSettings)
    {
        _httpClient = httpClient;
        _appSettings = appSettings.Value;
        var settings = options.Value;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CommitDownload");
        _httpClient.BaseAddress = new Uri(settings.BaseUrl);

        if (!string.IsNullOrWhiteSpace(settings.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.Token);
        }
    }

    public async Task<bool> RepositoryExistsAsync(string user, string repo,
        CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, $"{user}/{repo}");
        using var resp = await _httpClient.SendAsync(
            req,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        ThrowIfRateLimitedAsync(resp);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (!resp.IsSuccessStatusCode)
            throw await CreateApiRequestExceptionAsync("RepositoryExists failed.", resp);

        return true;
    }

    public async Task<List<CommitInfo>> GetCommitsAsync(string user, string repo, int pageNumber, int perPage,
        CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromMilliseconds(200);

        for (var attempt = 0; attempt <= _appSettings.MaxRetries; attempt++)
        {
            using var resp = await _httpClient.GetAsync(
                $"{user}/{repo}/commits?per_page={perPage}&page={pageNumber}",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            ThrowIfRateLimitedAsync(resp);

            if (resp.IsSuccessStatusCode)
            {
                try
                {
                    await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(
                        stream,
                        new JsonDocumentOptions { AllowTrailingCommas = true }, 
                        cancellationToken);

                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                        return [];

                    var list = new List<CommitInfo>();
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var sha = el.TryGetProperty("sha", out var shaEl) ? (shaEl.GetString() ?? "") : "";
                        if (!el.TryGetProperty("commit", out var cObj) || cObj.ValueKind != JsonValueKind.Object)
                            continue;

                        var message = cObj.TryGetProperty("message", out var msgEl) ? (msgEl.GetString() ?? "") : "";
                        var committer = "";
                        if (cObj.TryGetProperty("committer", out var commEl) &&
                            commEl.ValueKind == JsonValueKind.Object)
                        {
                            committer = commEl.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? "") : "";
                        }

                        list.Add(new CommitInfo
                        {
                            Sha = sha,
                            Message = message,
                            Committer = committer
                        });
                    }

                    return list;
                }
                catch (JsonException jex)
                {
                    var snippet = await SafeReadSnippetAsync(resp);
                    throw new ApiResponseParsingException("Failed to parse GitHub commits response.", snippet, jex);
                }
            }

            if ((int)resp.StatusCode >= 400 && (int)resp.StatusCode < 500 &&
                resp.StatusCode != HttpStatusCode.RequestTimeout)
            {
                throw await CreateApiRequestExceptionAsync("GetCommits failed.", resp);
            }

            if (attempt < _appSettings.MaxRetries)
                await Task.Delay(delay, cancellationToken);

            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
        }

        throw new ApiRequestException("GetCommits failed after retries.", HttpStatusCode.ServiceUnavailable);
    }

    private static void ThrowIfRateLimitedAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Forbidden || (int)resp.StatusCode == 429)
        {
            if (IsRateLimitResponse(resp))
            {
                DateTimeOffset? resetAt = TryGetRateLimitReset(resp);
                throw new RateLimitExceededException("GitHub rate limit exceeded.", resetAt);
            }
        }
    }

    private static bool IsRateLimitResponse(HttpResponseMessage resp)
    {
        if ((int)resp.StatusCode == 429) return true;
        if (resp.Headers.TryGetValues("X-RateLimit-Remaining", out var vals))
            return vals.FirstOrDefault() == "0";
        return false;
    }

    private static DateTimeOffset? TryGetRateLimitReset(HttpResponseMessage resp)
    {
        if (resp.Headers.TryGetValues("X-RateLimit-Reset", out var vals) &&
            long.TryParse(vals.FirstOrDefault(), out var epochSeconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static async Task<ApiRequestException> CreateApiRequestExceptionAsync(string context,
        HttpResponseMessage resp)
    {
        var body = await SafeReadSnippetAsync(resp);
        return new ApiRequestException($"{context} HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}.", resp.StatusCode,
            body);
    }

    private static async Task<string?> SafeReadSnippetAsync(HttpResponseMessage resp, int max = 1024)
    {
        try
        {
            var content = await resp.Content.ReadAsStringAsync();
            if (content?.Length > max) content = content[..max] + "...";
            return content;
        }
        catch
        {
            return null;
        }
    }
}