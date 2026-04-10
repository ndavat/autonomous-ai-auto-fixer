using AutoFixer.Models;
using FluentAssertions;
using Xunit;

namespace AutoFixer.Tests;

public class SecretPatternTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var pattern = new SecretPattern
        {
            Name = "AWS Access Key",
            Pattern = @"AKIA[0-9A-Z]{16}",
            Severity = "Critical"
        };

        // Assert
        pattern.Name.Should().Be("AWS Access Key");
        pattern.Pattern.Should().Be(@"AKIA[0-9A-Z]{16}");
        pattern.Severity.Should().Be("Critical");
    }

    [Fact]
    public void PatternList_ShouldContainMultiplePatterns()
    {
        // Arrange
        var patterns = new List<SecretPattern>
        {
            new SecretPattern { Name = "AWS Key", Pattern = @"AKIA.*", Severity = "Critical" },
            new SecretPattern { Name = "GitHub Token", Pattern = @"ghp_.*", Severity = "High" },
            new SecretPattern { Name = "Generic API Key", Pattern = @"api_key.*", Severity = "Medium" }
        };

        // Act & Assert
        patterns.Should().HaveCount(3);
        patterns.Should().Contain(p => p.Name == "AWS Key");
        patterns.Should().Contain(p => p.Severity == "High");
    }
}
