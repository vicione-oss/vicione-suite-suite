using Core.Shared.Instance.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserInterface.ControlPanels.Theme.Services;

internal sealed class ThemeControlPanelResetHandler(ILoginDesignService loginDesignService)
    : IControlPanelResetHandler<ThemeControlPanelState>
{
    public Task Reset(ThemeControlPanelState state, CancellationToken cancellationToken)
    {
        state.SelectedLoginDesign = loginDesignService.Design;

        return Task.CompletedTask;
    }
}
