using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.Services;

internal sealed partial class UpdateControlPanelCancelHandler : IControlPanelCancelHandler<UpdateControlPanelState>
{
    public Task Cancel(UpdateControlPanelState state, CancellationToken cancellationToken)
    {
        state.SwuFileUploadTicket?.Cancel();

        return Task.CompletedTask;
    }
}
