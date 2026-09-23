using Core.Module.Options;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Modules;
using Semver;

namespace Core.OS.Modules.Services;

public sealed partial class ModuleMetadataProvider : IModuleMetadataProvider
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
            availableMetadata = await _metadataCache.GetAvailableModuleMetadata(options.ForceRefresh, cancellationToken);
        }

        // Metadata versions are merged into bundles, ordered by their published date.
        var bundles = CreateBundles(installedBundles, availableMetadata?.OrderByDescending(k => k.Published).ToList() ?? []);

        // Versions drive the bundle state: can-update, errors and the rest.
        ApplyVersionEvaluation(bundles);

        // Pending operations have to be applied before the options are enriched.
        // because they might change the options (e.g. pending uninstall should not allow modification)
        await ApplyEnqueuedPackageOperations(bundles, cancellationToken);

        // Environment and stored options enrich the metadata with the current values.
        // and also set the flags for secrets/env vars so that the UI can handle them accordingly
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
            // With installation disabled by config, installed modules cannot be modified.
            module.CanBeModified = _loaderOptions.Value.AllowInstallation;

            if (!module.Installed)
                continue;

            try
            {
                // The metadata now carries the current option declarations.
                // we can't mix up env, secrets here!!! we need to flag them later
                // !! options might change between requests so we need to enrich them again
                await _optionsStore.ApplyStoredOptions(module.Metadata, _environmentConfig, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ignored, but the loop stops here.
                break;
            }
            catch (Exception ex)
            {
                LogFailedToEnrichOptions(_logger, ex, module.Metadata.Name);
            }
        }
    }

    public static List<ModuleMetadataBundle> CreateBundles(IReadOnlyCollection<ModuleMetadataBundle> installed, List<ModuleMetadata> available)
    {
        var result = new List<ModuleMetadataBundle>();

        // Already installed modules.
        result.AddRange(installed);

        // Available packages of uninstalled modules drop out when they cannot be modified.
        if (installed.Count > 0 && installed.All(k => !k.CanBeModified))
        {
            available.RemoveAll(k => installed.All(i => i.Metadata.Name != k.Name));
        }

        // Available modules: dependencies and versions.
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

            // Defined in the manifest but not downloaded.
            var existingVersionStr = existing.Metadata.Version != ModuleConstants.UnresolvedVersionMarker
                ? existing.Metadata.Version : "0.0.0";

            // Both versions are parsed once, for comparison and the CI version check.
            if (!SemVersion.TryParse(existingVersionStr, out var existingSemVer))
                continue;

            if (!SemVersion.TryParse(metadata.Version, out var availableSemVer))
                continue;

            // Already installed, so the available versions are known; downgrades are not allowed.
            // because of migrations etc. - for installed ci-versions we allow downgrades
            if (existing.Installed && CompareSemVersions(availableSemVer, existingSemVer) < 0
                && !IsSameBaseCiVersion(existingSemVer, availableSemVer))
                continue;

            // Different packages have carried the same version before, which would
            // fail then on cbo binding. this is fixed within pipeline but to ensure it
            // won't break...
            if (existing.AvailableVersions.Any(k => k == metadata.Version))
                continue;

            existing.AvailableVersions.Add(metadata.Version);
        }

        return result;
    }

    private static void ApplyVersionEvaluation(List<ModuleMetadataBundle> bundles)
    {
        foreach (var bundle in bundles)
        {
            if (bundle.AvailableVersions.Count == 0)
            {
                // Debug and test modules have no available versions.
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

            if (!bundle.Installed)
                continue;

            var latest = bundle.AvailableVersions.First();
            
            // Only released versions are resolved:
            if (!SemVersion.TryParse(latest, out var latestSemVer))
                continue;

            // A new artifact source was added after the initial resolve failed.
            // we want to display that it will be installed after next restart.
            // ci versions are not considered for automatic update
            var removedError = bundle.Errors.RemoveAll(k => k.ErrorCode == ModuleErrorCodes.FoundNoVersion);
            if (removedError > 0 && bundle.Metadata.Version == ModuleConstants.UnresolvedVersionMarker && !latestSemVer.IsPrerelease)
            {
                var dependency = new ModuleDependencyPackage
                {
                    Name = bundle.Metadata.Name,
                    Version = latest
                };
                bundle.PendingOperation = new ModulePackageOperation(dependency, ModulePackageOperationKind.Install);
                continue;
            }

            // A newer version plus a resolvable module means an update is possible.
            bundle.CanUpdate = (bundle.Metadata.Version != ModuleConstants.UnresolvedVersionMarker || latestSemVer.IsPrerelease)
                && bundle.Metadata.Version != latest;
        }
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

        // Matching major and minor is certainly compatible; the module versions are not yet ready to
        // fully support SemVer
        return metaVersion.Major == dependencyVersion.Major && metaVersion.Minor == dependencyVersion.Minor;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to enrich metadata options for module '{Module}'")]
    private static partial void LogFailedToEnrichOptions(ILogger<ModuleMetadataProvider> logger, Exception ex, string? module);
}
