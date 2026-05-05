using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.Models;
using FluentAssertions;

namespace AutoFixer.Tests;

public class CsvIngestorTests : IDisposable
{
    private readonly string _tempDir;

    public CsvIngestorTests()
    {
        LoggerSetup.Initialize();
        _tempDir = Path.Combine(Path.GetTempPath(), $"autofix-csv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task IngestFindingsAsync_Disabled_ShouldReturnEmpty()
    {
        var ingestor = new CsvIngestor(new CsvConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app", "any.csv");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_MissingFile_ShouldReturnEmpty()
    {
        var ingestor = new CsvIngestor(new CsvConfig { Enabled = true });

        var result = await ingestor.IngestFindingsAsync("acme/app", "does-not-exist.csv");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_DefaultColumns_ShouldParseFindings()
    {
        var path = Path.Combine(_tempDir, "report.csv");
        await File.WriteAllTextAsync(path, "Id,Severity,Type,Title,Description,FilePath,LineNumber\nCVE-2024-0001,High,Vulnerability,SQL Injection,Possible SQL injection in user input,src/Data/Repo.cs,42\nBUG-001,Medium,Bug,Null reference,Dereferencing null pointer,src/Service.cs,15\n");

        var ingestor = new CsvIngestor(new CsvConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(2);
        list[0].Id.Should().Be("CVE-2024-0001");
        list[0].Severity.Should().Be("High");
        list[0].Type.Should().Be("Vulnerability");
        list[0].Title.Should().Be("SQL Injection");
        list[0].FilePath.Should().Be("src/Data/Repo.cs");
        list[0].LineNumber.Should().Be(42);

        list[1].Id.Should().Be("BUG-001");
        list[1].Severity.Should().Be("Medium");
        list[1].LineNumber.Should().Be(15);
    }

    [Fact]
    public async Task IngestFindingsAsync_CustomColumns_ShouldMapCorrectly()
    {
        var path = Path.Combine(_tempDir, "custom.csv");
        await File.WriteAllTextAsync(path, "IssueID,Risk,Category,Name,Details,Path,Line\nISSUE-99,Critical,Security,XSS,Cross-site scripting,src/app.js,7\n");

        var ingestor = new CsvIngestor(new CsvConfig
        {
            Enabled = true,
            IdColumn = "IssueID",
            SeverityColumn = "Risk",
            TypeColumn = "Category",
            TitleColumn = "Name",
            DescriptionColumn = "Details",
            FilePathColumn = "Path",
            LineNumberColumn = "Line"
        });

        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().Be("ISSUE-99");
        list[0].Severity.Should().Be("Critical");
        list[0].Type.Should().Be("Security");
        list[0].Title.Should().Be("XSS");
        list[0].FilePath.Should().Be("src/app.js");
        list[0].LineNumber.Should().Be(7);
    }

    [Fact]
    public async Task IngestFindingsAsync_MissingOptionalColumns_ShouldUseDefaults()
    {
        var path = Path.Combine(_tempDir, "minimal.csv");
        await File.WriteAllTextAsync(path, "Id,Title\nFIND-01,Missing fields\n");

        var ingestor = new CsvIngestor(new CsvConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().Be("FIND-01");
        list[0].Title.Should().Be("Missing fields");
        list[0].Severity.Should().Be("Medium"); // default
        list[0].Type.Should().Be("finding");     // default
        list[0].Description.Should().BeEmpty();
        list[0].FilePath.Should().BeNull();
        list[0].LineNumber.Should().BeNull();
    }

    [Fact]
    public async Task IngestFindingsAsync_Cancellation_ShouldThrow()
    {
        var path = Path.Combine(_tempDir, "report.csv");
        await File.WriteAllTextAsync(path, "Id,Severity\nX,High\n");

        var ingestor = new CsvIngestor(new CsvConfig { Enabled = true });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => ingestor.IngestFindingsAsync("acme/app", path, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
