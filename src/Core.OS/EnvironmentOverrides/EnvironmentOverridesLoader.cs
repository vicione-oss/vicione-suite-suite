using System.IO.Abstractions;

namespace Core.OS.EnvironmentOverrides;

/// <summary>
/// Applies the runtime environment-variable override file to the process environment.
/// Must run before the host builder is created, so the overrides are honored by both the
/// .NET options pipeline and external libraries that read the process environment directly
/// at init (e.g., OpenTelemetry). Overrides win over inherited OS environment variables.
/// </summary>
internal static class EnvironmentOverridesLoader
{
    /// <param name="path">
    /// The file resolved by <see cref="EnvironmentOverridesFile.ResolvePath"/>, or <c>null</c> if
    /// the deployment did not switch the feature on. Passed in rather than resolved here so the
    /// caller can report an instance without overrides with the same value this step acted on.
    /// </param>
    /// <returns>
    /// The failure that kept the overrides from being applied, or <c>null</c> if there was none.
    /// Handed back instead of logged because this runs before logging is configured; the caller
    /// reports it once it can.
    /// </returns>
    public static async Task<Exception?> Apply(IFileSystem fileSystem, string? path)
    {
        // No configured path is as normal as a missing file: the instance runs on the inherited
        // environment.
        if (path is null || !fileSystem.File.Exists(path))
            return null;

        try
        {
            var contents = await fileSystem.File.ReadAllTextAsync(path);

            // The whole file is parsed before anything is applied: a syntax error halfway down
            // would otherwise leave the process running on the entries above it and nothing below,
            // which is a configuration nobody asked for.
            foreach (var (key, value) in EnvironmentOverridesFormat.Parse(contents))
                Environment.SetEnvironmentVariable(key, value);

            return null;
        }
        catch (Exception e)
        {
            // A broken file degrades to the inherited environment rather than stopping the process:
            // this runs ahead of the host builder, so the fallback host cannot catch a throw here
            // and Restart=always would turn it into an endless restart loop.
            return e;
        }
    }
}
