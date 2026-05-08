# Contributing to Autonomous AI Auto-Fixer

Thank you for your interest in contributing to the Autonomous AI Auto-Fixer! This document provides guidelines for contributing to the project.

## Code of Conduct

- Be respectful and inclusive in all communications
- Provide constructive feedback
- Focus on the technical merits of contributions
- Assume good faith from other contributors

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Git
- An IDE of your choice (Visual Studio 2022+, JetBrains Rider, or VS Code with C# extension)

### Setting Up Your Development Environment

```bash
# 1. Fork and clone the repository
git clone https://github.com/YOUR_USERNAME/autonomous-ai-auto-fixer.git
cd autonomous-ai-auto-fixer

# 2. Add upstream remote
git remote add upstream https://github.com/ndavat/autonomous-ai-auto-fixer.git

# 3. Restore dependencies
dotnet restore

# 4. Build
dotnet build

# 5. Run tests to verify everything works
dotnet test
```

## Development Workflow

### Branching Strategy

- `dotnet10-console-app` — Main development branch
- `feature/<feature-name>` — New features
- `fix/<bug-description>` — Bug fixes
- `docs/<description>` — Documentation changes

### Making Changes

1. **Sync with upstream** before starting:
   ```bash
   git checkout dotnet10-console-app
   git pull upstream dotnet10-console-app
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Implement your changes**, following the project conventions:
   - Use **nullable reference types** (enabled project-wide)
   - Use **async/await** for all I/O operations
   - Support **CancellationToken** in async methods
   - Use **structured logging** via Serilog (`Log.Information`, `Log.Error`, etc.)
   - Follow existing **namespace and folder conventions**

4. **Add unit tests** for new functionality in `AutoFixer.Tests/`

5. **Run tests** to ensure nothing is broken:
   ```bash
   dotnet test --verbosity normal
   ```

6. **Commit your changes** with a clear message:
   ```bash
   git commit -m "feat: Add feature description"
   ```
   
   We follow [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat:` — New feature
   - `fix:` — Bug fix
   - `docs:` — Documentation changes
   - `refactor:` — Code restructuring
   - `test:` — Test additions or changes
   - `chore:` — Maintenance tasks

7. **Push and open a Pull Request**:
   ```bash
   git push origin feature/your-feature-name
   ```

### Pull Request Guidelines

- Keep PRs focused on a single change
- Include a clear description of what the change does and why
- Reference any related issues
- Ensure all tests pass
- Update documentation if your changes affect user-facing behavior

## Project Architecture

See [playbook.md](playbook.md) for a comprehensive overview of the system architecture, code structure, and component interactions.

### Key Patterns

- **Dependency Inversion**: Components depend on interfaces (`IFindingIngestor`, `IVcsClient`, `ILlmClient`, `ISecretProvider`)
- **Configuration**: YAML via `NetEscapades.Configuration.Yaml` with environment variable overrides (prefix `AUTOFIXER_`)
- **Async/Await**: All I/O operations are asynchronous with `CancellationToken` support
- **Structured Logging**: Serilog with console and file sinks
- **Nullable Reference Types**: Enabled project-wide for safety

### Adding a New Ingestor

1. Create a class in `src/AutoFixer/Ingestion/` implementing `IFindingIngestor`
2. Add configuration section in `IngestionConfig` (in `Config.cs`)
3. Register the ingestor in `RemediationEngine`
4. Add unit tests in `AutoFixer.Tests/`
5. Update the default YAML config in `config/default.yaml`

### Adding a New VCS Client

1. Create a class in `src/AutoFixer/VCS/` implementing `IVcsClient`
2. Add configuration as needed in `VcsConfig`
3. Register the client in `RemediationEngine` based on provider selection
4. Add unit tests

## Code Style

- Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `PascalCase` for public members, `camelCase` for private fields
- Prefer file-scoped namespaces (`namespace AutoFixer.Something;`)
- Use expression-bodied members for simple properties/methods
- Keep methods focused and under ~50 lines when possible
- Use `var` when the type is obvious, explicit types otherwise

## Testing

- Use **xUnit** as the testing framework
- Test files go in `AutoFixer.Tests/`
- Name test methods as `MethodName_Scenario_ExpectedResult`
- Use `[Fact]` for simple tests, `[Theory]` with `[InlineData]` for parameterized tests
- Mock external dependencies (APIs, file systems) where appropriate
- Aim for meaningful coverage of business logic and edge cases

```bash
# Run all tests
dotnet test

# Run a specific test project
dotnet test AutoFixer.Tests/AutoFixer.Tests.csproj

# Run tests with verbose output
dotnet test --verbosity normal
```

## Documentation

- Update [playbook.md](playbook.md) for architectural changes
- Update [README.md](README.md) for feature additions or changes
- Update [CONFIGURATION.md](src/AutoFixer/CONFIGURATION.md) for configuration changes
- Update [SETUP_AND_DEPLOYMENT.md](src/AutoFixer/SETUP_AND_DEPLOYMENT.md) for deployment changes

## Reporting Issues

When reporting bugs:
1. Use the GitHub Issues tracker
2. Include steps to reproduce
3. Include expected vs actual behavior
4. Include relevant logs and configuration (sanitized)
5. Specify your environment (OS, .NET version)

## Questions?

- Open an issue on GitHub
- Check existing documentation in the `docs/` folder
- Review the [playbook.md](playbook.md) for architecture questions
