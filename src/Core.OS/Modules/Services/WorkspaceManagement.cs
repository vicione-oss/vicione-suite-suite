using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Modules.Services;

internal partial class WorkspaceManagement(IFileSystem fileSystem, IOptions<InstanceOptions> instanceOptions, ILogger<WorkspaceManagement> logger) : IWorkspaceManagement
{
    public string GetHomeDirectory(string moduleId)
    {
        // /path/to/home/module-id
        var homeRoot = fileSystem.GetRootedHomeDirectory(instanceOptions.Value);
        var moduleHome = fileSystem.Path.Combine(homeRoot, moduleId);
        EnsureDirectoryExists(fileSystem, moduleHome, logger);
        return moduleHome;
    }

    public string GetCacheDirectory(string moduleId)
    {
        // /path/to/cache/module-id
        var cacheRoot = fileSystem.GetRootedCacheDirectory(instanceOptions.Value);
        var moduleCache = fileSystem.Path.Combine(cacheRoot, moduleId);
        EnsureDirectoryExists(fileSystem, moduleCache, logger);
        return moduleCache;
    }

    private static void EnsureDirectoryExists(IFileSystem fileSystem, string directory, ILogger logger)
    {
        try
        {
            if (!fileSystem.Directory.Exists(directory))
                fileSystem.Directory.CreateDirectory(directory);
        }
        catch (UnauthorizedAccessException e)
        {
            // Fatal: the workspace cannot be prepared.
            LogInsufficientPermissions(logger, e);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Insufficient permissions to create workspace")]
    private static partial void LogInsufficientPermissions(ILogger logger, Exception exception);
}
