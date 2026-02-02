using Blazor.Shared.Services;
using Core.Shared.Logging;
using Microsoft.Extensions.Logging;

namespace Blazor.Server.Backend.Services;

/// <summary>
/// Handles in memory log level switching for the backend.
/// </summary>
internal sealed class BackendLogService(ILogLevelSwitch logLevelSwitch) : IBackendLogService
{
    public Task<LogLevel> GetLogLevel() => Task.FromResult(logLevelSwitch.LogLevel);

    public Task SetLogLevel(LogLevel level)
    {
        logLevelSwitch.LogLevel = level;
        return Task.CompletedTask;
    }
}
