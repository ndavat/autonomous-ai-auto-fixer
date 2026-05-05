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
    public CsvConfig Csv { get; set; } = new();
    public ExcelConfig Excel { get; set; } = new();
    public PdfConfig Pdf { get; set; } = new();
    public SarifConfig Sarif { get; set; } = new();
}

public class MendConfig
{
    public bool Enabled { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
}

public class SonarQubeConfig
{
    public bool Enabled { get; set; }
    public string? HostUrl { get; set; }
    public string? Token { get; set; }
}

public class TrivyConfig
{
    public bool Enabled { get; set; }
    public string ScanType { get; set; } = "fs";
}

public class CsvConfig
{
    public bool Enabled { get; set; }
    public string IdColumn { get; set; } = "Id";
    public string SeverityColumn { get; set; } = "Severity";
    public string TypeColumn { get; set; } = "Type";
    public string TitleColumn { get; set; } = "Title";
    public string DescriptionColumn { get; set; } = "Description";
    public string FilePathColumn { get; set; } = "FilePath";
    public string LineNumberColumn { get; set; } = "LineNumber";
}

public class ExcelConfig
{
    public bool Enabled { get; set; }
    public int SheetIndex { get; set; } = 0;
    public string IdColumn { get; set; } = "Id";
    public string SeverityColumn { get; set; } = "Severity";
    public string TypeColumn { get; set; } = "Type";
    public string TitleColumn { get; set; } = "Title";
    public string DescriptionColumn { get; set; } = "Description";
    public string FilePathColumn { get; set; } = "FilePath";
    public string LineNumberColumn { get; set; } = "LineNumber";
}

public class PdfConfig
{
    public bool Enabled { get; set; }
}

public class SarifConfig
{
    public bool Enabled { get; set; }
}

public class RemediationConfig
{
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
    public bool EnableValidation { get; set; } = true;
    public int ValidationRetries { get; set; } = 1;
    public List<string> Linters { get; set; } = new();
    public List<string> BuildVerificationCommands { get; set; } = new();
}

public class LlmConfig
{
    public string Provider { get; set; } = "azure-openai";
    public string ModelName { get; set; } = "gpt-4";
    public double Temperature { get; set; } = 0.0;
    public int MaxTokens { get; set; } = 4096;
    public int TimeoutSeconds { get; set; } = 300;
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
}

public class VcsConfig
{
    public string Provider { get; set; } = "github";
    public string? Token { get; set; }
    public string? ApiUrl { get; set; }
}

public class SecretsConfig
{
    public string Source { get; set; } = "environment"; // environment | keyvault
    public string? KeyVaultUrl { get; set; }
}

public class AppConfig
{
    public AgentConfig Agent { get; set; } = new();
    public IngestionConfig Ingestion { get; set; } = new();
    public RemediationConfig Remediation { get; set; } = new();
    public LlmConfig Llm { get; set; } = new();
    public VcsConfig Vcs { get; set; } = new();
    public SecretsConfig Secrets { get; set; } = new();
}
