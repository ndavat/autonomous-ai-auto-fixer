namespace AutoFixer.VCS;

public interface IVcsClient
{
    /// <summary>
    /// Create a branch, commit the given files, and open a pull request targeting <see cref="PullRequestRequest.BaseBranch"/>.
    /// </summary>
    /// <returns>The URL of the created pull request, or <c>null</c> if creation was skipped or failed.</returns>
    Task<string?> CreatePullRequestAsync(PullRequestRequest request, CancellationToken cancellationToken = default);
}
