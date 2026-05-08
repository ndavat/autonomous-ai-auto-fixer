# Changelog

All notable changes to the Autonomous AI Auto-Fixer project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned (Phase 6: Documentation & Release)
- Finalize user guide and playbook
- Add CONTRIBUTING.md contributor guide
- Add LICENSE file (MIT)
- Add CHANGELOG.md
- Update architecture diagram
- Tag v1.0 release

### Planned (Phase 4 Remaining)
- PR monitoring and self-correction based on reviewer comments

### Planned (Phase 5)
- Application Insights / OpenTelemetry metrics
- Enhanced audit logging with structured fields
- Health check endpoint
- Performance benchmarks

## [0.9.0] - 2025-03-26

### Added
- **Core .NET 10 Console Application** with System.CommandLine CLI
- **Configuration system** with YAML support (NetEscapades.Configuration.Yaml) and environment variable overrides (prefix `AUTOFIXER_`)
- **Structured logging** via Serilog (console + file sinks, rolling daily)
- **Ingestion layer** with multi-format support:
  - Mend/WhiteSource vulnerability ingest (API + JSON file)
  - SonarQube analysis results ingest (API + file)
  - Trivy vulnerability scan ingest (container/OS scan parsing)
  - SARIF format ingest (Static Analysis Results Interchange Format)
  - CSV file ingest (CsvHelper, configurable column mapping)
  - Excel file ingest (ClosedXML, configurable column/sheet mapping)
  - PDF file ingest (PdfPig, text extraction + pattern matching)
- **Remediation Engine**: Finding orchestration, validation loop with self-correction
- **LLM Client**: Azure OpenAI chat-completions (with key/endpoint env-var fallback and transient-error retry)
- **VCS Clients**:
  - GitHub REST API (branch create, file commit via Contents API, open PR)
  - Azure DevOps Git REST API (branch create, push commits, open PR)
- **Validation**: Shell-based build verification service that applies patches, runs build commands, and restores files
- **Secret Management**:
  - `ISecretProvider` abstraction
  - Environment variable provider
  - Azure Key Vault provider (DefaultAzureCredential)
  - Composite provider with fallback chain
- **Comprehensive unit test suite** (xUnit): Config, ingestors, VCS clients, secret providers, build verification
- **Docker support**: Dockerfile (multi-stage .NET 10 Alpine) and docker-compose.yml
- **Documentation**: playbook.md, README.md, CONFIGURATION.md, SETUP_AND_DEPLOYMENT.md, github_app_setup.md

### Changed
- Ported from Python to .NET 10 (complete rewrite in C#)
- Replaced Python Dockerfile with .NET 10 Alpine version
- Removed all Python files and Python-specific documentation

### Fixed
- N/A (initial .NET release)
