using AutoFixer.Audit;
using AutoFixer.Models;
using Serilog;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AutoFixer.Ingestion;

/// <summary>
/// Ingestor to consume SonarQube analysis results via its REST API.
/// </summary>
public class SonarQubeIngestor : IFindingIngestor
{
    private readonly SonarQubeConfig _config;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public SonarQubeIngestor(SonarQubeConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(SonarQubeIngestor));
        _httpClient = httpClient ?? new HttpClient();
        if (!string.IsNullOrEmpty(config.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{config.Token}:") ));
        }
    }

    public async Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("SonarQube ingestor is disabled. Skipping.");
            return Enumerable.Empty<Finding>();
        }

        // File-based ingestion when a SonarQube export is provided
        if (!string.IsNullOrEmpty(inputFilePath) && File.Exists(inputFilePath))
        {
            _logger.Information("Reading SonarQube export from file: {Path}", inputFilePath);
            try
            {
                await using var stream = File.OpenRead(inputFilePath);
                var projectResponse = await JsonSerializer.DeserializeAsync<SonarProjectResponse>(
                    stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
                return (projectResponse?.Issues ?? new List<SonarIssue>()).Select(ToFinding);
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize SonarQube export from {Path}", inputFilePath);
                return Enumerable.Empty<Finding>();
            }
        }

        if (string.IsNullOrEmpty(_config.HostUrl))
        {
            _logger.Warning("No SonarQube HostUrl configured and no input file supplied. Returning empty findings.");
            return Enumerable.Empty<Finding>();
        }

        var issues = await GetIssuesAsync(repoName, cancellationToken);
        return issues.Select(ToFinding);
    }

    private async Task<List<SonarIssue>> GetIssuesAsync(string repoName, CancellationToken cancellationToken)
    {
        // SonarQube API expects project key; we map repoName to projectKey via naming convention.
        var projectKey = repoName.Replace('/', '-');
        // Endpoint: api/issues/search?componentKeys={projectKey}
        var url = $"{_config.HostUrl}/api/issues/search?componentKeys={projectKey}&ps=500"; // ps=500 max page size

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var projectResponse = JsonSerializer.Deserialize<SonarProjectResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return projectResponse?.Issues ?? new List<SonarIssue>();
    }

    private Finding ToFinding(SonarIssue issue)
    {
        return new Finding
        {
            Id = issue.Key ?? Guid.NewGuid().ToString(),
            Source = "SonarQube",
            Severity = issue.Severity ?? "Unknown",
            Type = issue.Type ?? "Bug",
            Title = issue.Message ?? "SonarQube issue",
            Description = issue.Message ?? string.Empty,
            FilePath = issue.Component,
            LineNumber = int.TryParse(issue.Line, out var line) ? line : (int?)null,
            Metadata = new()
            {
                ["component"] = issue.Component,
                ["severity"] = issue.Severity,
                ["type"] = issue.Type,
                ["analysisKey"] = issue.AnalysisKey
            }
        };
    }

    // DTOs for SonarQube API responses
    private class SonarProjectResponse
    {
        public List<SonarIssue> Issues { get; set; } = new();
    }

    private class SonarIssue
    {
        public string? Key { get; set; }
        public string? Severity { get; set; }
        public string? Type { get; set; }
        public string? Message { get; set; }
        public string? Component { get; set; }
        public string? Line { get; set; }
        public string? AnalysisKey { get; set; }
    }
}
