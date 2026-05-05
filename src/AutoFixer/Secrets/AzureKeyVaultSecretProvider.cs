using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Serilog;

namespace AutoFixer.Secrets;

/// <summary>
/// Resolves secrets from an Azure Key Vault using <see cref="DefaultAzureCredential"/>.
/// </summary>
public class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient _client;
    private readonly ILogger _logger;

    public AzureKeyVaultSecretProvider(string vaultUrl)
    {
        _client = new SecretClient(new Uri(vaultUrl), new DefaultAzureCredential());
        _logger = Audit.LoggerSetup.GetLogger(nameof(AzureKeyVaultSecretProvider));
    }

    internal AzureKeyVaultSecretProvider(SecretClient client)
    {
        _client = client;
        _logger = Audit.LoggerSetup.GetLogger(nameof(AzureKeyVaultSecretProvider));
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetSecretAsync(name, cancellationToken: cancellationToken);
            return response.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.Debug("Secret {SecretName} not found in Key Vault.", name);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve secret {SecretName} from Key Vault.", name);
            return null;
        }
    }
}
