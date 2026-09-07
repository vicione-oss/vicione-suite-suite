using Core.Artifacts.JFrog;
using Core.Module;
using Core.Module.Extensions;
using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;

namespace Core.OS.Modules.Hosting;

/// <summary>
/// Encapsulates the module package synchronization logic used by
/// <see cref="ModulePreparationPipelineExtensions.UseModuleSynchronization"/>.
/// </summary>
internal static class ModuleSynchronizationStep
{
    public static async Task<IPreparationResult> Run(ModulePreparationContext ctx, IHttpClientFactory httpClientFactory, CancellationToken ct)
    {
        if (ctx.Manifest is null)
            return new ModuleHostPreparationResult("Module manifest was not prepared before loading the module host");

        if (ctx.RepositoryOptionsCache is null)
            return new ModuleHostPreparationResult("Artifact repository options were not loaded before loading the module host");

        if (ctx.ModuleOptions is null || ctx.ModuleLoaderOptions is null)
            return new ModuleHostPreparationResult("Module options were not loaded before loading the module host");

        // get versions of loaded debug modules
        var debugModuleVersions = await ctx.FileSystem.GetDebugModuleVersions(ctx.ModuleLoaderOptions, ct);

        // contains package versions with dependencies from AppData/modules.json
        var moduleIds = ctx.ModuleOptions
            .Where(k => k.Value.Enable)
            .Select(k => k.Key);

        // prevent downloading packages that are not enabled by configuration
        var packages = ctx.Manifest.GetValidModulePackages([.. moduleIds], debugModuleVersions, ctx.Logger);
        var repositoryLogger = ctx.LoggerFactory.CreateLogger<JFrogArtifactRepository>();
        var repository = new JFrogArtifactRepository(ctx.FileSystem, httpClientFactory, ctx.RepositoryOptionsCache, repositoryLogger);
        var moduleRepositoryLogger = ctx.LoggerFactory.CreateLogger<ModuleArtifactRepository>();
        var moduleRepository = new ModuleArtifactRepository(repository, ctx.FileSystem, moduleRepositoryLogger);
        
        // we have everything to start synchronizing the module packages with the configured artifact repositories
        var synchronizer = new ModuleSynchronizer(ctx.FileSystem)
            .WithApiAdapter(moduleRepository)
            .WithPackages(packages)
            .WithModulesPath(ctx.ModuleLoaderOptions)
            .WithPackageSdkValidation()
            .WithOrphanedVersionCleanup();

        // start synchronisation with configured options
        ctx.SynchronizationResults = await synchronizer.ProcessSynchronization(ct);
        ctx.SynchronizationResults.LogSynchronizationResults(ctx.Logger);

        var resolvedModules = ctx.SynchronizationResults.Resolved
                .Where(k => k.Error == null)
                .ToDictionary(k => k.Name, v => v.Version);

        // when we no resolved modules we don't need to update the manifest
        if (resolvedModules.Count == 0)
            return PreparationResult.Success;

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

        return PreparationResult.Success;
    }
}
