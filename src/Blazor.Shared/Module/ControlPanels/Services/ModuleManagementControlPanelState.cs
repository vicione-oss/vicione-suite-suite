using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

public sealed class ModuleManagementControlPanelState : ControlPanelState
{
    internal string? InstalledFilterText { get; set; }
    internal string? AvailableFilterText { get; set; }

    internal IQueryable<ModuleMetadataModel> InstalledModules
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(InstalledModules));
        }
    } = Enumerable.Empty<ModuleMetadataModel>().AsQueryable();

    internal IQueryable<ModuleMetadataModel> AvailableModules
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(AvailableModules));
        }
    } = Enumerable.Empty<ModuleMetadataModel>().AsQueryable();

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

    internal bool AllowInstallation { get; set; }

    internal List<ModulePackageOperation> InstallOperations { get; } = [];

    internal List<ModulePackageOperation> UninstallOperations { get; } = [];
}
