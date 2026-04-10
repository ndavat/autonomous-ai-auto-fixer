using System.CommandLine;
using System.CommandLine.Invocation;
using AutoFixer.Audit;
using AutoFixer.Models;
using AutoFixer.Remediation;

namespace AutoFixer;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Initialize logging
        LoggerSetup.Initialize();
        var logger = LoggerSetup.GetLogger("Program");

        // Define CLI options
        var modeOption = new Option<string>(
            "--mode", 
            () => "dry-run", 
            "Execution mode (dry-run or fix)"
        );

        var configOption = new Option<string>(
            "--config", 
            () => "config/default.yaml", 
            "Path to config file"
        );

        var inputFileOption = new Option<string?>(
            "--input-file", 
            null, 
            "Path to input file (PDF, CSV, Excel, etc.)"
        );

        var repoOption = new Option<string?>(
            "--repo", 
            null, 
            "Specify a single repository to scan"
        );

        var branchOption = new Option<string?>(
            "--branch", 
            null, 
            "The base branch to scan and branch off from (e.g., 'main', 'develop')"
        );

        var rootCommand = new RootCommand("Autonomous AI Auto-Fixer - .NET 10 Console App")
        {
            modeOption,
            configOption,
            inputFileOption,
            repoOption,
            branchOption
        };

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var mode = context.ParseResult.GetValueForOption(modeOption);
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var inputFile = context.ParseResult.GetValueForOption(inputFileOption);
            var repoName = context.ParseResult.GetValueForOption(repoOption);
            var baseBranch = context.ParseResult.GetValueForOption(branchOption);

            // Load configuration
            AppConfig config;
            try
            {
                config = ConfigLoader.LoadConfig(configPath);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load configuration from {ConfigPath}", configPath);
                context.ExitCode = 1;
                return;
            }

            // Override mode from CLI
            config.Agent.Mode = mode.ToLower() == "fix" ? AgentMode.Fix : AgentMode.DryRun;
            
            if (!string.IsNullOrEmpty(baseBranch))
            {
                config.Agent.BaseBranch = baseBranch;
            }

            logger.Information("Initializing Autonomous AI Auto-Fixer");
            logger.Information("Mode: {Mode}, Branch: {Branch}, InputFile: {InputFile}, Repo: {Repo}", 
                config.Agent.Mode, 
                config.Agent.BaseBranch,
                inputFile,
                repoName);

            if (!string.IsNullOrEmpty(inputFile))
            {
                logger.Information("Processing input file: {FilePath}", inputFile);
            }

            if (config.Agent.Mode == AgentMode.DryRun)
            {
                logger.Information("Running in DRY-RUN mode. No changes will be applied.");
            }
            else
            {
                logger.Warning("Running in FIX mode. Changes will be committed and PRs created.");
            }

            // Initialize Core Engine and start ingestion
            var engine = new RemediationEngine(config, inputFile);
            await engine.RunAsync(repoName);
        });

        return await rootCommand.InvokeAsync(args);
    }
}
