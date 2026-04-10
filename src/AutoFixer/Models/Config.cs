namespace AutoFixer.Models;

public class AgentConfig
{
    public AgentMode Mode { get; set; } = AgentMode.DryRun;
    public string BaseBranch { get; set; } = "main";
    public List<string> Repositories { get; set; } = new();
    public int MaxFindingsPerRepo { get; set; } = 10;
    public bool EnableSelfCorrection { get; set; } = true;
}

public class IngestionConfig
{
    public MendConfig Mend { get; set; } = new();
    public SonarQubeConfig SonarQube { get; set; } = new();
    public TrivyConfig Trivy { get; set; } = new();
}

public class MendConfig
{
    public bool Enabled { get; set; }
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class SonarQubeConfig
{
    public bool Enabled { get; set; }
    public string HostUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class TrivyConfig
{
    public bool Enabled { get; set; }
    public string ScanType { get; set; } = "fs";
}

public class RemediationConfig
{
    public string LlmProvider { get; set; } = "azure-openai";
    public string ModelName { get; set; } = "gpt-4";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
}

public class VcsConfig
{
    public string Provider { get; set; } = "github";
    public string? Token { get; set; }
    public string? ApiUrl { get; set; }
}

public class AppConfig
{
    public AgentConfig Agent { get; set; } = new();
    public IngestionConfig Ingestion { get; set; } = new();
    public RemediationConfig Remediation { get; set; } = new();
    public VcsConfig Vcs { get; set; } = new();
}
