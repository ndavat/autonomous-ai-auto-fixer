using AutoFixer.Models;
using FluentAssertions;
using Xunit;

namespace AutoFixer.Tests;

public class RemediationPlanTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var plan = new RemediationPlan
        {
            IssueId = "test-123",
            Severity = "High",
            Description = "Test vulnerability",
            SuggestedFix = "Apply patch",
            ConfidenceScore = 0.95m,
            AffectedFiles = new List<string> { "file1.cs", "file2.cs" }
        };

        // Assert
        plan.IssueId.Should().Be("test-123");
        plan.Severity.Should().Be("High");
        plan.Description.Should().Be("Test vulnerability");
        plan.SuggestedFix.Should().Be("Apply patch");
        plan.ConfidenceScore.Should().Be(0.95m);
        plan.AffectedFiles.Should().HaveCount(2);
    }

    [Fact]
    public void AffectedFiles_ShouldBeEmptyByDefault()
    {
        // Arrange & Act
        var plan = new RemediationPlan();

        // Assert
        plan.AffectedFiles.Should().NotBeNull();
        plan.AffectedFiles.Should().BeEmpty();
    }
}
