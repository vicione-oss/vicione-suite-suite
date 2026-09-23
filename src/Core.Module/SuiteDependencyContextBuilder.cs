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
/// Builds a <see cref="SuiteDependencyContext"/> from the assembly runtime references of the configured modules.
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
        /// Resolved paths to client module dlls.
        /// </summary>
        public List<ModulePathInfo> ClientModulePathInfos { get; init; } = [];

        public string? CoreDepsJsonFile { get; set; }

        public ModuleLoaderOptions? BackendLoaderOptions { get; set; }

        /// <summary>
        /// Backend module options from appsettings, environment and metadata.
        /// </summary>
        public Dictionary<string, ModuleOptions>? BackendModuleOptions { get; set; }

        /// <summary>
        /// Resolved paths to backend module dlls.
        /// </summary>
        public List<ModulePathInfo> BackendModulePathInfos { get; init; } = [];

        /// <summary>
        /// Validates matching SDK versions; a mismatch adds a startup error.
        /// </summary>
        public bool UseStartupValidation { get; internal set; }

        /// <summary>
        /// Maps assemblies by application priority (Core, UiHost, Backend, ...), ignoring their version.
        /// </summary>
        public bool UseAssemblyMapping { get; set; } = true;

        /// <summary>
        /// Each module keeps only its unique runtime libraries; redundant ones are left empty.
        /// </summary>
        public bool UseRuntimeContext { get; set; }

        /// <summary>
        /// Loads module dlls in a MetadataLoadContext to confirm they contain a backend or client module.
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
    /// Takes the core dependency information directly from the given deps.json file.
    /// </summary>
    public SuiteDependencyContextBuilder WithCore(string coreDepsJsonFilePath)
    {
        _options.CoreDepsJsonFile = coreDepsJsonFilePath;
        return this;
    }

    /// <summary>
    /// Includes a UiHost module, which client modules require.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No settings for the UiHost, settings of the wrong type, or an unset UiHostsPath.
    /// </exception>
    public SuiteDependencyContextBuilder WithUiHost(ModuleLoaderOptions loaderOptions, Dictionary<string, ModuleOptions> moduleOptions)
    {
        // The loader disables the UiHost by leaving it empty.
        if (string.IsNullOrEmpty(loaderOptions.UiHost))
            return this;

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
    /// Includes backend modules, which do not require a UiHost.
    /// </summary>
    public SuiteDependencyContextBuilder WithBackendModules(ModuleLoaderOptions loaderOptions, Dictionary<string, ModuleOptions> moduleOptions)
    {
        _options.BackendLoaderOptions = loaderOptions;
        _options.BackendModuleOptions = moduleOptions;
        return this;
    }

    /// <summary>
    /// Includes client modules; sets up the UiHost from the loader options if <see cref="WithUiHost"/> has not run.
    /// </summary>
    public SuiteDependencyContextBuilder WithClientModules(ModuleLoaderOptions loaderOptions, Dictionary<string, ModuleOptions> moduleOptions)
    {
        // A configured UiHost has to be in the setup before the client modules.
        if (!string.IsNullOrEmpty(loaderOptions.UiHost) && _options.UiHostOptions is null)
        {
            WithUiHost(loaderOptions, moduleOptions);
        }

        _options.ClientLoaderOptions = loaderOptions;
        _options.ClientModuleOptions = moduleOptions;
        return this;
    }

    /// <summary>
    /// Validates SDK versions and dependencies; failures land in the module's StartupErrors list.
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
    /// Creates the context from the configured options and the *.deps.json of the included assemblies.
    /// </summary>
    public SuiteDependencyContext Build(IFileSystem? fileSystem = null)
    {
        fileSystem ??= new FileSystem();

#if DEBUG
        // On deployment the path is configured, so a missing folder is an error.
        if (!string.IsNullOrEmpty(_options.BackendLoaderOptions?.ModulesPath))
            EnsureModuleFolderExists(fileSystem, _options.BackendLoaderOptions?.ModulesPath);
#endif
        ResolveBackendModulePathInfos(fileSystem, _options.BackendLoaderOptions, _options.BackendModuleOptions);
        ResolveUiHostPathInfo(fileSystem, _options.UiHostOptions);
        ResolveUiModules(fileSystem, _options.ClientLoaderOptions, _options.ClientModuleOptions);

        CleanupOptions();

        // The suite context also carries possible ui hosts.
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

        // Maps module assemblies onto the Core.OS ones, overriding module versions.
        if (_options.UseAssemblyMapping)
            context.UseAssemblyMapping();

        return context;
    }

    private void ResolveBackendModulePathInfos(IFileSystem fileSystem, ModuleLoaderOptions? loaderOptions, Dictionary<string, ModuleOptions>? moduleOptions)
    {
        if (loaderOptions is null || moduleOptions is null)
            return;

        var infos = new HashSet<ModulePathInfo>();

        var debugBackends = fileSystem.GetDebugBackendModuleAssemblyPaths(loaderOptions.ModuleDebugPaths)
            .Where(p => loaderOptions.IsValidModuleLocation(fileSystem, p))
            .Where(p => loaderOptions.AllowInclude(p, true));

        infos.UnionWith(CreateModulePathInfos(debugBackends, true));

        var deployedBackends = fileSystem.GetBackendModuleAssemblyPaths(loaderOptions.ModulesPath)
            .Where(p => loaderOptions.AllowInclude(p))
            .Where(k => IsEnabled(moduleOptions, k));

        // A debug assembly wins over the deployed one of the same module.
        var pathInfos = infos.UnionBy(CreateModulePathInfos(deployedBackends), k => fileSystem.Path.GetFileNameWithoutExtension(k.AssemblyPath)).ToArray();

        _options.BackendModulePathInfos.Clear();
        _options.BackendModulePathInfos.AddRange(pathInfos);
    }

    private void ResolveUiHostPathInfo(IFileSystem fileSystem, UiHostOptions? hostOptions)
    {
        if (hostOptions is null || !hostOptions.Enable)
            return;

        var uiHostAssemblyName = $"{_options.UiHostModuleId}{Constants.ModuleSuffixBackend}";

        // Debug modules take precedence when configured.
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

        // Nothing found in the debug paths, so fall back to the fixed suite folder.
        if (_options.UiHostPathInfo is null)
        {
            var uiHostsFolderPath = _options.UiHostDirectory;
            if (uiHostsFolderPath is null)
            {
                // The default folder applies when no uihost path is set.
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
        if (loaderOptions is null || moduleOptions is null || _options.UiHostPathInfo is null)
            return;

        // Deployment cleanup strips shared assemblies such as ViciOne.Suite.Sdk.Client from a deployed
        // module, so the ui host is added as an additional resolver source for ui modules.
        var infos = new HashSet<ModulePathInfo>();

        var debugUis = fileSystem.GetDebugClientModuleAssemblyPaths(loaderOptions.ModuleDebugPaths)
            .Where(p => loaderOptions.IsValidModuleLocation(fileSystem, p))
            .Where(p => loaderOptions.AllowInclude(p, true));

        infos.UnionWith(CreateModulePathInfos(debugUis, true));

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

        // The core context provides the libraries shareable with backend and ui host modules.
        var coreContext = ModuleDependencyContextFactory.CreateCoreContext(
            fileSystem,
            options.CoreDepsJsonFile);

        // The ui host identifies the provided assets and client libraries. While both hosts are still
        // deployed, this must not remove too many files.
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

        // The wasm ui and backend deploy into the same folder and wasm.backend references wasm.client,
        // so backend.deps.json alone is enough to filter out the redundancies.
        var frontendContexts = ModuleDependencyContextFactory.CreateUiModuleContexts(
            fileSystem,
            options.ClientModulePathInfos,
            uiHostContexts,
            options.UseRuntimeContext);

        // Contexts are ordered by how many other modules they reference.
        var allContexts = backendContexts
            .Concat(frontendContexts)
            .OrderBy(k => backendContexts.GetDependencyContexts(k).Count())
            .ToList();

        return new SuiteDependencyContext(coreContext, uiHostContexts.FirstOrDefault(), allContexts);
    }

    private static IEnumerable<ModulePathInfo> CreateModulePathInfos(IEnumerable<string> assemblyPaths, bool isDebug = false)
        => assemblyPaths.Select(p => new ModulePathInfo(p, isDebug));

    /// <summary>
    /// Works only as long as the module naming conventions hold.
    /// </summary>
    private static bool IsEnabled(Dictionary<string, ModuleOptions> moduleOptions, string filePath)
    {
        // '/some/path/to/ViciOne.Module.Backend.dll' yields 'ViciOne.Module' as the id.
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            var id = ModuleIdResolver.ResolveId(fileName);

            if (moduleOptions.TryGetValue(id, out var options))
                return options.Enable;
        }
        catch (InvalidOperationException)
        {
            // Happens in tests, where an assembly such as ViciOne.Suite.Blazor.Shared is not a module.
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
