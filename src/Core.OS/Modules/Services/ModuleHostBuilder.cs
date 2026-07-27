using System.IO.Abstractions;
using System.Text.Json;
using Core.Module;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.Module.Options;
using Core.OS.Hosting;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Factories;
using Core.Shared.Modules;
using Core.Shared.Modules.Contracts;
using Sdk.Backend.Modules;
using Sdk.Modules;
using Serilog;

namespace Core.OS.Modules.Services;

internal class ModuleHostBuilder(IFileSystem fileSystem, IConfiguration configuration, Dictionary<string, ModuleOptions> moduleOptions)
{
    private readonly SortedSet<ModuleMetadataBundle> _modules = new(new SortedMetadataComparer());
    private readonly ModuleLoaderOptions _loaderOptions = configuration.GetModuleLoaderOptions();

    private bool _buildDependencyContext;
    private SuiteDependencyContext? _suiteContext;
    private IConfigurationManager? _configurationManager;
    private IModuleOptionsStore? _moduleOptionsStore;
    private ModuleSynchronizationResults? _synchronizationResults;

    private class SortedMetadataComparer : IComparer<ModuleMetadataBundle>
    {
        public int Compare(ModuleMetadataBundle? x, ModuleMetadataBundle? y)
            => string.Compare(x?.Metadata.Title, y?.Metadata.Title, StringComparison.Ordinal);
    }

    /// <summary>
    /// Build <see cref="SuiteDependencyContext"/> for modules available through configuration
    /// </summary>    
    public ModuleHostBuilder WithSuiteDependencyContext()
    {
        _buildDependencyContext = true;
        return this;
    }

    public ModuleHostBuilder WithSynchronizationResults(ModuleSynchronizationResults? results)
    {
        _synchronizationResults = results;
        return this;
    }

    /// <summary>
    /// Use given <see cref="SuiteDependencyContext"/> for building module host
    /// </summary>    
    public ModuleHostBuilder WithSuiteDependencyContext(SuiteDependencyContext suiteContext)
    {
        _suiteContext = suiteContext;
        return this;
    }

    /// <summary>
    /// Add validation for module options provided by metadata, environment settings or user secrets.
    /// Add <see cref="IModuleOptionsStore"/> to service collection. Depends on <see cref="WithSuiteDependencyContext"/>
    /// </summary>    
    internal ModuleHostBuilder WithOptionsSupport(IConfigurationManager configurationManager, IModuleOptionsStore optionsStore)
    {
        _configurationManager = configurationManager;
        _moduleOptionsStore = optionsStore;
        return this;
    }

    public async Task<IModuleHost> Build(Func<IMvcBuilder?> addMvcBuilder, CancellationToken cancellationToken = default)
    {
        _modules.Clear();
        _moduleOptionsStore = null;

        // if we have synchronization results we need to process them and add errors to _modules
        ProcessSynchronizationResults();

        // regarding these options we build a context that contains all assembly information based on deps.json
        // we have about core, uihost and modules available on disk
        BuildSuiteDependencyContext();

        // add modules from suite context to _modules
        await AddSuiteContextModules(cancellationToken);

        // now validate the options we have from json, env, secrets or metadata
        // here we can intercept - modules that will be loaded are known now
        await AddModuleOptionsSupport(cancellationToken);

        // has to be added before loading ui host module because of necessary services as NavigationManager 
        var mvBuilder = addMvcBuilder.Invoke();

        // load the modules depending on suite context
        // based on the context we load the module assemblies into the application
        var loadedBundles = LoadBackendModuleBundles();

        // we need to provide a fallback dependency context
        _suiteContext ??= new SuiteDependencyContextBuilder()
            .WithCore(typeof(SystemBackendModule).Assembly)
            .Build();

        // combine all needed parts to create a ModuleHost
        var hostOptions = new ModuleHostOptions
        {
            Configuration = configuration,
            LoadedBundles = loadedBundles.Bundles,
            ModuleOptions = moduleOptions,
            Modules = _modules,
            MvcBuilder = mvBuilder,
            SuiteContext = _suiteContext,
        };

        var moduleHost = new ModuleHost(hostOptions);

        // -> WithOptionsSupport
        if (_moduleOptionsStore is not null)
        {
            // add module options available as configuration source
            _configurationManager?.AddModuleConfigurationSource(moduleHost, _moduleOptionsStore);
        }

        // errors occurred on loading or options are available in suite context
        // and need to be added to modules
        UpdateModuleErrorsFromContext(_suiteContext);

        LogModuleContexts(_suiteContext);

        return moduleHost;
    }

    private ModuleBundleLoadResult<ModuleBundle<BackendModule>> LoadBackendModuleBundles()
    {
        if (_suiteContext is null)
            return new();

        // load the modules depending on suite context
        // based on the context we load the module assemblies into the application
        return ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(_suiteContext);
    }

    private void ProcessSynchronizationResults()
    {
        if (_synchronizationResults is null)
            return;

        var sdkVersion = SuiteVersionUtils.GetSuiteSdkVersion();

        ProcessSynchronizationResults(_synchronizationResults.Resolved, sdkVersion);
        ProcessSynchronizationResults(_synchronizationResults.UpdateFailed, sdkVersion);
    }

    private void ProcessSynchronizationResults(IEnumerable<ModuleSynchronizationResult> updateResults, string sdkVersion)
    {
        if (_synchronizationResults is null)
            return;

        foreach (var updateResult in updateResults)
        {
            if (updateResult.Error is null)
                continue;

            var moduleBundle = _modules.FirstOrDefault(k => k.Metadata.Name == updateResult.Name);
            if (moduleBundle != null)
            {
                moduleBundle.Errors.Add(updateResult.Error);
                continue;
            }

            _modules.Add(ModuleMetadataBundleFactory.CreateErrorBundle(updateResult, sdkVersion));
        }
    }

