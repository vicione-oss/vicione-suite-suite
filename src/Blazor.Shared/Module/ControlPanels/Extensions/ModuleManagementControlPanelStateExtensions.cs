using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;

namespace Blazor.Shared.Module.ControlPanels.Extensions;

internal static class ModuleManagementControlPanelStateExtensions
{
    extension(ModuleManagementControlPanelStateBase state)
    {
        internal void SetModules(List<ModuleMetadataModel> models)
        {
            state.EnqueuedOperations.Clear();

            state.Modules = models.OrderBy(k => k.Title).AsQueryable();
        }

        internal void EnqueueOperations(List<ModulePackageOperation> operations)
        {
            foreach (var operation in operations)
            {
                var exists = state.EnqueuedOperations.FirstOrDefault(k => k.Package.Name == operation.Package.Name && k.OperationKind == operation.OperationKind);
                if (exists is not null)
                    state.EnqueuedOperations.Remove(exists);

                state.EnqueuedOperations.Add(operation);
            }
        }

        internal async Task<ISaveResult> SaveEnqueuedOperations(IModuleManagementService managementService, CancellationToken cancellationToken)
        {
            if (!state.HasPendingChanges())
                return new SaveSuccessResult();

            var result = await managementService.UpdateOperations([.. state.EnqueuedOperations], cancellationToken);
            if (result is ModuleManagementServiceErrorResult error)
                return new SaveErrorResult(error.ErrorMessage, error.ErrorCode);

            state.EnqueuedOperations.Clear();

            return new SaveSuccessResult();
        }

        internal bool ApplyOperationChanges(ModulePackageOperationsChanged changes)
        {
            var hasChanged = false;
            foreach (var change in changes.Changes)
            {
                var module = state.Modules.FirstOrDefault(k => k.Name == change.Operation.Package.Name);
                if (module is null)
                    continue;

                module.PendingOperation = UpdateOperation(module.PendingOperation, change);
                hasChanged = true;
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

                return change.Action != Sdk.Messaging.CrudAction.Deleted ? change.Operation : null;
            }
        }

        internal bool HasPendingChanges() => state.EnqueuedOperations.Count > 0;
    }

    extension(ModuleManagementInstalledControlPanelState state)
    {
        internal async Task CancelEditInOptionGrids()
        {
            foreach (var installedModule in state.Modules)
            {
                if (installedModule.OptionGrid is not null)
                {
                    await installedModule.OptionGrid.CancelEdit();
                    installedModule.HasModifiedOptions = false;
                }
            }
        }
    }

    extension(ModuleManagementBrowseControlPanelState state)
    {
        internal async Task LoadAvailableModules(IModuleManagementService managementService, bool forceReload, ILogger logger,
            CancellationToken cancellationToken)
        {
            state.BeginLoading();
            try
            {
                var result = await managementService.GetAvailableMetadata(forceReload, cancellationToken);

                state.RequestErrorCode = 0;
                state.RequestErrorMessage = null;
                state.AllowInstallation = result.AllowInstallation;
                state.SetModules(result.Modules);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to load module metadata");

                state.RequestErrorCode = 1;
                state.RequestErrorMessage = Localization.ModuleManagementBrowseControlPanel.ModuleMetadataLoadFailed;
            }
            finally
            {
                state.EndLoading();
            }
        }
    }
}
