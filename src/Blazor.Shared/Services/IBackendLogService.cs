using Microsoft.Extensions.Logging;

namespace Blazor.Shared.Services;

public interface IBackendLogService
{
    Task<LogLevel> GetLogLevel();

    Task SetLogLevel(LogLevel level);

    Task<IEnumerable<string>> GetLogPaths();

    Task<Stream> GetLog(string logPath, CancellationToken token = default);
}
