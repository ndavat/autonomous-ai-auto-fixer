using AutoFixer.Audit;
using AutoFixer.Models;
using ClosedXML.Excel;
using Serilog;

namespace AutoFixer.Ingestion;

/// <summary>
/// Ingestor for Excel (.xlsx) scan reports using ClosedXML.
/// Reads from the configured worksheet (default index 0) and maps rows to
/// <see cref="Finding"/> objects using configurable column names.
/// </summary>
public class ExcelIngestor : IFindingIngestor
{
    private readonly ExcelConfig _config;
    private readonly ILogger _logger;

    public ExcelIngestor(ExcelConfig config)
    {
        _config = config;
        _logger = LoggerSetup.GetLogger(nameof(ExcelIngestor));
    }

    public async Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("Excel ingestor is disabled. Skipping.");
            return Enumerable.Empty<Finding>();
        }

        if (string.IsNullOrEmpty(inputFilePath))
        {
            _logger.Warning("Excel ingestor requires an input file path. Use --input-file <path>.");
            return Enumerable.Empty<Finding>();
        }

        if (!File.Exists(inputFilePath))
        {
            _logger.Warning("Excel file not found at {Path}.", inputFilePath);
            return Enumerable.Empty<Finding>();
        }

        _logger.Information("Reading Excel report from {Path} for repository {Repo}", inputFilePath, repoName);

        try
        {
            return await Task.Run(() => ParseWorkbook(inputFilePath, cancellationToken).ToList(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error ingesting Excel file from {Path}", inputFilePath);
            return Enumerable.Empty<Finding>();
        }
    }

    private IEnumerable<Finding> ParseWorkbook(string filePath, CancellationToken ct)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.ElementAtOrDefault(_config.SheetIndex);
        if (worksheet == null)
        {
            _logger.Warning("Worksheet index {Index} not found in {Path}.", _config.SheetIndex, filePath);
            yield break;
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow == null)
        {
            _logger.Warning("No header row found in worksheet {Sheet}.", worksheet.Name);
            yield break;
        }

        var columns = headerRow.CellsUsed()
            .ToDictionary(
                c => c.GetValue<string>()?.Trim() ?? string.Empty,
                c => c.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);

        var dataRows = worksheet.RowsUsed().Skip(1); // skip header
        foreach (var row in dataRows)
        {
            ct.ThrowIfCancellationRequested();

            string? Get(string columnName) =>
                columns.TryGetValue(columnName, out var col)
                    ? row.Cell(col).GetValue<string>()?.Trim()
                    : null;

            var lineStr = Get(_config.LineNumberColumn);
            int? lineNum = int.TryParse(lineStr, out var ln) ? ln : null;

            yield return new Finding
            {
                Id = Get(_config.IdColumn) ?? Guid.NewGuid().ToString(),
                Source = "Excel",
                Severity = Get(_config.SeverityColumn) ?? "Medium",
                Type = Get(_config.TypeColumn) ?? "finding",
                Title = Get(_config.TitleColumn) ?? "Excel finding",
                Description = Get(_config.DescriptionColumn) ?? string.Empty,
                FilePath = Get(_config.FilePathColumn),
                LineNumber = lineNum,
                Metadata = new Dictionary<string, object?>()
            };
        }
    }
}
