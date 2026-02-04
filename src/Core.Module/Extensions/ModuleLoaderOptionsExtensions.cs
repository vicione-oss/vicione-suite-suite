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

            // add the excludes from the options
            var patterns = options.ExcludedPathParts.Select(part => $"**/{part}/**").ToArray();
            callMatcher.AddExcludePatterns(patterns);

            if (debugOnly)
            {
                // force the debug module to contain "Debug" within its path - could be extended once
                callMatcher.AddInclude("**/Debug/**");
            }
            else
            {
                // we need at least one include to match
                callMatcher.AddInclude("**/*.dll");
            }

            // the path is rooted but...
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
