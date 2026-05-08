# Autonomous AI Auto-Fixer - .NET 10 Console Application

A modern .NET 10 console application for autonomous code remediation.

## Project Structure

```
src/AutoFixer/
├── Program.cs                 # Main entry point with CLI
├── ConfigLoader.cs            # Configuration loading from YAML
├── AutoFixer.csproj           # Project file (.NET 10)
├── config/
│   └── default.yaml           # Default configuration
├── Models/
│   ├── Enums.cs               # AgentMode enum
│   ├── Finding.cs             # Finding model
│   └── Config.cs              # Configuration models
├── Audit/
│   └── Logger.cs              # Serilog logging setup
├── Ingestion/
│   ├── IFindingIngestor.cs    # Ingestor interface
│   ├── MendIngestor.cs        # Mend/WhiteSource vulnerability ingest
│   ├── SonarQubeIngestor.cs   # SonarQube analysis results
│   ├── TrivyIngestor.cs       # Trivy vulnerability scans
│   ├── SarifIngestor.cs       # SARIF format parsing
│   ├── CsvIngestor.cs         # CSV-based scan reports
│   ├── ExcelIngestor.cs       # Excel (.xlsx) scan reports
│   └── PdfIngestor.cs         # PDF scan report extraction
├── LLM/
│   ├── ILlmClient.cs          # LLM client interface
│   └── AzureOpenAIClient.cs   # Azure OpenAI chat-completions client
├── Remediation/
│   └── RemediationEngine.cs   # Core remediation orchestrator
├── Secrets/
│   ├── ISecretProvider.cs     # Secret resolution interface
│   ├── EnvironmentSecretProvider.cs
│   ├── AzureKeyVaultSecretProvider.cs
│   ├── CompositeSecretProvider.cs
│   └── SecretResolver.cs      # Unified fallback-chain resolver
├── Validation/
│   ├── IValidationService.cs  # Fix validation interface
│   ├── SimpleValidationService.cs  # Linter-based validation
│   ├── IBuildVerificationService.cs
│   └── ShellBuildVerificationService.cs  # Build command verification
└── VCS/
    ├── IVcsClient.cs          # VCS client interface
    ├── GitHubClient.cs        # GitHub REST API client
    ├── AzureDevOpsClient.cs   # Azure DevOps REST API client
    └── PullRequestRequest.cs  # PR request DTOs
```

## Prerequisites

- .NET 10 SDK (preview or release)
- YAML configuration file

## Building

```bash
cd src/AutoFixer
dotnet restore
dotnet build
```

## Running

### Dry-run mode (default)
```bash
dotnet run -- --mode dry-run --config config/default.yaml
```

### Fix mode
```bash
dotnet run -- --mode fix --config config/default.yaml --repo my-repo --branch main
```

### With input file
```bash
dotnet run -- --input-file findings.csv --mode dry-run
```

## CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `--mode` | Execution mode: `dry-run` or `fix` | `dry-run` |
| `--config` | Path to configuration YAML file | `config/default.yaml` |
| `--input-file` | Path to input file (PDF, CSV, Excel, etc.) | null |
| `--repo` | Specify a single repository to scan | null |
| `--branch` | Base branch to scan and branch off from | null |

## Configuration

The application uses YAML configuration with environment variable overrides (prefix: `AUTOFIXER_`).

See `config/default.yaml` for all available options.

## Features

1. **CLI Framework**: Uses `System.CommandLine` for modern command-line parsing
2. **Logging**: Uses `Serilog` for structured logging
3. **Configuration**: Uses `Microsoft.Extensions.Configuration` with YAML support
4. **Null Safety**: Enabled nullable reference types for better safety
5. **Async/Await**: Native C# async/await pattern throughout

## Implementation Status

- [x] **Core CLI Framework**: System.CommandLine with --mode, --config, --input-file, --repo, --branch
- [x] **Configuration**: YAML config loading with environment variable overrides (prefix `AUTOFIXER_`)
- [x] **Logging**: Serilog with console and file sinks (rolling daily)
- [x] **Ingestors**: Mend, SonarQube, Trivy, SARIF, CSV, Excel, PDF
- [x] **LLM Client**: Azure OpenAI chat-completions with retry logic
- [x] **VCS Clients**: GitHub REST API and Azure DevOps REST API
- [x] **Validation**: Linter shell-out and build verification service
- [x] **Secrets**: Environment variables, Azure Key Vault, and composite provider
- [x] **Self-Correction**: Configurable retry loop with validation feedback
- [x] **Unit Tests**: Comprehensive test suite covering all major components
- [ ] **PR Comment Feedback Loop**: Not yet implemented (Phase 4)
