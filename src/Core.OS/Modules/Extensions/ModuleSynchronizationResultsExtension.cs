using Core.OS.Modules.Contracts;
using Semver;

namespace Core.OS.Modules.Extensions;

internal static partial class ModuleSynchronizationResultsExtension
{
    public static void LogSynchronizationResults(this ModuleSynchronizationResults result, ILogger logger)
    {
        if (result.HttpResolveError is not null)
        {
            LogHttpError(logger, result.HttpResolveError);
        }

        // sdk version mismatch - we set package to be resolved to latest
        foreach (var synchronizeResult in result.Incompatible.Where(k => k.Error is null))
        {
            LogAutomaticUpgrade(logger, synchronizeResult.Name, synchronizeResult.Version);
        }

        LogResult(logger,
            result.Updated.Count,
            result.UpdateSkipped.Count,
            result.Deleted.Count,
            result.UpdateFailed.Count);

        foreach (var synchronizeResult in result.All.Where(k => k.Error is not null))
            LogResultErrors(logger, synchronizeResult.Name, synchronizeResult.Error!.Message);
    }

    [LoggerMessage(LogLevel.Error, "Module synchronization failed")]
    private static partial void LogHttpError(ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Information, "Automatic upgrade of module {Name} version '{Version}' because of sdk incompatibility")]
    private static partial void LogAutomaticUpgrade(ILogger logger, string name, SemVersion? version);

    [LoggerMessage(LogLevel.Information, "Update {Updated} modules (skipped:{Skipped}, removed:{Removed}, errors:{Errors})")]
    private static partial void LogResult(ILogger logger, int updated, int skipped, int removed, int errors);

    [LoggerMessage(LogLevel.Error, "Module {Name} synchronisation failed - {Message}")]
    private static partial void LogResultErrors(ILogger logger, string name, string? message);
}
