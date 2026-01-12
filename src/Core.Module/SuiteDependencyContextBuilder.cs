using System.IO.Abstractions;
using System.Reflection;
using Core.Module.Extensions;
using Core.Module.Options;
using Core.Module.Utils;
using Core.UiHosting;
using Sdk.Modules;
using ModuleOptions = Core.Module.Contracts.ModuleOptions;

namespace Core.Module;

/// <summary>
/// Builder class to create <see cref="SuiteDependencyContext"/> containing assembly runtime references
/// </summary>
public class SuiteDependencyContextBuilder
{
    private class SuiteDependencyContextOptions
    {
        public string? UiHostDirectory { get; set; }
        public string? UiHostModuleId { get; set; }
        public ModulePathInfo? UiHostPathInfo { get; set; }
        public UiHostOptions? UiHostOptions { get; internal set; }
        public ModuleLoaderOptions? ClientLoaderOptions { get; set; }
        public Dictionary<string, ModuleOptions>? ClientModuleOptions { get; set; }

        /// <summary>
        /// Resolved paths to client module dlls
        /// </summary>
        public List<ModulePathInfo> ClientModulePathInfos { get; init; } = [];

        /// <summary>
        /// Path to the Core.Os deps.json file
        /// </summary>
        public string? CoreDepsJsonFile { get; set; }

        /// <summary>
        /// Loader options for backend modules
        /// </summary>
        public ModuleLoaderOptions? BackendLoaderOptions { get; set; }

        /// <summary>
        /// Module options for backend modules from appsettings, env, metadata etc. 
        /// </summary>
        public Dictionary<string, ModuleOptions>? BackendModuleOptions { get; set; }

        /// <summary>
        /// Resolved paths to backend module dlls
        /// </summary>
        public List<ModulePathInfo> BackendModulePathInfos { get; init; } = [];

        /// <summary>
        /// Use to validate matching sdk versions of modules (metadata once?)
        /// startup error will be added on version mismatch
        /// </summary>
        public bool UseStartupValidation { get; internal set; }

        /// <summary>
        /// If set true assembly mappings will be calculated within the context
        /// to map assemblies by application priorities: Core, UiHost, Backend, ...
        /// ignoring their specific version.
        /// </summary>
        public bool UseAssemblyMapping { get; set; } = true;

        /// <summary>
        /// If set true the context gets optimized for resolving assembly dependencies
        /// modules will only have unique runtime libraries
        /// contexts redundant libraries will be empty
        /// </summary>
        public bool UseRuntimeContext { get; set; }

        /// <summary>
        /// If set true the module dlls will get loaded using a MetadataLoadContext
        /// to ensure they contain a backend or client module implementation. 
        /// </summary>
        public bool UseModuleTypeValidation { get; set; }
    }

    private readonly SuiteDependencyContextOptions _options = new();

    public SuiteDependencyContextBuilder WithMappingDisabled()
    {
        _options.UseAssemblyMapping = false;
        return this;
    }

    public SuiteDependencyContextBuilder WithCore(Assembly coreAssembly)
        => WithCore(ModuleHelpers.DllToDepsJson(Path.GetFullPath(coreAssembly.Location)));

    /// <summary>
    /// Add options for the core <see cref="ModuleDependencyContext"/>. It's takes the dependency information
    /// directly from <paramref name="coreDepsJsonFilePath"/> file
    /// </summary>
    /// <param name="coreDepsJsonFilePath"></param>
    /// <returns></returns>
    public SuiteDependencyContextBuilder WithCore(string coreDepsJsonFilePath)
    {
        _options.CoreDepsJsonFile = coreDepsJsonFilePath;
        return this;
    }

