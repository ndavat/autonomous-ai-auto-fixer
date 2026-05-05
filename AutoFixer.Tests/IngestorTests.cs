using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.Models;
using FluentAssertions;

namespace AutoFixer.Tests;

public class IngestorTests
{
    public IngestorTests()
    {
        // Ensure Serilog is initialized before ingestors try to GetLogger.
        LoggerSetup.Initialize();
    }

    [Fact]
    public async Task MendIngestor_WhenDisabled_ShouldReturnEmpty()
    {
        var ingestor = new MendIngestor(new MendConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SonarQubeIngestor_WhenDisabled_ShouldReturnEmpty()
    {
        var ingestor = new SonarQubeIngestor(new SonarQubeConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TrivyIngestor_WhenDisabled_ShouldReturnEmpty()
    {
        var ingestor = new TrivyIngestor(new TrivyConfig { Enabled = false });

        var result = await ingestor.IngestFindingsAsync("acme/app");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TrivyIngestor_WithInputFile_ShouldParseVulnerabilities()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"trivy-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, "{\n  \"Results\": [\n    {\n      \"Target\": \"package.json\",\n      \"Vulnerabilities\": [\n        {\n          \"VulnerabilityID\": \"CVE-2024-0001\",\n          \"Title\": \"Test vuln\",\n          \"Description\": \"Test description\",\n          \"Severity\": \"HIGH\",\n          \"PkgName\": \"lodash\",\n          \"InstalledVersion\": \"4.17.20\",\n          \"FixedVersion\": \"4.17.21\"\n        }\n      ]\n    }\n  ]\n}\n");

        try
        {
            var ingestor = new TrivyIngestor(new TrivyConfig { Enabled = true });
            var result = (await ingestor.IngestFindingsAsync("acme/app", tempPath)).ToList();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be("CVE-2024-0001");
            result[0].Source.Should().Be("Trivy");
            result[0].Severity.Should().Be("HIGH");
            result[0].FilePath.Should().Be("package.json");
            result[0].Metadata["package"].Should().Be("lodash");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task TrivyIngestor_WithMissingFile_ShouldReturnEmpty()
    {
        var ingestor = new TrivyIngestor(new TrivyConfig { Enabled = true });

        var result = await ingestor.IngestFindingsAsync("acme/app", "non-existent-file.json");

        result.Should().BeEmpty();
    }
}
