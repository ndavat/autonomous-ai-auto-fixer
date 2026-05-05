using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.Models;
using ClosedXML.Excel;
using FluentAssertions;

namespace AutoFixer.Tests;

public class ExcelIngestorTests : IDisposable
{
    private readonly string _tempDir;

    public ExcelIngestorTests()
    {
        LoggerSetup.Initialize();
        _tempDir = Path.Combine(Path.GetTempPath(), $"autofix-excel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    private string CreateWorkbook(Action<IXLWorksheet> populate)
    {
        var path = Path.Combine(_tempDir, $"report-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Findings");
        populate(worksheet);
        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public async Task IngestFindingsAsync_Disabled_ShouldReturnEmpty()
    {
        var ingestor = new ExcelIngestor(new ExcelConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app", "any.xlsx");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_MissingFile_ShouldReturnEmpty()
    {
        var ingestor = new ExcelIngestor(new ExcelConfig { Enabled = true });

        var result = await ingestor.IngestFindingsAsync("acme/app", "does-not-exist.xlsx");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_DefaultColumns_ShouldParseFindings()
    {
        var path = CreateWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "Severity";
            ws.Cell(1, 3).Value = "Type";
            ws.Cell(1, 4).Value = "Title";
            ws.Cell(1, 5).Value = "Description";
            ws.Cell(1, 6).Value = "FilePath";
            ws.Cell(1, 7).Value = "LineNumber";

            ws.Cell(2, 1).Value = "CVE-2024-0001";
            ws.Cell(2, 2).Value = "High";
            ws.Cell(2, 3).Value = "Vulnerability";
            ws.Cell(2, 4).Value = "SQL Injection";
            ws.Cell(2, 5).Value = "Possible SQL injection";
            ws.Cell(2, 6).Value = "src/Data/Repo.cs";
            ws.Cell(2, 7).Value = 42;

            ws.Cell(3, 1).Value = "BUG-001";
            ws.Cell(3, 2).Value = "Medium";
            ws.Cell(3, 3).Value = "Bug";
            ws.Cell(3, 4).Value = "Null reference";
            ws.Cell(3, 5).Value = "Dereferencing null";
            ws.Cell(3, 6).Value = "src/Service.cs";
            ws.Cell(3, 7).Value = 15;
        });

        var ingestor = new ExcelIngestor(new ExcelConfig { Enabled = true });
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
        list[1].LineNumber.Should().Be(15);
    }

    [Fact]
    public async Task IngestFindingsAsync_CustomColumns_ShouldMapCorrectly()
    {
        var path = CreateWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "IssueID";
            ws.Cell(1, 2).Value = "Risk";
            ws.Cell(1, 3).Value = "Category";
            ws.Cell(1, 4).Value = "Name";
            ws.Cell(1, 5).Value = "Details";
            ws.Cell(1, 6).Value = "Path";
            ws.Cell(1, 7).Value = "Line";

            ws.Cell(2, 1).Value = "ISSUE-99";
            ws.Cell(2, 2).Value = "Critical";
            ws.Cell(2, 3).Value = "Security";
            ws.Cell(2, 4).Value = "XSS";
            ws.Cell(2, 5).Value = "Cross-site scripting";
            ws.Cell(2, 6).Value = "src/app.js";
            ws.Cell(2, 7).Value = 7;
        });

        var ingestor = new ExcelIngestor(new ExcelConfig
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
        var path = CreateWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "Title";
            ws.Cell(2, 1).Value = "FIND-01";
            ws.Cell(2, 2).Value = "Missing fields";
        });

        var ingestor = new ExcelIngestor(new ExcelConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().Be("FIND-01");
        list[0].Title.Should().Be("Missing fields");
        list[0].Severity.Should().Be("Medium");
        list[0].Type.Should().Be("finding");
        list[0].Description.Should().BeEmpty();
        list[0].FilePath.Should().BeNull();
        list[0].LineNumber.Should().BeNull();
    }

    [Fact]
    public async Task IngestFindingsAsync_EmptySheet_ShouldReturnEmpty()
    {
        var path = CreateWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Id";
            // No data rows
        });

        var ingestor = new ExcelIngestor(new ExcelConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);

        result.Should().BeEmpty();
    }
}
