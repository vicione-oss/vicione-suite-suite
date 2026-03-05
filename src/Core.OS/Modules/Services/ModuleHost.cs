using System.IO.Abstractions;
using System.Reflection;
using Core.Module;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.Module.Options;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules.Contracts;
using Core.UiHosting;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Backend.Extensions;
using Sdk.Backend.Modules;
using Sdk.Modules;
using Serilog;

namespace Core.OS.Modules.Services;

internal sealed partial class ModuleHost : IModuleHost
{
    private readonly List<ModuleBundle<BackendModule>> _loadedModuleBundles;
    private readonly IReadOnlyCollection<ModuleMetadataBundle> _installedModules;
    private readonly InstanceOptions _instanceOptions;
    private readonly IConfiguration _config;
    private readonly SuiteDependencyContext _suiteContext;

    private IUiHostModule? UiHostModule { get; }
    public IMvcBuilder? MvcBuilder { get; init; }
    public IEnumerable<BackendModule> Modules => _loadedModuleBundles.Select(k => k.Module);

    public ModuleHost(ModuleHostOptions options)
    {
        _loadedModuleBundles = [.. options.LoadedBundles];
        _config = options.Configuration;
        _suiteContext = options.SuiteContext;
        _instanceOptions = _config.GetInstanceOptions();
        _installedModules = options.Modules;
        MvcBuilder = options.MvcBuilder;
        UiHostModule = FindUiHostModule();
    }

    public IEnumerable<Assembly> GetModuleAssemblies()
        => _loadedModuleBundles
            .Select(k => k.Assembly)
            .Distinct();

    public T? GetModule<T>() where T : BackendModule => (T?)Modules.SingleOrDefault(m => m is T);

    public void AddUiHostServices(IServiceCollection services, Func<IServiceCollection, IdentityBuilder?> getIdentity)
        => AddUiHostServices(services, MvcBuilder ?? throw new InvalidOperationException($"{nameof(MvcBuilder)} is not set."), getIdentity);

    public void AddUiHostServices(IServiceCollection services, IMvcBuilder mvcBuilder, Func<IServiceCollection, IdentityBuilder?> getIdentity)
    {
        // This will set up our SuiteUserContext and Role manager for headless or UiHost        
        var identityBuilder = getIdentity.Invoke(services);
        if (UiHostModule is null)
            return;

        var uiHostEnv = CreateUiHostEnvironment(_config);
        if (uiHostEnv is null)  // no UiHostModule
            return;

        services.AddSingleton(uiHostEnv);
        services.AddTransient<IUiHostEnvironment>(s => s.GetRequiredService<UiHostEnvironment>());

        // Load the ui modules
        UiHostModule.LoadUiDependencies(services, uiHostEnv);

        // Register services of ui host
        if (UiHostModule is BackendModule module)
            module.ConfigureServices(services, _config, mvcBuilder);

        // Security needs to be added after AddAuthentication
        if (identityBuilder is not null)
            UiHostModule.ConfigureIdentity(identityBuilder);

        // Register services of ui modules
        UiHostModule.ConfigureUiServices(services, uiHostEnv, (mod, err) =>
        {
            // todo: if a module fails on load existing mappings should be redirected
            Log.Error(err, "Failed to register ui services for '{Module}'", mod);
        });

        var moduleType = UiHostModule.GetType();

        // Register controllers within the module assembly
        mvcBuilder.AddApplicationPart(moduleType.Assembly);

        // Register WorkspaceService 
        services.AddWorkspaceProvider(moduleType);
    }

