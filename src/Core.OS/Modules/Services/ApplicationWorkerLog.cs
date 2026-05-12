using Sdk.Instance;

namespace Core.OS.Modules.Services;

internal static partial class ApplicationWorkerLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Starting application initialization")]
    internal static partial void LogStartingApplicationInitialization(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Init completed as {InstanceType} ({InstanceId})")]
    internal static partial void LogInitCompleted(this ILogger<ApplicationWorker> logger, InstanceType instanceType, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initializing module host")]
    internal static partial void LogInitializingModuleHost(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Modules are initialized")]
    internal static partial void LogModulesInitialized(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message bus depot started")]
    internal static partial void LogMessageBusDepotStarted(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Moving module resources")]
    internal static partial void LogMovingModuleResources(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Module resources sychronized")]
    internal static partial void LogModuleResourcesSynchronized(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Deleting orphaned nonces")]
    internal static partial void LogDeletingOrphanedNonces(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occurred while trying to delete orphaned nonces")]
    internal static partial void LogErrorDeletingOrphanedNonces(this ILogger<ApplicationWorker> logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occurred while trying to set culture")]
    internal static partial void LogErrorSettingCulture(this ILogger<ApplicationWorker> logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Culture set to {CultureName}")]
    internal static partial void LogCultureSet(this ILogger<ApplicationWorker> logger, string cultureName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Migrating application databases")]
    internal static partial void LogMigratingApplicationDatabases(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Invalidating active logins")]
    internal static partial void LogInvalidatingActiveLogins(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seeding initial data")]
    internal static partial void LogSeedingInitialData(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initializing local instance information")]
    internal static partial void LogInitializingLocalInstanceInformation(this ILogger<ApplicationWorker> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting registration for instance {InstanceId}")]
    internal static partial void LogStartingRegistration(this ILogger<ApplicationWorker> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Published {Command} on {InstanceType} instance")]
    internal static partial void LogPublishedRegisterInstance(this ILogger<ApplicationWorker> logger, string command, InstanceType instanceType);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Init failed")]
    internal static partial void LogInitFailed(this ILogger<ApplicationWorker> logger, Exception exception);
}
