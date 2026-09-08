using System.Text.Json;
using Core.OS.EnvironmentOverrides;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Modules.Extensions;
using Core.Shared.EnvironmentOverrides;
using Sdk.Messaging;

namespace Core.OS.Hosting.Services;

/// <summary>
/// Resolves the diagnostic groups of the failsafe debug page from disk.
/// Every group is resolved on its own and swallows its own failures: the page must render even
/// when <see cref="Instance.InstanceOptions.HomeDirectory"/> is itself the invalid value that put
/// the suite into failsafe mode.
/// </summary>
internal static class FallbackDiagnosticsFactory
{
    private const string UnknownVersion = "unknown";

    public static async Task<FallbackDiagnostics> Create(FallbackHostOptions options, CancellationToken cancellationToken = default)
        => new FallbackDiagnostics
        {
            Status = options.Status,
            Messages = options.Messages,
            Timestamp = DateTimeOffset.UtcNow,
            Version = await ResolveVersion(options, cancellationToken),
            Recovery = await ResolveRecovery(options, cancellationToken),
            EnvironmentOverrides = await ResolveEnvironmentOverrides(options, cancellationToken),
        };

    private static async Task<FallbackEnvironmentOverridesDiagnostics> ResolveEnvironmentOverrides(FallbackHostOptions options, CancellationToken cancellationToken)
    {
        var enabled = EnvironmentOverridesSwitch.IsEnabled();
        if (!enabled || options.FileSystem is null || options.Instance is null)
            return new FallbackEnvironmentOverridesDiagnostics { Enabled = enabled };

        string? path = null;
        var fileExists = false;
        try
        {
            path = EnvironmentOverridesFile.ResolvePath(options.FileSystem, options.Instance.HomeDirectory);
            if (path is null)
                return new FallbackEnvironmentOverridesDiagnostics { Enabled = true };

            fileExists = options.FileSystem.File.Exists(path);
            if (!fileExists)
                return new FallbackEnvironmentOverridesDiagnostics { Enabled = true, Path = path };

            var contents = await options.FileSystem.File.ReadAllTextAsync(path, cancellationToken);

            return new FallbackEnvironmentOverridesDiagnostics
            {
                Enabled = true,
                Path = path,
                FileExists = true,
                CanDisable = options.HostManagement is not null,
                Keys = [.. EnvironmentOverridesFormat.Parse(contents).Keys],
            };
        }
        catch (Exception e)
        {
            // The message never quotes the offending line, so it carries no override value.
            return new FallbackEnvironmentOverridesDiagnostics
            {
                Enabled = true,
                Path = path,
                FileExists = fileExists,
                CanDisable = fileExists && options.HostManagement is not null,
                Error = e.Message,
            };
        }
    }

    private static async Task<FallbackVersionDiagnostics> ResolveVersion(FallbackHostOptions options, CancellationToken cancellationToken)
    {
        var localVersion = UnknownVersion;
        string? branchName = null;
        try
        {
            localVersion = options.FileSystem is not null
                ? options.FileSystem.EvaluateLocalVersionString(out branchName)
                : SuiteVersionUtils.GetSuiteVersion();
        }
        catch
        {
            // The assembly version is the one value that should always be there; if it is not,
            // the rest of the page is still worth rendering.
        }

        var sdkVersion = UnknownVersion;
        try
        {
            sdkVersion = SuiteVersionUtils.GetSuiteSdkVersion();
        }
        catch
        {
            // Same reasoning: an unreadable SDK assembly name must not cost the whole page.
        }

        return new FallbackVersionDiagnostics
        {
            LocalVersion = localVersion,
            SdkVersion = sdkVersion,
            BranchName = branchName,
            DataVersion = await ResolveDataVersion(options, cancellationToken),
        };
    }

    private static async Task<string?> ResolveDataVersion(FallbackHostOptions options, CancellationToken cancellationToken)
    {
        if (options.FileSystem is null || options.Instance is null)
            return null;

        try
        {
            // The read-only helpers on purpose: DetectVersionDowngrade writes the file, and a page
            // request must not change what the next boot compares against.
            return options.FileSystem.DataVersionFileExists(options.Instance)
                ? (await options.FileSystem.ReadDataVersionFile(options.Instance, cancellationToken)).Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<FallbackRecoveryDiagnostics?> ResolveRecovery(FallbackHostOptions options, CancellationToken cancellationToken)
    {
        if (options.FileSystem is null || options.Instance is null)
            return null;

        try
        {
            // Deserialized here rather than through IFileSystemExtensions.ReadRecoveryState, which
            // deletes the file when it fails to parse. That would reset the crash cycle from a page
            // request and let the next boot leave the terminal state it was put into on purpose.
            var path = options.FileSystem.GetLocalRecoveryFilePath(options.Instance);
            if (!options.FileSystem.File.Exists(path))
                return null;

            var contents = await options.FileSystem.File.ReadAllTextAsync(path, cancellationToken);
            var state = JsonSerializer.Deserialize<RecoveryState>(contents, DefaultJsonSerializerSettings.Default);
            if (state is null)
                return null;

            return new FallbackRecoveryDiagnostics
            {
                LastStartup = state.LastStartup,
                Startups = state.Startups,
                RecoveryApplied = state.RecoveryApplied,
            };
        }
        catch
        {
            return null;
        }
    }
}
