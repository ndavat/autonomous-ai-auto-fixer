using Serilog;

namespace AutoFixer.Audit;

public static class LoggerSetup
{
    public static void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/autofixer-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public static ILogger GetLogger(string context = "AutoFixer")
    {
        return Log.Logger.ForContext("SourceContext", context);
    }
}
