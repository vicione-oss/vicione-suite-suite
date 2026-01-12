using Core.Module.Contracts;
using Core.Module.Extensions;
using Sdk.Modules;

namespace Core.Module;

/// <summary>
/// if we could find a base for IUiModuleBundle|ModuleBundle we could get rid of Sdk.Backend here
/// </summary>
internal static class ModuleAssemblyLoader
{
    public static ModuleBundleLoadResult<ModuleBundle<TModule>> LoadBackendModuleBundles<TModule>(SuiteDependencyContext suiteContext, Type? coreBackendModule = null) where TModule : IModule
    {
        // system bundle is always needed
        var result = new ModuleBundleLoadResult<ModuleBundle<TModule>>();

        // first we need to take core.os module (already loaded)!! 
        if (coreBackendModule is not null)
            result.Bundles.Add(ModuleBundleFactory.CreateInternalModuleFromType<TModule>(coreBackendModule));

        // second ui host to ensure right load order
        if (suiteContext.UiHosts.Count == 1)
            AddModuleBundle(result, suiteContext, suiteContext.UiHosts[0].AssemblyPath);

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
            result.ErrorDlls.Add(moduleDllFile, e);
        }
    }
}
