using Core.Module.Contracts;
using Core.Module.Extensions;
using Sdk.Modules;

namespace Core.Module;

/// <summary>
/// if we could find a base for IUiModuleBundle|ModuleBundle we could get rid of Sdk.Backend here
/// </summary>
internal static class ModuleAssemblyLoader
{
    public static ModuleBundleLoadResult<ModuleBundle<TModule>> LoadBackendModuleBundles<TModule>(SuiteDependencyContext suiteContext) where TModule : IModule
    {
        // system bundle is always needed
        var result = new ModuleBundleLoadResult<ModuleBundle<TModule>>();

        // second ui host to ensure right load order
        if (suiteContext.UiHost is not null)
            AddModuleBundle(result, suiteContext, suiteContext.UiHost.AssemblyPath);

        // now load all the configured backend modules
        AddModuleBundles(result, suiteContext);

        return result;
    }

    private static void AddModuleBundles<TModule>(ModuleBundleLoadResult<ModuleBundle<TModule>> result, SuiteDependencyContext suiteContext) where TModule : IModule
    {
        var moduleDllFiles = suiteContext.GetBackendModulesAssemblyPaths().ToArray();

        foreach (var moduleDllFile in moduleDllFiles)
        {
            AddModuleBundle(result, suiteContext, moduleDllFile);
        }
    }

    private static void AddModuleBundle<TModule>(ModuleBundleLoadResult<ModuleBundle<TModule>> result, SuiteDependencyContext suiteContext, string moduleDllFile) where TModule : IModule
    {
        try
        {
            // if we detected issues on sdk mismatch or missing options we skip loading the module
            // the issues are kept with the context StartupErrors            
            if (suiteContext.IsInvalidModule(moduleDllFile))
                return;

            // https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/default-probing
            var bundle = ModuleBundleFactory.CreateModuleFromFile<TModule>(suiteContext, moduleDllFile);

            if (result.Bundles.Any(k => Equals(k.Module.ModuleKey, bundle.Module.ModuleKey)))
                throw new InvalidOperationException($"Module from '{moduleDllFile}' is already loaded.");

            result.Bundles.Add(bundle);
        }
        catch (Exception e)
        {
            // if we have an error loading the module, we want to keep track of it in the context.
            // Context is used to identify module dependency issues before loading the assembly
            var moduleContext = (suiteContext.UiHost?.AssemblyPath == moduleDllFile ? suiteContext.UiHost : null)
                                ?? suiteContext.Modules.FirstOrDefault(k => k.AssemblyPath == moduleDllFile);

            if (moduleContext is not null)
                moduleContext.StartupErrors.Add(e);
            else
                throw;
        }
    }
}