    /// <summary>
    /// Add options to include an UiHost module. It is required if you want to add client modules to the context.
    /// </summary>    
    /// <exception cref="InvalidOperationException"></exception>
    public SuiteDependencyContextBuilder WithUiHost(ModuleLoaderOptions loaderOptions, Dictionary<string, ModuleOptions> moduleOptions)
    {
        // uihost is disabled by loader
        if (string.IsNullOrEmpty(loaderOptions.UiHost))
            return this;

        // no configuration section
        if (!moduleOptions.TryGetValue(loaderOptions.UiHost, out var options))
            throw new InvalidOperationException($"No settings found for UiHost '{loaderOptions.UiHost}'.");

        if (options is not UiHostOptions hostOptions)
            throw new InvalidOperationException($"Wrong settings type for UiHost '{loaderOptions.UiHost}'. Expected type is '{nameof(UiHostOptions)}'");

        if (string.IsNullOrWhiteSpace(loaderOptions.UiHostsPath))
            throw new InvalidOperationException($"{nameof(ModuleLoaderOptions.UiHostsPath)} is not set");

        _options.UiHostDirectory = loaderOptions.UiHostsPath;
        _options.UiHostOptions = hostOptions;
        _options.UiHostModuleId = loaderOptions.UiHost;
        return this;
    }

    /// <summary>
    /// Add options to include backend modules. Backend modules don't require any UiHost to be available
    /// </summary>    
    public SuiteDependencyContextBuilder WithBackendModules(ModuleLoaderOptions loaderOptions, Dictionary<string, ModuleOptions> moduleOptions)
    {
        _options.BackendLoaderOptions = loaderOptions;
        _options.BackendModuleOptions = moduleOptions;
        return this;
    }

    /// <summary>
    /// Add options to include client modules. It requires UiHost, so either it was already setup by <see cref="SuiteDependencyContextBuilder.WithUiHost"/>
    /// otherwise the call will try to setup UiHost with available loader options
    /// </summary>    
    public SuiteDependencyContextBuilder WithClientModules(ModuleLoaderOptions loaderOptions, Dictionary<string, ModuleOptions> moduleOptions)
    {
        // if we have set an uiHost we need to ensure it's added to the setup
        if (!string.IsNullOrEmpty(loaderOptions.UiHost) && _options.UiHostOptions is null)
        {
            WithUiHost(loaderOptions, moduleOptions);
        }

        _options.ClientLoaderOptions = loaderOptions;
        _options.ClientModuleOptions = moduleOptions;
        return this;
    }

    /// <summary>
    /// Enables the validation of used sdk versions and dependencies. Failed validation will result
    /// in errors contained in modules StartupErrors list
    /// </summary>    
    public SuiteDependencyContextBuilder WithStartupValidation()
    {
        _options.UseStartupValidation = true;
        return this;
    }

    public SuiteDependencyContextBuilder WithModuleTypeValidation()
    {
        _options.UseModuleTypeValidation = true;
        return this;
    }

    /// <summary>
    /// Create the dependency context based on the configured options and the *.deps.json contents of
    /// the included assemblies
    /// </summary>
    /// <param name="fileSystem">The abstract filesystem to access configured assemblies</param>
    /// <returns>The created context including all configured <see cref="ModuleDependencyContext"/></returns>
    public SuiteDependencyContext Build(IFileSystem? fileSystem = null)
    {
        fileSystem ??= new FileSystem();

#if DEBUG
        // on deployment the path should be configured and throw if it does not exist
        if (!string.IsNullOrEmpty(_options.BackendLoaderOptions?.ModulesPath))
            EnsureModuleFolderExists(fileSystem, _options.BackendLoaderOptions?.ModulesPath);
#endif
        ResolveBackendModulePathInfos(fileSystem, _options.BackendLoaderOptions, _options.BackendModuleOptions);
        ResolveUiHostPathInfo(fileSystem, _options.UiHostOptions);
        ResolveUiModules(fileSystem, _options.ClientLoaderOptions, _options.ClientModuleOptions);

        // cleanup the lists
        CleanupOptions();

        // contains also possible ui hosts
        var context = CreateSuiteContext(fileSystem, _options);

        if (_options.UseRuntimeContext)
            context.ClearRedundancy();

        if (_options.UseModuleTypeValidation)
            context.ValidateAssemblyModuleType();

        if (_options.UseStartupValidation)
        {
            context.ValidateSdkVersion();
            context.ValidateDependencies();
        }

        // map assemblies to core os ones overriding module versions
        if (_options.UseAssemblyMapping)
            context.UseAssemblyMapping();

        return context;
    }

