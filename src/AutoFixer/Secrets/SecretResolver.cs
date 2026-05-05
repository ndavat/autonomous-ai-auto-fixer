namespace AutoFixer.Secrets;

/// <summary>
/// Unified secret resolution helper. Implements the canonical fallback chain:
///   1. Configured value (e.g. VcsConfig.Token, LlmConfig.ApiKey)
///   2. <see cref="ISecretProvider"/> lookup by env-var name
/// </summary>
public class SecretResolver
{
    private readonly ISecretProvider? _provider;

    public SecretResolver(ISecretProvider? provider = null)
    {
        _provider = provider;
    }

    /// <summary>
    /// Resolve a secret following the fallback chain.
    /// </summary>
    /// <param name="configValue">Value supplied directly in configuration (highest priority).</param>
    /// <param name="envVarName">Environment variable / secret name to query the provider for when <paramref name="configValue"/> is absent.</param>
    /// <returns>The resolved secret, or <c>null</c> if not found anywhere.</returns>
    public async Task<string?> ResolveAsync(
        string? configValue,
        string envVarName,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(configValue))
            return configValue;

        if (_provider != null)
        {
            var secret = await _provider.GetSecretAsync(envVarName, cancellationToken);
            if (!string.IsNullOrEmpty(secret))
                return secret;
        }

        return null;
    }
}
