using AutoFixer.Audit;
using AutoFixer.Models;
using Serilog;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace AutoFixer.Ingestion;

/// <summary>
/// Ingestor for PDF scan reports using PdfPig.
/// Performs best-effort text extraction and pattern matching to identify findings.
/// If no structured patterns are detected, falls back to treating each page as a single finding.
/// </summary>
public class PdfIngestor : IFindingIngestor
{
    private readonly PdfConfig _config;
    private readonly ILogger _logger;

    // Common patterns found in security scan PDFs (e.g. Mend, SonarQube exports)
    private static readonly Regex SeverityPattern = new(
        @"Severity[\s]*[:\-]?[\s]*(Critical|High|Medium|Low|Info|Blocker|Major|Minor)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CvePattern = new(
        @"CVE-\d{4}-\d{4,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IdPattern = new(
        @"(?:ID|Issue|Finding)[\s]*[:\-]?[\s]*([A-Za-z0-9\-_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PdfIngestor(PdfConfig config)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(PdfIngestor));
    }

    public async Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("PDF ingestor is disabled. Skipping.");
            return Enumerable.Empty<Finding>();
        }

        if (string.IsNullOrEmpty(inputFilePath))
        {
            _logger.Warning("PDF ingestor requires an input file path. Use --input-file <path>.");
            return Enumerable.Empty<Finding>();
        }

        if (!File.Exists(inputFilePath))
        {
            _logger.Warning("PDF file not found at {Path}.", inputFilePath);
            return Enumerable.Empty<Finding>();
        }

        _logger.Information("Reading PDF report from {Path} for repository {Repo}", inputFilePath, repoName);

        try
        {
            return await Task.Run(() => ParsePdf(inputFilePath, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error ingesting PDF file from {Path}", inputFilePath);
            return Enumerable.Empty<Finding>();
        }
    }

    private IEnumerable<Finding> ParsePdf(string filePath, CancellationToken ct)
    {
        using var document = PdfDocument.Open(filePath);
        var findings = new List<Finding>();

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var pageFindings = ExtractFindingsFromText(text, page.Number);
            findings.AddRange(pageFindings);
        }

        _logger.Information("PDF ingestion found {Count} findings.", findings.Count);
        return findings;
    }

    private static IEnumerable<Finding> ExtractFindingsFromText(string text, int pageNumber)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            yield break;

        // Strategy 1: Try to detect structured findings by severity pattern
        var severityMatches = SeverityPattern.Matches(text);
        if (severityMatches.Count > 0)
        {
            foreach (var match in severityMatches.Cast<Match>())
            {
                var severity = match.Groups[1].Value;
                var contextStart = Math.Max(0, match.Index - 200);
                var contextLength = Math.Min(500, text.Length - contextStart);
                var context = text.Substring(contextStart, contextLength);

                var idMatch = IdPattern.Match(context);
                var cveMatch = CvePattern.Match(context);

                yield return new Finding
                {
                    Id = idMatch.Success ? idMatch.Groups[1].Value : $"pdf-page-{pageNumber}-{match.Index}",
                    Source = "PDF",
                    Severity = severity,
                    Type = "Vulnerability",
                    Title = cveMatch.Success ? cveMatch.Value : $"PDF finding (page {pageNumber})",
                    Description = context.Trim(),
                    FilePath = null,
                    LineNumber = null,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["page"] = pageNumber,
                        ["severityMatchIndex"] = match.Index
                    }
                };
            }

            yield break;
        }

        // Strategy 2: Fallback — treat the page as one finding
        yield return new Finding
        {
            Id = $"pdf-page-{pageNumber}",
            Source = "PDF",
            Severity = "Medium",
            Type = "finding",
            Title = $"PDF finding (page {pageNumber})",
            Description = trimmed.Substring(0, Math.Min(500, trimmed.Length)),
            FilePath = null,
            LineNumber = null,
            Metadata = new Dictionary<string, object?> { ["page"] = pageNumber }
        };
    }
}
