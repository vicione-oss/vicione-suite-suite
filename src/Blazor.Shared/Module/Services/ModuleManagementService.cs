using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Requests;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Modules;

namespace Blazor.Shared.Module.Services;

internal sealed class ModuleManagementService(IUiMediator mediator, IInstanceInformationProvider informationProvider) : IModuleManagementService
{
    public string GetSdkVersion()
        => informationProvider.Local.SdkVersion;

    public async Task<GetModuleMetadataBundlesResponse> GetModuleMetadata(bool addPreReleases = false, bool forceReload = false, CancellationToken cancellationToken = default)
    {
        Version? sdkVersion = null;
        if (Version.TryParse(GetSdkVersion(), out var parsedVersion))
        {
            // is it the debug version?
            sdkVersion = new Version(parsedVersion.Major, parsedVersion.Minor, parsedVersion.Build);
        }

        var request = new GetModuleMetadataBundlesRequest(true, true, sdkVersion, addPreReleases, forceReload);
        return await mediator.Request<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>(request, cancellationToken);
    }

    public async Task UpdateModulePackageVersions(List<ModuleMetadataModel> metadataModels, CancellationToken cancellationToken = default)
    {
        var manifest = new ModulePackageManifest();
        var installedPackages = metadataModels
            .Where(k => k.Installed)
            .Select(k => CreateDependencyPackage(k, true));

        var addedMetadata = metadataModels
            .Where(k => k.ToBeInstalled)
            .ToList();

        var addedPackages = addedMetadata
            .Select(k => CreateDependencyPackage(k, false));

        manifest.Packages.AddRange(installedPackages);
        manifest.Packages.AddRange(addedPackages);

        var command = new UpdateModulePackageManifest(manifest);
        await mediator.Send(command, cancellationToken);

        foreach (var package in addedMetadata)
        {
            if (!package.HasModifiedOptions)
                continue;

            await UpdateModuleOptions(package.Name, package.EditOptions.Values, cancellationToken);
        }
    }

    private static ModuleDependencyPackage CreateDependencyPackage(ModuleMetadataModel model, bool alreadyInstalled)
    {
        // if a module is already installed it can only set the update version
        // if it's a fresh installed one we use the selected version
        var targetVersion = alreadyInstalled && string.IsNullOrEmpty(model.UpdateVersion)
            ? model.Version
            : model.SelectedVersion;

        // the versions can contain a real version like x.y.z or ci-[pipeline_id]
        if (!string.IsNullOrEmpty(model.UpdateVersion))
        {
            // update to new pre-release
            return new()
            {
                Name = model.Name,
                Version = model.UpdateVersion,// we can't access "real" version when pre-release is used
                DependingOn = model.Dependencies,
            };
        }
        // keep version ? ci ?
        return new()
        {
            Name = model.Name,
            Version = targetVersion ?? model.Version,
            DependingOn = model.Dependencies,
        };
    }

    public async Task UpdateModuleOptions(string moduleId, IEnumerable<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default)
    {
        var command = new UpdateModuleOptions(moduleId, options.Where(k => k.Value != Core.Shared.Constants.SetByEnvironmentMarker).ToList());
        await mediator.Send(command, cancellationToken);
    }
}
