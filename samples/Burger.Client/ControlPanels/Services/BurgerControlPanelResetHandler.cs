using Sdk.Client.ControlPanels.Services;

namespace Burger.Client.ControlPanels.Services;

internal sealed class BurgerControlPanelResetHandler() : IControlPanelResetHandler<BurgerControlPanelState>
{
    public async Task Reset(BurgerControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            // do some stuff
            await Task.Delay(1000, cancellationToken);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
