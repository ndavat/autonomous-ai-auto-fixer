using AutoFixer.Models;
using FluentAssertions;
using Xunit;

namespace AutoFixer.Tests;

public class AuditLogEntryTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var entry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "RemediationApplied",
            Repository = "test/repo",
            IssueId = "issue-123",
            Details = "Applied security patch",
            Success = true
        };

        // Assert
        entry.Action.Should().Be("RemediationApplied");
        entry.Repository.Should().Be("test/repo");
        entry.IssueId.Should().Be("issue-123");
        entry.Details.Should().Be("Applied security patch");
        entry.Success.Should().BeTrue();
    }

    [Fact]
    public void FailedAction_ShouldHaveSuccessFalse()
    {
        // Arrange & Act
        var entry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "RemediationFailed",
            Repository = "test/repo",
            IssueId = "issue-456",
            Details = "Patch application failed",
            Success = false
        };

        // Assert
        entry.Success.Should().BeFalse();
        entry.Action.Should().Be("RemediationFailed");
    }
}
