using Core.OS.Hosting;
using Sdk.Modules;
using Constants = Core.Module.Constants;

namespace Core.OS.Modules.Services;

internal class ModuleMigrator(
    IModuleMetadataCache metadataCache,
    IWorkspaceManagement workspaceManagement,
    ILogger<ModuleMigrator> logger) : IModuleMigrator
{
    public async Task PrepareUpdateMigration(ModulePackageManifest manifest, CancellationToken cancellationToken = default)
    {
        var installedMetadata = await metadataCache.GetInstalledModuleMetadata(cancellationToken);

        foreach (var package in manifest.Packages)
        {
            // not installed -> no metadata -> no update
            var metadata = installedMetadata.FirstOrDefault(k => k.Metadata.Name == package.Name);
            if (metadata is null)
                continue;

            // no version update at all
            if (metadata.Metadata.Version == package.Version || metadata.Metadata.Version == ModuleConstants.UnknownVersionMarker)
                continue;

            var isPatchUpdate = SuiteVersionUtils.IsPatchUpdate(metadata.Metadata.Version, package.Version);

            // module handles it's migration automatically only reset cache
            var resetHome = !metadata.Metadata.AutonomousMigration && !isPatchUpdate;

            logger.LogInformation("Prepare module update {PackageName} to version '{Version}' (patch:{IsPatch})", package.Name, package.Version, isPatchUpdate);
            TryWriteResetFlags(metadata.Metadata.Name, resetHome, !isPatchUpdate);
        }

        // uninstalled modules without our sample ones
        var uninstalled = installedMetadata
            .Where(k => !ModuleConstants.SampleModuleIds.Contains(k.ModuleId) && k.Metadata.Version != Constants.ModuleCiVersionKey)
            .Where(k => manifest.Packages.All(p => p.Name != k.Metadata.Name));

        foreach (var bundle in uninstalled)
        {
            logger.LogInformation("Prepare uninstalling {PackageName} version '{Version}'", bundle.Metadata.Name, bundle.Metadata.Version);
            TryWriteResetFlags(bundle.Metadata.Name, true, true);
        }
    }

    private void TryWriteResetFlags(string moduleId, bool resetHome, bool resetCache)
    {
        try
        {
            if (moduleId == Sdk.Constants.SystemModuleId)
                throw new InvalidOperationException("Attempt to reset application directory rejected");

            if (resetHome)
                workspaceManagement.WriteResetHomeDirectoryFlag(moduleId);

            if (resetCache)
                workspaceManagement.WriteResetCacheDirectoryFlag(moduleId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to reset workspace for module {ModuleId}", moduleId);
        }
    }
}
