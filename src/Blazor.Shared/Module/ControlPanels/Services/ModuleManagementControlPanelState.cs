using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

public sealed class ModuleManagementControlPanelState : ControlPanelState
{
    private bool _allowPreReleases;
    private int _requestErrorCode;
    private string? _requestErrorMessage;

    internal string InstalledFilterText { get; set; } = string.Empty;
    internal string AvailableFilterText { get; set; } = string.Empty;

    internal List<ModuleMetadataModel> InstalledModules
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(InstalledModules));
        }
    } = [];

    internal List<ModuleMetadataModel> AvailableModules
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(AvailableModules));
        }
    } = [];

    public bool AllowPreReleases
    {
        get => _allowPreReleases;
        set
        {
            if (value != _allowPreReleases)
            {
                _allowPreReleases = value;

                OnPropertyChanged();
            }
        }
    }

    public int RequestErrorCode
    {
        get => _requestErrorCode;
        set
        {
            if (value != _requestErrorCode)
            {
                _requestErrorCode = value;

                OnPropertyChanged();
            }
        }
    }

    public string? RequestErrorMessage
    {
        get => _requestErrorMessage;
        set
        {
            if (value != _requestErrorMessage)
            {
                _requestErrorMessage = value;

                OnPropertyChanged();
            }
        }
    }

    internal bool IsInitialized { get; private set; }

    internal List<ModulePackageOperation> InstallOperations { get; } = [];

    internal List<ModulePackageOperation> UninstallOperations { get; } = [];

    internal void Initialize(List<ModuleMetadataModel> models)
    {
        UninstallOperations.Clear();
        InstallOperations.Clear();

        // split them for the tabs
        InstalledModules = [.. models.Where(k => k.Installed).OrderBy(k => k.Title)];
        AvailableModules = [.. models.Where(k => !k.Installed).OrderBy(k => k.Title)];
    }

    internal async Task CancelEditInOptionGrids()
    {
        foreach (var installedModule in InstalledModules)
        {
            if (installedModule.OptionGrid is not null)
            {
                await installedModule.OptionGrid.CancelEdit();
                installedModule.HasModifiedOptions = false;
            }
        }
    }

    internal void UpdateInstallOperations(List<ModulePackageOperation> operations)
    {
        foreach (var operation in operations)
        {
            var exists = InstallOperations.FirstOrDefault(k => k.Package.Name == operation.Package.Name && k.OperationKind == operation.OperationKind);
            if (exists is not null)
                InstallOperations.Remove(operation);

            InstallOperations.Add(operation);
        }
    }

    internal void UpdateUninstallOperations(List<ModulePackageOperation> operations)
    {
        foreach (var operation in operations)
        {
            var exists = UninstallOperations.FirstOrDefault(k => k.Package.Name == operation.Package.Name && k.OperationKind == operation.OperationKind);
            if (exists is not null)
                UninstallOperations.Remove(operation);

            UninstallOperations.Add(operation);
        }
    }

    internal bool ApplyOperationChanges(ModulePackageOperationsChanged changes)
    {
        var hasChanged = false;
        foreach (var change in changes.Changes)
        {
            var installed = InstalledModules.FirstOrDefault(k => k.Name == change.Operation.Package.Name);
            if (installed != null)
            {
                installed.PendingOperation = UpdateOperation(installed.PendingOperation, change);
                hasChanged = true;
                continue;
            }

            var available = AvailableModules.FirstOrDefault(k => k.Name == change.Operation.Package.Name);
            if (available != null)
            {
                available.PendingOperation = UpdateOperation(available.PendingOperation, change);
                hasChanged = true;
            }
        }
        return hasChanged;

        ModulePackageOperation? UpdateOperation(ModulePackageOperation? pending, ModulePackageChange change)
        {
            if (pending?.OperationKind == ModulePackageOperationKind.Install &&
                    change.Operation.OperationKind == ModulePackageOperationKind.Uninstall)
            {
                return null;
            }

            if (pending?.OperationKind == ModulePackageOperationKind.Uninstall &&
                    change.Operation.OperationKind == ModulePackageOperationKind.Install)
            {
                return null;
            }

            return change.Operation;
        }
    }

    internal bool HasPendingChanges() { return InstallOperations.Count > 0 || UninstallOperations.Count > 0; }
}
