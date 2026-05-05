using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.Models;
using FluentAssertions;

namespace AutoFixer.Tests;

public class PdfIngestorTests : IDisposable
{
    private readonly string _tempDir;

    public PdfIngestorTests()
    {
        LoggerSetup.Initialize();
        _tempDir = Path.Combine(Path.GetTempPath(), $"autofix-pdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    // Creates a minimal valid PDF with text content for testing.
    private string CreateMinimalPdf(string text)
    {
        var path = Path.Combine(_tempDir, $"report-{Guid.NewGuid():N}.pdf");

        var header = "%PDF-1.4\n";
        var obj1 = "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n";
        var obj2 = "2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n";
        var obj3 = "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Contents 4 0 R/Resources<</Font<</F1<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>>>>>>>endobj\n";
        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
        var content = $"BT /F1 12 Tf 100 700 Td ({escaped}) Tj ET";
        var obj4 = $"4 0 obj<</Length {content.Length}>>\nstream\n{content}\nendstream\nendobj\n";

        var off1 = header.Length;
        var off2 = off1 + obj1.Length;
        var off3 = off2 + obj2.Length;
        var off4 = off3 + obj3.Length;

        var xref = $"xref\n0 5\n0000000000 65535 f \n{off1:D10} 00000 n \n{off2:D10} 00000 n \n{off3:D10} 00000 n \n{off4:D10} 00000 n \n";
        var trailer = $"trailer\n<</Size 5/Root 1 0 R>>\nstartxref\n{off4 + obj4.Length}\n%%EOF\n";

        var pdf = header + obj1 + obj2 + obj3 + obj4 + xref + trailer;
        File.WriteAllText(path, pdf);
        return path;
    }

    [Fact]
    public async Task IngestFindingsAsync_Disabled_ShouldReturnEmpty()
    {
        var ingestor = new PdfIngestor(new PdfConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app", "any.pdf");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_MissingFile_ShouldReturnEmpty()
    {
        var ingestor = new PdfIngestor(new PdfConfig { Enabled = true });

        var result = await ingestor.IngestFindingsAsync("acme/app", "does-not-exist.pdf");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_WithSeverityPatterns_ShouldExtractFindings()
    {
        var path = CreateMinimalPdf("ID: VULN-001 Severity: High CVE-2024-1234 SQL Injection");

        var ingestor = new PdfIngestor(new PdfConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Severity.Should().Be("High");
        list[0].Title.Should().Be("CVE-2024-1234");
        list[0].Id.Should().Be("VULN-001");
        list[0].Source.Should().Be("PDF");
    }

    [Fact]
    public async Task IngestFindingsAsync_WithoutPatterns_ShouldFallbackToPageFinding()
    {
        var path = CreateMinimalPdf("Generic scan report with no severity markers.");

        var ingestor = new PdfIngestor(new PdfConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().Be("pdf-page-1");
        list[0].Severity.Should().Be("Medium");
        list[0].Source.Should().Be("PDF");
    }

    [Fact]
    public async Task IngestFindingsAsync_Cancellation_ShouldThrow()
    {
        var path = CreateMinimalPdf("Severity: High");

        var ingestor = new PdfIngestor(new PdfConfig { Enabled = true });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => ingestor.IngestFindingsAsync("acme/app", path, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
