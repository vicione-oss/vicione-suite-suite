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
                // ensure modules get ordered by their dependencies
                .OrderDescending(new ModuleDependencyContextComparer(suiteContext))
                .Select(k => k.AssemblyPath);

        internal IEnumerable<ModulePathInfo> GetUiModulesAssemblyPathInfos()
            => suiteContext.Modules
                .Where(k => k.ModuleType == ModuleType.Client)
                // relevant for client only modules with referenced public lib
                .OrderDescending(new ModuleDependencyContextComparer(suiteContext))
                .Select(k => new ModulePathInfo(k.AssemblyPath, k.IsDebugSource));

        internal Dictionary<string, string> GetAllUiRuntimeDependencies(Func<string, bool>? isGoodAssembly)
        {
            // the problem -> in published version the deployed assemblies are available as their runtime dependencies describe it
            // but in debug the ms libraries come from default paths that needs to be resolved differently
            var uiAssemblies = new List<string>();
            var uiContexts = suiteContext.Modules.Where(k => k.ModuleType == ModuleType.Client);

            // to pack
            foreach (var moduleContext in uiContexts)
            {
                var moduleRuntimeAssemblyPaths = moduleContext.RuntimeLibraries
                    .Where(k => isGoodAssembly?.Invoke(k.Name) ?? true)
                    .Select(k => suiteContext.GetContextAssemblyPath(k.Name, k.Version))
                    .Where(k => !string.IsNullOrEmpty(k) && File.Exists(k));

                uiAssemblies.AddRange(moduleRuntimeAssemblyPaths!);

                // there a indirect dependencies that come with core.os like MQTTNet these we need to take from core.os and find
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
                    // on debug these assemblies are not available in the debug folders but on
                    // ms cache etc. therefore we try to take get it's location from loaded assembly
                    var loadContext = AssemblyLoadContext.All.FirstOrDefault(k
                        => k.Assemblies.Any(a => a.GetName().Name == rtDependency.Name));

                    var assembly = loadContext?.Assemblies.FirstOrDefault(a => a.GetName().Name == rtDependency.Name);
                    if (assembly is not null && !string.IsNullOrEmpty(assembly.Location))
                        uiAssemblies.Add(assembly.Location);
                    // todo: maybe other ways..?
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

            // only one host can be loaded so only one can provide assemblies
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
