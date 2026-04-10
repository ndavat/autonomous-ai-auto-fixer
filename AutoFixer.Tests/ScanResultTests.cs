using AutoFixer.Models;
using FluentAssertions;
using Xunit;

namespace AutoFixer.Tests;

public class ScanResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var result = new ScanResult
        {
            RepositoryUrl = "https://github.com/test/repo",
            CommitSha = "abc123def456",
            BranchName = "main",
            ScanDate = DateTime.UtcNow,
            TotalIssues = 5,
            CriticalIssues = 1,
            HighIssues = 2,
            MediumIssues = 1,
            LowIssues = 1
        };

        // Assert
        result.RepositoryUrl.Should().Be("https://github.com/test/repo");
        result.CommitSha.Should().Be("abc123def456");
        result.BranchName.Should().Be("main");
        result.TotalIssues.Should().Be(5);
        result.CriticalIssues.Should().Be(1);
        result.HighIssues.Should().Be(2);
        result.MediumIssues.Should().Be(1);
        result.LowIssues.Should().Be(1);
    }

    [Fact]
    public void IssuesList_ShouldBeEmptyByDefault()
    {
        // Arrange & Act
        var result = new ScanResult();

        // Assert
        result.Issues.Should().NotBeNull();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void AddIssue_ShouldIncrementCounters()
    {
        // Arrange
        var result = new ScanResult
        {
            Issues = new List<RemediationPlan>()
        };

        var criticalIssue = new RemediationPlan { Severity = "Critical" };
        var highIssue = new RemediationPlan { Severity = "High" };

        // Act
        result.Issues.Add(criticalIssue);
        result.Issues.Add(highIssue);

        // Assert
        result.Issues.Should().HaveCount(2);
    }
}
