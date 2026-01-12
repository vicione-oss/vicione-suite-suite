using Core.Module.Comparer;
using Core.Module.Options;
using Core.Shared.Modules;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using MassTransit;
using Microsoft.Extensions.Options;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Consumers;

public sealed class GetModuleMetadataBundlesConsumer(IModuleMetadataCache metadataCache, IOptions<ModuleLoaderOptions> loaderOptions, ILogger<GetModuleMetadataBundlesConsumer> logger)
    : RequestConsumer<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>
{
    private readonly ModuleLoaderOptions _loaderOptions = loaderOptions.Value;
    private List<ModuleMetadataBundle>? _installedBundles = null;
    private List<ModuleMetadata>? _availableMetadata = null;

    protected override async Task<GetModuleMetadataBundlesResponse> Respond(ConsumeContext<GetModuleMetadataBundlesRequest> context)
    {
        if (context.Message.Installed)
        {
            _installedBundles = await metadataCache.GetInstalledModuleMetadata(context.CancellationToken);

            // if installation is disabled by config installed modules are not allowed to be modified
            if (!_loaderOptions.AllowInstallation)
                _installedBundles.ForEach(k => k.CanBeModified = false);
        }

        if (context.Message.Available)
        {
            _availableMetadata = await metadataCache.GetAvailableModuleMetadata(context.Message.SdkVersion,
                    loaderOptions.Value.AllowPreReleases && context.Message.IncludePreReleases,
                    context.Message.ForceRefresh,
                    context.CancellationToken);

            if (!_loaderOptions.AllowInstallation && _installedBundles is not null)
            {
                // todo: maybe it's better to request only the available versions per installed module here
                // more requests but less data...

                // only updating installed modules is allowed
                _availableMetadata.RemoveAll(k => _installedBundles.All(i => i.Metadata.Name != k.Name));
            }
        }

        // we merge the versions of the metadata to bundles and order them by the published date from metadata
        var bundles = CreateBundles(_installedBundles ?? [], _availableMetadata?.OrderByDescending(k => k.Published).ToList() ?? []);

        return new GetModuleMetadataBundlesResponse(bundles, loaderOptions.Value.AllowPreReleases);
    }

    protected override Task<GetModuleMetadataBundlesResponse> HandleException(ConsumeContext<GetModuleMetadataBundlesRequest> context, Exception e)
    {
        logger.LogError(e, $"Failed to handle {nameof(GetModuleMetadataBundlesRequest)}");

        // if we failed to request available modules we still can deliver installed ones        
        return Task.FromResult(new GetModuleMetadataBundlesResponse(
            _installedBundles?.Count > 0 ? CreateBundles(_installedBundles, []) : [],
            _loaderOptions.AllowPreReleases,
            new(ModuleErrorCodes.RequestVersionsFailed, e.Message)));
    }

    public static List<ModuleMetadataBundle> CreateBundles(List<ModuleMetadataBundle> installed, List<ModuleMetadata> available)
    {
        var result = new List<ModuleMetadataBundle>();

        // handle the already installed ones
        foreach (var bundle in installed)
        {
            result.Add(bundle);
        }

        var comparer = new StringVersionComparer();

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

            // it's installed already so it also has the available versions - we don't allow downgrade
            // because of migrations etc.
            var compareResult = comparer.Compare(metadata.Version, existing.Metadata.Version);
            if (existing.Installed && compareResult < 0)
                continue;

            // it happened that different packages contained the same version - this would
            // fail then on cbo binding. this is fixed within pipeline but to ensure it
            // won't break...
            if (existing.AvailableVersions.Any(k => k == metadata.Version))
                continue;

            existing.AvailableVersions.Add(metadata.Version);
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
            bundle.AvailableVersions.Sort((x, y) => comparer.Compare(y, x));
        }

        return result;
    }

    public static void UpdateMissingDependencies(ModuleMetadataBundle model, List<ModuleMetadataBundle> installed)
    {
        model.MissingDependencies.Clear();

        if (model.Metadata.Dependencies is null)
            return;

        model.MissingDependencies.AddRange(model.Metadata.Dependencies
            .Where(k => !installed.Any(i => IsDependencySupported(i.Metadata, k))));
    }

    private static bool IsDependencySupported(ModuleMetadata metadata, ModuleDependencyPackage dependency)
    {
        if (metadata.Name != dependency.Name)
            return false;

        if (metadata.Version == dependency.Version)
            return true;

        // metadata dependency version can't be parsed so...
        if (!Version.TryParse(metadata.Version, out var metaVersion))
            return false;

        // from actual prerelease versioning we can't know if it's related to a 17.0.0
        if (!Version.TryParse(dependency.Version, out var dependencyVersion))
            return false;

        // this can break like 0.17.0 is needed, but we have 0.18.0 with breaking changes
        return metaVersion >= dependencyVersion;
    }
}
