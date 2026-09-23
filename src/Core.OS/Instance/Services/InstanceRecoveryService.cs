using System.IO.Abstractions;
using Core.OS.Instance.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Instance.Services;

internal sealed class InstanceRecoveryService(IFileSystem fileSystem, IOptions<InstanceOptions> options, ILogger<InstanceRecoveryService> logger) :
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

            logger.LogInformation("Graceful shutdown - reset startup recovery counter");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset startup recovery counter");
        }

        return Task.CompletedTask;
    }
}
