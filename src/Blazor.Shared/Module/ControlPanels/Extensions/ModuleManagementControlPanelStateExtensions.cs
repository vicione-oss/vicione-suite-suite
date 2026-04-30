using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;

namespace Blazor.Shared.Module.ControlPanels.Extensions;

internal static class ModuleManagementControlPanelStateExtensions
{
    extension(ModuleManagementControlPanelState state)
    {
        internal void UpdateModules(List<ModuleMetadataModel> models)
        {
            state.UninstallOperations.Clear();
            state.InstallOperations.Clear();

            // no module allows modification so installation is not allowed
            state.AllowInstallation = models.Any(k => k.CanBeModified);

            // split them for the tabs
            state.InstalledModules = models.Where(k => k.Installed).OrderBy(k => k.Title).AsQueryable();
            state.AvailableModules = models.Where(k => !k.Installed).OrderBy(k => k.Title).AsQueryable();
        }

        internal async Task CancelEditInOptionGrids()
        {
            foreach (var installedModule in state.InstalledModules)
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
                var exists = state.InstallOperations.FirstOrDefault(k => k.Package.Name == operation.Package.Name && k.OperationKind == operation.OperationKind);
                if (exists is not null)
                    state.InstallOperations.Remove(operation);

                state.InstallOperations.Add(operation);
            }
        }

        internal void UpdateUninstallOperations(List<ModulePackageOperation> operations)
        {
            foreach (var operation in operations)
            {
                var exists = state.UninstallOperations.FirstOrDefault(k => k.Package.Name == operation.Package.Name && k.OperationKind == operation.OperationKind);
                if (exists is not null)
                    state.UninstallOperations.Remove(operation);

                state.UninstallOperations.Add(operation);
            }
        }

        internal bool ApplyOperationChanges(ModulePackageOperationsChanged changes)
        {
            var hasChanged = false;
            foreach (var change in changes.Changes)
            {
                var installed = state.InstalledModules.FirstOrDefault(k => k.Name == change.Operation.Package.Name);
                if (installed != null)
                {
                    installed.PendingOperation = UpdateOperation(installed.PendingOperation, change);
                    hasChanged = true;
                    continue;
                }

                var available = state.AvailableModules.FirstOrDefault(k => k.Name == change.Operation.Package.Name);
                if (available != null)
                {
                    available.PendingOperation = UpdateOperation(available.PendingOperation, change);
                    hasChanged = true;
                }
            }
            return hasChanged;

            static ModulePackageOperation? UpdateOperation(ModulePackageOperation? pending, ModulePackageChange change)
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

        internal bool HasPendingChanges() => state.InstallOperations.Count > 0 || state.UninstallOperations.Count > 0;
    }
}
