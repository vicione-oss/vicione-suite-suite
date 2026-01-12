using System.IO.Abstractions;
using Core.OS.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Modules.Services;

internal class WorkspaceManagement(IFileSystem fileSystem, IOptions<InstanceOptions> instanceOptions, ILogger<WorkspaceManagement> logger) : IWorkspaceManagement
{
    internal const string ResetDirectoryIdentifier = ".reset-flag";

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

    public void WriteResetHomeDirectoryFlag(string moduleId)
    {
        var moduleHome = GetHomeDirectory(moduleId);

        CreateResetTriggerFile(fileSystem, moduleHome, () => LogMarkedReset("home", moduleId));
    }

    public void WriteResetCacheDirectoryFlag(string moduleId)
    {
        var cacheHome = GetCacheDirectory(moduleId);

        CreateResetTriggerFile(fileSystem, cacheHome, () => LogMarkedReset("cache", moduleId));
    }

    private void LogMarkedReset(string workspace, string moduleId)
        => logger.LogInformation("Marked {Workspace} reset on startup for module {ModuleId}", workspace, moduleId);

    private static void CreateResetTriggerFile(IFileSystem fileSystem, string modulePath, Action created)
    {
        var triggerFile = fileSystem.Path.Combine(modulePath, ResetDirectoryIdentifier);
        if (!fileSystem.Directory.Exists(modulePath))
            return;

        // already marked for reset
        if (fileSystem.File.Exists(triggerFile))
            return;

        fileSystem.File.Create(triggerFile, 0, FileOptions.None);
        created.Invoke();
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
            // this is fatal!
            logger.LogCritical(e, "Insufficient permissions to create workspace");
            throw;
        }
    }

    /// <summary>
    /// Checks home and cache directories for existing <see cref="WorkspaceManagement.ResetDirectoryIdentifier">reset flag files</see>.
    /// If a directory contains this flag its contents get deleted recursively.
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public static void ResetMarkedModuleWorkspace(IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger)
    {
        var homeRoot = fileSystem.GetRootedHomeDirectory(options);
        ClearExistingDirectory(fileSystem, homeRoot, "home", logger);

        var cacheRoot = fileSystem.GetRootedCacheDirectory(options);
        ClearExistingDirectory(fileSystem, cacheRoot, "cache", logger);
        return;

        static void ClearExistingDirectory(IFileSystem fileSystem, string directory, string source, Serilog.ILogger logger)
        {
            if (!fileSystem.Directory.Exists(directory))
                return;

            var moduleDirectories = fileSystem.Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            var resetDirectories = moduleDirectories.Where(p => ResetTriggerFileExists(fileSystem, p));

            fileSystem.TryDeleteDirectories(resetDirectories, source, logger);
        }

        static bool ResetTriggerFileExists(IFileSystem fileSystem, string modulePath)
        {
            var triggerFile = fileSystem.Path.Combine(modulePath, ResetDirectoryIdentifier);
            return fileSystem.File.Exists(triggerFile);
        }
    }
}
