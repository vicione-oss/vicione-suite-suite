using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Burger.Client.ControlPanels.Services;

internal sealed class BurgerControlPanelSaveHandler()
    : IControlPanelSaveHandler<BurgerControlPanelState>
{
    public async Task<ISaveResult> Save(BurgerControlPanelState state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(state.Description))
            throw new InvalidOperationException("No description provided");

        return new SaveSuccessResult();
    }
}
