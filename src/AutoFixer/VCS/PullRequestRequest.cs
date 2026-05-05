namespace AutoFixer.VCS;

/// <summary>
/// A single file to be written (created or updated) as part of a PR commit.
/// </summary>
public record FileChange(string Path, string Content);

/// <summary>
/// Full description of a pull request to be created by an <see cref="IVcsClient"/>.
/// </summary>
/// <param name="RepoFullName">"owner/repo" (GitHub) or equivalent identifier.</param>
/// <param name="BaseBranch">The branch the PR targets (e.g. "main").</param>
/// <param name="HeadBranch">The new branch that will hold the commits.</param>
/// <param name="Title">PR title.</param>
/// <param name="Description">PR body / markdown description.</param>
/// <param name="Files">Files to write on the head branch before opening the PR.</param>
public record PullRequestRequest(
    string RepoFullName,
    string BaseBranch,
    string HeadBranch,
    string Title,
    string Description,
    IReadOnlyList<FileChange> Files);
