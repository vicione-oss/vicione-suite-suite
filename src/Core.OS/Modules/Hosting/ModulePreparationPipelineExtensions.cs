using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance.Services;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;

namespace Core.OS.Modules.Hosting;

/// <summary>
/// Pipeline steps individually abortable stages executed by <see cref="ModulePreparationPipeline"/>.
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
            await ctx.RepositoryStore.MigrateConfiguredRepositories(ctx.Builder.Configuration, ctx.Logger, ct);

            // A temporary token service is needed, because the repository source tokens may need refreshing.
            var tokenService = new ArtifactRepositoryTokenService(pipeline.HttpClientFactory);
            var logger = ctx.LoggerFactory.CreateLogger<ArtifactRepositoryOptionsCache>();

            // The cache is registered as a singleton later and keeps the store reference.
            ctx.RepositoryOptionsCache = new ArtifactRepositoryOptionsCache(ctx.Builder.Configuration, logger);
            await ctx.RepositoryOptionsCache.ReloadOptions(ctx.RepositoryStore, tokenService, ct);
        });

    private static ModuleHostPreparationResult AbortOnMissingManifest() =>
        new("Module manifest was not prepared before loading the module host");

    /// <summary>
    /// Loads the module loader options into preparation context. Combines appsettings, environment etc.
    /// with module manifest to produce the final module options.
    /// </summary>
    public static ModulePreparationPipeline UseModuleLoaderOptions(this ModulePreparationPipeline pipeline)
        => pipeline.Use(async (ctx, ct) =>
        {
            if (ctx.Manifest is null)
                return AbortOnMissingManifest();

            ctx.ModuleLoaderOptions = ctx.Builder.Configuration.GetModuleLoaderOptions();

            // Combines appsettings and environment with the module manifest.
            var additionalModules = ctx.Builder.Environment.IsDevelopment() ? ModuleConstants.SampleModuleIds : [];
            ctx.ModuleOptions = ctx.Builder.Configuration.CreateModuleOptions(ctx.Manifest, additionalModules);

            return PreparationResult.Success;
        });

    /// <summary>
    /// Synchronizes module packages with the configured artifact repositories and persists
    /// resolved package versions to the manifest. See <see cref="ModuleSynchronizationStep"/>.
    /// </summary>
    public static ModulePreparationPipeline UseModuleSynchronization(this ModulePreparationPipeline pipeline)
        => pipeline.Use((ctx, ct) => ModuleSynchronizationStep.Run(ctx, pipeline.HttpClientFactory, ct));

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

            if (ctx.ModuleLoaderOptions is null)
                return new ModuleHostPreparationResult("Module loader options were not loaded before loading the module host");

            // Combines appsettings and environment with the module manifest.
            var additionalModules = ctx.Builder.Environment.IsDevelopment() ? ModuleConstants.SampleModuleIds : [];
            var moduleOptions = ctx.Builder.Configuration.CreateModuleOptions(ctx.Manifest, additionalModules);
            var uiHostOptions = ctx.Builder.Configuration.CreateUiHostOptions(ctx.ModuleLoaderOptions);
            if (uiHostOptions is not null)
                moduleOptions[ctx.ModuleLoaderOptions.UiHost!] = uiHostOptions;

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
