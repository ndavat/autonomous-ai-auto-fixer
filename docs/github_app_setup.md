# GitHub App Authentication Setup Guide

## Why GitHub Apps?
- More secure than Personal Access Tokens
- Fine-grained repository permissions
- Better audit logging
- Can be installed organization-wide

## Setup Steps:

### 1. Create GitHub App
- Go to GitHub Organization Settings → Developer settings → GitHub Apps
- Create a new app with these permissions:
  - Repository permissions:
    - Contents: Write
    - Pull requests: Write
    - Metadata: Read
  - Subscribe to Push events

### 2. Generate Private Key
- Download the private key file (.pem)
- Store it securely in Azure Key Vault

### 3. Update Configuration

**Using appsettings.json:**
```json
{
  "vcs": {
    "primary": "github",
    "github": {
      "appId": 123456,
      "installationId": 789012
    }
  },
  "secrets": {
    "source": "azure-key-vault",
    "githubTokenSecretName": "github-app-private-key"
  }
}
```

**Using environment variables:**
```bash
export VCS__Primary=github
export VCS__GitHub__AppId=123456
export VCS__GitHub__InstallationId=789012
export SECRETS__Source=azure-key-vault
export SECRETS__GitHubTokenSecretName=github-app-private-key
```

**Using YAML config (config/default.yaml):**
```yaml
vcs:
  primary: github
  github:
    appId: 123456
    installationId: 789012

secrets:
  source: azure-key-vault
  githubTokenSecretName: github-app-private-key
```

### 4. Install App
- Install the GitHub App on your target repositories
- Note the Installation ID from the URL

## Benefits:
- Repository-scoped access
- Automatic token refresh
- Better security audit trail
- No user dependency