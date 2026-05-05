using AutoFixer.Audit;
using AutoFixer.Models;
using Serilog;
using System.Text.Json;

namespace AutoFixer.Ingestion;

/// <summary>
/// Ingestor for SARIF (Static Analysis Results Interchange Format) reports.
/// SARIF is the industry-standard JSON format for static-analysis and security-scanning results.
/// Supports reading from a .sarif file supplied via <c>--input-file</c>.
/// </summary>
public class SarifIngestor : IFindingIngestor
{
    private readonly SarifConfig _config;
    private readonly ILogger _logger;

    public SarifIngestor(SarifConfig? config = null)
    {
        _config = config ?? new SarifConfig();
        _logger = LoggerSetup.GetLogger(nameof(SarifIngestor));
    }

    public async Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("SARIF ingestor is disabled. Skipping.");
            return Enumerable.Empty<Finding>();
        }

        if (string.IsNullOrEmpty(inputFilePath))
        {
            _logger.Warning("SARIF ingestor requires an input file path. Use --input-file <path>.");
            return Enumerable.Empty<Finding>();
        }

        if (!File.Exists(inputFilePath))
        {
            _logger.Warning("SARIF file not found at {Path}.", inputFilePath);
            return Enumerable.Empty<Finding>();
        }

        _logger.Information("Reading SARIF report from {Path} for repository {Repo}", inputFilePath, repoName);

        try
        {
            await using var stream = File.OpenRead(inputFilePath);
            var sarif = await JsonSerializer.DeserializeAsync<SarifLog>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }, cancellationToken);

            if (sarif?.Runs == null || sarif.Runs.Count == 0)
            {
                _logger.Warning("SARIF file contained no runs.");
                return Enumerable.Empty<Finding>();
            }

            var findings = new List<Finding>();
            foreach (var run in sarif.Runs)
            {
                if (run.Results == null) continue;

                var toolName = run.Tool?.Driver?.Name ?? "SARIF";
                foreach (var result in run.Results)
                {
                    findings.Add(ToFinding(result, toolName));
                }
            }

            _logger.Information("SARIF ingestion found {Count} findings.", findings.Count);
            return findings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error ingesting SARIF file from {Path}", inputFilePath);
            return Enumerable.Empty<Finding>();
        }
    }

    private static Finding ToFinding(SarifResult result, string toolName)
    {
        var location = result.Locations?.FirstOrDefault();
        var physicalLocation = location?.PhysicalLocation;
        var region = physicalLocation?.Region;

        return new Finding
        {
            Id = result.RuleId ?? result.Guid ?? Guid.NewGuid().ToString(),
            Source = toolName,
            Severity = MapLevel(result.Level),
            Type = result.Kind ?? "finding",
            Title = result.Message?.Text ?? result.RuleId ?? "SARIF finding",
            Description = result.Message?.Text ?? string.Empty,
            FilePath = physicalLocation?.ArtifactLocation?.Uri,
            LineNumber = region?.StartLine,
            Metadata = new Dictionary<string, object?>
            {
                ["ruleId"] = result.RuleId,
                ["ruleIndex"] = result.RuleIndex,
                ["kind"] = result.Kind,
                ["level"] = result.Level,
                ["guid"] = result.Guid,
            }
        };
    }

    private static string MapLevel(string? level) => level?.ToLowerInvariant() switch
    {
        "error" => "High",
        "warning" => "Medium",
        "note" => "Low",
        "none" => "Info",
        _ => "Medium",
    };

    // ---- SARIF JSON DTOs (minimal subset) ----

    private class SarifLog
    {
        public List<SarifRun>? Runs { get; set; }
    }

    private class SarifRun
    {
        public SarifTool? Tool { get; set; }
        public List<SarifResult>? Results { get; set; }
    }

    private class SarifTool
    {
        public SarifToolDriver? Driver { get; set; }
    }

    private class SarifToolDriver
    {
        public string? Name { get; set; }
    }

    private class SarifResult
    {
        public string? RuleId { get; set; }
        public string? Guid { get; set; }
        public string? Kind { get; set; }
        public string? Level { get; set; }
        public int? RuleIndex { get; set; }
        public SarifMessage? Message { get; set; }
        public List<SarifLocation>? Locations { get; set; }
    }

    private class SarifMessage
    {
        public string? Text { get; set; }
    }

    private class SarifLocation
    {
        public SarifPhysicalLocation? PhysicalLocation { get; set; }
    }

    private class SarifPhysicalLocation
    {
        public SarifArtifactLocation? ArtifactLocation { get; set; }
        public SarifRegion? Region { get; set; }
    }

    private class SarifArtifactLocation
    {
        public string? Uri { get; set; }
    }

    private class SarifRegion
    {
        public int? StartLine { get; set; }
        public int? StartColumn { get; set; }
        public int? EndLine { get; set; }
        public int? EndColumn { get; set; }
    }
}
