using System.IO.Abstractions;
using Core.Module.Utils;
using Core.UiHosting;
using Microsoft.Extensions.DependencyModel;
using Sdk.Modules;

namespace Core.Module;

internal static class ModuleDependencyContextFactory
{
    private sealed record ModuleDependencyContextOptions(ModuleType ContextType, bool UseRuntimeMode = true)
    {
        public Func<string, bool>? AssetFilter { get; init; }
    }

    public static ModuleDependencyContext CreateCoreContext(IFileSystem fileSystem, string coreDepsJson, bool useRuntimeContext = true)
    {
        var coreOptions = new ModuleDependencyContextOptions(ModuleType.Backend, useRuntimeContext);

        return CreateModuleDependencyContext(fileSystem, coreOptions, new ModulePathInfo(coreDepsJson));
    }

    public static ModuleDependencyContext[] CreateUiHostContexts(IFileSystem fileSystem, List<ModulePathInfo> hostModulePaths, bool useRuntimeContext = true)
    {
        if (hostModulePaths.Count == 0)
            return [];

        var uiHostOptions = new ModuleDependencyContextOptions(ModuleType.Backend, useRuntimeContext);

        return hostModulePaths
            .Select(f => CreateModuleDependencyContext(fileSystem, uiHostOptions, f))
            .ToArray();
    }

    public static ModuleDependencyContext[] CreateBackendModuleContexts(IFileSystem fileSystem, List<ModulePathInfo> modulePaths,
        ModuleDependencyContext[] uiHostContexts, bool useRuntimeContext = true)
    {
        var backendOptions = new ModuleDependencyContextOptions(ModuleType.Backend, useRuntimeContext)
        {
            AssetFilter = asset => IsAssetProvidedByAllUiHosts(uiHostContexts, asset)
        };

        return CreateModuleDependencyContexts(fileSystem, backendOptions, modulePaths).ToArray();
    }

    public static ModuleDependencyContext[] CreateUiModuleContexts(IFileSystem fileSystem, List<ModulePathInfo> modulePaths,
        ModuleDependencyContext[] uiHostContexts, bool useRuntimeContext = true)
    {
        var frontendOptions = new ModuleDependencyContextOptions(ModuleType.Client, useRuntimeContext)
        {
            AssetFilter = asset => IsAssetProvidedByAllUiHosts(uiHostContexts, asset)
        };
        return CreateModuleDependencyContexts(fileSystem, frontendOptions, modulePaths).ToArray();
    }

    private static bool IsAssetProvidedByAllUiHosts(ModuleDependencyContext[] uiHostContexts, string name)
        => uiHostContexts.All(k => k.RuntimeAssets.Any(rtl => rtl.Name == name));

    private static IEnumerable<ModuleDependencyContext> CreateModuleDependencyContexts(IFileSystem fileSystem, ModuleDependencyContextOptions options,
        IEnumerable<ModulePathInfo> depsJsonFiles)
        => depsJsonFiles.Select(f => CreateModuleDependencyContext(fileSystem, options, f));

    private static ModuleDependencyContext CreateModuleDependencyContext(IFileSystem fileSystem, ModuleDependencyContextOptions options, ModulePathInfo info)
    {
        var moduleDepsJson = ModuleHelpers.DllToDepsJson(info.AssemblyPath);
        using var reader = new DependencyContextJsonReader();
        using var file = fileSystem.File.OpenRead(moduleDepsJson);
        var context = reader.Read(file);
        var module = new ModuleDependencyContext(options.ContextType, moduleDepsJson, info.IsDebugSource);

        // The libraries resolvable for this deps.json.
        // backend and ui deployed into the same folder so we'll have same file referenced by two deps.json

        module.AddRuntimeLibraries(context);

        // Used for the components under wwwroot/_content.
        var assets = GetAssetLibraries(moduleDepsJson).ToArray();
        module.RuntimeAssets.AddRange(assets.Where(a => IsGoodAsset(a.Name)));

        if (!options.UseRuntimeMode)
        {
            module.RedundantAssets.AddRange(assets.Where(a => !IsGoodAsset(a.Name)));
        }

        return module;

        bool IsGoodAsset(string arg)
            => options.AssetFilter is null || !options.AssetFilter.Invoke(arg);
    }

    private static IEnumerable<AssetLibrary> GetAssetLibraries(string depsJsonFile)
    {
        var directory = Path.GetDirectoryName(depsJsonFile);
        if (string.IsNullOrEmpty(directory))
            return [];

        var wwwContent = Path.Combine([directory, Constants.WwwRoot, "_content"]);
        if (!Directory.Exists(wwwContent))
            return [];

        return Directory
            .GetDirectories(wwwContent)
            .Select(k => new AssetLibrary(Path.GetFileName(k), k));
    }
}
