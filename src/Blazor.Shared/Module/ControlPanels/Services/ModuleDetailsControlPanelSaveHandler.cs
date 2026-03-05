using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Modules;

namespace Blazor.Shared.Module.ControlPanels.Services;

public sealed class ModuleDetailsControlPanelSaveHandler(IModuleManagementService mgmtService, IMessageBannerService messageBannerService) : IControlPanelSaveHandler<ModuleDetailsControlPanelState>
{
    public async Task<ISaveResult> Save(ModuleDetailsControlPanelState state, CancellationToken cancellationToken)
    {
        try
        {
            if (state.ModuleMetadata is null)
                return new SaveErrorResult("Is empty");

            if (state.ModuleMetadata.PendingOperation is null
                && state.ModuleMetadata.Installed
                && state.VersionToInstall == state.ModuleMetadata.Version)
                return new SaveErrorResult("Version is already installed");

            await UpdateModulePackage(state, cancellationToken);

            await UpdateModuleOptions(state, cancellationToken);

            messageBannerService.ShowMessageBanner(MessageType.Warning, MessageBanner.Localization.MessageBanner.SuiteRestartRequired);

            return new SaveSuccessResult();
        }
        catch (Exception e)
        {
            return new SaveErrorResult(e.Message);
        }
    }

    private async Task UpdateModulePackage(ModuleDetailsControlPanelState state, CancellationToken cancellationToken = default)
    {
        var package = new ModuleDependencyPackage
        {
            Name = state.ModuleMetadata!.Name,
            Version = state.VersionToInstall
        };
        var operation = new ModulePackageOperation(package, ModulePackageOperationKind.Install);

        await mgmtService.SendUpdateModulePackages([operation], cancellationToken);
    }

    private async Task UpdateModuleOptions(ModuleDetailsControlPanelState state, CancellationToken cancellationToken = default)
    {
        var module = state.ModuleMetadata!;
        if (!module.HasModifiedOptions || string.IsNullOrWhiteSpace(module.ModuleId))
            return;

        await mgmtService.SendUpdateModuleOptions(module.ModuleId, module.EditOptions.Values, cancellationToken);

        module.HasModifiedOptions = false;
    }
}
