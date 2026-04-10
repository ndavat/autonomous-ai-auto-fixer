# Setup and Deployment Guide for AutoFixer .NET Application

## Prerequisites

- **.NET 10 SDK** (Preview or Release)
  - Download: https://dotnet.microsoft.com/download/dotnet/10.0
  - Verify: `dotnet --version`
- **Git** for version control
- **Azure CLI** (optional, for Azure deployments)
- **Docker** (optional, for containerized deployment)

## Local Development Setup

### 1. Clone the Repository

```bash
git clone https://github.com/ndavat/autonomous-ai-auto-fixer.git
cd autonomous-ai-auto-fixer
git checkout dotnet10-console-app
```

### 2. Install Dependencies

```bash
dotnet restore src/AutoFixer/AutoFixer.csproj
dotnet restore AutoFixer.Tests/AutoFixer.Tests.csproj
```

### 3. Configure Environment

Create a local development configuration:

```bash
# Option A: Copy and edit appsettings.Development.json
cp src/AutoFixer/appsettings.json src/AutoFixer/appsettings.Development.json
# Edit with your local settings

# Option B: Use environment variables
export DOTNET_ENVIRONMENT=Development
export agent__mode=dry-run
export vcs__primary=github
```

### 4. Set Up Secrets

For local development, use environment variables:

```bash
# GitHub Token (if using GitHub)
export GITHUB_TOKEN=your_github_token

# Azure Key Vault (if using Azure)
export secrets__source=environment
export GITHUB_TOKEN_SECRET_NAME=github-token
```

### 5. Build the Application

```bash
dotnet build src/AutoFixer/AutoFixer.csproj --configuration Debug
```

### 6. Run Tests

```bash
dotnet test AutoFixer.Tests/AutoFixer.Tests.csproj
```

### 7. Run the Application

```bash
# Using default configuration
dotnet run --project src/AutoFixer

# With specific options
dotnet run --project src/AutoFixer -- --mode dry-run --repo my-org/my-repo

# With custom config file
dotnet run --project src/AutoFixer -- --config ./config/default.yaml
```

## Publishing for Production

### Publish as Self-Contained Application

```bash
# Windows x64
dotnet publish src/AutoFixer/AutoFixer.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o ./publish/win-x64

# Linux x64
dotnet publish src/AutoFixer/AutoFixer.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o ./publish/linux-x64

# macOS ARM64
dotnet publish src/AutoFixer/AutoFixer.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -o ./publish/osx-arm64
```

### Publish as Framework-Dependent (Requires .NET Runtime)

```bash
dotnet publish src/AutoFixer/AutoFixer.csproj \
  -c Release \
  --self-contained false \
  -o ./publish/framework-dependent
```

## Docker Deployment

### Build Docker Image

The repository includes a `Dockerfile` for containerization:

```bash
# Build the image
docker build -t autofixer:latest .

# Run the container
docker run --rm \
  -e DOTNET_ENVIRONMENT=Production \
  -e GITHUB_TOKEN=your_token \
  -v $(pwd)/logs:/app/logs \
  autofixer:latest \
  --mode dry-run --repo my-org/my-repo
```

### Docker Compose

Use the provided `docker-compose.yml` for multi-container setups:

```bash
docker-compose up -d
```

## Azure Deployment

### Option 1: Azure App Service

```bash
# Create resource group
az group create --name autofixer-rg --location eastus

# Create App Service Plan
az appservice plan create \
  --name autofixer-plan \
  --resource-group autofixer-rg \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --resource-group autofixer-rg \
  --plan autofixer-plan \
  --name autofixer-app \
  --runtime "DOTNET|10.0"

# Deploy
zip -r deploy.zip ./publish/framework-dependent/*
az webapp deployment source config-zip \
  --resource-group autofixer-rg \
  --name autofixer-app \
  --src deploy.zip
```

### Option 2: Azure Container Instances

```bash
# Build and push to Azure Container Registry
az acr create --resource-group autofixer-rg --name autofixercr --sku Basic
az acr login --name autofixercr
docker tag autofixer:latest autofixercr.azurecr.io/autofixer:latest
docker push autofixercr.azurecr.io/autofixer:latest

# Deploy to ACI
az container create \
  --resource-group autofixer-rg \
  --name autofixer \
  --image autofixercr.azurecr.io/autofixer:latest \
  --cpu 1 \
  --memory 1 \
  --environment-variables \
    DOTNET_ENVIRONMENT=Production \
    GITHUB_TOKEN=your_token
```

### Option 3: Azure Kubernetes Service (AKS)

