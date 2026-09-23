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

        // Every runtime library provided by core or a uihost.
        foreach (var library in GetSuiteModels(context, fileSystem, options))
        {
            Console.WriteLine(options.Version ? $"{library.FileName}:{library.Version}" : library.FileName);
        }

        // Native libraries Core.OS ships on linux.
        foreach (var library in NativeExtraLibs)
        {
            // https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading#library-name-variations
            Console.WriteLine($"lib{library}");
            Console.WriteLine($"lib{library}.so");
            Console.WriteLine($"{library}.so");
            Console.WriteLine($"{library}.dll");
        }

        // Asset libraries a uihost provides.
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

        // The sdk version the suite itself uses.
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

        // Only core and blazor.server need checking.
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
    /// Joins the core and uihost runtime libraries with the framework assemblies the suite ships.
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
    /// Joins the core and uihost runtime libraries.
    /// </summary>
    private static IEnumerable<FileVersionModel> GetRuntimeLibraryModels(SuiteDependencyContext context)
    {
        var uiHostLibraries = context.UiHost?.RuntimeLibraries ?? [];

        // Dll reference files are not of interest.
        var coreAndHostLibraries = context.Core.RuntimeLibraries.Union(uiHostLibraries)
            .Where(k => k.Type != "reference")
            .DistinctBy(k => k.Name);

        // The runtime libraries in deps.json do not match the published assemblies: the ms libs come
        // from publishing with --self-contained.
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
    /// Framework assemblies the suite ships, because it is published self-contained.
    /// </summary>
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
