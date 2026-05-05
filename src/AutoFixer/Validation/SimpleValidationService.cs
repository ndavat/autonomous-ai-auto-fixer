using AutoFixer.Audit;
using AutoFixer.Models;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoFixer.Validation;

/// <summary>
/// Very simple validation service that runs configured linter commands.
/// If no linters are configured, it always returns true.
/// </summary>
public class SimpleValidationService : IValidationService
{
    private readonly ILogger _logger;
    private readonly List<string> _linters;

    public SimpleValidationService(IEnumerable<string>? linters = null)
    {
        _logger = LoggerSetup.GetLogger(nameof(SimpleValidationService));
        _linters = linters?.ToList() ?? new();
    }

    public async Task<bool> ValidateFixAsync(Finding finding, string proposedPatch, CancellationToken cancellationToken = default)
    {
        if (!_linters.Any())
        {
            _logger.Debug("No linters configured – assuming validation passes.");
            return true;
        }

        foreach (var linter in _linters)
        {
            try
            {
                // Execute the linter command. It should return exit code 0 for success.
                // The command may contain placeholders like {patchFile} which we replace.
                var tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, proposedPatch, cancellationToken);
                var cmd = linter.Replace("{patchFile}", tempFile);
                _logger.Information("Running linter: {Cmd}", cmd);

                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = isWindows ? "cmd" : "/bin/sh",
                        Arguments = isWindows ? $"/C {cmd}" : $"-c \"{cmd}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                process.Start();
                var readOutput = process.StandardOutput.ReadToEndAsync();
                var readError = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(readOutput, readError);
                await process.WaitForExitAsync(cancellationToken);

                _logger.Debug("Linter output: {Output}", readOutput.Result);
                if (process.ExitCode != 0)
                {
                    _logger.Warning("Linter {Linter} failed with exit code {Code}. Error: {Error}", linter, process.ExitCode, readError.Result);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception while running linter {Linter}", linter);
                return false;
            }
        }

        return true;
    }
}
