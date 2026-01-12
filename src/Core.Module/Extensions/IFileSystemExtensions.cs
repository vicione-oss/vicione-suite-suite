using System.IO.Abstractions;
using System.Reflection;
using Core.Module.Utils;
using MassTransit.Metadata;

namespace Core.Module.Extensions;

public static class IFileSystemExtensions
{
    public static IEnumerable<string> GetUiHostModuleAssemblyPaths(this IFileSystem fileSystem, string? searchPath, string assemblyName)
        => fileSystem.GetModuleDlls(searchPath).Where(path => path.Contains(assemblyName, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> GetDebugUiHostModuleAssemblyPaths(this IFileSystem fileSystem, List<string>? searchPaths, string assemblyName)
        => fileSystem.GetDebugModuleAssemblyPaths(searchPaths, false, assemblyName);

    public static IEnumerable<string> GetBackendModuleAssemblyPaths(this IFileSystem fileSystem, string? searchPath)
        => fileSystem.GetModuleDlls(searchPath).Where(path => fileSystem.DoesNotContain(path, Constants.ModuleSuffixClient));

    public static IEnumerable<string> GetDebugBackendModuleAssemblyPaths(this IFileSystem fileSystem, List<string>? searchPaths)
        => fileSystem.GetDebugModuleAssemblyPaths(searchPaths, false);

    public static IEnumerable<string> GetUiModuleAssemblyPaths(this IFileSystem fileSystem, string? searchPath, string uiHostDepsJsonPath)
        => fileSystem.GetModuleDlls(searchPath).Where(path => fileSystem.DoesNotContain(path, Constants.ModuleSuffixBackend));

    public static IEnumerable<string> GetDebugClientModuleAssemblyPaths(this IFileSystem fileSystem, List<string>? searchPaths)
        => fileSystem.GetDebugModuleAssemblyPaths(searchPaths, true);

    private static bool DoesNotContain(this IFileSystem fileSystem, string? path, string contain)
        => fileSystem.Path.GetFileNameWithoutExtension(path)?.Contains(contain, StringComparison.OrdinalIgnoreCase) == false;

    private static List<string> GetDebugModuleAssemblyPaths(this IFileSystem fileSystem, List<string>? searchPaths, bool clientSide, string? assemblyName = null)
    {
        if (searchPaths is null)
            return [];

        var modulePaths = new List<string>();

        foreach (var path in searchPaths)
        {
            var debugPath = path;
            if (!fileSystem.Path.IsPathRooted(debugPath))
            {
                var rootPath = fileSystem.GetRepositoryRootPath();
                // transform relative repository path to absolute path
                debugPath = fileSystem.Path.Combine(rootPath, path.TrimStart(fileSystem.Path.DirectorySeparatorChar));
            }

            if (!fileSystem.Directory.Exists(debugPath))
                continue;

            var depsFilter = clientSide ? Constants.ClientDepsJsonFilter : Constants.BackendDepsJsonFilter;
            var relevantDlls = fileSystem.Directory
                .GetFiles(debugPath, assemblyName ?? depsFilter, SearchOption.AllDirectories)
                .Select(ModuleHelpers.DepsJsonToDll);

            modulePaths.AddRange(relevantDlls);
        }

        return modulePaths;
    }

    /// <summary>
    /// returns all module dll (with .deps.json) paths recursive from searchPath 
    /// </summary>    
    /// <exception cref="DirectoryNotFoundException"></exception>
    private static string[] GetModuleDlls(this IFileSystem fileSystem, string? searchPath)
    {
        if (string.IsNullOrEmpty(searchPath))
            throw new InvalidOperationException("Modules directory is not configured");

        var moduleDirectory = fileSystem.GetRootedPath(searchPath);

#if DEBUG
        if (!fileSystem.Directory.Exists(moduleDirectory))
            fileSystem.Directory.CreateDirectory(moduleDirectory);
#endif

        return !fileSystem.Directory.Exists(moduleDirectory)
            ? throw new DirectoryNotFoundException($"Can't load modules from {moduleDirectory}")
            : fileSystem.Directory.GetFiles(moduleDirectory, "*.deps.json", SearchOption.AllDirectories)
                .Select(ModuleHelpers.DepsJsonToDll)
                .ToArray();
    }

    public static string GetRootedPath(this IFileSystem fileSystem, string? path)
    {
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("path is null or empty");

        if (path.StartsWith('.'))
        {
            var directory = fileSystem.Directory.GetCurrentDirectory();
            var rooted = fileSystem.Path.Combine(directory, TrimPath(fileSystem, path));
            if (fileSystem.Directory.Exists(rooted))
                return rooted;

            throw new DirectoryNotFoundException(rooted);
        }

        if (!fileSystem.Path.IsPathRooted(path) || !fileSystem.Path.IsPathFullyQualified(path))
        {
            // get path of executable not the working directory
            var location = Assembly.GetExecutingAssembly().Location;
            var folder = fileSystem.Path.GetDirectoryName(location) ?? throw new DirectoryNotFoundException(location);

            // transform relative repository path to absolute path
            return fileSystem.Path.Combine(folder, TrimPath(fileSystem, path));
        }

        return path;

        static string TrimPath(IFileSystem fileSystem, string pathToTrim)
            => pathToTrim.TrimStart('.').TrimStart(fileSystem.Path.DirectorySeparatorChar);
    }

    private static string GetRepositoryRootPath(this IFileSystem fileSystem)
    {
        if (HostMetadataCache.IsRunningInContainer)
            return "/src";

        for (var directory = fileSystem.DirectoryInfo.New(Environment.CurrentDirectory); directory.Parent is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles(".root-marker").Any())
            {
                return directory.FullName;
            }
        }

        return string.Empty;
    }
}
