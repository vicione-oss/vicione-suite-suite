using Core.Module;
using Core.Module.Comparer;
using Core.OS.Hosting;
using Sdk.Modules;
using Constants = Core.Module.Constants;

namespace Core.OS.Modules.Services;

internal class ModuleMigrator(
    IModuleMetadataCache metadataCache,
    IModuleArtifactRepository moduleRepository,
    IWorkspaceManagement workspaceManagement,
    ILogger<ModuleMigrator> logger) : IModuleMigrator
{
    public async Task PrepareUpdateMigration(ModulePackageManifest manifest, CancellationToken cancellationToken = default)
    {
        var resetOptions = await GetInstalledModulesResetOptions(manifest, cancellationToken);

        foreach (var option in resetOptions)
        {
            TryWriteResetFlags(option.Name, option.ResetHome, option.ResetCache);
        }
    }

    private async Task<IEnumerable<ResetOption>> GetInstalledModulesResetOptions(ModulePackageManifest manifest, CancellationToken cancellationToken = default)
    {
        var installedMetadata = await metadataCache.GetInstalledModuleMetadata(cancellationToken);
        var options = new List<ResetOption>();

        foreach (var package in manifest.Packages)
        {
            // not installed -> no metadata -> no update
            var installed = installedMetadata.FirstOrDefault(k => k.Metadata.Name == package.Name);
            if (installed is null
                || installed.Metadata.Version == ModuleConstants.UnresolvedVersionMarker
                || installed.Metadata.Version == Constants.ModuleCiVersionKey)
                continue;

            // check if it's an update - can be modified manually
            var compareResult = new StringVersionComparer().Compare(package.Version, installed.Metadata.Version);
            if (compareResult != 1)
                continue;

            // download metadata for target version - might differ from installed one
            // if we miss a single metadata we cancel the whole update process           
            var targetMetadata = await moduleRepository.GetModuleMetadata(package, cancellationToken)
                ?? throw new InvalidOperationException($"Can't get metadata of module '{package.Name}' version '{package.Version}'.");

            // module handles it's migration automatically only reset cache
            var isPatchUpdate = SuiteVersionUtils.IsPatchUpdate(installed.Metadata.Version, package.Version);
            var resetHome = !targetMetadata.AutonomousMigration && !isPatchUpdate;

            logger.LogInformation("Prepare update of module {PackageName} to version '{Version}' (patch:{IsPatch})", package.Name, package.Version, isPatchUpdate);

            options.Add(new ResetOption(installed.Metadata.Name, resetHome, !isPatchUpdate));
        }

        var uninstalled = installedMetadata
            .Where(k => !ModuleConstants.SampleModuleIds.Contains(k.ModuleId) && k.Metadata.Version != Constants.ModuleCiVersionKey)
            .Where(k => manifest.Packages.All(p => p.Name != k.Metadata.Name))
            .Select(k =>
            {
                logger.LogInformation("Prepare uninstall of module {PackageName}, version '{Version}'", k.Metadata.Name, k.Metadata.Version);

                return new ResetOption(k.Metadata.Name, true, true);
            });

        options.AddRange(uninstalled);

        return options;
    }

    private void TryWriteResetFlags(string moduleId, bool resetHome, bool resetCache)
    {
        try
        {
            if (moduleId == Shared.Constants.SystemModuleId)
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

    private record ResetOption(string Name, bool ResetHome, bool ResetCache);
}
