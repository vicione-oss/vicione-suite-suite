using System.Reflection;
using Core.Module.Contracts;
using Sdk.Extensions;
using Sdk.Modules;

namespace Core.Module;

internal static class ModuleBundleFactory
{
    public static ModuleBundle<TModule> CreateModuleFromFile<TModule>(SuiteDependencyContext suiteContext, string moduleDllFile) where TModule : IModule
        => TryCreateModuleForTestAssembly<TModule>(moduleDllFile) ?? CreateModuleWithLoadContext<TModule>(suiteContext, moduleDllFile);

    private static ModuleBundle<TModule>? TryCreateModuleForTestAssembly<TModule>(string moduleDllFile) where TModule : IModule
    {
        if (Environment.CurrentDirectory.Split(Path.DirectorySeparatorChar).Contains("tests", StringComparer.OrdinalIgnoreCase))
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(
                    a => !a.IsDynamic && string.Equals(Path.GetFileName(a.Location), Path.GetFileName(moduleDllFile), StringComparison.OrdinalIgnoreCase));
            if (assembly is not null)
            {
                var module = assembly.GetFirstInstance<TModule>() ??
                    throw new InvalidOperationException($"Missing implementation of {nameof(TModule)} in {assembly.FullName}");

                return new ModuleBundle<TModule>(module, assembly, moduleDllFile);
            }
        }

        return null;
    }

    private static ModuleBundle<TModule> CreateModuleWithLoadContext<TModule>(SuiteDependencyContext suiteContext, string moduleDllFile) where TModule : IModule
    {
        var context = ModuleAssemblyLoadContext.Create(suiteContext, moduleDllFile);
        var assembly = context.GetMainAssembly();
        var assemblyPath = context.GetMainAssemblyPath();
        var module = assembly.GetFirstInstance<TModule>();

        return module is null
            ? throw new InvalidOperationException($"No Backend module found in {assembly.FullName}")
            : new ModuleBundle<TModule>(module, assembly, assemblyPath);
    }

    public static ModuleBundle<TModule> CreateInternalModuleFromType<TModule>(Type moduleType) where TModule : IModule
    {
        var assembly = Assembly.GetAssembly(moduleType) ??
            throw new InvalidOperationException($"Internal module {moduleType.Name} can't be loaded");

        var module = assembly.GetFirstInstance<TModule>() ??
            throw new InvalidOperationException($"Missing implementation of {nameof(TModule)} in {assembly.FullName}");

        return new ModuleBundle<TModule>(module, assembly, assembly.Location);
    }
}
