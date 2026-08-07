using System.IO.Abstractions;
using Core.OS.Instance.Extensions;

namespace Core.OS.EnvironmentOverrides;

/// <summary>
/// Locates the override file at the very top of <c>Main</c>, where the host builder - and with it
/// the options pipeline - does not exist yet, because the overrides have to be in the process
/// environment before it is created.
/// </summary>
internal static class EnvironmentOverridesStartup
{
    /// <returns>The path of the override file, or <c>null</c> if the feature is switched off.</returns>
    public static string? ResolvePath(IFileSystem fileSystem)
        => EnvironmentOverridesFile.ResolvePath(fileSystem, ReadHomeDirectory());

    // The same sources the host builder reads the instance section from, narrowed to the one key
    // this needs: binding the whole section here would validate it before logging can report it.
    private static string? ReadHomeDirectory()
        => new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build()
            .GetInstanceHomeDirectory();
}
