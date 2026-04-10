# Autonomous AI Auto-Fixer - .NET 10 Console Application

This is a .NET 10 console application port of the original Python-based Autonomous AI Auto-Fixer.

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
│   └── IFindingIngestor.cs    # Ingestor interface
├── Remediation/
│   └── RemediationEngine.cs   # Core remediation engine
├── Validation/                # (Placeholder for validation logic)
├── VCS/                       # (Placeholder for VCS clients)
├── Secrets/                   # (Placeholder for secrets management)
└── Strategies/                # (Placeholder for remediation strategies)
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

## Key Differences from Python Version

1. **CLI Framework**: Uses `System.CommandLine` instead of `click`
2. **Logging**: Uses `Serilog` instead of `structlog`
3. **Configuration**: Uses `Microsoft.Extensions.Configuration` with YAML support
4. **Null Safety**: Enabled nullable reference types for better safety
5. **Async/Await**: Native C# async/await pattern throughout

## Next Steps

To complete the port, implement the following components:

- [ ] Mend ingestor (`Ingestion/Mend/`)
- [ ] SonarQube ingestor (`Ingestion/SonarQube/`)
- [ ] Trivy ingestor (`Ingestion/Trivy/`)
- [ ] LLM client for remediation suggestions
- [ ] VCS clients (GitHub, Azure DevOps)
- [ ] PR creation and monitoring
- [ ] Build verification and linting
- [ ] Self-correction logic
