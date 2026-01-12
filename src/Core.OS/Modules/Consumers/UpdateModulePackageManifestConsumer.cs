using Core.Module;
using Core.Module.Extensions;
using Core.OS.Instance;
using Core.Shared.HostManagement.Events;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Sdk.Modules;
using Semver;

namespace Core.OS.Modules.Consumers;

public sealed class UpdateModulePackageManifestConsumer(
    IModuleManifestProvider manifestProvider,
    ILocalInstanceInformationProvider localInstance,
    IModuleHost moduleHost,
    IModuleMigrator moduleMigrator,
    ILogger<UpdateModulePackageManifestConsumer> logger) : IConsumer<UpdateModulePackageManifest>
{
    private readonly Guid _localInstanceId = localInstance.Local.Id;
    public async Task Consume(ConsumeContext<UpdateModulePackageManifest> context)
    {
        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} ModuleCount:{Count}",
            nameof(UpdateModulePackageManifest),
            context.CorrelationId,
            context.Message.Manifest.Packages.Count);

        var success = false;
        try
        {
            // remove our well known sample modules
            context.Message.Manifest.Packages.RemoveAll(k
                => ModuleConstants.SampleModuleIds.Contains(k.Name) || k.Version == Constants.ModuleCiVersionKey);

            var currentManifest = manifestProvider.GetManifest();

            // clear out modules locally debugged
            HandleDebugPackages(context.Message.Manifest, currentManifest);

            // ensure no 'breaking' version gets written to manifest
            SanitizePackages(context.Message.Manifest);

            // todo - restore old on failure?
            await manifestProvider.UpdateManifestPackages(context.Message.Manifest.Packages, context.CancellationToken);

            // prepare module migration on version update 
            await moduleMigrator.PrepareUpdateMigration(context.Message.Manifest, context.CancellationToken);

            // log used packages to identify restart issues more easily
            logger.LogInformation("Module manifest update with {Count} packages: {Packages}",
                context.Message.Manifest.Packages.Count,
                string.Join(", ", context.Message.Manifest.Packages
                    .OrderBy(p => p.Name)
                    .Select(p => $"{p.Name}:{p.Version}")));

            success = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update package manifest on instance {InstanceId}", _localInstanceId);
        }

        await context.Publish(new ModuleManifestUpdatedEvent(localInstance.Local.Id, success));

        if (success)
            await context.Publish(new SystemRestartRequired(context.Message.CorrelationId, RestartReason.ModuleConfiguration), context.CancellationToken);
    }

    private void SanitizePackages(ModulePackageManifest target)
    {
        // if some weird things are sent as package version we need
        // to reset them to latest to be sure it breaks nothing
        foreach (var package in target.Packages)
        {
            // we have a valid version - good
            if (SemVersion.TryParse(package.Version, out _))
                continue;

            // something invalid so reset to autoresolve
            package.Version = ModuleConstants.LatestVersionKey;
        }
    }

    private void HandleDebugPackages(ModulePackageManifest target, ModulePackageManifest current)
    {
        // we display the debug/sample modules as installed but we don't want to have 
        // them in the manifest - except a version was selected
        var debugModules = moduleHost.GetContext()
            .Modules
            .Where(k => k.IsDebugSource)
            .Select(CreateKeyValuePair)
            .DistinctBy(k => k.Key)
            .ToList();

        if (debugModules.Count == 0)
            return;

        foreach (var package in target.Packages)
        {
            // not debugged
            var debugged = debugModules.FirstOrDefault(k => k.Key == package.Name);
            if (debugged.Key is null)
                continue;

            // debugged but with selected version -> install
            if (debugged.Value != package.Version)
                continue;

            var currentPackage = current.Packages.FirstOrDefault(k => k.Name == package.Name);
            if (currentPackage is null)
                continue;

            // keep version from current manifest
            package.Version = currentPackage.Version;
        }
        return;

        static KeyValuePair<string, string> CreateKeyValuePair(ModuleDependencyContext context)
            => new(context.ModuleId, context.GetMainLibrary().Version);
    }
}
