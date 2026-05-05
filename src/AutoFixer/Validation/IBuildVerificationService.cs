using AutoFixer.Models;

namespace AutoFixer.Validation;

/// <summary>
/// Verifies that applying a proposed fix does not break the project build.
/// </summary>
public interface IBuildVerificationService
{
    /// <summary>
    /// Apply <paramref name="proposedPatch"/> to the file identified by <paramref name="finding.FilePath"/>,
    /// run the configured build commands, and restore the original file afterwards.
    /// </summary>
    /// <returns><c>true</c> if all build commands return exit code 0.</returns>
    Task<bool> VerifyBuildAsync(Finding finding, string proposedPatch, CancellationToken cancellationToken = default);
}
