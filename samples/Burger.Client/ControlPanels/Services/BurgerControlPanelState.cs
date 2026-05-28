using Sdk.Client.ControlPanels.Services;

namespace Burger.Client.ControlPanels.Services;

public sealed class BurgerControlPanelState : ControlPanelState
{
    private string? _description;

    public string? Description
    {
        get => _description;
        set
        {
            if (value != _description)
            {
                _description = value;

                OnPropertyChanged();
            }
        }
    }
}
