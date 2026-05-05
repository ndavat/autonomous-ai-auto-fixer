using AutoFixer.Models;

namespace AutoFixer.LLM;

public interface ILlmClient
{
    /// <summary>
    /// Generate a code patch for the given finding. The prompt is built based on providing
    /// the finding’s details and any context required.
    /// </summary>
    Task<string> GenerateFixAsync(Finding finding, CancellationToken cancellationToken = default);
}
