using AutoFixer.Audit;
using AutoFixer.Models;
using AutoFixer.Secrets;
using Serilog;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AutoFixer.VCS;

/// <summary>
/// Real GitHub PR client. Authenticates with a Personal Access Token (or installation token)
/// taken from <see cref="VcsConfig.Token"/> or the <c>GITHUB_TOKEN</c> environment variable.
/// </summary>
public class GitHubClient : IVcsClient, IDisposable
{
    private const string DefaultApiUrl = "https://api.github.com";
    private const string ApiVersion = "2022-11-28";

    private readonly VcsConfig _config;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ISecretProvider? _secretProvider;
    private readonly string _apiBase;
    private string? _resolvedToken;

    public GitHubClient(VcsConfig config, ISecretProvider? secretProvider = null, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(GitHubClient));
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _secretProvider = secretProvider;

        _apiBase = (string.IsNullOrWhiteSpace(_config.ApiUrl) ? DefaultApiUrl : _config.ApiUrl).TrimEnd('/');

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AutoFixer/1.0");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
        {
            _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", ApiVersion);
        }
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<string?> CreatePullRequestAsync(PullRequestRequest request, CancellationToken cancellationToken = default)
    {
        var token = await ResolveTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.Warning(
                "No GitHub token configured (VcsConfig.Token / GITHUB_TOKEN). Skipping PR creation for {Repo}.",
                request.RepoFullName);
            return null;
        }

        if (!TryParseRepo(request.RepoFullName, out var owner, out var repo))
        {
            _logger.Error("Invalid repository name {Repo}. Expected 'owner/repo'.", request.RepoFullName);
            return null;
        }

        _logger.Information(
            "Creating GitHub PR in {Owner}/{Repo}: {Head} -> {Base}",
            owner, repo, request.HeadBranch, request.BaseBranch);

        var baseSha = await GetBranchShaAsync(owner, repo, request.BaseBranch, cancellationToken);
        if (baseSha is null)
        {
            _logger.Error("Could not resolve base branch {Branch} in {Owner}/{Repo}.", request.BaseBranch, owner, repo);
            return null;
        }

        if (!await CreateBranchAsync(owner, repo, request.HeadBranch, baseSha, cancellationToken))
        {
            return null;
        }

        foreach (var file in request.Files)
        {
            if (!await PutFileAsync(owner, repo, file, request.HeadBranch, request.Title, cancellationToken))
            {
                _logger.Error("Aborting PR creation because commit of {Path} failed.", file.Path);
                return null;
            }
        }

        return await OpenPullRequestAsync(owner, repo, request, cancellationToken);
    }

    private static bool TryParseRepo(string repoFullName, out string owner, out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;
        if (string.IsNullOrWhiteSpace(repoFullName)) return false;
        var parts = repoFullName.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        owner = parts[0];
        repo = parts[1];
        return true;
    }

    private async Task<string?> GetBranchShaAsync(string owner, string repo, string branch, CancellationToken ct)
    {
        var url = $"{_apiBase}/repos/{owner}/{repo}/git/ref/heads/{Uri.EscapeDataString(branch)}";
        using var response = await SendAsync(HttpMethod.Get, url, content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogErrorBodyAsync(response, $"resolve base branch {branch}", ct);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("object").GetProperty("sha").GetString();
    }

    private async Task<bool> CreateBranchAsync(string owner, string repo, string branch, string baseSha, CancellationToken ct)
    {
        var url = $"{_apiBase}/repos/{owner}/{repo}/git/refs";
        var payload = new { @ref = $"refs/heads/{branch}", sha = baseSha };
        using var response = await SendJsonAsync(HttpMethod.Post, url, payload, ct);
        if (response.IsSuccessStatusCode) return true;

        // 422 means the ref already exists — treat as non-fatal.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            _logger.Warning("Branch {Branch} already exists in {Owner}/{Repo}; reusing it.", branch, owner, repo);
            return true;
        }

        await LogErrorBodyAsync(response, $"create branch {branch}", ct);
        return false;
    }

    private async Task<bool> PutFileAsync(string owner, string repo, FileChange file, string branch, string commitMessage, CancellationToken ct)
    {
        var path = file.Path.TrimStart('/');
        var contentsUrl = $"{_apiBase}/repos/{owner}/{repo}/contents/{path}?ref={Uri.EscapeDataString(branch)}";

        string? existingSha = null;
        using (var getResponse = await SendAsync(HttpMethod.Get, contentsUrl, content: null, ct))
        {
            if (getResponse.IsSuccessStatusCode)
            {
                var body = await getResponse.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("sha", out var shaProp))
                {
                    existingSha = shaProp.GetString();
                }
            }
            else if (getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                await LogErrorBodyAsync(getResponse, $"check existing file {path}", ct);
            }
        }

        var putUrl = $"{_apiBase}/repos/{owner}/{repo}/contents/{path}";
        var payload = new Dictionary<string, object?>
        {
            ["message"] = commitMessage,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(file.Content)),
            ["branch"] = branch,
        };
        if (existingSha is not null) payload["sha"] = existingSha;

        using var response = await SendJsonAsync(HttpMethod.Put, putUrl, payload, ct);
        if (response.IsSuccessStatusCode) return true;

        await LogErrorBodyAsync(response, $"commit {path}", ct);
        return false;
    }

    private async Task<string?> OpenPullRequestAsync(string owner, string repo, PullRequestRequest request, CancellationToken ct)
    {
        var url = $"{_apiBase}/repos/{owner}/{repo}/pulls";
        var payload = new
        {
            title = request.Title,
            body = request.Description,
            head = request.HeadBranch,
            @base = request.BaseBranch,
        };

        using var response = await SendJsonAsync(HttpMethod.Post, url, payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to open PR for {Repo}: {Status} {Body}", request.RepoFullName, (int)response.StatusCode, body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        var htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
        _logger.Information("Opened PR for {Repo}: {Url}", request.RepoFullName, htmlUrl);
        return htmlUrl;
    }

    private async Task<string?> ResolveTokenAsync(CancellationToken ct)
    {
        if (_resolvedToken != null) return _resolvedToken;

        if (!string.IsNullOrEmpty(_config.Token))
        {
            _resolvedToken = _config.Token;
            return _resolvedToken;
        }

        if (_secretProvider != null)
        {
            var secret = await _secretProvider.GetSecretAsync("GITHUB_TOKEN", ct);
            if (!string.IsNullOrEmpty(secret))
            {
                _resolvedToken = secret;
                return _resolvedToken;
            }
        }

        _resolvedToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return _resolvedToken;
    }

    private Task<HttpResponseMessage> SendJsonAsync<T>(HttpMethod method, string url, T payload, CancellationToken ct)
    {
        var content = JsonContent.Create(payload);
        return SendAsync(method, url, content, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        var token = await ResolveTokenAsync(ct);
        var request = new HttpRequestMessage(method, url);
        if (content is not null) request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
    }

    private async Task LogErrorBodyAsync(HttpResponseMessage response, string action, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.Error("GitHub {Action} failed: {Status} {Body}", action, (int)response.StatusCode, Truncate(body, 500));
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "…";

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
