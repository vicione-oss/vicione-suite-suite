using System.Reflection;
using System.Runtime.Loader;
using Core.UiHosting;
using Sdk.Modules;

namespace Core.Module.Extensions;

public static partial class SuiteDependencyContextExtensions
{
    extension(SuiteDependencyContext suiteContext)
    {
        internal void ClearRedundancy()
        {
            ClearContextRedundancy(suiteContext.Core);

            if (suiteContext.UiHost is not null)
                ClearContextRedundancy(suiteContext.UiHost);

            foreach (var moduleContext in suiteContext.Modules)
                ClearContextRedundancy(moduleContext);

            static void ClearContextRedundancy(ModuleDependencyContext context)
            {
                context.RedundantAssets.Clear();
                context.RedundantLibraries.Clear();
            }
        }

        private IEnumerable<ModuleDependencyContext> GetAllContexts()
        {
            yield return suiteContext.Core;

            if (suiteContext.UiHost is not null)
                yield return suiteContext.UiHost;

            foreach (var module in suiteContext.Modules)
                yield return module;
        }

        internal ModuleDependencyContext GetContextByName(string assemblyName)
        {
            if (suiteContext.Core.AssemblyName == assemblyName)
                return suiteContext.Core;

            if (suiteContext.UiHost?.AssemblyName == assemblyName)
                return suiteContext.UiHost;

            return suiteContext.Modules.First(k => k.AssemblyName == assemblyName);
        }

        public IEnumerable<string> GetAllRedundantPaths(Func<string, bool>? filter = null)
            => suiteContext.Modules
                .SelectMany(mc => mc.GetRedundantLibraryPaths().Concat(mc.RedundantAssets.Select(k => k.Path)))
                .Where(k => filter is null || filter.Invoke(k))
                .Distinct()
                .Order();

        internal IEnumerable<string> GetBackendModulesAssemblyPaths()
            => suiteContext.Modules
                .Where(k => k.ModuleType == ModuleType.Backend)
                // Ordered by dependency so a module loads after what it depends on.
                .OrderDescending(new ModuleDependencyContextComparer(suiteContext))
                .Select(k => k.AssemblyPath);

        internal IEnumerable<ModulePathInfo> GetUiModulesAssemblyPathInfos()
            => suiteContext.Modules
                .Where(k => k.ModuleType == ModuleType.Client)
                // Matters for client-only modules that reference a public lib.
                .OrderDescending(new ModuleDependencyContextComparer(suiteContext))
                .Select(k => new ModulePathInfo(k.AssemblyPath, k.IsDebugSource));

        internal Dictionary<string, string> GetAllUiRuntimeDependencies(Func<string, bool>? isGoodAssembly)
        {
            // A published deployment provides assemblies exactly as their runtime dependencies describe,
            // but in debug the ms libraries come from default paths and need resolving differently.
            var uiAssemblies = new List<string>();
            var uiContexts = suiteContext.Modules.Where(k => k.ModuleType == ModuleType.Client);

            foreach (var moduleContext in uiContexts)
            {
                var moduleRuntimeAssemblyPaths = moduleContext.RuntimeLibraries
                    .Where(k => isGoodAssembly?.Invoke(k.Name) ?? true)
                    .Select(k => suiteContext.GetContextAssemblyPath(k.Name, k.Version))
                    .Where(k => !string.IsNullOrEmpty(k) && File.Exists(k));

                uiAssemblies.AddRange(moduleRuntimeAssemblyPaths!);

                // Indirect dependencies shipped with Core.OS, such as MQTTNet, are taken from there.
                foreach (var rtDependency in moduleContext.RuntimeLibraries.SelectMany(k => k.Dependencies))
                {
                    if (isGoodAssembly is not null && !isGoodAssembly.Invoke(rtDependency.Name))
                        continue;

                    var assemblyPath = suiteContext.GetContextAssemblyPath(rtDependency.Name, rtDependency.Version);
                    if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                    {
                        uiAssemblies.Add(assemblyPath);
                        continue;
                    }

#if DEBUG
                    // In a debug build these live in the ms cache rather than the debug folders, so the
                    // location is taken from the already loaded assembly.
                    var loadContext = AssemblyLoadContext.All.FirstOrDefault(k
                        => k.Assemblies.Any(a => a.GetName().Name == rtDependency.Name));

                    var assembly = loadContext?.Assemblies.FirstOrDefault(a => a.GetName().Name == rtDependency.Name);
                    if (assembly is not null && !string.IsNullOrEmpty(assembly.Location))
                        uiAssemblies.Add(assembly.Location);
#endif
                }
            }

            return uiAssemblies
                .Where(k => !k.Contains(".runtime.", StringComparison.Ordinal))
                .DistinctBy(Path.GetFileName)
                .ToDictionary(f => Path.GetFileName(f) ?? throw new InvalidOperationException(), f => f);
        }

        private string? GetContextAssemblyPath(string name, string version)
        {
            var coreAssemblyPath = suiteContext.GetCoreContextRuntimeLibraryPath(name, version);
            if (!string.IsNullOrEmpty(coreAssemblyPath))
                return coreAssemblyPath;

            foreach (var moduleContext in suiteContext.Modules)
            {
                if (moduleContext.IsGoodAssembly(name, version, out var assemblyPath))
                    return assemblyPath;
            }

            return null;
        }

        public string? GetCoreContextRuntimeLibraryPath(string name, string version)
        {
            if (suiteContext.Core.IsGoodAssembly(name, version, out var coreAssemblyPath))
                return coreAssemblyPath;

            // Only one host is ever loaded, so only one can provide assemblies.
            if (suiteContext.UiHost is not null && suiteContext.UiHost.IsGoodAssembly(name, version, out var uiAssemblyPath))
                return uiAssemblyPath;

            return null;
        }

        public ModuleDependencyContext? ResolveModuleContext(AssemblyName assemblyName)
        {
            if (assemblyName.Name is null)
                return null;

            foreach (var moduleContext in suiteContext.Modules)
            {
                var rt = moduleContext.GetRuntimeLibrary(assemblyName.Name, assemblyName.Version);
                if (rt != null)
                    return moduleContext;
            }

            return null;
        }
    }
}
