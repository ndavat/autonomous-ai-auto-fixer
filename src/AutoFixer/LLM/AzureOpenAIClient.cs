using AutoFixer.Audit;
using AutoFixer.Models;
using AutoFixer.Secrets;
using Serilog;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AutoFixer.LLM;

/// <summary>
/// Azure OpenAI chat-completions client. Calls the deployment's
/// <c>/openai/deployments/{deployment}/chat/completions</c> endpoint.
///
/// Authenticates with an API key taken from <see cref="LlmConfig.ApiKey"/> or the
/// <c>AZURE_OPENAI_API_KEY</c> / <c>OPENAI_API_KEY</c> environment variables. If neither an
/// endpoint nor a key is configured the client falls back to a clearly-labelled stub
/// response so that the pipeline can still be exercised end-to-end (e.g. in dry-run mode).
/// </summary>
public class AzureOpenAIClient : ILlmClient, IDisposable
{
    private const string ApiVersion = "2024-06-01";
    private const string SystemPrompt =
        "You are an expert secure-coding assistant. Given a static analysis finding, " +
        "produce a minimal, correct code patch that remediates the issue. " +
        "Respond with ONLY the patched file contents (no markdown fences, no explanatory prose). " +
        "Preserve the file's original formatting, imports and surrounding logic wherever possible.";

    private readonly LlmConfig _config;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ISecretProvider? _secretProvider;
    private readonly string? _endpoint;
    private string? _resolvedApiKey;

    public AzureOpenAIClient(LlmConfig? config = null, ISecretProvider? secretProvider = null, HttpClient? httpClient = null)
    {
        _config = config ?? new LlmConfig();
        _logger = LoggerSetup.GetLogger(nameof(AzureOpenAIClient));
        _secretProvider = secretProvider;

        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(30, _config.TimeoutSeconds))
        };

        _endpoint = !string.IsNullOrWhiteSpace(_config.Endpoint)
            ? _config.Endpoint!.TrimEnd('/')
            : Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")?.TrimEnd('/');
    }

    public async Task<string> GenerateFixAsync(Finding finding, CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(apiKey))
        {
            _logger.Warning(
                "Azure OpenAI is not fully configured (endpoint/api-key missing). Returning stub patch for finding {Id}.",
                finding.Id);
            return BuildStubPatch(finding);
        }

        var userPrompt = BuildUserPrompt(finding);
        var url = $"{_endpoint}/openai/deployments/{Uri.EscapeDataString(_config.ModelName)}/chat/completions?api-version={ApiVersion}";

        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userPrompt },
            },
            temperature = _config.Temperature,
            max_tokens = _config.MaxTokens,
        };

        _logger.Information(
            "Calling Azure OpenAI deployment {Model} for finding {Id} ({Title})",
            _config.ModelName, finding.Id, finding.Title);

        var attempt = 0;
        var maxAttempts = Math.Max(1, 3);
        while (true)
        {
            attempt++;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(payload),
                };
                request.Headers.Add("api-key", apiKey);
                request.Headers.Accept.ParseAdd("application/json");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warning(
                        "Azure OpenAI returned {Status} on attempt {Attempt}/{Max}: {Body}",
                        (int)response.StatusCode, attempt, maxAttempts, Truncate(body, 500));

                    if (IsTransient(response.StatusCode) && attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                        continue;
                    }

                    _logger.Error("Azure OpenAI request failed after {Attempt} attempts for finding {Id}.", attempt, finding.Id);
                    return string.Empty;
                }

                return ExtractContent(body, finding);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.Warning(ex, "Azure OpenAI call failed on attempt {Attempt}/{Max}; retrying.", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Azure OpenAI call failed irrecoverably for finding {Id}.", finding.Id);
                return string.Empty;
            }
        }
    }

    private static string BuildUserPrompt(Finding finding)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Finding ID: {finding.Id}");
        sb.AppendLine($"Source: {finding.Source}");
        sb.AppendLine($"Severity: {finding.Severity}");
        sb.AppendLine($"Type: {finding.Type}");
        sb.AppendLine($"Title: {finding.Title}");
        if (!string.IsNullOrWhiteSpace(finding.Description))
        {
            sb.AppendLine();
            sb.AppendLine("Description:");
            sb.AppendLine(finding.Description);
        }
        if (!string.IsNullOrWhiteSpace(finding.FilePath))
        {
            sb.AppendLine();
            sb.Append("File: ").Append(finding.FilePath);
            if (finding.LineNumber.HasValue) sb.Append(" (line ").Append(finding.LineNumber.Value).Append(')');
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(finding.CodeSnippet))
        {
            sb.AppendLine();
            sb.AppendLine("Current code:");
            sb.AppendLine("```");
            sb.AppendLine(finding.CodeSnippet);
            sb.AppendLine("```");
        }
        sb.AppendLine();
        sb.AppendLine("Return the full patched file contents that fix the finding. Do not include commentary.");
        return sb.ToString();
    }

    private string ExtractContent(string responseBody, Finding finding)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                _logger.Warning("Azure OpenAI response had no choices for finding {Id}.", finding.Id);
                return string.Empty;
            }

            var message = choices[0].GetProperty("message");
            var content = message.TryGetProperty("content", out var c) ? c.GetString() : null;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.Warning("Azure OpenAI returned empty content for finding {Id}.", finding.Id);
                return string.Empty;
            }
            return StripMarkdownFences(content.Trim());
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to parse Azure OpenAI response for finding {Id}.", finding.Id);
            return string.Empty;
        }
    }

    private static string StripMarkdownFences(string content)
    {
        // If the model wrapped the reply in ``` fences, strip the outer fence.
        if (!content.StartsWith("```")) return content;

        var firstNewline = content.IndexOf('\n');
        if (firstNewline < 0) return content;
        var stripped = content[(firstNewline + 1)..];
        if (stripped.EndsWith("```"))
        {
            stripped = stripped[..^3].TrimEnd();
        }
        return stripped;
    }

    private static bool IsTransient(System.Net.HttpStatusCode code) =>
        code == System.Net.HttpStatusCode.RequestTimeout ||
        code == System.Net.HttpStatusCode.TooManyRequests ||
        (int)code >= 500;

    private async Task<string?> ResolveApiKeyAsync(CancellationToken ct)
    {
        if (_resolvedApiKey != null) return _resolvedApiKey;

        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _resolvedApiKey = _config.ApiKey;
            return _resolvedApiKey;
        }

        if (_secretProvider != null)
        {
            var secret = await _secretProvider.GetSecretAsync("AZURE_OPENAI_API_KEY", ct)
                        ?? await _secretProvider.GetSecretAsync("OPENAI_API_KEY", ct);
            if (!string.IsNullOrEmpty(secret))
            {
                _resolvedApiKey = secret;
                return _resolvedApiKey;
            }
        }

        _resolvedApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
                       ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return _resolvedApiKey;
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "…";

    private static string BuildStubPatch(Finding finding) =>
        $"// AUTOFIX STUB - Azure OpenAI not configured.\n" +
        $"// Finding: {finding.Id} ({finding.Severity})\n" +
        $"// Title: {finding.Title}\n" +
        $"// Configure LlmConfig.Endpoint and LlmConfig.ApiKey (or AZURE_OPENAI_ENDPOINT / AZURE_OPENAI_API_KEY)\n" +
        $"// to enable real AI-generated fixes.\n";

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
