using AutoFixer.Models;
using AutoFixer.Audit;
using Serilog;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace AutoFixer.Ingestion;

/// <summary>
/// Ingestor for Mend (formerly WhiteSource) vulnerability reports.
/// Supports API-based and file-based ingestion.
/// </summary>
public class MendIngestor : IFindingIngestor
{
    private readonly MendConfig _config;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public MendIngestor(MendConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(MendIngestor));
        _httpClient = httpClient ?? new HttpClient();
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"APIKey {config.ApiKey}");
        }
    }

    public async Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("Mend ingestor is disabled. Skipping.");
            return Enumerable.Empty<Finding>();
        }

        _logger.Information("Starting Mend ingestion for repository: {Repo}", repoName);

        try
        {
            // Prefer file-based ingestion when a report file was supplied via --input-file
            if (!string.IsNullOrEmpty(inputFilePath) && File.Exists(inputFilePath))
            {
                return await IngestFromFileAsync(inputFilePath, cancellationToken);
            }

            // If API URL is configured, attempt API call
            if (!string.IsNullOrEmpty(_config.ApiUrl))
            {
                return await IngestFromApiAsync(repoName, cancellationToken);
            }

            _logger.Warning("No Mend API URL configured and no input file supplied. Returning empty findings.");
            return Enumerable.Empty<Finding>();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error ingesting Mend findings for {Repo}", repoName);
            return Enumerable.Empty<Finding>();
        }
    }

    private async Task<IEnumerable<Finding>> IngestFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.Information("Reading Mend report from file: {Path}", filePath);
        await using var stream = File.OpenRead(filePath);
        var vulnerabilities = await JsonSerializer.DeserializeAsync<List<MendVulnerability>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        return vulnerabilities == null
            ? Enumerable.Empty<Finding>()
            : vulnerabilities.Select(ToFinding);
    }

    private async Task<IEnumerable<Finding>> IngestFromApiAsync(string repoName, CancellationToken cancellationToken)
    {
        // Example: call Mend API to get vulnerabilities for a project
        // The actual endpoint depends on Mend's API; this is a placeholder.
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.ApiUrl}/projects/{repoName}/vulnerabilities");
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var vulnerabilities = JsonSerializer.Deserialize<List<MendVulnerability>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (vulnerabilities == null)
            return Enumerable.Empty<Finding>();

        return vulnerabilities.Select(ToFinding);
    }

    private static Finding ToFinding(MendVulnerability v) => new()
    {
        Id = v.Id ?? Guid.NewGuid().ToString(),
        Source = "Mend",
        Severity = v.Severity ?? "Medium",
        Type = "Vulnerability",
        Title = v.Name ?? "Unknown vulnerability",
        Description = v.Description ?? string.Empty,
        FilePath = v.FilePath,
        LineNumber = v.Line,
        Metadata = new Dictionary<string, object?>
        {
            ["cve"] = v.Cve,
            ["library"] = v.Library,
            ["score"] = v.Score
        }
    };

    // Helper classes for JSON deserialization
    private class MendVulnerability
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Severity { get; set; }
        public string? FilePath { get; set; }
        public int? Line { get; set; }
        public string? Cve { get; set; }
        public string? Library { get; set; }
        public double? Score { get; set; }
    }
}