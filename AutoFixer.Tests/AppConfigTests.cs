using AutoFixer.Models;
using FluentAssertions;
using Xunit;

namespace AutoFixer.Tests;

public class AppConfigTests
{
    [Fact]
    public void Constructor_ShouldInitializeDefaults()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        config.TargetRepos.Should().NotBeNull();
        config.ScanSettings.Should().NotBeNull();
        config.RemediationSettings.Should().NotBeNull();
        config.AuditSettings.Should().NotBeNull();
    }

    [Fact]
    public void ScanSettings_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        config.ScanSettings.Should().NotBeNull();
    }

    [Fact]
    public void RemediationSettings_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        config.RemediationSettings.Should().NotBeNull();
        config.RemediationSettings.AutoApply.Should().BeFalse();
        config.RemediationSettings.DryRun.Should().BeTrue();
    }
}
