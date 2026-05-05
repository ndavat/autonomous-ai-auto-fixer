using AutoFixer.Models;
using FluentAssertions;

namespace AutoFixer.Tests;

public class AppConfigTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllSections()
    {
        var config = new AppConfig();

        config.Agent.Should().NotBeNull();
        config.Ingestion.Should().NotBeNull();
        config.Remediation.Should().NotBeNull();
        config.Llm.Should().NotBeNull();
        config.Vcs.Should().NotBeNull();
    }

    [Fact]
    public void Agent_ShouldHaveSafeDefaults()
    {
        var config = new AppConfig();

        config.Agent.Mode.Should().Be(AgentMode.DryRun);
        config.Agent.BaseBranch.Should().Be("main");
        config.Agent.MaxFindingsPerRepo.Should().Be(10);
        config.Agent.EnableSelfCorrection.Should().BeTrue();
        config.Agent.Repositories.Should().BeEmpty();
    }

    [Fact]
    public void Remediation_ShouldHaveValidationEnabledByDefault()
    {
        var config = new AppConfig();

        config.Remediation.EnableValidation.Should().BeTrue();
        config.Remediation.ValidationRetries.Should().Be(1);
        config.Remediation.MaxRetries.Should().Be(3);
        config.Remediation.Linters.Should().BeEmpty();
        config.Remediation.BuildVerificationCommands.Should().BeEmpty();
    }

    [Fact]
    public void Llm_ShouldDefaultToAzureOpenAI()
    {
        var config = new AppConfig();

        config.Llm.Provider.Should().Be("azure-openai");
        config.Llm.ModelName.Should().Be("gpt-4");
        config.Llm.Temperature.Should().Be(0.0);
        config.Llm.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void Vcs_ShouldDefaultToGitHub()
    {
        var config = new AppConfig();

        config.Vcs.Provider.Should().Be("github");
        config.Vcs.Token.Should().BeNull();
    }

    [Fact]
    public void Ingestion_AllSourcesShouldBeDisabledByDefault()
    {
        var config = new AppConfig();

        config.Ingestion.Mend.Enabled.Should().BeFalse();
        config.Ingestion.SonarQube.Enabled.Should().BeFalse();
        config.Ingestion.Trivy.Enabled.Should().BeFalse();
        config.Ingestion.Csv.Enabled.Should().BeFalse();
        config.Ingestion.Excel.Enabled.Should().BeFalse();
        config.Ingestion.Pdf.Enabled.Should().BeFalse();
        config.Ingestion.Sarif.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Secrets_ShouldDefaultToEnvironment()
    {
        var config = new AppConfig();

        config.Secrets.Should().NotBeNull();
        config.Secrets.Source.Should().Be("environment");
        config.Secrets.KeyVaultUrl.Should().BeNull();
    }
}
