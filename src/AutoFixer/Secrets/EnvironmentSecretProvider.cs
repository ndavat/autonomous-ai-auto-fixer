namespace AutoFixer.Secrets;

/// <summary>
/// Reads secrets from environment variables.
/// </summary>
public class EnvironmentSecretProvider : ISecretProvider
{
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(Environment.GetEnvironmentVariable(name));
    }
}
