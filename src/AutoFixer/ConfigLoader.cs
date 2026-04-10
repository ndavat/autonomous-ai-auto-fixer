using Microsoft.Extensions.Configuration;
using AutoFixer.Models;

namespace AutoFixer;

public static class ConfigLoader
{
    public static AppConfig LoadConfig(string configPath)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddYamlFile(configPath, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "AUTOFIXER_")
            .Build();

        var config = new AppConfig();
        configuration.Bind(config);

        return config;
    }
}
