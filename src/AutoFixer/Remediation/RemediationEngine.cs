using AutoFixer.Audit;
using AutoFixer.Ingestion;
using AutoFixer.LLM;
using AutoFixer.Models;
using AutoFixer.Secrets;
using AutoFixer.Validation;
using AutoFixer.VCS;
using Serilog;

namespace AutoFixer.Remediation;

public class RemediationEngine
{
    private readonly AppConfig _config;
    private readonly ILogger _logger;
    private readonly string? _inputFile;
    private readonly List<IFindingIngestor> _ingestors = new();
    private readonly ILlmClient _llmClient;
    private readonly IVcsClient _vcsClient;
    private readonly IValidationService _validationService;
    private readonly IBuildVerificationService? _buildVerificationService;
    private readonly ISecretProvider _secretProvider;

    public RemediationEngine(AppConfig config, string? inputFile = null)
    {
        _config = config;
        _inputFile = inputFile;
        _logger = LoggerSetup.GetLogger(nameof(RemediationEngine));

        // Initialize secret provider
        _secretProvider = CreateSecretProvider();

        // Initialize ingestors based on configuration
        InitializeIngestors();

        // Initialize LLM client
        _llmClient = new AzureOpenAIClient(_config.Llm, _secretProvider);

        // Initialize VCS client based on provider
        _vcsClient = _config.Vcs.Provider.Equals("github", StringComparison.OrdinalIgnoreCase)
            ? new GitHubClient(_config.Vcs, _secretProvider)
            : _config.Vcs.Provider.Equals("azure-devops", StringComparison.OrdinalIgnoreCase)
                ? new AzureDevOpsClient(_config.Vcs, _secretProvider)
                : throw new NotSupportedException($"VCS provider '{_config.Vcs.Provider}' is not supported yet.");

        // Initialize validation service
        _validationService = _config.Remediation.EnableValidation
            ? new SimpleValidationService(_config.Remediation.Linters)
            : new SimpleValidationService();

        // Initialize build verification service
        _buildVerificationService = _config.Remediation.BuildVerificationCommands.Any()
            ? new ShellBuildVerificationService(_config.Remediation.BuildVerificationCommands, timeoutSeconds: _config.Remediation.TimeoutSeconds)
            : null;
    }

