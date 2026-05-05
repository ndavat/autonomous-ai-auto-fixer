# AutoFixer Playbook

## 1. Introduction

**Autonomous AI Auto-Fixer** is a .NET 10 console application designed to automatically remediate technical debt and security vulnerabilities. It integrates with **SonarQube**, **Mend**, and **Trivy** to ingest findings, generates validated code fixes using AI, and submits Pull Requests to **Azure Repos** and **GitHub**.

**Current Status** (as of analysis):
- ✅ Core CLI framework (System.CommandLine)
- ✅ Configuration loading (YAML + Environment Variables)
- ✅ Structured logging (Serilog)
- ✅ Model definitions (Finding, Config classes)
- ✅ Remediation engine (fully implemented)
- ✅ Unit test project with comprehensive tests
- ✅ Ingestors: Mend, SonarQube, Trivy, SARIF, CSV, Excel, PDF
- ✅ LLM integration (Azure OpenAI client)
- ✅ VCS clients (GitHub REST API, Azure DevOps REST API)
- ✅ Validation service (linter commands, build verification)
- ✅ Secret providers (Environment, Azure Key Vault, Composite)

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     CLI (Program.cs)                        │
│  – Parses arguments (--mode, --config, --input-file, etc.)  │
│  – Loads configuration via ConfigLoader                     │
│  – Instantiates RemediationEngine                           │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│               RemediationEngine (Orchestrator)               │
│  – Iterates over target repositories                        │
│  – Calls ingestors to gather findings                       │
│  – Applies risk policy                                      │
│  – Coordinates fix generation (LLM) and validation          │
│  – Creates PRs via VCS clients                              │
└───────┬───────────────┬───────────────────┬─────────────────┘
        │               │                   │
┌───────▼──────┐ ┌──────▼───────┐  ┌────────▼────────┐
│  Ingestors   │ │  Strategies  │  │  VCS Clients    │
│ (IFinding-   │ │ (CodeSmell,  │  │ (GitHub, Azure  │
│  Ingestor)   │ │  Dependency) │  │  DevOps)        │
└──────────────┘ └──────────────┘  └─────────────────┘
```

**Key Patterns**:
- **Dependency Inversion**: `IFindingIngestor` interface allows plugging in different scanning tools.
- **Configuration via .NET Configuration**: YAML files, JSON appsettings, environment variables (prefix `AUTOFIXER_`).
- **Async/Await**: All I/O operations are asynchronous (`Task`, `CancellationToken`).
- **Structured Logging**: Serilog with console and file sinks.

---

## 3. Current Code Structure

```
.
├── AutoFixer.sln
├── Dockerfile
├── docker-compose.yml
├── README.md (project overview)
├── playbook.md
├── global.json
├── config/
│   └── default.yaml
├── docs/
│   ├── architecture.drawio
│   └── github_app_setup.md
├── src/AutoFixer/
│   ├── Program.cs                # Entry point, CLI setup
│   ├── ConfigLoader.cs           # Loads YAML + env vars into AppConfig
│   ├── AutoFixer.csproj          # .NET 10 project
│   ├── config/
│   │   └── default.yaml          # Default YAML config
│   ├── Models/
│   │   ├── Enums.cs              # AgentMode enum (DryRun, Fix)
│   │   ├── Config.cs             # AppConfig and all sub-config classes
│   │   └── Finding.cs            # Finding model (represents a scan result)
│   ├── Audit/
│   │   └── Logger.cs             # Serilog initialization
│   ├── Ingestion/
│   │   ├── IFindingIngestor.cs   # Interface for ingestors
│   │   ├── MendIngestor.cs       # Mend/WhiteSource vulnerability ingest
│   │   ├── SonarQubeIngestor.cs  # SonarQube analysis results
│   │   ├── TrivyIngestor.cs      # Trivy vulnerability scans
│   │   ├── SarifIngestor.cs      # SARIF (Static Analysis Results Interchange Format)
│   │   ├── CsvIngestor.cs        # CSV-based scan reports
│   │   ├── ExcelIngestor.cs      # Excel (.xlsx) scan reports
│   │   └── PdfIngestor.cs        # PDF scan report extraction
│   ├── LLM/
│   │   ├── ILlmClient.cs         # Interface for LLM fix generation
│   │   └── AzureOpenAIClient.cs  # Azure OpenAI chat-completions client
│   ├── Remediation/
│   │   └── RemediationEngine.cs  # Core orchestrator
│   ├── Secrets/
│   │   ├── ISecretProvider.cs    # Secret resolution interface
│   │   ├── EnvironmentSecretProvider.cs
│   │   ├── AzureKeyVaultSecretProvider.cs
│   │   ├── CompositeSecretProvider.cs
│   │   └── SecretResolver.cs     # Unified fallback-chain resolver
│   ├── Validation/
│   │   ├── IValidationService.cs # Fix validation interface
│   │   ├── SimpleValidationService.cs  # Linter-based validation
│   │   ├── IBuildVerificationService.cs
│   │   └── ShellBuildVerificationService.cs  # Build command verification
│   └── VCS/
│       ├── IVcsClient.cs         # VCS client interface
│       ├── GitHubClient.cs       # GitHub REST API client
│       ├── AzureDevOpsClient.cs  # Azure DevOps REST API client
│       └── PullRequestRequest.cs # PR request DTOs
├── AutoFixer.Tests/
│   ├── AutoFixer.Tests.csproj
│   ├── AppConfigTests.cs
│   ├── AzureDevOpsClientTests.cs
│   ├── ConfigLoaderTests.cs
│   ├── CsvIngestorTests.cs
│   ├── ExcelIngestorTests.cs
│   ├── FindingTests.cs
│   ├── GitHubClientTests.cs
│   ├── IngestorTests.cs
│   ├── PdfIngestorTests.cs
│   ├── SarifIngestorTests.cs
│   ├── SecretProviderTests.cs
│   └── ShellBuildVerificationTests.cs
└── patches/
    └── manager-032426.py.patch
