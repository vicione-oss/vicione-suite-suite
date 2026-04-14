using Core.Module.Options;
using Core.Module.Utils;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Modules;
using Semver;

namespace Core.OS.Modules.Services;

public sealed class ModuleMetadataProvider : IModuleMetadataProvider
{
    private readonly IModuleOptionsStore _optionsStore;
    private readonly IModulePackageOperationStore _packageOperationStore;
    private readonly IOptions<ModuleLoaderOptions> _loaderOptions;
    private readonly IConfiguration _environmentConfig;
    private readonly ILogger<ModuleMetadataProvider> _logger;
    private readonly IModuleHost _moduleHost;
    private readonly IModuleArtifactCache _metadataCache;

    public ModuleMetadataProvider(IModuleHost moduleHost,
        IModuleArtifactCache metadataCache,
        IModuleOptionsStore optionsStore,
        IModulePackageOperationStore packageOperationStore,
        IOptions<ModuleLoaderOptions> loaderOptions,
        ILogger<ModuleMetadataProvider> logger)
    {
        _moduleHost = moduleHost;
        _metadataCache = metadataCache;
        _optionsStore = optionsStore;
        _packageOperationStore = packageOperationStore;
        _loaderOptions = loaderOptions;
        _logger = logger;
        _environmentConfig = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()
            .Build();
    }

    public async Task<List<ModuleMetadataBundle>> GetModuleMetadata(GetModuleMetadataOptions options, CancellationToken cancellationToken = default)
    {
        var installedBundles = _moduleHost.GetManifestModules();

        List<ModuleMetadata>? availableMetadata = null;

        if (options.IncludeAvailable)
        {
            var sdkVersion = ModuleHelpers.GetSdkAssemblyVersion();

            availableMetadata = await _metadataCache.GetAvailableModuleMetadata(sdkVersion, options.ForceRefresh, cancellationToken);
        }

        // we merge the versions of the metadata to bundles and order them by the published date from metadata
        var bundles = CreateBundles(installedBundles ?? [], availableMetadata?.OrderByDescending(k => k.Published).ToList() ?? []);

        await ApplyEnqueuedPackageOperations(bundles, cancellationToken);

        await ApplyEnvironmentOptions(bundles, cancellationToken);

        return bundles;
    }

    private async Task ApplyEnqueuedPackageOperations(IReadOnlyCollection<ModuleMetadataBundle> modules, CancellationToken cancellationToken = default)
    {
        var operations = await _packageOperationStore.GetEnqueuedOperations(cancellationToken);
        if (operations.Count == 0)
            return;

        foreach (var operation in operations)
        {
            var module = modules.FirstOrDefault(op => op.Metadata.Name == operation.Package.Name);
            if (module == null) continue;

            module.PendingOperation = operation;
        }
    }