    private ISecretProvider CreateSecretProvider()
    {
        if (_config.Secrets.Source.Equals("keyvault", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(_config.Secrets.KeyVaultUrl))
        {
            _logger.Information("Using Azure Key Vault secret provider: {Url}", _config.Secrets.KeyVaultUrl);
            return new CompositeSecretProvider(
                new AzureKeyVaultSecretProvider(_config.Secrets.KeyVaultUrl),
                new EnvironmentSecretProvider());
        }

        _logger.Debug("Using environment-variable secret provider.");
        return new EnvironmentSecretProvider();
    }

    private void InitializeIngestors()
    {
        // Mend
        if (_config.Ingestion.Mend.Enabled)
        {
            _ingestors.Add(new MendIngestor(_config.Ingestion.Mend));
            _logger.Information("Mend ingestor enabled.");
        }

        // SonarQube
        if (_config.Ingestion.SonarQube.Enabled)
        {
            _ingestors.Add(new SonarQubeIngestor(_config.Ingestion.SonarQube));
            _logger.Information("SonarQube ingestor enabled.");
        }

        // Trivy
        if (_config.Ingestion.Trivy.Enabled)
        {
            _ingestors.Add(new TrivyIngestor(_config.Ingestion.Trivy));
            _logger.Information("Trivy ingestor enabled.");
        }

        // CSV
        if (_config.Ingestion.Csv.Enabled)
        {
            _ingestors.Add(new CsvIngestor(_config.Ingestion.Csv));
            _logger.Information("CSV ingestor enabled.");
        }

        // Excel
        if (_config.Ingestion.Excel.Enabled)
        {
            _ingestors.Add(new ExcelIngestor(_config.Ingestion.Excel));
            _logger.Information("Excel ingestor enabled.");
        }

        // PDF
        if (_config.Ingestion.Pdf.Enabled)
        {
            _ingestors.Add(new PdfIngestor(_config.Ingestion.Pdf));
            _logger.Information("PDF ingestor enabled.");
        }

        // SARIF
        if (_config.Ingestion.Sarif.Enabled)
        {
            _ingestors.Add(new SarifIngestor(_config.Ingestion.Sarif));
            _logger.Information("SARIF ingestor enabled.");
        }
    }

    public async Task RunAsync(string? repoName = null, CancellationToken cancellationToken = default)
    {
        var modeText = _config.Agent.Mode == AgentMode.DryRun ? "DRY-RUN" : "FIX";
        _logger.Information("Initializing Autonomous AI Auto-Fixer");
        _logger.Information("Mode: {Mode}, BaseBranch: {Branch}", modeText, _config.Agent.BaseBranch);

        if (!string.IsNullOrEmpty(_inputFile))
        {
            _logger.Information("Processing input file: {FilePath}", _inputFile);
        }

        if (_config.Agent.Mode == AgentMode.DryRun)
        {
            _logger.Information("Running in DRY-RUN mode. No changes will be applied.");
        }
        else
        {
            _logger.Warning("Running in FIX mode. Changes will be committed and PRs created.");
        }

        var repositories = string.IsNullOrEmpty(repoName) 
            ? _config.Agent.Repositories 
            : new List<string> { repoName! };

        foreach (var repo in repositories)
        {
            _logger.Information("Processing repository: {Repo}", repo);
            await ProcessRepositoryAsync(repo, cancellationToken);
        }
    }

    private async Task ProcessRepositoryAsync(string repoName, CancellationToken cancellationToken)
    {
        _logger.Information("Scanning repository {Repo} for findings...", repoName);

        // Gather findings from all enabled ingestors
        var allFindings = new List<Finding>();
        foreach (var ingestor in _ingestors)
        {
            try
            {
                var findings = await ingestor.IngestFindingsAsync(repoName, _inputFile, cancellationToken);
                var findingsList = findings.ToList();
                allFindings.AddRange(findingsList);
                _logger.Information("Ingestor {Ingestor} returned {Count} findings.", ingestor.GetType().Name, findingsList.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while ingesting from {Ingestor}", ingestor.GetType().Name);
            }
        }

        // Limit to max findings per repo
        var limitedFindings = allFindings
            .Take(_config.Agent.MaxFindingsPerRepo)
            .ToList();

        _logger.Information("Total findings after limit: {Count}", limitedFindings.Count);

        if (_config.Agent.Mode == AgentMode.Fix && limitedFindings.Any())
        {
            foreach (var finding in limitedFindings)
            {
                await ProcessFindingAsync(finding, repoName, cancellationToken);
            }
        }
        else if (_config.Agent.Mode == AgentMode.DryRun)
        {
            // In dry-run, just log what would be fixed
            foreach (var finding in limitedFindings)
            {
                _logger.Information("[DRY-RUN] Would fix finding {Id}: {Title}", finding.Id, finding.Title);
            }
        }
    }

    private async Task ProcessFindingAsync(Finding finding, string repoName, CancellationToken cancellationToken)
    {
        _logger.Information("Processing finding {Id} from {Source}", finding.Id, finding.Source);

        // Generate fix using LLM
        var patch = await _llmClient.GenerateFixAsync(finding, cancellationToken);
        if (string.IsNullOrWhiteSpace(patch))
        {
            _logger.Warning("LLM returned empty fix for finding {Id}. Skipping.", finding.Id);
            return;
        }

        int attempts = 0;
        var maxAttempts = _config.Remediation.ValidationRetries;
        bool validated = false;
        while (!validated && attempts <= maxAttempts)
        {
            if (_config.Remediation.EnableValidation)
            {
                _logger.Information("Validating patch for finding {Id}, attempt {Attempt}", finding.Id, attempts + 1);
                validated = await _validationService.ValidateFixAsync(finding, patch, cancellationToken);
                if (!validated && _config.Agent.EnableSelfCorrection && attempts < maxAttempts)
                {
                    _logger.Warning("Validation failed. Regenerating patch for finding {Id}.", finding.Id);
                    patch = await _llmClient.GenerateFixAsync(finding, cancellationToken);
                    attempts++;
                    continue;
                }
            }
            else
            {
                validated = true; // no validation required
            }
            break;
        }

        if (!validated)
        {
            _logger.Warning("Patch validation failed after {Attempt} attempts. Skipping PR creation for finding {Id}.", maxAttempts + 1, finding.Id);
            return;
        }

        // Build verification: apply patch locally and run build commands
        if (_buildVerificationService != null)
        {
            _logger.Information("Running build verification for finding {Id}", finding.Id);
            var buildPassed = await _buildVerificationService.VerifyBuildAsync(finding, patch, cancellationToken);
            if (!buildPassed)
            {
                _logger.Warning("Build verification failed for finding {Id}. Skipping PR creation.", finding.Id);
                return;
            }
        }

        // Build PR request: apply the generated patch to the target file (or to a .autofix
        // note file when no file path is available from the finding).
        var shortId = finding.Id.Length >= 8 ? finding.Id.Substring(0, 8) : finding.Id;
        var safeId = new string(shortId.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        var branchName = $"autofix/{safeId}";
        var prTitle = $"Auto-fix: {finding.Title}";
        var prDescription =
            $"This PR fixes finding `{finding.Id}` from **{finding.Source}**.\n\n" +
            $"- **Severity:** {finding.Severity}\n" +
            $"- **Type:** {finding.Type}\n" +
            (string.IsNullOrEmpty(finding.FilePath) ? "" : $"- **File:** `{finding.FilePath}`\n") +
            (finding.LineNumber.HasValue ? $"- **Line:** {finding.LineNumber.Value}\n" : "") +
            $"\n{finding.Description}\n\n" +
            "_Generated by Autonomous AI Auto-Fixer._";

        var targetPath = !string.IsNullOrWhiteSpace(finding.FilePath)
            ? finding.FilePath!
            : $".autofix/{safeId}.patch";

        var prRequest = new PullRequestRequest(
            RepoFullName: repoName,
            BaseBranch: _config.Agent.BaseBranch,
            HeadBranch: branchName,
            Title: prTitle,
            Description: prDescription,
            Files: new[] { new FileChange(targetPath, patch) });

        var prUrl = await _vcsClient.CreatePullRequestAsync(prRequest, cancellationToken);
        if (prUrl is not null)
        {
            _logger.Information("PR created for finding {Id}: {Url}", finding.Id, prUrl);
        }
        else
        {
            _logger.Warning("PR was not created for finding {Id} (skipped or failed).", finding.Id);
        }
    }
}
