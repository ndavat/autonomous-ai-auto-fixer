using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace AutoFixer.Tests;

public class SonarQubeIngestorTests : IDisposable
{
    private readonly string _tempDir;

    public SonarQubeIngestorTests()
    {
        LoggerSetup.Initialize();
        _tempDir = Path.Combine(Path.GetTempPath(), $"autofix-sonar-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task IngestFindingsAsync_Disabled_ShouldReturnEmpty()
    {
        var ingestor = new SonarQubeIngestor(new SonarQubeConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app", "any.json");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_MissingFile_ShouldReturnEmpty()
    {
        var ingestor = new SonarQubeIngestor(new SonarQubeConfig
        {
            Enabled = true,
            HostUrl = null
        });

        var result = await ingestor.IngestFindingsAsync("acme/app", "does-not-exist.json");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_ValidJsonFile_ShouldParseFindings()
    {
        var path = Path.Combine(_tempDir, "sonar-export.json");
        var report = new
        {
            issues = new[]
            {
                new
                {
                    key = "AY2hV7H4-abc",
                    severity = "BLOCKER",
                    type = "BUG",
                    message = "Add a private constructor to hide the implicit public one",
                    component = "src/AutoFixer/Models/Config.cs",
                    line = "12",
                    analysisKey = "ANALYSIS-001"
                },
                new
                {
                    key = "AY2hV7H4-def",
                    severity = "MINOR",
                    type = "CODE_SMELL",
                    message = "Remove this unused variable",
                    component = "src/AutoFixer/Program.cs",
                    line = "42",
                    analysisKey = "ANALYSIS-002"
                }
            }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report));

        var ingestor = new SonarQubeIngestor(new SonarQubeConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(2);
        list[0].Id.Should().Be("AY2hV7H4-abc");
        list[0].Source.Should().Be("SonarQube");
        list[0].Severity.Should().Be("BLOCKER");
        list[0].Type.Should().Be("BUG");
        list[0].Title.Should().Be("Add a private constructor to hide the implicit public one");
        list[0].FilePath.Should().Be("src/AutoFixer/Models/Config.cs");
        list[0].LineNumber.Should().Be(12);
        list[0].Metadata["analysisKey"].Should().Be("ANALYSIS-001");

        list[1].Id.Should().Be("AY2hV7H4-def");
        list[1].Severity.Should().Be("MINOR");
        list[1].Type.Should().Be("CODE_SMELL");
        list[1].LineNumber.Should().Be(42);
    }

    [Fact]
    public async Task IngestFindingsAsync_NullFields_ShouldUseDefaults()
    {
        var path = Path.Combine(_tempDir, "sonar-minimal.json");
        var report = new
        {
            issues = new[]
            {
                new
                {
                    key = (string?)null,
                    severity = (string?)null,
                    type = (string?)null,
                    message = (string?)null,
                    component = (string?)null,
                    line = (string?)null,
                    analysisKey = (string?)null
                }
            }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report));

        var ingestor = new SonarQubeIngestor(new SonarQubeConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().NotBeNullOrEmpty(); // falls back to Guid
        list[0].Source.Should().Be("SonarQube");
        list[0].Severity.Should().Be("Unknown");
        list[0].Type.Should().Be("Bug");
        list[0].Title.Should().Be("SonarQube issue");
        list[0].LineNumber.Should().BeNull();
    }

    [Fact]
    public async Task IngestFindingsAsync_NoHostUrlOrFile_ShouldReturnEmpty()
    {
        var ingestor = new SonarQubeIngestor(new SonarQubeConfig
        {
            Enabled = true,
            HostUrl = null
        });

        var result = await ingestor.IngestFindingsAsync("acme/app");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_ApiCall_ShouldReturnFindings()
    {
        var apiResponse = new
        {
            issues = new[]
            {
                new
                {
                    key = "API-SQ-001",
                    severity = "MAJOR",
                    type = "VULNERABILITY",
                    message = "Hardcoded credentials detected",
                    component = "src/secrets.cs",
                    line = "10",
                    analysisKey = "api-analysis"
                }
            }
        };

        var handler = new CapturingHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/api/issues/search"))
                return TestHelpers.JsonResponse(HttpStatusCode.OK, apiResponse);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var http = new HttpClient(handler);
        var ingestor = new SonarQubeIngestor(
            new SonarQubeConfig
            {
                Enabled = true,
                HostUrl = "https://sonarqube.example.com",
                Token = "squ-token"
            },
            http);

        var result = await ingestor.IngestFindingsAsync("acme/app");
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().Be("API-SQ-001");
        list[0].Severity.Should().Be("MAJOR");
        list[0].Type.Should().Be("VULNERABILITY");
        list[0].Source.Should().Be("SonarQube");
        list[0].LineNumber.Should().Be(10);

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Contain("/api/issues/search");
        handler.Requests[0].RequestUri!.Query.Should().Contain("componentKeys=acme-app");
    }

    [Fact]
    public async Task IngestFindingsAsync_EmptyIssues_ShouldReturnEmpty()
    {
        var path = Path.Combine(_tempDir, "sonar-empty.json");
        var report = new { issues = Array.Empty<object>() };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report));

        var ingestor = new SonarQubeIngestor(new SonarQubeConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_DeserializationError_ShouldReturnEmpty()
    {
        var path = Path.Combine(_tempDir, "sonar-bad.json");
        await File.WriteAllTextAsync(path, "not json at all");

        var ingestor = new SonarQubeIngestor(new SonarQubeConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_NonNumericLine_ShouldNotParse()
    {
        var path = Path.Combine(_tempDir, "sonar-badline.json");
        var report = new
        {
            issues = new[]
            {
                new
                {
                    key = "K-1",
                    severity = "INFO",
                    type = "HOTSPOT",
                    message = "Security hotspot",
                    component = "src/auth.cs",
                    line = "not-a-number",
                    analysisKey = "ak"
                }
            }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report));

        var ingestor = new SonarQubeIngestor(new SonarQubeConfig { Enabled = true });
        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].LineNumber.Should().BeNull();
    }

    [Fact]
    public async Task IngestFindingsAsync_FilePreferredOverApi_WhenFileExists()
    {
        // Create a file with different content to prove file-based path is used
        var path = Path.Combine(_tempDir, "sonar-file.json");
        var fileReport = new
        {
            issues = new[]
            {
                new
                {
                    key = "FROM-FILE",
                    severity = "CRITICAL",
                    type = "BUG",
                    message = "From file",
                    component = "file.cs",
                    line = "1",
                    analysisKey = "file-key"
                }
            }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(fileReport));

        // Also configure an API URL — the file should take priority
        var handler = new CapturingHandler(_ => TestHelpers.JsonResponse(HttpStatusCode.OK, new { issues = new[] { new { key = "FROM-API", severity = "LOW", type = "BUG", message = "api", component = "api.cs", line = "99", analysisKey = "ak" } } }));
        using var http = new HttpClient(handler);
        var ingestor = new SonarQubeIngestor(
            new SonarQubeConfig { Enabled = true, HostUrl = "https://sonar.example.com" },
            http);

        var result = await ingestor.IngestFindingsAsync("acme/app", path);
        var list = result.ToList();

        list.Should().HaveCount(1);
        list[0].Id.Should().Be("FROM-FILE", "file-based ingestion should take priority");

        // API should not have been called
        handler.Requests.Should().BeEmpty();
    }


}
