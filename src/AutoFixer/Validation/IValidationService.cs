using AutoFixer.Models;

namespace AutoFixer.Validation;

public interface IValidationService
{
    /// <summary>
    /// Validate the proposed fix for a given finding.
    /// Returns true if validation passes.
    /// </summary>
    Task<bool> ValidateFixAsync(Finding finding, string proposedPatch, CancellationToken cancellationToken = default);
}
