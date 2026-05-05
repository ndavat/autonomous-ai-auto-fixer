using AutoFixer.Audit;
using AutoFixer.Secrets;
using Azure;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Moq;

namespace AutoFixer.Tests;

public class SecretProviderTests : IDisposable
{
    private readonly string _envVarName;

    public SecretProviderTests()
    {
        LoggerSetup.Initialize();
        _envVarName = $"AUTOFIXER_TEST_SECRET_{Guid.NewGuid():N}";
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_envVarName, null);
    }

    [Fact]
    public async Task EnvironmentSecretProvider_ExistingVar_ShouldReturnValue()
    {
        Environment.SetEnvironmentVariable(_envVarName, "secret-value");
        var provider = new EnvironmentSecretProvider();

        var result = await provider.GetSecretAsync(_envVarName);

        result.Should().Be("secret-value");
    }

    [Fact]
    public async Task EnvironmentSecretProvider_MissingVar_ShouldReturnNull()
    {
        var provider = new EnvironmentSecretProvider();

        var result = await provider.GetSecretAsync("DEFINITELY_MISSING_VAR_12345");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CompositeSecretProvider_FirstProviderSucceeds_ShouldReturnValue()
    {
        var envProvider = new EnvironmentSecretProvider();
        var mockKvProvider = new Mock<ISecretProvider>();
        mockKvProvider.Setup(p => p.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var composite = new CompositeSecretProvider(mockKvProvider.Object, envProvider);
        Environment.SetEnvironmentVariable(_envVarName, "env-secret");

        var result = await composite.GetSecretAsync(_envVarName);

        result.Should().Be("env-secret");
        mockKvProvider.Verify(p => p.GetSecretAsync(_envVarName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompositeSecretProvider_FirstProviderFindsSecret_ShouldNotQuerySecond()
    {
        var mockFirst = new Mock<ISecretProvider>();
        mockFirst.Setup(p => p.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("from-first");
        var mockSecond = new Mock<ISecretProvider>();

        var composite = new CompositeSecretProvider(mockFirst.Object, mockSecond.Object);

        var result = await composite.GetSecretAsync(_envVarName);

        result.Should().Be("from-first");
        mockSecond.Verify(p => p.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompositeSecretProvider_AllProvidersMiss_ShouldReturnNull()
    {
        var mockFirst = new Mock<ISecretProvider>();
        mockFirst.Setup(p => p.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var mockSecond = new Mock<ISecretProvider>();
        mockSecond.Setup(p => p.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var composite = new CompositeSecretProvider(mockFirst.Object, mockSecond.Object);

        var result = await composite.GetSecretAsync(_envVarName);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AzureKeyVaultSecretProvider_FoundSecret_ShouldReturnValue()
    {
        var mockClient = new Mock<SecretClient>();
        var secretResponse = Response.FromValue(
            new KeyVaultSecret("my-secret", "kv-value"),
            Mock.Of<Response>());
        mockClient.Setup(c => c.GetSecretAsync("my-secret", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secretResponse);

        var provider = new AzureKeyVaultSecretProvider(mockClient.Object);
        var result = await provider.GetSecretAsync("my-secret");

        result.Should().Be("kv-value");
    }

    [Fact]
    public async Task AzureKeyVaultSecretProvider_NotFound_ShouldReturnNull()
    {
        var mockClient = new Mock<SecretClient>();
        var requestFailed = new RequestFailedException(404, "Secret not found");
        mockClient.Setup(c => c.GetSecretAsync("missing-secret", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(requestFailed);

        var provider = new AzureKeyVaultSecretProvider(mockClient.Object);
        var result = await provider.GetSecretAsync("missing-secret");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AzureKeyVaultSecretProvider_OtherException_ShouldReturnNull()
    {
        var mockClient = new Mock<SecretClient>();
        mockClient.Setup(c => c.GetSecretAsync("bad-secret", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var provider = new AzureKeyVaultSecretProvider(mockClient.Object);
        var result = await provider.GetSecretAsync("bad-secret");

        result.Should().BeNull();
    }
}
