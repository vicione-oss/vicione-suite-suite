using System.IO.Abstractions;
using Core.Module.Exceptions;
using Core.Module.Utils;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Services;

internal sealed partial class ModuleSynchronizer(IFileSystem fileSystem)
{
    private static IEnumerable<ModuleDependencyPackage> GetUnresolvedPackages(IEnumerable<ModuleDependencyPackage>? packages)
        => packages?.Where(k => k.Version == ModuleConstants.LatestVersionKey) ?? [];

    public async Task<ModuleSynchronizationResults> ProcessSynchronization(CancellationToken cancellationToken = default)
    {
        var options = BuildOptions();
        var results = new ModuleSynchronizationResults();

        // Validation uses local metadata or tries to download it from api        
        await ValidateSdkVersionCompatibility(results, options, cancellationToken);

        // Resolve versions
        await ResolveModuleVersions(results, options, cancellationToken);

        // Update manifest could be extracted
        DeleteOrphanedModuleVersions(results, options);

        // Download modules
        await DownloadAndExtractModulePackages(results, options, cancellationToken);

        return results;
    }

    /// <summary>
    /// Validates SDK compatibility for each <see cref="SynchronizationOptions.Packages"/> if it's already resolved to a valid version.
    /// Failed packages will be added to <see cref="ModuleSynchronizationResults.Incompatible"/> list.
    /// </summary>
    private async Task ValidateSdkVersionCompatibility(ModuleSynchronizationResults result, SynchronizationOptions options, CancellationToken cancellationToken = default)
    {
        if (!options.ValidatePackages)
            return;

        foreach (var package in options.Packages)
        {
            // Ignore packages that will be resolved anyway
            if (package.HasUnresolvedVersion())
                continue;

            try
            {
                await ValidateSdkVersionCompatibility(package, options, cancellationToken);
            }
            catch (SdkIncompatibilityException iex)
            {
                result.Incompatible.Add(new ModuleSynchronizationResult(package.Name)
                {
                    Version = package.Version,
                    Error = new ErrorInfo(ModuleErrorCodes.SdkVersionIncompatible, iex.Message)
                });

                // Sdk is incompatible - set version to be resolved                
                package.Version = ModuleConstants.LatestVersionKey;
            }
            catch (Exception ex)
            {
                result.Incompatible.Add(new ModuleSynchronizationResult(package.Name)
                {
                    Version = package.Version,
                    Error = new ErrorInfo(ModuleErrorCodes.UnknownCompatibilityError, ex.Message)
                });
            }
        }
    }

    /// <summary>
    /// Validates sdk compatibility of the given package by local metadata, if available, otherwise
    /// metadata gets downloaded from artifact API using <see cref="SynchronizationOptions.ModuleRepository"/>.
    /// </summary>    
    /// <exception cref="InvalidOperationException">Thrown if metadata can't be retrieved locally nor from API</exception>
    private async Task ValidateSdkVersionCompatibility(ModuleDependencyPackage package, SynchronizationOptions options, CancellationToken cancellationToken = default)
    {
        var metadataPath = fileSystem.GetModuleMetadataPath(options.ModulesPath, package);
        ModuleMetadata? metadata;

        if (fileSystem.File.Exists(metadataPath))
        {
            // Module is already downloaded so metadata is available locally
            metadata = await fileSystem.DeserializeModuleMetadata(metadataPath, cancellationToken);
        }
        else
        {
            // Try to download the metadata via api
            metadata = await options.ModuleRepository.GetModuleMetadata(package, cancellationToken);
        }

        if (metadata is null)
            throw new InvalidOperationException($"Can't find module metadata for '{package.Name}' version '{package.Version}'");

        // todo - if could be sure that only major version changes include breaking changes
        // we could assume that 0.17.1 sdk fits to module requirement 0.17.0 for example
        // todo - we could also use the metadata if it's available for all modules
        ModuleVersionValidator.ValidateSdkCompatibility(options.SdkVersion, metadata.MinSuiteSdkVersion);
    }

    private static async Task ResolveModuleVersions(ModuleSynchronizationResults result, SynchronizationOptions options, CancellationToken cancellationToken)
    {
        var toBeResolved = GetUnresolvedPackages(options.Packages).ToList();
        if (toBeResolved.Count == 0)
            return;

        try
        {
            // We try to resolve enabled packages with version=='latest' to an available version for current sdk
            foreach (var package in toBeResolved)
            {
                result.Resolved.Add(await ResolveLatestVersion(package, options, cancellationToken));
            }
        }
        catch (HttpRequestException ex)
        {
            // If something is misconfigured within package settings or network is unavailable            
            result.HttpResolveError = ex;

            // We need this these later on
            var stillUnresolved = toBeResolved.Where(k => result.Resolved.All(r => r.Name != k.Name))
                .Select(k => new ModuleSynchronizationResult(k.Name)
                {
                    Version = k.Version,
                    Error = new ErrorInfo(ModuleErrorCodes.ResolveVersionFailed, "Failed to resolve module version")
                });

            result.Resolved.AddRange(stillUnresolved);
        }
    }

