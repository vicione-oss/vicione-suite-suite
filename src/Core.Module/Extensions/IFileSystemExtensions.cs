using System.IO.Abstractions;
using System.Reflection;
using Core.Module.Options;
using Core.Module.Utils;
using MassTransit.Metadata;

namespace Core.Module.Extensions;

public static class IFileSystemExtensions
{
    extension(IFileSystem fileSystem)
    {
        public IEnumerable<string> GetUiHostModuleAssemblyPaths(string? searchPath, string assemblyName)
            => fileSystem.GetModuleDlls(searchPath).Where(path => path.Contains(assemblyName, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<string> GetDebugUiHostModuleAssemblyPaths(List<string>? searchPaths, string assemblyName)
            => fileSystem.GetDebugModuleAssemblyPaths(searchPaths, false, assemblyName);

        public IEnumerable<string> GetBackendModuleAssemblyPaths(string? searchPath)
            => fileSystem.GetModuleDlls(searchPath).Where(path => fileSystem.DoesNotContain(path, Constants.ModuleSuffixClient));

        public IEnumerable<string> GetDebugBackendModuleAssemblyPaths(List<string>? searchPaths)
            => fileSystem.GetDebugModuleAssemblyPaths(searchPaths, false);

        public IEnumerable<string> GetUiModuleAssemblyPaths(string? searchPath, string uiHostDepsJsonPath)
            => fileSystem.GetModuleDlls(searchPath).Where(path => fileSystem.DoesNotContain(path, Constants.ModuleSuffixBackend));

        public IEnumerable<string> GetDebugClientModuleAssemblyPaths(List<string>? searchPaths)
            => fileSystem.GetDebugModuleAssemblyPaths(searchPaths, true);

        private bool DoesNotContain(string? path, string contain)
            => fileSystem.Path.GetFileNameWithoutExtension(path)?.Contains(contain, StringComparison.OrdinalIgnoreCase) == false;

        private List<string> GetDebugModuleAssemblyPaths(List<string>? searchPaths, bool clientSide, string? assemblyName = null)
        {
            if (searchPaths is null)
                return [];

            var modulePaths = new List<string>();

            foreach (var path in searchPaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

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
        private string[] GetModuleDlls(string? searchPath)
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

        public string GetRootedPath(string? path)
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

        private string GetRepositoryRootPath()
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

        public async Task<Dictionary<string, string>> GetDebugModuleVersions(ModuleLoaderOptions loaderOptions, CancellationToken cancellationToken)
        {
            var moduleVersions = new Dictionary<string, string>();

            // Valid debug module paths
            var modulePaths = fileSystem.GetDebugBackendModuleAssemblyPaths(loaderOptions.ModuleDebugPaths)
                .Where(p => loaderOptions.IsValidModuleLocation(fileSystem, p))
                .Where(p => loaderOptions.AllowInclude(p, true));

            foreach (var modulePath in modulePaths)
            {
                var moduleFolder = fileSystem.Path.GetDirectoryName(modulePath);
                if (string.IsNullOrEmpty(moduleFolder))
                    continue;

                var moduleName = fileSystem.Path.GetFileNameWithoutExtension(modulePath);
                if (!ModuleHelpers.TryGetFamilyNamePart(moduleName, out var moduleId))
                    continue;

                var repoRoot = fileSystem.GetPathContaining(moduleFolder, ["readme.md", "changelog.md"], "*.md");
                if (string.IsNullOrEmpty(repoRoot))
                    continue;

                var versionFile = fileSystem.Path.Combine(repoRoot, "VERSION");
                if (!fileSystem.Path.Exists(versionFile))
                    continue;

                // The VERSION file might contain linebreaks etc. We'll filter them out
                var version = await fileSystem.File.ReadAllTextAsync(versionFile, cancellationToken);
                var cleaned = new string([.. version.Where(c => !char.IsControl(c) && (char.IsLetterOrDigit(c) || char.IsPunctuation(c)))]);

                moduleVersions.TryAdd(moduleId, cleaned);
            }

            return moduleVersions;
        }

        private string GetPathContaining(string startPath, IList<string> fileNames, string fileFilter)
        {
            for (var directory = fileSystem.DirectoryInfo.New(startPath); directory.Parent is not null; directory = directory.Parent)
            {
                if (directory.EnumerateFiles(fileFilter)
                    .Any(x => fileNames.Any(fn => x.Name.Equals(fn, StringComparison.OrdinalIgnoreCase))))
                {
                    return directory.FullName;
                }
            }

            return string.Empty;
        }
    }
}