```

### 3.1 Program.cs
- Uses `System.CommandLine` to define options: `--mode`, `--config`, `--input-file`, `--repo`, `--branch`.
- Loads config via `ConfigLoader.LoadConfig(configPath)`.
- Overrides mode from CLI.
- Creates `RemediationEngine` and calls `RunAsync`.

### 3.2 ConfigLoader.cs
- Uses `Microsoft.Extensions.Configuration` with YAML file (via `AddYamlFile`) and environment variables (prefix `AUTOFIXER_`).
- Binds to `AppConfig`.

### 3.3 Models
- **AgentMode**: `DryRun` or `Fix`.
- **Finding**: Properties include `Id`, `Source`, `Severity`, `Type`, `Title`, `Description`, `FilePath`, `LineNumber`, `CodeSnippet`, `Metadata`.
- **AppConfig**: Top‑level with `Agent`, `Ingestion`, `Remediation`, `Vcs` sections.
- **AgentConfig**: Mode, BaseBranch, Repositories list, MaxFindingsPerRepo, EnableSelfCorrection.
- **IngestionConfig**: Mend, SonarQube, Trivy settings.
- **RemediationConfig**: LLM provider, model name, retries, timeout.
- **VcsConfig**: Provider (github/azure-devops), Token, ApiUrl.

### 3.4 RemediationEngine.cs
- Fully implemented orchestrator that:
  - Initializes all enabled ingestors based on configuration
  - Creates secret providers (environment variables or Azure Key Vault)
  - Initializes LLM client for fix generation
  - Creates VCS client (GitHub or Azure DevOps)
  - Sets up validation and build verification services
- `RunAsync` iterates over repositories and processes findings
- `ProcessFindingAsync` generates fixes via LLM, validates them, runs build verification, and creates PRs
- Supports self-correction with configurable retry loops

### 3.5 Logger.cs
- Initializes Serilog with console and file sinks (logs/autofixer-.log, rolling daily).

### 3.6 Code Analysis Findings
- **Namespace Usage**: The project uses top‑level `using` directives for clear imports and fully qualified names where needed.
- **Nullability**: Nullable reference types are enabled, improving safety.
- **Async Patterns**: All heavy I/O operations are asynchronous with `CancellationToken` support.
- **Logging**: Consistent structured logging via Serilog (console + file sinks).
- **Configuration**: YAML-based config via `NetEscapades.Configuration.Yaml` with environment variable overrides (prefix `AUTOFIXER_`).
- **Testing**: Comprehensive unit tests covering config, ingestors, VCS clients, secret providers, and build verification.
- **Error Handling**: All components have consistent try/catch with `OperationCanceledException` passthrough.
- **Code Organization**: Clear folder separation (Audit, Ingestion, LLM, Models, Remediation, Secrets, Validation, VCS).
- **Secrets**: Multi‑source secret resolution (config → Key Vault → environment variables) via `ISecretProvider` chain.

---

## 4. Configuration

The application supports multiple configuration sources:

| Source | File / Variable | Notes |
|--------|----------------|-------|
| YAML (legacy) | `config/default.yaml` or custom via `--config` | Loaded by `ConfigLoader` |
| JSON (recommended) | `appsettings.json`, `appsettings.{Environment}.json` | .NET standard, auto‑loaded based on `DOTNET_ENVIRONMENT` |
| Environment Variables | `AUTOFIXER__SECTION__SETTING` | Override any config value |

**Example Environment Overrides**:
```bash
export AUTOFIXER_AGENT__MODE=fix
export AUTOFIXER_VCS__PROVIDER=github
export AUTOFIXER_LLM__PROVIDER=azure-openai
export DOTNET_ENVIRONMENT=Production
```

**Key Configuration Sections** (from `AppConfig`):
- **Agent**: mode, base branch, repositories, max findings per repo, self‑correction flag.
- **Ingestion**: enable/disable Mend, SonarQube, Trivy; API URLs and credentials.
- **Remediation**: LLM provider, model, temperature, max tokens, retries, timeout.
- **VCS**: provider (github/azure‑devops), token, API URL.
- **Secrets**: source (environment, azure‑key‑vault), vault URL, tenant ID.

---

## 5. Build & Run

### 5.1 Prerequisites
- .NET 10 SDK (https://dotnet.microsoft.com/download/dotnet/10.0)
- (Optional) Docker for containerized deployment.
- (Optional) Azure CLI for Azure deployments.

### 5.2 Restore & Build
```bash
dotnet restore src/AutoFixer/AutoFixer.csproj
dotnet build src/AutoFixer/AutoFixer.csproj --configuration Release
```

### 5.3 Run (Development)
```bash
# Using default YAML config (dry‑run mode)
dotnet run --project src/AutoFixer -- --mode dry-run