    private void ResolveBackendModulePathInfos(IFileSystem fileSystem, ModuleLoaderOptions? loaderOptions, Dictionary<string, ModuleOptions>? moduleOptions)
    {
        if (loaderOptions is null || moduleOptions is null)
            return;

        var infos = new HashSet<ModulePathInfo>();

        // the debug paths
        var debugBackends = fileSystem.GetDebugBackendModuleAssemblyPaths(loaderOptions.ModuleDebugPaths)
            .Where(p => loaderOptions.IsValidModuleLocation(fileSystem, p))
            .Where(p => loaderOptions.AllowInclude(p, true));

        infos.UnionWith(CreateModulePathInfos(debugBackends, true));

        // the deployed ones from modules directory
        var deployedBackends = fileSystem.GetBackendModuleAssemblyPaths(loaderOptions.ModulesPath)
            .Where(p => loaderOptions.AllowInclude(p))
            .Where(k => IsEnabled(moduleOptions, k));

        // ignore deployed assemblies if we have them from debug
        var pathInfos = infos.UnionBy(CreateModulePathInfos(deployedBackends), k => fileSystem.Path.GetFileNameWithoutExtension(k.AssemblyPath)).ToArray();

        _options.BackendModulePathInfos.Clear();
        _options.BackendModulePathInfos.AddRange(pathInfos);
    }

    private void ResolveUiHostPathInfo(IFileSystem fileSystem, UiHostOptions? hostOptions)
    {
        if (hostOptions is null || !hostOptions.Enable)
            return;

        var uiHostAssemblyName = $"{_options.UiHostModuleId}{Constants.ModuleSuffixBackend}";

        // first search for debug modules if it is set
        if (_options.UiHostDirectory is not null)
        {
            var paths = fileSystem
                .GetDebugUiHostModuleAssemblyPaths([_options.UiHostDirectory], $"{uiHostAssemblyName}.deps.json")
                .Where(p => p.Contains("Debug", StringComparison.Ordinal) || p.Contains("Release", StringComparison.Ordinal));

            var uiHostPath = paths.FirstOrDefault();
            if (!string.IsNullOrEmpty(uiHostPath))
            {
                _options.UiHostPathInfo = new(ModuleHelpers.DepsJsonToDll(uiHostPath), true);
                return;
            }
        }

        // if we found nothing take it from fixed suite folder
        if (_options.UiHostPathInfo is null)
        {
            var uiHostsFolderPath = _options.UiHostDirectory;
            if (uiHostsFolderPath is null)
            {
                // fallback to default if no uihost path is set
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyFolder = fileSystem.Path.GetDirectoryName(assemblyLocation);
                uiHostsFolderPath = fileSystem.Path.Combine(assemblyFolder!, Constants.DefaultUiHostsDirectory);
            }

            var paths = fileSystem.GetUiHostModuleAssemblyPaths(uiHostsFolderPath, uiHostAssemblyName);
            var uiHostPath = paths.FirstOrDefault();

            if (!string.IsNullOrEmpty(uiHostPath))
                _options.UiHostPathInfo = new(ModuleHelpers.DepsJsonToDll(uiHostPath));
        }
    }

    private void ResolveUiModules(IFileSystem fileSystem, ModuleLoaderOptions? loaderOptions, Dictionary<string, ModuleOptions>? moduleOptions)
    {
        if (loaderOptions is null || moduleOptions is null)
            return;

        // to share libraries provided by ui host we use it's resolver on our ui modules
        if (_options.UiHostPathInfo is null)
            throw new InvalidOperationException("Ui modules can't be added without a valid UiHost");

        // when we use deployed modules they don't have e.g. ViciOne.Suite.Sdk.Client on their own
        // because it was removed on deployment cleanup.
        // to resolve these dependencies for ui modules we add the ui host as additional resolver source.

        var infos = new HashSet<ModulePathInfo>();

        // the debug paths
        var debugUis = fileSystem.GetDebugClientModuleAssemblyPaths(loaderOptions.ModuleDebugPaths)
            .Where(p => loaderOptions.IsValidModuleLocation(fileSystem, p))
            .Where(p => loaderOptions.AllowInclude(p, true));

        infos.UnionWith(CreateModulePathInfos(debugUis, true));

        // the deployed ones from modules directory
        var uiHostDepsJson = ModuleHelpers.DllToDepsJson(_options.UiHostPathInfo.AssemblyPath);
        var deployedUis = fileSystem.GetUiModuleAssemblyPaths(loaderOptions.ModulesPath, uiHostDepsJson)
            .Where(p => loaderOptions.AllowInclude(p))
            .Where(k => IsEnabled(moduleOptions, k));

        var pathInfos = infos.UnionBy(CreateModulePathInfos(deployedUis), k
            => fileSystem.Path.GetFileNameWithoutExtension(k.AssemblyPath)).ToArray();

        _options.ClientModulePathInfos.Clear();
        _options.ClientModulePathInfos.AddRange(pathInfos);
    }

