using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace AutoFixer.Tests;

public class MendIngestorTests : IDisposable
{
    private readonly string _tempDir;

    public MendIngestorTests()
    {
        LoggerSetup.Initialize();
        _tempDir = Path.Combine(Path.GetTempPath(), $"autofix-mend-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task IngestFindingsAsync_Disabled_ShouldReturnEmpty()
    {
        var ingestor = new MendIngestor(new MendConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app", "any.json");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_MissingFile_ShouldReturnEmpty()
    {
        var ingestor = new MendIngestor(new MendConfig { Enabled = true });

        var result = await ingestor.IngestFindingsAsync("acme/app", "does-not-exist.json");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_ValidJsonFile_ShouldParseFindings()
    {
        var path = Path.Combine(_tempDir, "mend-report.json");
        var vulnerabilities = new[]
        {
            new
            {
                id = "CVE-2024-0001",
                name = "SQL Injection",
                description = "Possible SQL injection in user input",
                severity = "High",
                filePath = "src/Data/Repo.cs",
                line = 42,
                cve = "CVE-2024-0001",
                library = "System.Data.SqlClient",
                score = 8.5
            },
            new
            {
                id = "CVE-2024-0002",
                name = "XSS",
                description = "Cross-site scripting in comment field",
                severity = "Medium",
                filePath = "src/Views/Home.cshtml",
                line = 15,
                cve = "CVE-2024-0002",
                library = "Microsoft.AspNetCore.Mvc",
                score = 5.0
            }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(vulnerabilities));

        var ingestor = new MendIngestor(new MendConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(2);
        list[0].Id.Should().Be("CVE-2024-0001");
        list[0].Source.Should().Be("Mend");
        list[0].Severity.Should().Be("High");
        list[0].Type.Should().Be("Vulnerability");
        list[0].Title.Should().Be("SQL Injection");
        list[0].FilePath.Should().Be("src/Data/Repo.cs");
        list[0].LineNumber.Should().Be(42);
        list[0].Metadata["cve"].Should().Be("CVE-2024-0001");
        list[0].Metadata["library"].Should().Be("System.Data.SqlClient");
        list[0].Metadata["score"].Should().Be(8.5);

        list[1].Id.Should().Be("CVE-2024-0002");
        list[1].Severity.Should().Be("Medium");
        list[1].LineNumber.Should().Be(15);
    }

    [Fact]
    public async Task IngestFindingsAsync_NullFields_ShouldUseDefaults()
    {
        var path = Path.Combine(_tempDir, "mend-minimal.json");
        var vulnerabilities = new[]
        {
            new
            {
                id = (string?)null,
                name = (string?)null,
                description = (string?)null,
                severity = (string?)null,
                filePath = (string?)null,
                line = (int?)null,
                cve = (string?)null,
                library = (string?)null,
                score = (double?)null
            }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(vulnerabilities));

        var ingestor = new MendIngestor(new MendConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().NotBeNullOrEmpty(); // falls back to Guid
        list[0].Source.Should().Be("Mend");
        list[0].Severity.Should().Be("Medium"); // default
        list[0].Title.Should().Be("Unknown vulnerability");
        list[0].Description.Should().BeEmpty();
        list[0].FilePath.Should().BeNull();
        list[0].LineNumber.Should().BeNull();
    }

    [Fact]
    public async Task IngestFindingsAsync_NoApiUrlOrFile_ShouldReturnEmpty()
    {
        var ingestor = new MendIngestor(new MendConfig
        {
            Enabled = true,
            ApiUrl = null
        });

        var result = await ingestor.IngestFindingsAsync("acme/app");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_ApiCall_ShouldReturnFindings()
    {
        var vulnerabilities = new[]
        {
            new
            {
                id = "API-001",
                name = "API vuln",
                description = "Found via API",
                severity = "Critical",
                filePath = "src/app.js",
                line = 7,
                cve = "CVE-2024-9999",
                library = "lodash",
                score = 9.8
            }
        };

        var handler = new CapturingHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/vulnerabilities"))
                return TestHelpers.JsonResponse(HttpStatusCode.OK, vulnerabilities);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var http = new HttpClient(handler);
        var ingestor = new MendIngestor(
            new MendConfig
            {
                Enabled = true,
                ApiUrl = "https://mend.example.com",
                ApiKey = "test-key"
            },
            http);

        var result = await ingestor.IngestFindingsAsync("acme/app");
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().Be("API-001");
        list[0].Severity.Should().Be("Critical");
        list[0].Source.Should().Be("Mend");

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().EndWith("/acme/app/vulnerabilities");
        handler.Requests[0].Headers.Authorization.Should().NotBeNull();
        handler.Requests[0].Headers.Authorization!.Scheme.Should().Be("APIKey");
    }

    [Fact]
    public async Task IngestFindingsAsync_InvalidJsonFile_ShouldReturnEmpty()
    {
        var path = Path.Combine(_tempDir, "mend-bad.json");
        await File.WriteAllTextAsync(path, "this is not valid json");

        var ingestor = new MendIngestor(new MendConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);

        result.Should().BeEmpty();
    }


}
