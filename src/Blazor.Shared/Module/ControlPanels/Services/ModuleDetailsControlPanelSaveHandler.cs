using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Modules;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleDetailsControlPanelSaveHandler(IModuleManagementService mgmtService, IMessageBannerService messageBannerService) : IControlPanelSaveHandler<ModuleDetailsControlPanelState>
{
    public async Task<ISaveResult> Save(ModuleDetailsControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.ModuleMetadata is null)
            return new SaveErrorResult("Module is not available");

        // Options
        var optionsResult = await UpdateOptions(state, cancellationToken);
        if (optionsResult is ModuleManagementServiceErrorResult optionsError)
            return new SaveErrorResult(optionsError.ErrorMessage, optionsError.ErrorCode);

        state.ModuleMetadata.HasModifiedOptions = false;

        // Package operations
        if (state.ModuleMetadata.PendingOperation is null
            && state.ModuleMetadata.Installed
            && state.VersionToInstall == state.ModuleMetadata.Version)
        {
            return new SaveSuccessResult(); // The requested version is already installed.
        }

        var updateResult = await UpdateOperations(state, cancellationToken);
        if (updateResult is ModuleManagementServiceErrorResult error)
            return new SaveErrorResult(error.ErrorMessage, error.ErrorCode);

        messageBannerService.ShowMessageBanner(MessageType.Warning, MessageBanner.Localization.MessageBanner.SuiteRestartRequired);

        return new SaveSuccessResult();
    }

    private async Task<IModuleManagementServiceResult> UpdateOptions(ModuleDetailsControlPanelState state, CancellationToken cancellationToken = default)
    {
        var module = state.ModuleMetadata!;
        if (!module.HasModifiedOptions || string.IsNullOrWhiteSpace(module.ModuleId))
            return new ModuleManagementServiceSuccessResult();

        return await mgmtService.UpdateOptions(module.ModuleId, module.EditOptions.Values, cancellationToken);
    }

    private async Task<IModuleManagementServiceResult> UpdateOperations(ModuleDetailsControlPanelState state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state.VersionToInstall))
            return new ModuleManagementServiceErrorResult("Version to install is not specified");

        var package = new ModuleDependencyPackage
        {
            Name = state.ModuleMetadata!.Name,
            Version = state.VersionToInstall
        };
        var operation = new ModulePackageOperation(package, ModulePackageOperationKind.Install);

        return await mgmtService.UpdateOperations([operation], cancellationToken);
    }
}
