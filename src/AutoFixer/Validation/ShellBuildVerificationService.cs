using AutoFixer.Audit;
using AutoFixer.Models;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoFixer.Validation;

/// <summary>
/// Applies a proposed patch to the target file in the working directory, runs a list of
/// configured shell build commands (e.g. <c>dotnet build</c>), and restores the original
/// file content regardless of the outcome.
/// </summary>
public class ShellBuildVerificationService : IBuildVerificationService
{
    private readonly ILogger _logger;
    private readonly List<string> _commands;
    private readonly string _workingDirectory;
    private readonly TimeSpan _commandTimeout;

    public ShellBuildVerificationService(IEnumerable<string> commands, string? workingDirectory = null, int timeoutSeconds = 300)
    {
        _commands = commands.ToList();
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
        _commandTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        _logger = LoggerSetup.GetLogger(nameof(ShellBuildVerificationService));
    }

    public async Task<bool> VerifyBuildAsync(Finding finding, string proposedPatch, CancellationToken cancellationToken = default)
    {
        if (!_commands.Any())
        {
            _logger.Debug("No build verification commands configured – skipping.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(finding.FilePath))
        {
            _logger.Debug("Finding has no file path – skipping build verification.");
            return true;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, finding.FilePath!));
        var safeWorkingDir = Path.GetFullPath(_workingDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(safeWorkingDir, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warning("File path {FilePath} escapes working directory – skipping build verification.", finding.FilePath);
            return true;
        }

        var directory = Path.GetDirectoryName(fullPath);
        var fileExisted = File.Exists(fullPath);
        string? originalContent = null;

        if (fileExisted)
        {
            originalContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
        }

        try
        {
            if (!string.IsNullOrEmpty(directory) && directory != _workingDirectory)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, proposedPatch, cancellationToken);
            _logger.Information("Applied patch to {Path} for build verification.", fullPath);

            foreach (var cmd in _commands)
            {
                if (!await RunCommandAsync(cmd, _workingDirectory, cancellationToken))
                {
                    _logger.Warning("Build verification failed for command: {Cmd}", cmd);
                    return false;
                }
            }

            _logger.Information("Build verification passed for finding {Id}.", finding.Id);
            return true;
        }
        finally
        {
            try
            {
                if (fileExisted && originalContent != null)
                {
                    await File.WriteAllTextAsync(fullPath, originalContent, CancellationToken.None);
                    _logger.Debug("Restored original content of {Path}.", fullPath);
                }
                else if (!fileExisted && File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.Debug("Removed temporary file {Path}.", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to restore file {Path} after build verification. File may be in an inconsistent state.", fullPath);
            }
        }
    }

    private async Task<bool> RunCommandAsync(string command, string workingDirectory, CancellationToken ct)
    {
        try
        {
            _logger.Information("Running build command: {Cmd}", command);

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd" : "/bin/sh",
                    Arguments = isWindows ? $"/C {command}" : $"-c \"{command}\"",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();

            using var timeoutCts = new CancellationTokenSource(_commandTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var linkedToken = linkedCts.Token;

            using var killRegistration = linkedToken.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            });

            var readOutput = process.StandardOutput.ReadToEndAsync(linkedToken);
            var readError = process.StandardError.ReadToEndAsync(linkedToken);
            await Task.WhenAll(readOutput, readError);
            await process.WaitForExitAsync(linkedToken);

            _logger.Debug("Build command output: {Output}", readOutput.Result);
            if (process.ExitCode != 0)
            {
                _logger.Warning("Build command failed with exit code {Code}. Error: {Error}", process.ExitCode, readError.Result);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.Warning("Build command {Cmd} was cancelled by caller.", command);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Build command {Cmd} timed out.", command);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while running build command {Cmd}", command);
            return false;
        }
    }
}