    private async Task AddModuleOptionsSupport(CancellationToken cancellationToken)
    {
        if (_configurationManager is null)
            return;

        if (_suiteContext is null)
            throw new InvalidOperationException($"Call {nameof(WithSuiteDependencyContext)} to use options support");

        _moduleOptionsStore = new ModuleOptionsStore(fileSystem, _configurationManager);
        await _moduleOptionsStore.ValidateModuleOptions(_suiteContext, cancellationToken);
    }

    private void BuildSuiteDependencyContext()
    {
        if (!_buildDependencyContext)
            return;

        var builder = new SuiteDependencyContextBuilder()
            .WithCore(typeof(SystemBackendModule).Assembly)
            .WithUiHost(_loaderOptions, moduleOptions)
            .WithBackendModules(_loaderOptions, moduleOptions)
            .WithClientModules(_loaderOptions, moduleOptions)
            .WithStartupValidation();

        if (_loaderOptions.UseTypeValidation)
            builder.WithModuleTypeValidation();

        _suiteContext = builder.Build(fileSystem);

        Log.Debug("Built context based on SDK version {SdkVersion}", _suiteContext.Core.GetSdkVersion());
    }

    private async Task AddSuiteContextModules(CancellationToken cancellationToken)
    {
        if (_suiteContext is null)
            return;

        foreach (var moduleContext in _suiteContext.Modules)
        {
            // handle/backend client
            var existing = _modules.FirstOrDefault(k => k.ModuleId == moduleContext.ModuleId);
            if (existing is not null)
            {
                if (moduleContext.ModuleType == ModuleType.Client)
                    existing.HasFrontend = true;

                if (moduleContext.ModuleType == ModuleType.Backend)
                    existing.HasBackend = true;

                continue;
            }

            // skip modules loaded by debug path
            if (moduleContext.IsDebugSource)
            {
                _modules.Add(await ModuleMetadataBundleFactory.CreateDebugBundle(fileSystem, moduleContext, cancellationToken));
                continue;
            }

            try
            {
                var metadata = await fileSystem.DeserializeModuleMetadata(moduleContext, cancellationToken);

                _modules.Add(ModuleMetadataBundleFactory.CreateBundle(moduleContext, metadata));
            }
            catch (Exception e)
            {
                var fallback = ModuleMetadataBundleFactory.CreateFallbackBundle(fileSystem, moduleContext);
                fallback.Errors.Add(new(ModuleErrorCodes.MetadataInvalid, e.Message));
                _modules.Add(fallback);
            }
        }
    }

    /// <summary>
    /// Assigns startup errors kept in context to the installed module metadata
    /// </summary>    
    private void UpdateModuleErrorsFromContext(SuiteDependencyContext suiteContext)
    {
        if (_suiteContext is null)
            return;

        foreach (var module in _modules)
        {
            // backend/client have same errors
            var modContext = suiteContext.Modules.FirstOrDefault(k => k.ModuleId == module.ModuleId);
            if (modContext is null)
                continue;

            // only add errors that are not already 
            var modErrors = modContext.GetErrorInfos()
                .Where(k => module.Errors.All(m => k.ErrorCode != m.ErrorCode));

            module.Errors.AddRange(modErrors);
        }
    }

    /// <summary>
    /// Errors occurred on options etc are logged here
    /// </summary>
    private void LogModuleContexts(SuiteDependencyContext suiteContext)
    {
        if (_suiteContext is null)
            return;

        foreach (var module in suiteContext.Modules)
        {
            Log.Debug("Context available for {Type} module {Id} source '{Folder}' (Errors={Cnt}).",
                module.ModuleType, module.ModuleId, module.AssemblyFolder, module.StartupErrors.Count);
        }

        foreach (var moduleGroup in suiteContext.Modules.Where(k => k.StartupErrors.Count > 0).GroupBy(k => k.ModuleId))
        {
            var startupErrors = moduleGroup
                .SelectMany(k => k.StartupErrors)
                .DistinctBy(k => k.Message);

            foreach (var startupError in startupErrors)
            {
                Log.Warning("Module '{ModuleId}' has startup error - {Message}{InnerMessage}",
                    moduleGroup.Key,
                    startupError.Message,
                    startupError.InnerException?.Message ?? string.Empty);
            }
        }

        if (!string.IsNullOrEmpty(_loaderOptions.DumpMappingFilePath))
        {
            // mapping
            var summary = suiteContext.CreateMappingSummary();
            fileSystem.File.WriteAllText(_loaderOptions.DumpMappingFilePath, JsonSerializer.Serialize(summary, ModuleSerializerOptions.GetOptions()));
        }

#if DEBUG
        if (_loaderOptions.ModuleDebugPaths is not null)
        {
            foreach (var path in _loaderOptions.ModuleDebugPaths)
            {
                // ignore our local repo paths
                if (string.Equals(path, "src", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "samples", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!fileSystem.Directory.Exists(path))
                    Log.Warning("Configured module debug path '{Path}' does not exist", path);
            }
        }

        foreach (var module in suiteContext.Modules.Where(m => m.IsDebugSource))
            Log.Information("Debug module '{ModuleId}' from path '{ModulePath}'", module.AssemblyName, module.AssemblyFolder);
#endif
    }
}