    public void AddModuleServices(IServiceCollection services, IMvcBuilder? mvcBuilder = null)
    {
        var builder = mvcBuilder ?? MvcBuilder ?? throw new InvalidOperationException($"{nameof(MvcBuilder)} is not set.");

        foreach (var module in Modules.Where(m => m is not IUiHostModule))
        {
            try
            {
                // Log module metadata to check loaded version in log
                var moduleBundle = _installedModules.First(k => k.Metadata.Name == module.ModuleId);
                Log.Information("Init module {Module} version '{Version}'", module.ModuleId, moduleBundle.Metadata.Version);

                // todo: Add a way to prevent this from the module
                var name = ModuleIdResolver.GetModuleName(module.ModuleId);
                var type = module.GetType();

                if (!module.DisableDefaultFeature)
                    services.AddModuleFeature(_ => new ModuleFeature(module.ModuleId, name, $"The default permission for {name}."));

                // todo init settings - load from json or throw?
                module.ConfigureServices(services, _config, builder);

                // Register WorkspaceService 
                services.AddWorkspaceProvider(type);

                // Register controllers within the module assembly
                builder.AddApplicationPart(type.Assembly);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on register services for {Module} - skipping", module.ModuleId);
            }
        }

        // needs to be done after all modules registered their services
        builder.AddControllersAsServices();
    }

    public void MapModuleEndpoints(IEndpointRouteBuilder endpoints)
    {
        var logger = endpoints.ServiceProvider.GetRequiredService<ILogger<ModuleHost>>();

        if (UiHostModule is null)
        {
            const string message = "UI host is disabled";
            LogUiHostDisabled(logger);
            endpoints.MapGet("/", () => message);
        }

        foreach (var module in Modules)
        {
            LogMappingEndpoints(logger, module.ModuleId);
            try
            {
                module.MapEndpoints(endpoints);
            }
            catch (Exception e)
            {
                LogMappingEndpointsError(logger, e, module.ModuleId);
            }
        }
    }

    /// <summary>
    /// Try to call <see cref="IUiHostModule.UseSecurity"/> if the UiHostModule is initialized.    
    /// </summary>
    public void UseSecurity(IApplicationBuilder app)
    {
        if (UiHostModule is null)
            return;

        var logger = GetLogger(app.ApplicationServices);
        LogUseSecurityFrom(logger, UiHostModule.ModuleId);

        try
        {
            UiHostModule.UseSecurity(app, _instanceOptions.UseHeaderForwarding);
        }
        catch (Exception e)
        {
            LogUseSecurityError(logger, e, UiHostModule.ModuleId);
        }
    }

    /// <summary>
    /// Try to call <see cref="IUiHostModule.UseUiHost"/> if the UiHostModule is initialized.    
    /// </summary>
    public void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (UiHostModule is null)
            return;

        var logger = GetLogger(app.ApplicationServices);
        LogInitializeUiHost(logger, UiHostModule.ModuleId);

