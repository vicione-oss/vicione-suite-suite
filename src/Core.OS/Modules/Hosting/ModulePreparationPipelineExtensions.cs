using Core.Module.Extensions;
using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance.Services;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using Microsoft.Extensions.Options;

namespace Core.OS.Modules.Hosting;

/// <summary>
/// Pipeline steps that lift the former monolithic <c>AddModuleHost</c> body into discrete,
/// individually abortable stages executed by <see cref="ModulePreparationPipeline"/>.
/// </summary>
internal static partial class ModulePreparationPipelineExtensions
{
    /// <summary>
    /// Applies enqueued install/uninstall operations to the module manifest and stores the
    /// resulting manifest in the context. A corrupt manifest surfaces as an abort result so
    /// startup can fall back gracefully instead of crashing.
    /// </summary>
    public static ModulePreparationPipeline UseApplyEnqueuedOperations(this ModulePreparationPipeline pipeline)
        => pipeline.Use(async (ctx, ct) =>
        {
            try
            {
                ctx.Manifest = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(
                    ctx.FileSystem, ctx.InstanceOptions, ctx.LoggerFactory, ct);

                return PreparationResult.Success;
            }
            catch (Exception ex)
            {
                LogApplyOperationsFailed(ctx.Logger, ex);
                return new ModuleHostPreparationResult($"Failed to apply enqueued module operations: {ex.Message}");
            }
        });

    /// <summary>
    /// Loads the artifact repository options into a cache using the shared repository store.
    /// The cache is kept in the context and registered with DI in the final step.
    /// </summary>
    public static ModulePreparationPipeline UseRepositoryOptions(this ModulePreparationPipeline pipeline)
        => pipeline.Use(async (ctx, ct) =>
        {
            ctx.RepositoryStore ??= new ArtifactRepositoryStore(ctx.FileSystem, Options.Create(ctx.InstanceOptions), ctx.Logger);
            await ctx.RepositoryStore.MigrateConfiguredRepositories(ctx.Builder.Configuration, ct);

            // TODO: we need to refresh the options cache on changes to repositories
            ctx.RepositoryOptionsCache = new ArtifactRepositoryOptionsCache(ctx.Builder.Configuration);
            await ctx.RepositoryOptionsCache.ReloadOptions(ctx.RepositoryStore, ct);
        });

    private static ModuleHostPreparationResult AbortOnMissingManifest() =>
        new("Module manifest was not prepared before loading the module host");

    /// <summary>
    /// Loads the artifact repository options into a cache using the shared repository store.
    /// The cache is kept in the context and registered with DI in the final step.
    /// </summary>
    public static ModulePreparationPipeline UseModuleLoaderOptions(this ModulePreparationPipeline pipeline)
        => pipeline.Use(async (ctx, ct) =>
        {
            if (ctx.Manifest is null)
                return AbortOnMissingManifest();

            ctx.LoaderOptions = ctx.Builder.Configuration.GetModuleLoaderOptions();

            // combine appsettings, environment etc. with module manifest
            var additionalModules = ctx.Builder.Environment.IsDevelopment() ? ModuleConstants.SampleModuleIds : [];
            ctx.ModuleOptions = ctx.Builder.Configuration.CreateModuleOptions(ctx.Manifest, additionalModules);

            return PreparationResult.Success;
        });