    private void CleanupOptions()
    {
        if (_options.UiHostPathInfo is not null)
            _options.BackendModulePathInfos.Remove(_options.UiHostPathInfo);
    }

    private static SuiteDependencyContext CreateSuiteContext(IFileSystem fileSystem, SuiteDependencyContextOptions options)
    {
        if (string.IsNullOrEmpty(options.CoreDepsJsonFile))
            throw new InvalidOperationException("core deps.json path missing");

        if (!File.Exists(options.CoreDepsJsonFile))
            throw new InvalidOperationException("core deps.json path does not exist");

        List<ModulePathInfo> uiHostPathInfos = options.UiHostPathInfo is not null ? [options.UiHostPathInfo] : [];

        // core context provides libraries that can be shared with backend|ui host modules 
        var coreContext = ModuleDependencyContextFactory.CreateCoreContext(
            fileSystem,
            options.CoreDepsJsonFile);

        // depending on the ui host we can identify provided assets and client libraries
        // till we deploy both hosts we have to ensure we don't remove too many files
        var uiHostContexts = ModuleDependencyContextFactory.CreateUiHostContexts(
            fileSystem,
            uiHostPathInfos,
            options.UseRuntimeContext);

        var backendContexts = ModuleDependencyContextFactory.CreateBackendModuleContexts(
            fileSystem,
            options.BackendModulePathInfos,
            uiHostContexts,
            options.UseRuntimeContext)
            .ToList();

        // actually we deploy wasm ui/backend into same folder. wasm.backend references wasm.client
        // and therefore the backend.deps.json is sufficient to filter out redundancies
        var frontendContexts = ModuleDependencyContextFactory.CreateUiModuleContexts(
            fileSystem,
            options.ClientModulePathInfos,
            uiHostContexts,
            options.UseRuntimeContext);

        // contexts get ordered by the count of their reference to other modules
        var allContexts = backendContexts
            .Concat(frontendContexts)
            .OrderBy(k => backendContexts.GetDependencyContexts(k).Count())
            .ToList();

        return new SuiteDependencyContext(coreContext, uiHostContexts.FirstOrDefault(), allContexts);
    }

    private static IEnumerable<ModulePathInfo> CreateModulePathInfos(IEnumerable<string> assemblyPaths, bool isDebug = false)
        => assemblyPaths.Select(p => new ModulePathInfo(p, isDebug));

    // a quite bad check that only works because of the current naming conventions
    private static bool IsEnabled(Dictionary<string, ModuleOptions> moduleOptions, string filePath)
    {
        // filepath like '/some/path/to/ViciOne.Module.Backend.dll' we should get 'ViciOne.Module' as id 
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            var id = ModuleIdResolver.ResolveId(fileName);

            if (moduleOptions.TryGetValue(id, out var options))
                return options.Enable;
        }
        catch (InvalidOperationException)
        {
            // can happen in tests - Assembly 'ViciOne.Suite.Blazor.Shared' does not fit suite module naming conventions 
        }

        return false;
    }

    private static void EnsureModuleFolderExists(IFileSystem fileSystem, string? modulesPath)
    {
        if (string.IsNullOrEmpty(modulesPath))
            return;

        var moduleDirectory = fileSystem.GetRootedPath(modulesPath);
        if (fileSystem.Directory.Exists(moduleDirectory))
            return;

        fileSystem.Directory.CreateDirectory(moduleDirectory);
    }
}