```bash
# Create AKS cluster
az aks create \
  --resource-group autofixer-rg \
  --name autofixer-aks \
  --node-count 1 \
  --enable-addons monitoring

# Get credentials
az aks get-credentials --resource-group autofixer-rg --name autofixer-aks

# Deploy to Kubernetes
kubectl create namespace autofixer
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

## CI/CD Pipeline Examples

### GitHub Actions

Create `.github/workflows/build-deploy.yml`:

```yaml
name: Build and Deploy

on:
  push:
    branches: [dotnet10-console-app]
  pull_request:
    branches: [dotnet10-console-app]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET 10
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore src/AutoFixer/AutoFixer.csproj
    
    - name: Build
      run: dotnet build src/AutoFixer/AutoFixer.csproj --no-restore
    
    - name: Test
      run: dotnet test AutoFixer.Tests/AutoFixer.Tests.csproj --no-build
    
    - name: Publish
      run: |
        dotnet publish src/AutoFixer/AutoFixer.csproj \
          -c Release \
          -r linux-x64 \
          --self-contained true \
          -o ./publish
    
    - name: Upload artifacts
      uses: actions/upload-artifact@v4
      with:
        name: autofixer-publish
        path: ./publish
```

### Azure DevOps Pipeline

Create `azure-pipelines.yml`:

```yaml
trigger:
  branches:
    include:
    - dotnet10-console-app

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    packageType: 'sdk'
    version: '10.x'

- script: dotnet restore src/AutoFixer/AutoFixer.csproj
  displayName: 'Restore dependencies'

- script: dotnet build src/AutoFixer/AutoFixer.csproj --no-restore
  displayName: 'Build'

- script: dotnet test AutoFixer.Tests/AutoFixer.Tests.csproj --no-build
  displayName: 'Run tests'

- script: |
    dotnet publish src/AutoFixer/AutoFixer.csproj \
      -c Release \
      -r linux-x64 \
      --self-contained true \
      -o $(Build.ArtifactStagingDirectory)
  displayName: 'Publish'

- task: PublishBuildArtifacts@1
  inputs:
    PathtoPublish: '$(Build.ArtifactStagingDirectory)'
    ArtifactName: 'drop'
```

## Configuration Management

### Environment-Specific Settings

Use the built-in configuration system:

```bash
# Development
export DOTNET_ENVIRONMENT=Development
dotnet run --project src/AutoFixer

# Staging
export DOTNET_ENVIRONMENT=Staging
dotnet run --project src/AutoFixer

# Production
export DOTNET_ENVIRONMENT=Production
dotnet run --project src/AutoFixer
```

### Azure Key Vault Integration

For production secrets management:

```bash
# Enable Key Vault in configuration
export secrets__source=azure-key-vault
export secrets__vaultUrl=https://your-vault.vault.azure.net/
export secrets__tenantId=your-tenant-id

# Authenticate (in Azure environment)
az login
# Or use managed identity in Azure services
```

## Monitoring and Logging

### Log Files

Logs are written to the `logs/` directory by default:
- Pattern: `logs/autofixer-.log`
- Retention: Configurable (default 30 days)

### Application Insights (Azure)

Add Application Insights for advanced monitoring:

```bash
# Add package
dotnet add src/AutoFixer/AutoFixer.csproj package Microsoft.Extensions.Azure
dotnet add src/AutoFixer/AutoFixer.csproj package Azure.Identity

# Configure connection string
export APPLICATIONINSIGHTS_CONNECTION_STRING=your_connection_string
```

## Troubleshooting

### Common Issues

1. **.NET SDK Not Found**
   ```bash
   # Install .NET 10 SDK
   wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
   ```

2. **Configuration Loading Errors**
   ```bash
   # Validate JSON syntax using dotnet
   dotnet tool install -g dotnet-format
   dotnet format --verify-no-changes src/AutoFixer/appsettings.json
   
   # Or use jq if available
   cat src/AutoFixer/appsettings.json | jq .
   
   # Check file permissions
   chmod 644 src/AutoFixer/appsettings*.json
   ```

3. **Permission Denied on Linux**
   ```bash
   # Make executable
   chmod +x ./publish/linux-x64/AutoFixer
   
   # Run with appropriate permissions
   ./publish/linux-x64/AutoFixer --help
   ```

### Debug Mode

Enable verbose logging for debugging:

```bash
export logging__level=Debug
export logging__console__enabled=true
dotnet run --project src/AutoFixer -- --mode dry-run
```

## Support

For issues and questions:
- GitHub Issues: https://github.com/ndavat/autonomous-ai-auto-fixer/issues
- Documentation: See `/docs` folder
- Configuration Guide: See `CONFIGURATION.md`
