using System.Reflection;
using Core.Module;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.UiHosting;
using Sdk.Backend.Modules;
using Sdk.Modules;

namespace Core.OS.Modules.Services;

internal sealed class UiHostEnvironment(SuiteDependencyContext suiteContext, IModuleHost moduleHost) : IUiHostEnvironment
{
    private readonly SuiteDependencyContext _suiteContext = suiteContext;
    private readonly IModuleHost _moduleHost = moduleHost;

    public bool IsDevelopment { get; init; }
    public string? ModulePath { get; set; }

    public BackendModule? GetBackendModule(string moduleId)
        => _moduleHost.GetModules().FirstOrDefault(m => m.ModuleKey.ModuleId == moduleId && m is BackendModule) as BackendModule;

    public string? GetWwwRootFolder()
    {
        if (_suiteContext.UiHost is null)
            return null;

        var moduleFolder = Path.GetDirectoryName(_suiteContext.UiHost.AssemblyPath);
        if (string.IsNullOrEmpty(moduleFolder))
            throw new InvalidOperationException($"Failed to get folder of {_suiteContext.UiHost.AssemblyPath}");

        // in debug mode the debug folder is the wwwroot because there's the staticwebassets.json
        if (IsDevelopment)
            return moduleFolder;

        return Path.Combine(moduleFolder, Constants.WwwRoot);
    }

    public Dictionary<ModulePathInfo, string> GetModulesContentPathInfos()
    {
        var dic = new Dictionary<ModulePathInfo, string>();

        foreach (var pathInfo in _suiteContext.GetUiModulesAssemblyPathInfos())
        {
            var assemblyDirectory = Path.GetDirectoryName(pathInfo.AssemblyPath);
            if (!pathInfo.IsDebugSource)
            {
                var possibleWwwRoot = Path.Combine(assemblyDirectory!, Constants.WwwRoot);
                if (!Directory.Exists(possibleWwwRoot))
                    continue;

                dic.Add(pathInfo, possibleWwwRoot);
                continue;
            }

            dic.Add(pathInfo, assemblyDirectory!);
        }

        return dic;
    }

    public IEnumerable<ModuleMetadata> GetModuleMetadata()
    {
        return _suiteContext.Modules
            .Select(CreateInfo)
            .OrderBy(m => m.Name);

        static ModuleMetadata CreateInfo(ModuleDependencyContext context)
        {
            var rt = context.GetMainLibrary();
            var sdkVersion = context.GetSdkVersion();

            return new ModuleMetadata()
            {
                Name = context.AssemblyName,
                Version = rt.Version,
                MinSuiteSdkVersion = sdkVersion
            };
        }
    }

    public IEnumerable<IUiModuleBundle> LoadModuleBundles(Func<string, Assembly, IUiModuleBundle?> createBundle)
    {
        var result = new ModuleBundleLoadResult<IUiModuleBundle>();

        var pathInfos = _suiteContext.GetUiModulesAssemblyPathInfos()
            .Select(k => k.AssemblyPath)
            .ToArray();

        AddUiModuleBundles(result, pathInfos, _suiteContext, createBundle);

        return result.Bundles;
    }

    private static void AddUiModuleBundles(ModuleBundleLoadResult<IUiModuleBundle> result,
        string[] moduleDllFiles,
        SuiteDependencyContext suiteContext,
        Func<string, Assembly, IUiModuleBundle?> createBundle)
    {
        foreach (var moduleDllFile in moduleDllFiles)
        {
            AddUiModuleBundle(result, moduleDllFile, suiteContext, createBundle);
        }
    }

    private static void AddUiModuleBundle(ModuleBundleLoadResult<IUiModuleBundle> result,
        string moduleDllFile,
        SuiteDependencyContext suiteContext,
        Func<string, Assembly, IUiModuleBundle?> createBundle)
    {
        try
        {
            // if we detected issues on sdk mismatch or missing options we skip loading the module
            if (suiteContext.IsInvalidModule(moduleDllFile))
            {
                return;
            }

            var context = ModuleAssemblyLoadContext.Create(suiteContext, moduleDllFile);
            var assembly = context.GetMainAssembly();
            var bundle = createBundle(moduleDllFile, assembly) ??
                throw new InvalidOperationException("Failed to create ui bundle");

            if (result.Bundles.Any(k => Equals(k.Module.ModuleKey, bundle.Module.ModuleKey)))
            {
                return;
            }

            result.Bundles.Add(bundle);
        }
        catch (Exception e)
        {
            suiteContext.Modules.First(k => k.AssemblyPath == moduleDllFile).StartupErrors.Add(e);
        }
    }
}
