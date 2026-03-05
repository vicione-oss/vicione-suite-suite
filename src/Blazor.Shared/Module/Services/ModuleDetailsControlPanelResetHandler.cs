using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.Services;

public sealed class ModuleDetailsControlPanelResetHandler : IControlPanelResetHandler<ModuleDetailsControlPanelState>
{
    public Task Reset(ModuleDetailsControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.ModuleMetadata is null)
            return Task.CompletedTask;

        state.VersionToInstall = string.Empty;

        if (state.ModuleMetadata.PendingOperation is null)
            return Task.CompletedTask;

        if (state.ModuleMetadata.PendingOperation.OperationKind == Core.Shared.Modules.Contracts.ModulePackageOperationKind.Install)
        {
            state.VersionToInstall = state.ModuleMetadata.PendingOperation.Package.Version;
        }

        return Task.CompletedTask;
    }
}
