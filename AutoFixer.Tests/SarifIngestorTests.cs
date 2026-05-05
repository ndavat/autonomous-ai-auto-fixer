using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.Models;
using FluentAssertions;

namespace AutoFixer.Tests;

public class SarifIngestorTests
{
    public SarifIngestorTests()
    {
        LoggerSetup.Initialize();
    }

    [Fact]
    public async Task IngestFindingsAsync_WithoutInputFile_ShouldReturnEmpty()
    {
        var ingestor = new SarifIngestor();

        var result = await ingestor.IngestFindingsAsync("acme/app", inputFilePath: null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_MissingFile_ShouldReturnEmpty()
    {
        var ingestor = new SarifIngestor();

        var result = await ingestor.IngestFindingsAsync("acme/app", "does-not-exist.sarif");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestFindingsAsync_ValidFile_ShouldParseFindings()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sarif-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, "{\n  \"runs\": [\n    {\n      \"tool\": {\n        \"driver\": {\n          \"name\": \"Semgrep\"\n        }\n      },\n      \"results\": [\n        {\n          \"ruleId\": \"csharp.sql-injection\",\n          \"level\": \"error\",\n          \"kind\": \"fail\",\n          \"message\": {\n            \"text\": \"Possible SQL injection vulnerability\"\n          },\n          \"locations\": [\n            {\n              \"physicalLocation\": {\n                \"artifactLocation\": {\n                  \"uri\": \"src/Data/Repository.cs\"\n                },\n                \"region\": {\n                  \"startLine\": 42\n                }\n              }\n            }\n          ]\n        }\n      ]\n    }\n  ]\n}\n");

        try
        {
            var ingestor = new SarifIngestor(new SarifConfig { Enabled = true });
            var result = (await ingestor.IngestFindingsAsync("acme/app", tempPath)).ToList();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be("csharp.sql-injection");
            result[0].Source.Should().Be("Semgrep");
            result[0].Severity.Should().Be("High"); // error -> High
            result[0].Type.Should().Be("fail");
            result[0].Title.Should().Be("Possible SQL injection vulnerability");
            result[0].FilePath.Should().Be("src/Data/Repository.cs");
            result[0].LineNumber.Should().Be(42);
            result[0].Metadata["ruleId"].Should().Be("csharp.sql-injection");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task IngestFindingsAsync_MultipleRuns_ShouldAggregateAllResults()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sarif-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, "{\n  \"runs\": [\n    {\n      \"tool\": { \"driver\": { \"name\": \"ToolA\" } },\n      \"results\": [\n        { \"ruleId\": \"rule-1\", \"level\": \"warning\", \"message\": { \"text\": \"First issue\" } }\n      ]\n    },\n    {\n      \"tool\": { \"driver\": { \"name\": \"ToolB\" } },\n      \"results\": [\n        { \"ruleId\": \"rule-2\", \"level\": \"note\", \"message\": { \"text\": \"Second issue\" } }\n      ]\n    }\n  ]\n}\n");

        try
        {
            var ingestor = new SarifIngestor(new SarifConfig { Enabled = true });
            var result = (await ingestor.IngestFindingsAsync("acme/app", tempPath)).ToList();

            result.Should().HaveCount(2);
            result[0].Source.Should().Be("ToolA");
            result[0].Severity.Should().Be("Medium"); // warning -> Medium
            result[1].Source.Should().Be("ToolB");
            result[1].Severity.Should().Be("Low");    // note -> Low
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task IngestFindingsAsync_EmptyRuns_ShouldReturnEmpty()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sarif-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, "{ \"runs\": [] }");

        try
        {
            var ingestor = new SarifIngestor(new SarifConfig { Enabled = true });
            var result = await ingestor.IngestFindingsAsync("acme/app", tempPath);

            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
