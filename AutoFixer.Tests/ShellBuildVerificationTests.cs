using AutoFixer.Audit;
using AutoFixer.Models;
using AutoFixer.Validation;
using FluentAssertions;

namespace AutoFixer.Tests;

public class ShellBuildVerificationTests : IDisposable
{
    private readonly string _tempDir;

    public ShellBuildVerificationTests()
    {
        LoggerSetup.Initialize();
        _tempDir = Path.Combine(Path.GetTempPath(), $"autofix-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task VerifyBuildAsync_NoCommands_ShouldReturnTrue()
    {
        var service = new ShellBuildVerificationService(Array.Empty<string>(), _tempDir);
        var finding = new Finding { Id = "F-1", FilePath = "foo.cs" };

        var result = await service.VerifyBuildAsync(finding, "patch");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyBuildAsync_NoFilePath_ShouldReturnTrue()
    {
        var service = new ShellBuildVerificationService(new[] { "echo ok" }, _tempDir);
        var finding = new Finding { Id = "F-2", FilePath = null };

        var result = await service.VerifyBuildAsync(finding, "patch");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyBuildAsync_SinglePassingCommand_ShouldReturnTrueAndWritePatch()
    {
        var fileName = $"test-{Guid.NewGuid():N}.txt";
        var service = new ShellBuildVerificationService(new[] { "echo build-ok" }, _tempDir);
        var finding = new Finding { Id = "F-3", FilePath = fileName };
        var original = "original content";
        var patch = "patched content";

        var filePath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(filePath, original);

        var result = await service.VerifyBuildAsync(finding, patch);

        result.Should().BeTrue();
        // File should be restored to original after verification
        var restored = await File.ReadAllTextAsync(filePath);
        restored.Should().Be(original);
    }

    [Fact]
    public async Task VerifyBuildAsync_FailingCommand_ShouldReturnFalseAndRestoreOriginal()
    {
        var fileName = $"test-{Guid.NewGuid():N}.txt";
        var service = new ShellBuildVerificationService(new[] { "exit 1" }, _tempDir);
        var finding = new Finding { Id = "F-4", FilePath = fileName };
        var original = "original";
        var patch = "patched";

        var filePath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(filePath, original);

        var result = await service.VerifyBuildAsync(finding, patch);

        result.Should().BeFalse();
        var restored = await File.ReadAllTextAsync(filePath);
        restored.Should().Be(original);
    }

    [Fact]
    public async Task VerifyBuildAsync_NewFile_ShouldDeleteAfterVerification()
    {
        var fileName = $"new-{Guid.NewGuid():N}.txt";
        var service = new ShellBuildVerificationService(new[] { "echo ok" }, _tempDir);
        var finding = new Finding { Id = "F-5", FilePath = fileName };
        var patch = "new content";

        var filePath = Path.Combine(_tempDir, fileName);
        File.Exists(filePath).Should().BeFalse();

        var result = await service.VerifyBuildAsync(finding, patch);

        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse("temporary file should be cleaned up");
    }

    [Fact]
    public async Task VerifyBuildAsync_SecondCommandFails_ShouldReturnFalse()
    {
        var fileName = $"test-{Guid.NewGuid():N}.txt";
        var service = new ShellBuildVerificationService(new[] { "echo first", "exit 1" }, _tempDir);
        var finding = new Finding { Id = "F-6", FilePath = fileName };
        var original = "original";

        var filePath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(filePath, original);

        var result = await service.VerifyBuildAsync(finding, "patch");

        result.Should().BeFalse();
        var restored = await File.ReadAllTextAsync(filePath);
        restored.Should().Be(original);
    }

    [Fact]
    public async Task VerifyBuildAsync_NestedDirectory_ShouldCreateAndCleanUp()
    {
        var fileName = $"nested/dir/test-{Guid.NewGuid():N}.txt";
        var service = new ShellBuildVerificationService(new[] { "echo ok" }, _tempDir);
        var finding = new Finding { Id = "F-7", FilePath = fileName };
        var patch = "nested patch";

        var filePath = Path.Combine(_tempDir, fileName);
        var dir = Path.GetDirectoryName(filePath)!;
        Directory.Exists(dir).Should().BeFalse();

        var result = await service.VerifyBuildAsync(finding, patch);

        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyBuildAsync_AbsolutePathEscapingWorkingDir_ShouldSkipAndReturnTrue()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), $"autofix-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "escape.txt");
        await File.WriteAllTextAsync(outsideFile, "outside content");

        try
        {
            var service = new ShellBuildVerificationService(new[] { "echo ok" }, _tempDir);
            // Pass an absolute path that is outside _tempDir
            var finding = new Finding { Id = "F-8", FilePath = outsideFile };

            var result = await service.VerifyBuildAsync(finding, "malicious patch");

            result.Should().BeTrue("should skip for safety when path escapes working directory");
            // Ensure the outside file was NOT modified
            var content = await File.ReadAllTextAsync(outsideFile);
            content.Should().Be("outside content");
        }
        finally
        {
            try { Directory.Delete(outsideDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
