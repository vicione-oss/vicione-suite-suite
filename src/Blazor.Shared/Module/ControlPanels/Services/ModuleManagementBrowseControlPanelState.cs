using Blazor.Shared.Module.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

public sealed class ModuleManagementBrowseControlPanelState(IModuleManagementService managementService)
    : ModuleManagementControlPanelStateBase(managementService)
{
    /// <summary>
    /// When <see langword="false"/>, the available modules are listed, but cannot be installed.
    /// </summary>
    internal bool AllowInstallation
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged();
        }
    }

    public int RequestErrorCode
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;

                OnPropertyChanged();
            }
        }
    }

    public string? RequestErrorMessage
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;

                OnPropertyChanged();
            }
        }
    }
}
