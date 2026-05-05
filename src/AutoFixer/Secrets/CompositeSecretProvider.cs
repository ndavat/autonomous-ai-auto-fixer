namespace AutoFixer.Secrets;

/// <summary>
/// Tries each provider in order and returns the first non-null secret value found.
/// </summary>
public class CompositeSecretProvider : ISecretProvider
{
    private readonly List<ISecretProvider> _providers;

    public CompositeSecretProvider(params ISecretProvider[] providers)
    {
        _providers = providers.ToList();
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            var value = await provider.GetSecretAsync(name, cancellationToken);
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }
}
