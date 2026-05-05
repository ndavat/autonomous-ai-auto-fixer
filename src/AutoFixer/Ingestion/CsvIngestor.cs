using AutoFixer.Audit;
using AutoFixer.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using System.Globalization;

namespace AutoFixer.Ingestion;

/// <summary>
/// Ingestor for CSV scan reports. Expects a header row with configurable column names
/// (defaults: Id, Severity, Type, Title, Description, FilePath, LineNumber).
/// Supports reading from a file supplied via <c>--input-file</c>.
/// </summary>
public class CsvIngestor : IFindingIngestor
{
    private readonly CsvConfig _config;
    private readonly ILogger _logger;

    public CsvIngestor(CsvConfig config)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(CsvIngestor));
    }

    public async Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("CSV ingestor is disabled. Skipping.");
            return Enumerable.Empty<Finding>();
        }

        if (string.IsNullOrEmpty(inputFilePath))
        {
            _logger.Warning("CSV ingestor requires an input file path. Use --input-file <path>.");
            return Enumerable.Empty<Finding>();
        }

        if (!File.Exists(inputFilePath))
        {
            _logger.Warning("CSV file not found at {Path}.", inputFilePath);
            return Enumerable.Empty<Finding>();
        }

        _logger.Information("Reading CSV report from {Path} for repository {Repo}", inputFilePath, repoName);

        try
        {
            using var stream = File.OpenRead(inputFilePath);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
            });

            var records = csv.GetRecords<dynamic>().ToList();
            var findings = new List<Finding>();

            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dict = (IDictionary<string, object>)record;
                findings.Add(MapToFinding(dict));
            }

            _logger.Information("CSV ingestion found {Count} findings.", findings.Count);
            return findings;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error ingesting CSV file from {Path}", inputFilePath);
            return Enumerable.Empty<Finding>();
        }
    }

    private Finding MapToFinding(IDictionary<string, object> dict)
    {
        string? Get(string column) =>
            dict.TryGetValue(column, out var value) ? value?.ToString() : null;

        var lineStr = Get(_config.LineNumberColumn);
        int? lineNum = int.TryParse(lineStr, out var ln) ? ln : null;

        return new Finding
        {
            Id = Get(_config.IdColumn) ?? Guid.NewGuid().ToString(),
            Source = "CSV",
            Severity = Get(_config.SeverityColumn) ?? "Medium",
            Type = Get(_config.TypeColumn) ?? "finding",
            Title = Get(_config.TitleColumn) ?? "CSV finding",
            Description = Get(_config.DescriptionColumn) ?? string.Empty,
            FilePath = Get(_config.FilePathColumn),
            LineNumber = lineNum,
            Metadata = new Dictionary<string, object?>(dict.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)))
        };
    }
}
