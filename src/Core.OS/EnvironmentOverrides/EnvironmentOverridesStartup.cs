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

    /// <summary>
    /// The file an operator set aside from the failsafe debug page, if one is there. Nothing applies
    /// it - it is resolved only so the boot it is skipped on can say so in the journal.
    /// </summary>
    /// <returns>The path of a present disabled override file, or <c>null</c>.</returns>
    public static string? ResolveDisabledPath(IFileSystem fileSystem)
    {
        var path = EnvironmentOverridesFile.ResolveDisabledPath(fileSystem, ReadHomeDirectory());

        return path is not null && fileSystem.File.Exists(path) ? path : null;
    }

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
