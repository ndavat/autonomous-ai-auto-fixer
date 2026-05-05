namespace AutoFixer.Secrets;

/// <summary>
/// Resolves secrets by name. Implementations may read from environment variables,
/// Azure Key Vault, or other secure secret stores.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Retrieve the secret value for the given name.
    /// </summary>
    /// <returns>The secret value, or <c>null</c> if not found.</returns>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