        try
        {
            var uiHostEnvironment = app.ApplicationServices.GetRequiredService<IUiHostEnvironment>();

            UiHostModule.UseUiHost(app, env, uiHostEnvironment);
        }
        catch (Exception e)
        {
            LogInitializeUiHostError(logger, e, UiHostModule.ModuleId);
        }
    }

    /// <summary>
    /// Try to call <see cref="BackendModule.ConfigureMessageBus" /> for each loaded module.
    /// Errors get logged to static Serilog instance
    /// </summary>
    public void ConfigureBusRegistrationConfigurator(IBusRegistrationConfigurator busConfig)
    {
        foreach (var module in Modules)
        {
            try
            {
                //no good way for us to prevent a module from stealing or overriding services
                module.ConfigureMessageBus(busConfig, _instanceOptions.Type);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on message bus configuration for '{ModuleId}' - skipping", module.ModuleId);
            }
        }
    }

    /// <summary>
    /// Try to call <see cref="BackendModule.UseServices" /> for each loaded module.
    /// Errors get logged
    /// </summary>    
    public void UseModuleServices(IApplicationBuilder app)
    {
        var logger = GetLogger(app.ApplicationServices);
        foreach (var module in Modules)
        {
            LogConfigureServices(logger, module.ModuleId);
            try
            {
                module.UseServices(app);
            }
            catch (Exception e)
            {
                LogConfigureServicesError(logger, e, module.ModuleId);
            }
        }

        // UI modules will be initialized on page because most of them need clients scope
    }

    public void MoveModuleResources(IServiceProvider serviceProvider)
    {
        var logger = GetLogger(serviceProvider);
        var fileSystem = serviceProvider.GetRequiredService<IFileSystem>();

        if (string.IsNullOrEmpty(_instanceOptions.HomeDirectory))
            throw new InvalidOperationException("HomeDirectory is not set.");

        foreach (var bundle in _loadedModuleBundles)
        {
            var module = bundle.Module;

            var resourceDirectoryName = module.GetResourceDirectory(serviceProvider);
            if (string.IsNullOrEmpty(resourceDirectoryName))
                continue;

            LogMovingResources(logger, module.ModuleKey.ModuleId);

            try
            {
                var assemblyLocation = fileSystem.Path.GetDirectoryName(bundle.AssemblyLocation);
                if (string.IsNullOrEmpty(assemblyLocation))
                    throw new InvalidOperationException($"Assembly location for bundle '{bundle.Module}' not found.");

                var resourcePath = fileSystem.Path.Combine(assemblyLocation, resourceDirectoryName);
                if (!fileSystem.Directory.Exists(resourcePath))
                {
                    LogResourceDirectoryNotFound(logger, resourcePath, module.ModuleKey.ModuleId);
                    continue;
                }

                var moduleAppData = fileSystem.CreateModuleAppDataDirectory(_instanceOptions, module.ModuleKey.ModuleId);

                foreach (var resource in fileSystem.Directory.GetFiles(resourcePath, "*.*", SearchOption.AllDirectories))
                {
                    var targetPath = fileSystem.Path.Combine(moduleAppData, fileSystem.Path.GetRelativePath(resourcePath, resource));
                    var targetDirectory = fileSystem.Path.GetDirectoryName(targetPath);
                    if (string.IsNullOrEmpty(targetDirectory))
                        throw new InvalidOperationException($"Target directory for bundle '{bundle.Module}' is empty.");

                    fileSystem.Directory.CreateDirectory(targetDirectory);

                    // file updates not supported - files won't be overriden! 
                    if (!fileSystem.File.Exists(targetPath))
                        fileSystem.File.Copy(resource, targetPath, false);
                }
            }
            catch (Exception e)
            {
                LogMovingResourcesError(logger, e, module.ModuleKey.ModuleId);
            }
        }
    }

    public async Task MigrateAndSeedModuleData(IServiceScope scope, IConfiguration config, CancellationToken stoppingToken)
    {
        var logger = GetLogger(scope.ServiceProvider);
        foreach (var module in Modules)
        {
            try
            {
                if (module.ModuleInitializer is null)
                    continue;

                await module.ModuleInitializer.OnPreMigrate(scope.ServiceProvider, stoppingToken);

                LogMigrateDatabase(logger, module.ModuleId);
                await module.ModuleInitializer.Migrate(scope.ServiceProvider, stoppingToken);

                if (_instanceOptions.Type != Sdk.Instance.InstanceType.Slave) // Slaves will get their seeds from Master
                {
                    LogSeedData(logger, module.ModuleId);
                    await module.ModuleInitializer.OnPostMigrate(scope.ServiceProvider, stoppingToken);
                }
            }
            catch (Exception e)
            {
                // todo: this might prevent depending modules working correctly!!
                LogSetupDataError(logger, e, module.ModuleId);
            }
        }
    }

    public Task CallOnInitialized(IServiceScope scope, CancellationToken stoppingToken)
    {
        var logger = GetLogger(scope.ServiceProvider);
        return Task.WhenAll(Modules.Select(m => OnInit(m, scope.ServiceProvider, logger, stoppingToken)));

        static async Task OnInit(BackendModule m, IServiceProvider s, ILogger<ModuleHost> logger, CancellationToken ct)
        {
            try
            {
                if (m.ModuleInitializer is not null)
                    await m.ModuleInitializer.OnInitialized(s, ct);
            }
            catch (Exception e)
            {
                LogOnInitializedError(logger, e, nameof(IModuleInitializer.OnInitialized), m.ModuleId);
            }
        }
    }

    private IUiHostModule? FindUiHostModule() => (IUiHostModule?)Modules.FirstOrDefault(m => m is IUiHostModule);

    private UiHostEnvironment? CreateUiHostEnvironment(IConfiguration config)
    {
        if (UiHostModule is null)
            return null;

        var uiHostOptions = config.BindSection<UiHostOptions>(UiHostModule.ModuleId);

        // the ui host has to load the ui modules itself 
        var moduleBundle = _loadedModuleBundles.First(k => k.Module.ModuleKey.ModuleId == UiHostModule.ModuleId);

        return new UiHostEnvironment(_suiteContext)
        {
            // use debug modules only in development mode 
            IsDevelopment = uiHostOptions.UseDebugRoot,
            ModulePath = Path.GetDirectoryName(moduleBundle.AssemblyLocation),
        };
    }

    public SuiteDependencyContext GetContext() => _suiteContext;

    public IEnumerable<IModule> GetModules() => _loadedModuleBundles.Select(k => k.Module);

    public string GetSdkVersion() => _suiteContext.Core.GetSdkVersion();

    public IReadOnlyCollection<ModuleMetadataBundle> GetManifestModules()
    {
        // ensure we always return a new instance to keep original instance untouched ignoring the AvailableVersions
        return _installedModules.Select(k => new ModuleMetadataBundle()
        {
            ModuleId = k.ModuleId,
            Metadata = k.Metadata,
            AvailableVersions = [],
            Errors = [.. k.Errors],
            Installed = k.Installed,
            CanBeModified = k.CanBeModified,
            CanUpdate = k.CanUpdate,
            HasBackend = k.HasBackend,
            HasFrontend = k.HasFrontend,
            IsDebugSource = k.IsDebugSource,
            MissingDependencies = [.. k.MissingDependencies],
        }).ToList();
    }

    private static ILogger<ModuleHost> GetLogger(IServiceProvider provider)
        => provider.GetRequiredService<ILogger<ModuleHost>>();

    [LoggerMessage(Level = LogLevel.Information, Message = "UI host is disabled")]
    private static partial void LogUiHostDisabled(ILogger<ModuleHost> logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Mapping endpoints for '{ModuleId}'")]
    private static partial void LogMappingEndpoints(ILogger<ModuleHost> logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while mapping endpoints for '{ModuleId}' - skipping")]
    private static partial void LogMappingEndpointsError(ILogger<ModuleHost> logger, Exception exception, string moduleId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Use security from '{ModuleId}'")]
    private static partial void LogUseSecurityFrom(ILogger<ModuleHost> logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error on use '{ModuleId}' security")]
    private static partial void LogUseSecurityError(ILogger<ModuleHost> logger, Exception exception, string moduleId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Initialize '{ModuleId}' as the UI host")]
    private static partial void LogInitializeUiHost(ILogger<ModuleHost> logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error on initialize '{ModuleId}' as the UI host - skipping")]
    private static partial void LogInitializeUiHostError(ILogger<ModuleHost> logger, Exception exception, string moduleId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Configure services for '{ModuleId}'")]
    private static partial void LogConfigureServices(ILogger<ModuleHost> logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error on configure services for '{ModuleId}' - skipping")]
    private static partial void LogConfigureServicesError(ILogger<ModuleHost> logger, Exception exception, string moduleId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Moving resources for module '{ModuleId}'")]
    private static partial void LogMovingResources(ILogger<ModuleHost> logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No directory named '{ResourceDirectory}' was found for module '{ModuleId}'")]
    private static partial void LogResourceDirectoryNotFound(ILogger<ModuleHost> logger, string resourceDirectory, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error on moving resources for {ModuleId} - skipping")]
    private static partial void LogMovingResourcesError(ILogger<ModuleHost> logger, Exception exception, string moduleId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Migrate database for '{ModuleId}'")]
    private static partial void LogMigrateDatabase(ILogger<ModuleHost> logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Seed data for '{ModuleId}'")]
    private static partial void LogSeedData(ILogger<ModuleHost> logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error on setup data for '{ModuleId}' - skipping")]
    private static partial void LogSetupDataError(ILogger<ModuleHost> logger, Exception exception, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error calling {Method} for '{ModuleId}' - skipping")]
    private static partial void LogOnInitializedError(ILogger<ModuleHost> logger, Exception exception, string method, string moduleId);
}
