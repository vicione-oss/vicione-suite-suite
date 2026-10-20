using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Services;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

/// <summary>
/// Resets the part of the shared <see cref="NetworkInterfaceControlPanelState"/> that one tab shows, leaving the edits of the
/// other tabs untouched.
/// </summary>
internal abstract class NetworkInterfaceControlPanelResetHandlerBase(IUiMediator mediator, ISystemConfigurationService systemConfigurationService)
    : IControlPanelResetHandler<NetworkInterfaceControlPanelState>
{
    public async Task Reset(NetworkInterfaceControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is null)
                ResetToDefaults(state);
            else
            {
                state.Name = networkInterface.CommonInformation.Name;

                await Reset(state, networkInterface, cancellationToken);
            }

            ClearChanges(state);
        }
        finally
        {
            state.EndLoading();
        }
    }

    protected abstract Task Reset(NetworkInterfaceControlPanelState state, NetworkInterfaceDetail networkInterface, CancellationToken cancellationToken);

    protected abstract void ResetToDefaults(NetworkInterfaceControlPanelState state);

    protected abstract void ClearChanges(NetworkInterfaceControlPanelState state);

    protected Task UpdateDHCPLease(NetworkInterfaceControlPanelState state, NetworkInterfaceDetail networkInterface, CancellationToken cancellationToken)
        => state.UpdateDHCPLease(mediator, networkInterface, cancellationToken);

    protected async Task UpdateOriginalPhysicalAddress(NetworkInterfaceControlPanelState state, CancellationToken cancellationToken)
    {
        var response = await mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
            new GetOriginalPhysicalAddress(state.Name), cancellationToken);

        state.OriginalPhysicalAddress = response.OriginalPhysicalAddress;
    }
}
