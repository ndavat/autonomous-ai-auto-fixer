using AutoFixer.Audit;
using AutoFixer.Models;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace AutoFixer.Ingestion;

/// <summary>
/// Ingestor for Trivy vulnerability/configuration scan results.
/// Supports reading JSON scan reports (e.g., from 'trivy fs . --format json --output report.json').
/// </summary>
public class TrivyIngestor : IFindingIngestor
{
    private readonly TrivyConfig _config;
    private readonly ILogger _logger;

    public TrivyIngestor(TrivyConfig config)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(TrivyIngestor));
    }

    public async Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("Trivy ingestor is disabled. Skipping.");
            return Enumerable.Empty<Finding>();
        }

        _logger.Information("Starting Trivy ingestion for repository: {Repo}", repoName);

        // Prefer explicit input file from CLI, otherwise fall back to default location.
        var reportPath = !string.IsNullOrEmpty(inputFilePath)
            ? inputFilePath
            : Path.Combine("data", "inputs", "trivy-report.json");

        if (!File.Exists(reportPath))
        {
            _logger.Warning("Trivy report not found at {Path}. Returning empty.", reportPath);
            return Enumerable.Empty<Finding>();
        }

        try
        {
            await using var stream = File.OpenRead(reportPath);
            var report = await JsonSerializer.DeserializeAsync<TrivyReport>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);

            if (report?.Results == null)
                return Enumerable.Empty<Finding>();

            var findings = new List<Finding>();
            foreach (var result in report.Results)
            {
                if (result.Vulnerabilities == null) continue;
                foreach (var vuln in result.Vulnerabilities)
                {
                    findings.Add(new Finding
                    {
                        Id = vuln.VulnerabilityId ?? Guid.NewGuid().ToString(),
                        Source = "Trivy",
                        Severity = vuln.Severity ?? "Unknown",
                        Type = "Vulnerability",
                        Title = vuln.Title ?? vuln.VulnerabilityId ?? "Unknown vulnerability",
                        Description = vuln.Description ?? string.Empty,
                        FilePath = result.Target,
                        Metadata = new Dictionary<string, object?>
                        {
                            ["package"] = vuln.PkgName,
                            ["installedVersion"] = vuln.InstalledVersion,
                            ["fixedVersion"] = vuln.FixedVersion,
                            ["cvss"] = vuln.Cvss,
                            ["primaryUrl"] = vuln.PrimaryUrl
                        }
                    });
                }
            }

            _logger.Information("Trivy ingestion found {Count} vulnerabilities.", findings.Count);
            return findings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error ingesting Trivy report");
            return Enumerable.Empty<Finding>();
        }
    }

    // DTOs for Trivy JSON output
    private class TrivyReport
    {
        public List<TrivyResult>? Results { get; set; }
    }

    private class TrivyResult
    {
        public string? Target { get; set; }
        public List<TrivyVulnerability>? Vulnerabilities { get; set; }
    }

    private class TrivyVulnerability
    {
        public string? VulnerabilityId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Severity { get; set; }
        public string? PkgName { get; set; }
        public string? InstalledVersion { get; set; }
        public string? FixedVersion { get; set; }
        public string? PrimaryUrl { get; set; }
        public double? Cvss { get; set; }
    }
}
