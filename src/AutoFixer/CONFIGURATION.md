# .NET Application Settings

## Configuration Files

The AutoFixer application supports multiple configuration formats:

### JSON Configuration (Recommended for .NET)

- **appsettings.json** - Default configuration for all environments
- **appsettings.Development.json** - Development-specific settings (override)
- **appsettings.Production.json** - Production-specific settings (override)
- **appsettings.{EnvironmentName}.json** - Custom environment settings

Configuration is automatically loaded based on the `DOTNET_ENVIRONMENT` environment variable.

### YAML Configuration (Legacy Support)

- **config/default.yaml** - Original YAML configuration format
- Use `--config` CLI option to specify custom YAML config path

## Environment Variables

Override any configuration setting using environment variables with the pattern:
`Section__SettingName` (double underscore for hierarchy)

```bash
# Example overrides
export agent__mode=fix
export vcs__primary=github
export llm__provider=azure-openai
export secrets__source=azure-key-vault
export DOTNET_ENVIRONMENT=Production
```

## Configuration Sections

### Agent Settings
- `mode`: Execution mode (`dry-run` or `fix`)
- `maxRetries`: Maximum retry attempts for failed operations
- `riskPolicy`: Risk tolerance (`low-risk-only`, `medium`, `high`)

### VCS Settings
- `primary`: Primary version control system (`azure-devops` or `github`)
- `azureDevOps`: Azure DevOps organization and project settings
- `github`: GitHub App ID and installation ID

### LLM Settings
- `provider`: LLM provider (`github-copilot`, `azure-openai`, etc.)
- `model`: Model name or `auto` for automatic selection
- `temperature`: Sampling temperature (0.0-1.0)
- `maxTokens`: Maximum tokens in response

### Secrets Management
- `source`: Secret source (`environment`, `azure-key-vault`)
- `vaultUrl`: Azure Key Vault URL
- `tenantId`: Azure tenant ID
- `githubTokenSecretName`: Name of GitHub token secret in vault

### Ingestion Settings
- `sonarqube`: SonarQube URL and bug prioritization
- `mend`: Mend vulnerability scanning enabled/disabled
- `trivy`: Trivy container scanning enabled/disabled

### Remediation Settings
- `priorityOrder`: Order of issue types to fix
- `approvalRequired`: Issue types requiring human approval
- `contextLines`: Number of code context lines for AI analysis

### Validation Settings
- `linters`: Language-specific linters to run after fixes
- `buildVerification`: Enable/disable build verification

### Logging Settings
- `level`: Minimum log level (`Debug`, `Information`, `Warning`, `Error`)
- `file.enabled`: Enable file logging
- `file.path`: Log file path pattern
- `file.retentionDays`: Days to retain log files
- `console.enabled`: Enable console logging

## Usage Examples

### Development Mode
```bash
export DOTNET_ENVIRONMENT=Development
dotnet run --project src/AutoFixer
```

### Production Mode with Overrides
```bash
export DOTNET_ENVIRONMENT=Production
export agent__mode=fix
export vcs__primary=github
dotnet run --project src/AutoFixer -- --repo my-org/my-repo
```

### Using Custom Config File
```bash
dotnet run --project src/AutoFixer -- --config ./custom-config.yaml
```

## Best Practices

1. **Never commit secrets**: Use environment variables or Azure Key Vault
2. **Use appsettings.Development.json**: For local development overrides
3. **Use appsettings.Production.json**: For production settings
4. **Validate configuration**: Check settings before running in fix mode
5. **Start with dry-run**: Always test in dry-run mode first
