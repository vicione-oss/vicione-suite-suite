using System.Reflection;
using System.Runtime.Loader;
using Core.UiHosting;
using Sdk.Modules;

namespace Core.Module.Extensions;

public static partial class SuiteDependencyContextExtensions
{
    internal static void ClearRedundancy(this SuiteDependencyContext suiteContext)
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

    private static IEnumerable<ModuleDependencyContext> GetAllContexts(this SuiteDependencyContext context)
    {
        yield return context.Core;

        if (context.UiHost is not null)
            yield return context.UiHost;

        foreach (var module in context.Modules)
            yield return module;
    }

    internal static ModuleDependencyContext GetContextByName(this SuiteDependencyContext suiteContext, string assemblyName)
    {
        if (suiteContext.Core.AssemblyName == assemblyName)
            return suiteContext.Core;

        if (suiteContext.UiHost?.AssemblyName == assemblyName)
            return suiteContext.UiHost;

        return suiteContext.Modules.First(k => k.AssemblyName == assemblyName);
    }

    public static IEnumerable<string> GetAllRedundantPaths(this SuiteDependencyContext suiteContext, Func<string, bool>? filter = null)
        => suiteContext.Modules
            .SelectMany(mc => mc.GetRedundantLibraryPaths().Concat(mc.RedundantAssets.Select(k => k.Path)))
            .Where(k => filter is null || filter.Invoke(k))
            .Distinct()
            .Order();

    internal static IEnumerable<string> GetBackendModulesAssemblyPaths(this SuiteDependencyContext suiteContext)
        => suiteContext.Modules
            .Where(k => k.ModuleType == ModuleType.Backend)
            // ensure modules get ordered by their dependencies
            .OrderDescending(new ModuleDependencyContextComparer(suiteContext))
            .Select(k => k.AssemblyPath);

    internal static IEnumerable<ModulePathInfo> GetUiModulesAssemblyPathInfos(this SuiteDependencyContext suiteContext)
        => suiteContext.Modules
            .Where(k => k.ModuleType == ModuleType.Client)
            // relevant for client only modules with referenced public lib
            .OrderDescending(new ModuleDependencyContextComparer(suiteContext))
            .Select(k => new ModulePathInfo(k.AssemblyPath, k.IsDebugSource));

    internal static Dictionary<string, string> GetAllUiRuntimeDependencies(this SuiteDependencyContext context, Func<string, bool>? isGoodAssembly)
    {
        // the problem -> in published version the deployed assemblies are available as their runtime dependencies describe it
        // but in debug the ms libraries come from default paths that needs to be resolved differently
        var uiAssemblies = new List<string>();
        var uiContexts = context.Modules.Where(k => k.ModuleType == ModuleType.Client);

        // to pack
        foreach (var moduleContext in uiContexts)
        {
            var moduleRuntimeAssemblyPaths = moduleContext.RuntimeLibraries
                .Where(k => isGoodAssembly?.Invoke(k.Name) ?? true)
                .Select(k => context.GetContextAssemblyPath(k.Name, k.Version))
                .Where(k => !string.IsNullOrEmpty(k) && File.Exists(k));

            uiAssemblies.AddRange(moduleRuntimeAssemblyPaths!);

            // there a indirect dependencies that come with core.os like MQTTNet these we need to take from core.os and find
            foreach (var rtDependency in moduleContext.RuntimeLibraries.SelectMany(k => k.Dependencies))
            {
                if (isGoodAssembly is not null && !isGoodAssembly.Invoke(rtDependency.Name))
                    continue;

                var assemblyPath = context.GetContextAssemblyPath(rtDependency.Name, rtDependency.Version);
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

    private static string? GetContextAssemblyPath(this SuiteDependencyContext context, string name, string version)
    {
        var coreAssemblyPath = context.GetCoreContextRuntimeLibraryPath(name, version);
        if (!string.IsNullOrEmpty(coreAssemblyPath))
            return coreAssemblyPath;

        foreach (var moduleContext in context.Modules)
        {
            if (moduleContext.IsGoodAssembly(name, version, out var assemblyPath))
                return assemblyPath;
        }

        return null;
    }

    public static string? GetCoreContextRuntimeLibraryPath(this SuiteDependencyContext context, string name, string version)
    {
        if (context.Core.IsGoodAssembly(name, version, out var coreAssemblyPath))
            return coreAssemblyPath;

        // only one host can be loaded so only one can provide assemblies
        if (context.UiHost is not null && context.UiHost.IsGoodAssembly(name, version, out var uiAssemblyPath))
            return uiAssemblyPath;

        return null;
    }

    public static ModuleDependencyContext? ResolveModuleContext(this SuiteDependencyContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name is null)
            return null;

        foreach (var moduleContext in context.Modules)
        {
            var rt = moduleContext.GetRuntimeLibrary(assemblyName.Name, assemblyName.Version);
            if (rt != null)
                return moduleContext;
        }

        return null;
    }
}