    /// <summary>
    /// Try to resolve latest package version via module API
    /// </summary>
    /// <param name="package">The package to be resolved</param>
    /// <param name="options">Required SDK is used from options</param>
    /// <param name="cancellationToken">Cancellation token to cancel async tasks</param>
    /// <returns>Returns resolve result containing the resolved version if Error is null</returns>
    /// <remarks>
    /// Exceptions will be wrapped into <see cref="ModuleSynchronizationResult.Error" /> but HttpRequestException will bubble
    /// </remarks>
    private static async Task<ModuleSynchronizationResult> ResolveLatestVersion(ModuleDependencyPackage package, SynchronizationOptions options, CancellationToken cancellationToken = default)
    {
        var result = new ModuleSynchronizationResult(package.Name);

        try
        {
            var metadataArtifact = await options.ModuleRepository.QueryLatestModuleMetadataArtifact(options.SdkVersion.ToVersion(), package.Name, cancellationToken);
            if (metadataArtifact is null)
            {
                result.Error = new ErrorInfo(ModuleErrorCodes.FoundNoVersion, $"No compatible version of {package.Name} for ViciOne.Suite.Sdk '{ModuleHelpers.GetNormalizedVersion(options.SdkVersion)}' found.");
                return result;
            }

            // We could download the metadata to get the version or parse it from name like
            // e.g. 0.24.0-win-x64_0.19.0.json or 0.24.0-ci2343243-win-x64_0.19.0.json
            if (!ModuleNameVersionRegex.GetVersions(metadataArtifact.Name, out var parsedVersion, out var ciVersion))
            {
                result.Error = new ErrorInfo(ModuleErrorCodes.FoundInvalidVersion, $"Can't determine version of '{package.Name}' for path '{metadataArtifact.Path}'.");
                return result;
            }

            if (!string.IsNullOrWhiteSpace(ciVersion))
            {
                result.Error = new ErrorInfo(ModuleErrorCodes.FoundCiVersionOnly, $"Skip ci version of '{package.Name}' for path '{metadataArtifact.Path}'.");
                return result;
            }

            // e.g. 0.24.0
            result.Version = parsedVersion;
        }
        catch (HttpRequestException)
        {
            // Pass it through to stop attempt to resolve further modules if api access failed
            throw;
        }
        catch (Exception e)
        {
            result.Error = new ErrorInfo(ModuleErrorCodes.UnknownResolveError, e.Message);
        }

        return result;
    }


    /// <summary>
    /// Delete module package versions from modules path that are not contained in <see cref="SynchronizationOptions.Packages"/>
    /// </summary>    
    private void DeleteOrphanedModuleVersions(ModuleSynchronizationResults results, SynchronizationOptions options)
    {
        if (!_deleteOrphanedVersions)
            return;

        // Get all module directories and versions
        // module-path/{Module}/{Version} e.g. suite-modules/ViciOne.Suite.DataCollectionWizard/0.10.0
        var existingModules = fileSystem.GetModulePackageVersionPaths(options.ModulesPath);

        foreach (var module in existingModules)
        {
            // skip used ones
            var package = options.Packages.FirstOrDefault(k => k.Name == module.Name && k.Version == module.Version);
            if (package is not null)
                continue;

            var deleted = new ModuleSynchronizationResult(module.Name)
            {
                Version = module.Version,
                RelativeFolder = module.RelativePath
            };
            results.Deleted.Add(deleted);

            try
            {
                // Delete version folder
                var moduleVersionPath = fileSystem.Path.Combine(options.ModulesPath, module.RelativePath);
                var moduleContainerPath = fileSystem.Directory.GetParent(moduleVersionPath);
                fileSystem.Directory.Delete(moduleVersionPath, true);

                // Remove the whole module folder if it does not contain any version folders
                var folders = moduleContainerPath?.GetDirectories().Length ?? 1;
                if (folders > 0)
                    continue;

                moduleContainerPath?.Delete(true);
            }
            catch (Exception ex)
            {
                deleted.Error = new ErrorInfo(ModuleErrorCodes.DeleteFailed, ex.Message);
            }
        }
    }

    private static async Task DownloadAndExtractModulePackages(ModuleSynchronizationResults result, SynchronizationOptions options, CancellationToken cancellationToken)
    {
        // We had an error on resolving versions from API so we don't try to download anything
        if (result.HttpResolveError is not null)
            return;

        // These packages were successfully resolved
        var resolvedPackages = result.Resolved
            .Where(k => k.Error is null && !string.IsNullOrEmpty(k.Version))
            .Select(x => new ModuleDependencyPackage { Name = x.Name, Version = x.Version! });

        // Add fresh resolved ones to already configured ones
        var packagesToDownload = options.Packages
            .Where(p => !p.HasUnresolvedVersion())
            .UnionBy(resolvedPackages, k => k.Name)
            .ToArray();

        if (packagesToDownload.Length <= 0)
            return;

        var results = await options.ModuleRepository.DownloadAndExtract(options.ModulesPath, packagesToDownload, cancellationToken);

        result.Updated.AddRange(results.Where(k => k is { Skipped: false, Error: null })
            .Select(k => new ModuleSynchronizationResult(k.Module.Name) { Version = k.Module.Version }));

        result.UpdateSkipped.AddRange(results.Where(k => k is { Skipped: true, Error: null })
            .Select(k => new ModuleSynchronizationResult(k.Module.Name) { Version = k.Module.Version }));

        result.UpdateFailed.AddRange(results.Where(k => k.Error is not null)
            .Select(k => new ModuleSynchronizationResult(k.Module.Name)
            {
                Version = k.Module.Version,
                Error = new ErrorInfo(ModuleErrorCodes.DownloadFailed, k.Error!.Message)
            }));
    }
}
