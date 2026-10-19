using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

/// <summary>
/// The modules of one module management tab and the package operations enqueued on it.
/// </summary>
/// <remarks>
/// The state applies <see cref="IModuleManagementService.OperationsChanged"/> itself, so a tab that is not mounted, or was
/// saved while not mounted, still shows the current pending operations.
/// </remarks>
public abstract class ModuleManagementControlPanelStateBase : ControlPanelState, IDisposable
{
    /// <summary>
    /// The property name <see cref="ControlPanelState.Changed"/> carries when pending operations of <see cref="Modules"/> change.
    /// </summary>
    internal const string PendingOperationsChanged = nameof(PendingOperationsChanged);

    private readonly IModuleManagementService _managementService;

    internal string? FilterText { get; set; }

    internal IQueryable<ModuleMetadataModel> Modules
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(Modules));
        }
    } = Enumerable.Empty<ModuleMetadataModel>().AsQueryable();

    internal List<ModulePackageOperation> EnqueuedOperations { get; } = [];

    protected ModuleManagementControlPanelStateBase(IModuleManagementService managementService)
    {
        _managementService = managementService;
        _managementService.OperationsChanged += ManagementServiceOperationsChanged;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _managementService.OperationsChanged -= ManagementServiceOperationsChanged;
    }

    private Task ManagementServiceOperationsChanged(ModulePackageOperationsChanged changes, CancellationToken cancellationToken)
    {
        if (this.ApplyOperationChanges(changes))
            OnPropertyChanged(PendingOperationsChanged);

        return Task.CompletedTask;
    }
}
