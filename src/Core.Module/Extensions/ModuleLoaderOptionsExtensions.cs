using System.IO.Abstractions;
using Core.Module.Options;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Module.Extensions;

internal static class ModuleLoaderOptionsExtensions
{
    extension(ModuleLoaderOptions options)
    {
        public bool AllowInclude(string assemblyPath, bool debugOnly = false)
        {
            Matcher callMatcher = new();

            // Adds the excludes from the options.
            var patterns = options.ExcludedPathParts.Select(part => $"**/{part}/**").ToArray();
            callMatcher.AddExcludePatterns(patterns);

            if (debugOnly)
            {
                // A debug module has to carry "Debug" in its path.
                callMatcher.AddInclude("**/Debug/**");
            }
            else
            {
                // At least one include has to match.
                callMatcher.AddInclude("**/*.dll");
            }

            // The path is rooted, but not necessarily normalized.
            var root = Path.IsPathRooted(assemblyPath) ? Path.GetPathRoot(assemblyPath)! : "\\";

            return callMatcher.Match(root, assemblyPath).HasMatches;
        }

        public bool IsValidModuleLocation(IFileSystem fileSystem, string assemblyPath)
        {
            var modulesPath = fileSystem.GetRootedPath(options.ModulesPath);
            if (assemblyPath.StartsWith(modulesPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"You attempt to debug a deployed module '{assemblyPath}'.");

            return true;
        }
    }
}
