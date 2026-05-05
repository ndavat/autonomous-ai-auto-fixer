using AutoFixer.Models;
using FluentAssertions;

namespace AutoFixer.Tests;

public class FindingTests
{
    [Fact]
    public void DefaultConstructor_ShouldInitializeWithEmptyStrings()
    {
        var finding = new Finding();

        finding.Id.Should().BeEmpty();
        finding.Source.Should().BeEmpty();
        finding.Severity.Should().BeEmpty();
        finding.Type.Should().BeEmpty();
        finding.Title.Should().BeEmpty();
        finding.Description.Should().BeEmpty();
        finding.FilePath.Should().BeNull();
        finding.LineNumber.Should().BeNull();
        finding.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void PropertyAssignment_ShouldRoundTrip()
    {
        var finding = new Finding
        {
            Id = "cve-2024-1234",
            Source = "Mend",
            Severity = "High",
            Type = "Vulnerability",
            Title = "Log4Shell",
            Description = "Remote code execution via JNDI.",
            FilePath = "pom.xml",
            LineNumber = 42,
            CodeSnippet = "<version>2.14.0</version>",
            Metadata = { ["cve"] = "CVE-2021-44228" }
        };

        finding.Id.Should().Be("cve-2024-1234");
        finding.Source.Should().Be("Mend");
        finding.LineNumber.Should().Be(42);
        finding.Metadata["cve"].Should().Be("CVE-2021-44228");
    }
}
