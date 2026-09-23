using Core.Module.Contracts;
using Core.Module.Extensions;
using Sdk.Modules;

namespace Core.Module;

/// <summary>
/// A common base for IUiModuleBundle and ModuleBundle would remove the Sdk.Backend dependency here.
/// </summary>
internal static class ModuleAssemblyLoader
{
    public static ModuleBundleLoadResult<ModuleBundle<TModule>> LoadBackendModuleBundles<TModule>(SuiteDependencyContext suiteContext) where TModule : IModule
    {
        // The system bundle is always needed.
        var result = new ModuleBundleLoadResult<ModuleBundle<TModule>>();

        // The ui host comes second to keep the load order correct.
        if (suiteContext.UiHost is not null)
            AddModuleBundle(result, suiteContext, suiteContext.UiHost.AssemblyPath);

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
            // A module with an sdk mismatch or missing options is skipped; the reasons stay in the
            // context's StartupErrors.
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
            // The context records load errors, and is what identifies dependency issues before the
            // assembly is loaded.
            var moduleContext = (suiteContext.UiHost?.AssemblyPath == moduleDllFile ? suiteContext.UiHost : null)
                                ?? suiteContext.Modules.FirstOrDefault(k => k.AssemblyPath == moduleDllFile);

            if (moduleContext is not null)
                moduleContext.StartupErrors.Add(e);
            else
                throw;
        }
    }
}
