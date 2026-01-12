using System.IO.Abstractions;
using System.Reflection;
using Blazor.Shared.Services;
using Core.Shared.Logging;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;

namespace Blazor.Server.Backend.Services;

/// <summary>
/// 
/// </summary>
internal sealed class BackendLogService : IBackendLogService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogLevelSwitch _logLevelSwitch;
    private readonly string _logDirectory;

    public BackendLogService(IFileSystem fileSystem,
        ILogLevelSwitch logLevelSwitch,
        ILogOptions logOptions,
        ILogger<BackendLogService> logger)
    {
        _fileSystem = fileSystem;
        _logLevelSwitch = logLevelSwitch;

        var logPath = logOptions.LogPath ?? string.Empty;
        _logDirectory = _fileSystem.Path.IsPathFullyQualified(logPath)
            ? logPath
            : GetFallbackLogPath(logPath);

        string GetFallbackLogPath(string logPath)
        {
#if DEBUG            
            var assembly = Assembly.GetAssembly(typeof(BackendModule));
            var binPath = _fileSystem.Path.GetDirectoryName(assembly!.Location);

            return _fileSystem.Path.Combine(binPath!, logPath);
#else
            return _fileSystem.Path.Combine(_fileSystem.Directory.GetCurrentDirectory(), logPath);
#endif
        }
    }

    public Task<LogLevel> GetLogLevel() => Task.FromResult(_logLevelSwitch.LogLevel);

    public Task SetLogLevel(LogLevel level)
    {
        _logLevelSwitch.LogLevel = level;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetLogPaths()
    {
        if (!_fileSystem.Directory.Exists(_logDirectory))
            return Task.FromResult(Enumerable.Empty<string>());

        return Task.FromResult(_fileSystem.Directory.GetFiles(_logDirectory, "*.*", SearchOption.AllDirectories)
            .Select(p => _fileSystem.Path.GetRelativePath(_logDirectory, p)));
    }

    public Task<Stream> GetLog(string logPath, CancellationToken token = default)
    {
        var fullPath = _fileSystem.Path.Combine(_logDirectory, Uri.UnescapeDataString(logPath));
        if (!_fileSystem.File.Exists(fullPath))
            throw new FileNotFoundException(fullPath);

        // is there a better way?
        var stream = _fileSystem.FileStream.New(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Task.FromResult<Stream>(stream);
    }
}
