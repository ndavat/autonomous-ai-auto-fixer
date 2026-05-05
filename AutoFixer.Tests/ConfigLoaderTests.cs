using AutoFixer;
using AutoFixer.Models;
using FluentAssertions;

namespace AutoFixer.Tests;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempYaml;

    public ConfigLoaderTests()
    {
        _tempYaml = Path.Combine(Path.GetTempPath(), $"autofixer-test-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(_tempYaml, "agent:\n  mode: Fix\n  baseBranch: develop\n  maxFindingsPerRepo: 5\ningestion:\n  mend:\n    enabled: true\n    apiUrl: https://mend.example.com\nllm:\n  provider: azure-openai\n  modelName: gpt-4o\nvcs:\n  provider: github\n  token: test-token\n");
    }

    [Fact]
    public void LoadConfig_ShouldBindYamlToAppConfig()
    {
        var config = ConfigLoader.LoadConfig(_tempYaml);

        config.Agent.Mode.Should().Be(AgentMode.Fix);
        config.Agent.BaseBranch.Should().Be("develop");
        config.Agent.MaxFindingsPerRepo.Should().Be(5);
        config.Ingestion.Mend.Enabled.Should().BeTrue();
        config.Ingestion.Mend.ApiUrl.Should().Be("https://mend.example.com");
        config.Llm.ModelName.Should().Be("gpt-4o");
        config.Vcs.Token.Should().Be("test-token");
    }

    [Fact]
    public void LoadConfig_MissingFile_ShouldStillReturnDefaults()
    {
        var config = ConfigLoader.LoadConfig("does-not-exist.yaml");

        config.Should().NotBeNull();
        config.Agent.Mode.Should().Be(AgentMode.DryRun);
        config.Vcs.Provider.Should().Be("github");
    }

    public void Dispose()
    {
        if (File.Exists(_tempYaml))
        {
            File.Delete(_tempYaml);
        }
    }
}
