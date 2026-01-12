using Core.Tests.Tools;
using Microsoft.Extensions.Configuration;

namespace Core.OS.Tests.Extensions;

internal static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddCoreAppSettings(this IConfigurationBuilder builder, string? environment = null)
    {
        builder.AddJsonFile(Path.Combine(
        [
            PathHelpers.GetSourcePath(),
            "Core.OS",
            "appsettings.json",
        ]));

        if (string.IsNullOrEmpty(environment))
            return builder;

        return builder.AddJsonFile(Path.Combine(
        [
            PathHelpers.GetSourcePath(),
            "Core.OS",
            $"appsettings.{environment}.json",
        ]));
    }
}