    private async Task ApplyEnvironmentOptions(IReadOnlyCollection<ModuleMetadataBundle> modules, CancellationToken cancellationToken = default)
    {
        foreach (var module in modules)
        {
            // if installation is disabled by config installed modules are not allowed to be modified
            module.CanBeModified = _loaderOptions.Value.AllowInstallation;

            if (!module.Installed)
                continue;

            try
            {
                // now we have the metadata with the current option declarations
                // we can't mix up env, secrets here!!! we need to flag them later
                // !! options might change between requests so we need to enrich them again
                await _optionsStore.ApplyStoredOptions(module.Metadata, _environmentConfig, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enrich metadata options for module '{Module}'", module.Metadata.Name);
            }
        }
    }

    public static List<ModuleMetadataBundle> CreateBundles(IReadOnlyCollection<ModuleMetadataBundle> installed, List<ModuleMetadata> available)
    {
        var result = new List<ModuleMetadataBundle>();

        // handle the already installed ones
        result.AddRange(installed);

        // remove available packages of modules not installed if they can't be modified
        if (installed.Count > 0 && installed.All(k => !k.CanBeModified))
        {
            available.RemoveAll(k => installed.All(i => i.Metadata.Name != k.Name));
        }

        // handle available ones - dependencies, versions -> todo: metadata for selected version
        foreach (var metadata in available)
        {
            var existing = result.FirstOrDefault(k => k.Metadata.Name == metadata.Name);
            if (existing is null)
            {
                existing = new ModuleMetadataBundle
                {
                    Metadata = metadata,
                    ModuleId = metadata.Name,
                };

                UpdateMissingDependencies(existing, installed);

                result.Add(existing);
            }

            // if module is defined in manifest but not downloaded...
            var existingVersionStr = existing.Metadata.Version != ModuleConstants.UnresolvedVersionMarker
                ? existing.Metadata.Version : "0.0.0";

            // parse both versions once for comparison and CI version check
            if (!SemVersion.TryParse(existingVersionStr, out var existingSemVer))
                continue;

            if (!SemVersion.TryParse(metadata.Version, out var availableSemVer))
                continue;

            // it's installed already so it also has the available versions - we don't allow downgrade
            // because of migrations etc. - for installed ci-versions we allow downgrades
            if (existing.Installed && CompareSemVersions(availableSemVer, existingSemVer) < 0
                && !IsSameBaseCiVersion(existingSemVer, availableSemVer))
                continue;

            // it happened that different packages contained the same version - this would
            // fail then on cbo binding. this is fixed within pipeline but to ensure it
            // won't break...
            if (existing.AvailableVersions.Any(k => k == metadata.Version))
                continue;

            existing.AvailableVersions.Add(metadata.Version);
            existing.CanUpdate = existing.Installed;    // it's installed and we have at least one update version
        }

        foreach (var bundle in result)
        {
            if (bundle.AvailableVersions.Count == 0)
            {
                // for debug/test modules we don't have available versions
                bundle.AvailableVersions.Add(bundle.Metadata.Version);
                continue;
            }

            // todo - this sorting could maybe done by api query
            bundle.AvailableVersions.Sort((x, y) =>
            {
                _ = SemVersion.TryParse(x, out var xSemVer);
                _ = SemVersion.TryParse(y, out var ySemVer);
                return CompareSemVersions(ySemVer, xSemVer); // descending
            });
        }

        return result;
    }

    public static void UpdateMissingDependencies(ModuleMetadataBundle model, IReadOnlyCollection<ModuleMetadataBundle> installed)
    {
        model.MissingDependencies.Clear();

        if (model.Metadata.Dependencies is null)
            return;

        model.MissingDependencies.AddRange(model.Metadata.Dependencies
            .Where(k => !installed.Any(i => IsDependencySupported(i.Metadata, k))));
    }

    private static int CompareSemVersions(SemVersion? x, SemVersion? y)
    {
        if (x is null && y is null)
            return 0;

        if (x is not null && y is null)
            return 1;

        if (x is null && y is not null)
            return -1;

        return SemVersion.CompareSortOrder(x, y);
    }

    private static bool IsSameBaseCiVersion(SemVersion? existing, SemVersion? available) =>
        existing is not null && IsCiVersion(existing)
        && available is not null && IsCiVersion(available)
        && existing.Major == available.Major
        && existing.Minor == available.Minor
        && existing.Patch == available.Patch;

    private static bool IsCiVersion(SemVersion version) =>
        version.IsPrerelease && version.Prerelease.StartsWith("ci", StringComparison.OrdinalIgnoreCase);

    private static bool IsDependencySupported(ModuleMetadata metadata, ModuleDependencyPackage dependency)
    {
        if (metadata.Name != dependency.Name || metadata.Version == ModuleConstants.UnresolvedVersionMarker)
            return false;

        if (metadata.Version == dependency.Version)
            return true;

        if (!SemVersion.TryParse(metadata.Version, out var metaVersion))
            return false;

        if (!SemVersion.TryParse(dependency.Version, out var dependencyVersion))
            return false;

        // if major, minor fit it's compatible for sure - in our modules the version is not yet ready to
        // fully support SemVer
        return metaVersion.Major == dependencyVersion.Major && metaVersion.Minor == dependencyVersion.Minor;
    }
}
