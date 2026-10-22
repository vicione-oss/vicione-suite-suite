using System.IO.Abstractions;
using Core.OS.Instance.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Instance.Services;

internal sealed partial class InstanceRecoveryService(IFileSystem fileSystem, IOptions<InstanceOptions> options, ILogger<InstanceRecoveryService> logger) :
    IHostedService
{
    /// <summary>
    /// not used...recovery analyzer has to run before any service registration
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// on graceful shutdown like sigterm 0, restart etc.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (options.Value.Recovery is null || options.Value.Recovery.TimespanMinutes <= 0)
                return Task.CompletedTask;

            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(options.Value);
            fileSystem.File.Delete(recoveryFilePath);

            LogRecoveryCounterReset(logger);
        }
        catch (Exception ex)
        {
            LogRecoveryCounterResetFailed(logger, ex);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Graceful shutdown - reset startup recovery counter")]
    private static partial void LogRecoveryCounterReset(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to reset startup recovery counter")]
    private static partial void LogRecoveryCounterResetFailed(ILogger logger, Exception exception);
}
