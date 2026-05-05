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
/// Azure DevOps Git REST API client. Authenticates with a Personal Access Token taken from
/// <see cref="VcsConfig.Token"/> or the <c>AZURE_DEVOPS_PAT</c> / <c>ADO_PAT</c> environment variables.
/// </summary>
public class AzureDevOpsClient : IVcsClient, IDisposable
{
    private readonly VcsConfig _config;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ISecretProvider? _secretProvider;
    private readonly string _apiBase;
    private string? _resolvedToken;

    public AzureDevOpsClient(VcsConfig config, ISecretProvider? secretProvider = null, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(AzureDevOpsClient));
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _secretProvider = secretProvider;

        // Parse apiUrl to extract org, project and api base
        var apiUrl = string.IsNullOrWhiteSpace(_config.ApiUrl)
            ? throw new ArgumentException("Azure DevOps requires VcsConfig.ApiUrl (e.g. https://dev.azure.com/my-org/my-project).")
            : _config.ApiUrl.TrimEnd('/');

        // Expected format: https://dev.azure.com/{org}/{project} or https://{org}.visualstudio.com/{project}
        _apiBase = apiUrl;
    }

    public async Task<string?> CreatePullRequestAsync(PullRequestRequest request, CancellationToken cancellationToken = default)
    {
        var token = await ResolveTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.Warning(
                "No Azure DevOps PAT configured (VcsConfig.Token / AZURE_DEVOPS_PAT / ADO_PAT). Skipping PR creation for {Repo}.",
                request.RepoFullName);
            return null;
        }

        var repoName = request.RepoFullName;

        // Resolve the base branch object-id so we can create a new branch from it
        var baseBranchRef = $"refs/heads/{request.BaseBranch}";
        var baseObjectId = await GetRefObjectIdAsync(repoName, baseBranchRef, cancellationToken);
        if (string.IsNullOrEmpty(baseObjectId))
        {
            _logger.Error("Could not resolve base branch {Branch} in repo {Repo}.", request.BaseBranch, repoName);
            return null;
        }

        var newBranchRef = $"refs/heads/{request.HeadBranch}";
        if (!await CreateBranchAsync(repoName, newBranchRef, baseObjectId, cancellationToken))
        {
            return null;
        }

        // Push each file as a commit via the Pushes API
        if (!await PushFilesAsync(repoName, newBranchRef, request.Title, request.Files, cancellationToken))
        {
            return null;
        }

        return await OpenPullRequestAsync(repoName, request, cancellationToken);
    }

    private async Task<string?> GetRefObjectIdAsync(string repoName, string refName, CancellationToken ct)
    {
        var url = $"{_apiBase}/_apis/git/repositories/{Uri.EscapeDataString(repoName)}/refs?filter={Uri.EscapeDataString(refName)}&api-version=7.1-preview.1";
        using var response = await SendAsync(HttpMethod.Get, url, content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogErrorBodyAsync(response, $"resolve ref {refName}", ct);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
        {
            _logger.Error("Ref {Ref} not found in repo {Repo}.", refName, repoName);
            return null;
        }

        return values[0].GetProperty("objectId").GetString();
    }

    private async Task<bool> CreateBranchAsync(string repoName, string newRef, string baseObjectId, CancellationToken ct)
    {
        var url = $"{_apiBase}/_apis/git/repositories/{Uri.EscapeDataString(repoName)}/refs?api-version=7.1-preview.1";
        var payload = new[]
        {
            new
            {
                name = newRef,
                newObjectId = baseObjectId,
                oldObjectId = "0000000000000000000000000000000000000000",
            }
        };

        using var response = await SendJsonAsync(HttpMethod.Post, url, payload, ct);
        if (response.IsSuccessStatusCode) return true;

        // 409 Conflict can mean the ref already exists
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.Warning("Branch {Ref} already exists in {Repo}; reusing it.", newRef, repoName);
            return true;
        }

        await LogErrorBodyAsync(response, $"create branch {newRef}", ct);
        return false;
    }

    private async Task<bool> PushFilesAsync(string repoName, string branchRef, string commitMessage, IReadOnlyList<FileChange> files, CancellationToken ct)
    {
        var url = $"{_apiBase}/_apis/git/repositories/{Uri.EscapeDataString(repoName)}/pushes?api-version=7.1-preview.1";

        var changes = files.Select(f => new
        {
            changeType = "edit", // Azure DevOps will infer add vs edit based on path existence
            item = new { path = f.Path.StartsWith('/') ? f.Path : $"/{f.Path}" },
            newContent = new
            {
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(f.Content)),
                contentType = "base64encoded"
            }
        }).ToList();

        var payload = new
        {
            refUpdates = new[]
            {
                new
                {
                    name = branchRef,
                    oldObjectId = await GetRefObjectIdAsync(repoName, branchRef, ct) ?? "0000000000000000000000000000000000000000"
                }
            },
            commits = new[]
            {
                new
                {
                    comment = commitMessage,
                    changes
                }
            }
        };

        using var response = await SendJsonAsync(HttpMethod.Post, url, payload, ct);
        if (response.IsSuccessStatusCode) return true;

        await LogErrorBodyAsync(response, $"push files to {branchRef}", ct);
        return false;
    }

    private async Task<string?> OpenPullRequestAsync(string repoName, PullRequestRequest request, CancellationToken ct)
    {
        var url = $"{_apiBase}/_apis/git/repositories/{Uri.EscapeDataString(repoName)}/pullrequests?api-version=7.1-preview.1";
        var payload = new
        {
            sourceRefName = $"refs/heads/{request.HeadBranch}",
            targetRefName = $"refs/heads/{request.BaseBranch}",
            title = request.Title,
            description = request.Description,
        };

        using var response = await SendJsonAsync(HttpMethod.Post, url, payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to open PR for {Repo}: {Status} {Body}", repoName, (int)response.StatusCode, Truncate(body, 500));
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        var htmlUrl = doc.RootElement.TryGetProperty("_links", out var links)
            && links.TryGetProperty("web", out var web)
            && web.TryGetProperty("href", out var href)
            ? href.GetString()
            : null;

        if (htmlUrl is null && doc.RootElement.TryGetProperty("pullRequestId", out var idProp))
        {
            var prId = idProp.GetInt32();
            htmlUrl = $"{_apiBase}/_git/{Uri.EscapeDataString(repoName)}/pullrequest/{prId}";
        }

        _logger.Information("Opened PR for {Repo}: {Url}", repoName, htmlUrl);
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
            var secret = await _secretProvider.GetSecretAsync("AZURE_DEVOPS_PAT", ct)
                        ?? await _secretProvider.GetSecretAsync("ADO_PAT", ct);
            if (!string.IsNullOrEmpty(secret))
            {
                _resolvedToken = secret;
                return _resolvedToken;
            }
        }

        _resolvedToken = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT")
                      ?? Environment.GetEnvironmentVariable("ADO_PAT");
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($":{token}")));
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
    }

    private async Task LogErrorBodyAsync(HttpResponseMessage response, string action, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.Error("Azure DevOps {Action} failed: {Status} {Body}", action, (int)response.StatusCode, Truncate(body, 500));
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "…";

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