# With specific repo and branch
dotnet run --project src/AutoFixer -- --mode dry-run --repo my-org/my-repo --branch develop

# Using JSON config (via environment)
export DOTNET_ENVIRONMENT=Development
dotnet run --project src/AutoFixer
```

### 5.4 Run (Published Self‑Contained)
```bash
dotnet publish src/AutoFixer/AutoFixer.csproj -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64
./publish/linux-x64/AutoFixer --mode dry-run --repo my-org/my-repo
```

### 5.5 Docker
```bash
docker build -t autofixer:latest .
docker run --rm -e DOTNET_ENVIRONMENT=Production -e GITHUB_TOKEN=xxx autofixer:latest --mode dry-run
```

---

## 6. Testing

Unit tests are located in `AutoFixer.Tests/`.

```bash
dotnet test AutoFixer.Tests/AutoFixer.Tests.csproj --verbosity normal
```

**Current Test Coverage**:
- `AppConfigTests` – validates configuration defaults and all sections.
- `AzureDevOpsClientTests` – tests Azure DevOps PR creation (happy path, existing branch, auth).
- `ConfigLoaderTests` – validates YAML config binding.
- `CsvIngestorTests` – tests CSV parsing with default/custom columns.
- `ExcelIngestorTests` – tests Excel parsing with default/custom columns.
- `FindingTests` – validates Finding model defaults and property assignment.
- `GitHubClientTests` – tests GitHub PR creation (happy path, existing branch, auth).
- `IngestorTests` – tests Trivy JSON parsing.
- `PdfIngestorTests` – tests PDF text extraction and pattern matching.
- `SarifIngestorTests` – tests SARIF (JSON) report parsing.
- `SecretProviderTests` – tests environment, Key Vault, and composite secret providers.
- `ShellBuildVerificationTests` – tests build verification (passing, failing, file restore, path safety).

---

## 7. Development Workflow

1. **Pick a Task** from TODO list (see Section 11).
2. **Create Feature Branch**:
   ```bash
   git checkout -b feature/implement-mend-ingestor
   ```
3. **Implement** the component following existing patterns:
   - Ingestors should implement `IFindingIngestor`.
   - Use async/await, nullable reference types, and DI‑friendly design.
4. **Add Unit Tests** in `AutoFixer.Tests/`.
5. **Run Tests** (`dotnet test`) and ensure they pass.
6. **Update Documentation** (README, CONFIGURATION.md, SETUP_AND_DEPLOYMENT.md) if needed.
7. **Submit Pull Request**; CI will build and test automatically.

---

## 8. Extending the System

### 8.1 Adding a New Ingestor (e.g., Mend)
1. Create `src/AutoFixer/Ingestion/MendIngestor.cs` implementing `IFindingIngestor`.
2. Implement `IngestFindingsAsync` to call Mend API or parse Mend export files (PDF, Excel, CSV, JSON).
3. Register the ingestor in `RemediationEngine` (or use DI container in future).
4. Update configuration (`Ingestion:Mend` section) with required API keys/URLs.

### 8.2 Integrating an LLM for Fix Generation
- Create `src/AutoFixer/LLM/ILlmClient.cs` interface.
- Implement a client for GitHub Copilot, Azure OpenAI, or Claude (HTTP API).
- Use in `RemediationEngine` to generate fixes based on `Finding` details.
- Implement validation loop (linters, build checks) and self‑correction retries.

### 8.3 Adding VCS Support
- Define `src/AutoFixer/VCS/IVcsClient.cs` with methods `CreatePullRequestAsync`, `PostCommentAsync`, etc.
- Implement `GitHubClient` and `AzureDevOpsClient`.
- Use `VcsConfig` to select provider and supply credentials.

---

## 9. Deployment

### 9.1 Azure App Service
```bash
az group create --name autofixer-rg --location eastus
az appservice plan create --name autofixer-plan --resource-group autofixer-rg --sku B1 --is-linux
az webapp create --resource-group autofixer-rg --plan autofixer-plan --name autofixer-app --runtime "DOTNET|10.0"
# Deploy published output (framework‑dependent or self‑contained)
```

### 9.2 Azure Container Instances
```bash
az acr create --resource-group autofixer-rg --name autofixercr --sku Basic
az acr login --name autofixercr
docker tag autofixer:latest autofixercr.azurecr.io/autofixer:latest
docker push autofixercr.azurecr.io/autofixer:latest
az container create --resource-group autofixer-rg --name autofixer --image autofixercr.azurecr.io/autofixer:latest --cpu 1 --memory 1 --environment-variables DOTNET_ENVIRONMENT=Production GITHUB_TOKEN=xxx
```

### 9.3 Kubernetes (AKS)
- Build and push image to ACR.
- Apply `deployment.yaml` and `service.yaml` (to be created).
- Use `kubectl` to manage the deployment.

### 9.4 CI/CD
- **GitHub Actions**: `.github/workflows/build-deploy.yml` (example in SETUP_AND_DEPLOYMENT.md).
- **Azure DevOps**: `azure-pipelines.yml` (example present).
- Both restore, build, test, publish, and optionally deploy.

---

## 10. Troubleshooting

| Symptom | Possible Cause | Solution |
|---------|---------------|----------|
| `Failed to load configuration` | YAML file missing or malformed | Check path, validate YAML syntax |
| `dotnet: command not found` | .NET SDK not installed | Install .NET 10 SDK |
| `Permission denied` on Linux | Executable lacks execute permission | `chmod +x ./publish/linux-x64/AutoFixer` |
| No findings processed | Ingestors not implemented yet | Currently placeholder; implement ingestors |
| PR not created | VCS client not implemented | Implement GitHub/Azure DevOps client |
| Log files not created | `logs/` directory missing or permissions | Ensure `logs/` exists and is writable |

**Enable Debug Logging**:
```bash
export AUTOFIXER_LOGGING__LEVEL=Debug
dotnet run --project src/AutoFixer -- --mode dry-run
```

---

## 11. TODO & Roadmap

### Phase 1: Core Ingestors ✅
- [x] Implement `MendIngestor` (API and file‑based ingestion)
- [x] Implement `SonarQubeIngestor` (API and report parsing)
- [x] Implement `TrivyIngestor` (container/OS scan parsing)
- [x] Implement `SarifIngestor` (SARIF format parsing)

### Phase 2: AI Fix Generation ✅
- [x] Create `ILlmClient` interface and `AzureOpenAIClient`.
- [x] Implement prompt construction using `Finding` context.
- [x] Add retry logic with validation feedback.

### Phase 3: VCS Integration ✅
- [x] Implement `GitHubClient` (REST API, PR creation).
- [x] Implement `AzureDevOpsClient`.

### Phase 4: Validation & Self‑Correction ✅
- [x] Integrate linter checks after fixes
- [x] Implement build verification step
- [x] Self‑correction loop: if validation fails, retry with improved prompt
- [ ] Add PR monitoring and self‑correction based on reviewer comments

### Phase 5: Observability & Hardening
- [ ] Add Application Insights or OpenTelemetry metrics
- [ ] Enhance audit logging with structured fields
- [ ] Implement health check endpoint (if running as service)
- [ ] Add performance benchmarks

### Phase 6: Documentation & Release
- [ ] Finalize user guide (playbook.md – this document)
- [ ] Write contributor guide
- [ ] Create architecture diagram (update docs/architecture.drawio)
- [ ] Tag v1.0 release