    public static ModulePreparationPipeline UseModuleSynchronization(this ModulePreparationPipeline pipeline)
        => pipeline.Use(async (ctx, ct) =>
        {
            if (ctx.Manifest is null)
                return new ModuleHostPreparationResult("Module manifest was not prepared before loading the module host");

            if (ctx.RepositoryOptionsCache is null)
                return new ModuleHostPreparationResult("Artifact repository options were not loaded before loading the module host");

            if (ctx.ModuleOptions is null || ctx.LoaderOptions is null)
                return new ModuleHostPreparationResult("Module options were not loaded before loading the module host");

            // get versions of loaded debug modules
            var debugModuleVersions = await ctx.FileSystem.GetDebugModuleVersions(ctx.LoaderOptions, ct);

            // contains package versions with dependencies from AppData/modules.json
            var moduleIds = ctx.ModuleOptions
                .Where(k => k.Value.Enable)
                .Select(k => k.Key);

            // prevent downloading packages that are not enabled by configuration
            var packages = ctx.Manifest.GetValidModulePackages([.. moduleIds], debugModuleVersions, ctx.Logger);

            using var synchronizer = new ModuleSynchronizer(ctx.FileSystem)
                .WithApiAdapter(ctx.RepositoryOptionsCache)
                .WithPackages(packages)
                .WithModulesPath(ctx.LoaderOptions)
                .WithPackageSdkValidation()
                .WithOrphanedVersionCleanup()
                .WithLoggerFactory(ctx.LoggerFactory);

            // start synchronisation with configured options
            ctx.SynchronizationResults = await synchronizer.ProcessSynchronization(ct);
            ctx.SynchronizationResults.LogSynchronizationResults(ctx.Logger);

            var resolvedModules = ctx.SynchronizationResults.Resolved
                    .Where(k => k.Error == null)
                    .ToDictionary(k => k.Name, v => v.Version);

            if (resolvedModules.Count > 0)
            {
                // manifest could contain a module that is only disabled by env or appsettings
                // we don't want to modify these but need to update the resolved package versions
                var update = ctx.Manifest.UpdatePackageVersions(resolvedModules);
                if (update is not null)
                {
                    ctx.Manifest.Packages.Clear();
                    ctx.Manifest.Packages.AddRange(packages);
                    ctx.Manifest.LastModified = DateTimeOffset.UtcNow;

                    // persist the resolved package versions so next synchronization won't resolve them again
                    await ModulePackageManifestStore.Store(ctx.Manifest, ctx.FileSystem, ctx.InstanceOptions, ct);
                }
            }

            return PreparationResult.Success;
        });


    /// <summary>
    /// Builds the module host and load module assemblies via <see cref="ModuleHostBuilder"/>.
    /// It sets <see cref="SuitePreparationContext.ModuleHost"/> and <see cref="SuitePreparationContext.ModuleOptionsStore"/>.
    /// It calls <see cref="MvcServiceCollectionExtensions.AddControllersWithViews"/> on process of building the module host.
    /// </summary>
    public static ModulePreparationPipeline UseModuleHost(this ModulePreparationPipeline pipeline)
        => pipeline.Use(async (ctx, ct) =>
        {
            if (ctx.Manifest is null)
                return new ModuleHostPreparationResult("Module manifest was not prepared before loading the module host");

            if (ctx.RepositoryOptionsCache is null)
                return new ModuleHostPreparationResult("Artifact repository options were not loaded before loading the module host");

            if (ctx.LoaderOptions is null)
                return new ModuleHostPreparationResult("Module loader options were not loaded before loading the module host");

            // combine appsettings, environment etc. with module manifest
            var additionalModules = ctx.Builder.Environment.IsDevelopment() ? ModuleConstants.SampleModuleIds : [];
            var moduleOptions = ctx.Builder.Configuration.CreateModuleOptions(ctx.Manifest, additionalModules);
            var uiHostOptions = ctx.Builder.Configuration.CreateUiHostOptions(ctx.LoaderOptions);
            if (uiHostOptions is not null)
                moduleOptions[ctx.LoaderOptions.UiHost!] = uiHostOptions;

            ctx.ModuleOptionsStore = new ModuleOptionsStore(ctx.FileSystem, ctx.Builder.Configuration);

            var hostBuilder = new ModuleHostBuilder(ctx.FileSystem, ctx.Builder.Configuration, moduleOptions)
                .WithSynchronizationResults(ctx.SynchronizationResults)
                .WithSuiteDependencyContext()
                .WithOptionsSupport(ctx.Builder.Configuration, ctx.ModuleOptionsStore);

            try
            {
                ctx.ModuleHost = await hostBuilder.Build(ctx.Builder.Services.AddControllersWithViews, ct);

                LogModuleHostCreated(ctx.Logger);
                return PreparationResult.Success;
            }
            catch (Exception ex)
            {
                LogModuleHostFailed(ctx.Logger, ex);
                return new ModuleHostPreparationResult($"Failed to build the module host: {ex.Message}");
            }
        });

    [LoggerMessage(LogLevel.Error, "Failed to apply enqueued module package operations")]
    private static partial void LogApplyOperationsFailed(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Module host created successfully")]
    private static partial void LogModuleHostCreated(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to build the module host")]
    private static partial void LogModuleHostFailed(ILogger logger, Exception exception);
}
