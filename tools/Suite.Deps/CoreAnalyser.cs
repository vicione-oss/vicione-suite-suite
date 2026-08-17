using System.IO.Abstractions;
using System.Reflection;
using Core.Module;
using Core.Module.Extensions;
using Core.Module.Options;
using Microsoft.Extensions.DependencyModel;
using Sdk.Modules;

namespace Suite.Deps;

internal static class CoreAnalyser
{
    private static readonly string[] NativeExtraLibs = ["e_sqlite3"];

    public static Task Execute(CoreAnalyserOptions options)
    {
        var fileSystem = new FileSystem();
        var context = CreateSuiteDependencyContext(options, fileSystem);

        WriteHeader(context, options);

        // all the runtime libraries provided by core or uihost
        foreach (var library in GetSuiteModels(context, fileSystem, options))
        {
            Console.WriteLine(options.Version ? $"{library.FileName}:{library.Version}" : library.FileName);
        }

        // native libraries shipped by Cores.OS on linux
        foreach (var library in NativeExtraLibs)
        {
            // https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading#library-name-variations
            Console.WriteLine($"lib{library}");
            Console.WriteLine($"lib{library}.so");
            Console.WriteLine($"{library}.so");
            Console.WriteLine($"{library}.dll");
        }

        // asset libraries provided by uihost
        foreach (var assets in GetAssetLibraries(context))
        {
            Console.WriteLine($"/wwwroot/_content/{assets.Name}");
        }

        return Task.CompletedTask;
    }

    private static void WriteHeader(SuiteDependencyContext context, CoreAnalyserOptions options)
    {
        if (!options.Header)
            return;

        var sdkAssembly = Assembly.GetAssembly(typeof(IModule));
        if (sdkAssembly is null)
            throw new MissingMemberException(nameof(sdkAssembly));

        // use sdk version that suite uses
        var sdkAssemblyName = sdkAssembly.GetName();
        var sdkVersion = context.Core.RuntimeLibraries.First(k => k.Name == sdkAssemblyName.Name);

        Console.WriteLine($"#SDK:{sdkVersion.Name}:v{sdkVersion.Version}");
        Console.WriteLine($"#RUNTIMES:{string.Join(',', options.Runtimes)}");
        Console.WriteLine($"#LANGUAGES:{string.Join(',', options.Languages)}");
    }

    private static SuiteDependencyContext CreateSuiteDependencyContext(CoreAnalyserOptions options, IFileSystem fileSystem)
    {
        var rootedSuitePath = fileSystem.GetRootedPath(options.SuitePath);
        var config = ConfigurationUtils.GetSuiteConfiguration(rootedSuitePath, options.Environment);
        var coreDepsJsonFile = ConfigurationUtils.GetCoreDepsJsonFilePath(rootedSuitePath);
        var loaderOptions = ConfigurationUtils.GetSuiteModuleLoaderOptions(config, rootedSuitePath);

        // we only need to check core and blazor.server        
        var moduleOptions = new Dictionary<string, Core.Module.Contracts.ModuleOptions>
        {
            {
                Constants.BlazorServerModuleId, config.GetUiHostOptions(Constants.BlazorServerModuleId) ?? new UiHostOptions()
            },
        };

        var context = new SuiteDependencyContextBuilder()
            .WithCore(coreDepsJsonFile)
            .WithUiHost(loaderOptions, moduleOptions)
            .WithBackendModules(loaderOptions, moduleOptions)
            .WithClientModules(loaderOptions, moduleOptions)
            .WithMappingDisabled()
            .Build(fileSystem);

        if (context.UiHost is null)
            throw new InvalidOperationException("No ui host was found");

        return context;
    }

    /// <summary>
    /// Join core and uihost runtime libraries with framework assemblies shipped with the suite
    /// </summary>    
    private static IEnumerable<FileVersionModel> GetSuiteModels(SuiteDependencyContext context, IFileSystem fileSystem, CoreAnalyserOptions options)
    {
        var coreAndHostLibraries = GetRuntimeLibraryModels(context);

        var frameworkAssemblies = GetFrameworkAssemblies(fileSystem, options)
            .Select(k => new FileVersionModel(k, null));

        return coreAndHostLibraries.Union(frameworkAssemblies)
            .DistinctBy(k => k.FileName)
            .OrderBy(k => k.FileName);
    }

    /// <summary>
    /// Join core and uihost runtime libraries
    /// </summary>   
    private static IEnumerable<FileVersionModel> GetRuntimeLibraryModels(SuiteDependencyContext context)
    {
        var uiHostLibraries = context.UiHost?.RuntimeLibraries ?? [];

        // remove the dll reference files - they are not of interest
        var coreAndHostLibraries = context.Core.RuntimeLibraries.Union(uiHostLibraries)
            .Where(k => k.Type != "reference")
            .DistinctBy(k => k.Name);

        // the runtime libraries defined in deps.json do not match the published assemblies
        // ms libs are added because --self-contained flag is used on dotnet publish
        return coreAndHostLibraries
            .Select(k => new FileVersionModel(GetExistingAssemblyPath(k), k.Version))
            .Where(k => !string.IsNullOrEmpty(k.FileName))
            .DistinctBy(k => k.FileName)
            .OrderBy(k => k.FileName);

        string? GetExistingAssemblyPath(RuntimeLibrary rtl)
        {
            var assemblyPath = context.GetCoreContextRuntimeLibraryPath(rtl.Name, rtl.Version);
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                return Path.GetFileName(assemblyPath);
            }

            var fallbackPath = Path.Combine(context.Core.AssemblyFolder, $"{rtl.Name}.dll");
            if (File.Exists(fallbackPath))
                return Path.GetFileName(fallbackPath);

            return null;
        }
    }

    /// <summary>
    /// Get framework assemblies shipped with the suite because it gets published self-contained
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    private static IEnumerable<string?> GetFrameworkAssemblies(IFileSystem fileSystem, CoreAnalyserOptions options)
    {
        var rootedSuitePath = fileSystem.GetRootedPath(options.SuitePath);
        return Directory.EnumerateFiles(rootedSuitePath, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(IsFrameworkAssembly);

        static bool IsFrameworkAssembly(string? fileName) => !string.IsNullOrWhiteSpace(fileName) &&
                (fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase));
    }

    private record FileVersionModel(string? FileName, string? Version);

    private static IEnumerable<AssetLibrary> GetAssetLibraries(SuiteDependencyContext context)
    {
        var uiHostAssets = context.UiHost?.RuntimeAssets ?? [];

        return context.Core.RuntimeAssets
            .Union(uiHostAssets)
            .DistinctBy(k => k.Name)
            .OrderBy(k => k.Name);
    }
}
