using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Module.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ModuleManagementControlPanel : ControlPanelBase<ModuleManagementControlPanelState>
{
    private readonly string _restartIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _reloadIconCssClasses = MonochromeIconName.Reload.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _installIconCssClasses = MonochromeIconName.Import.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _uninstallIconCssClasses = MonochromeIconName.UninstallLight.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _updateIconCssClasses = MonochromeIconName.InstallPendingLight.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();

    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];
    private bool _dialogVisible;

    [Inject]
    internal IModuleManagementService ManagementService { get; set; } = default!;

    [Inject]
    public ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject]
    private IInstanceInformationProvider InformationProvider { get; set; } = default!;

    [Inject]
    private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject]
    public ILogger<ModuleManagementControlPanel> Logger { get; set; } = default!;

    [Inject(Key = typeof(InstalledModuleManagementControlPanelServiceKey))]
    private IGridItemSelection<ModuleMetadataModel> InstalledModuleSelection { get; set; } = default!;

    [Inject(Key = typeof(AvailableModuleManagementControlPanelServiceKey))]
    private IGridItemSelection<ModuleMetadataModel> AvailableModuleSelection { get; set; } = default!;

    private bool IsStandalone => InformationProvider.Local.Type == InstanceType.Standalone;

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        State.Changed -= StateChanged;
        ManagementService.OperationsChanged -= ManagementServiceOperationsChanged;

        InstalledModuleSelection.Changed -= InstalledModuleSelectionChanged;
        AvailableModuleSelection.Changed -= AvailableModuleSelectionChanged;

        await base.DisposeAsyncCore();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        InstalledModuleSelection.Clear();
        AvailableModuleSelection.Clear();

        State.Changed += StateChanged;
        ManagementService.OperationsChanged += ManagementServiceOperationsChanged;

        InstalledModuleSelection.Changed += InstalledModuleSelectionChanged;
        AvailableModuleSelection.Changed += AvailableModuleSelectionChanged;
    }

    private async Task ManagementServiceOperationsChanged(ModulePackageOperationsChanged changes, CancellationToken token)
    {
        var hasChanged = State.ApplyOperationChanges(changes);
        if (hasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs args)
    {
        var stateHasChanged = false;

        if (args.PropertyNames.Contains(nameof(State.InstalledModules)))
        {
            InstalledModuleSelection.Clear();
            stateHasChanged = true;
        }
        else if (args.PropertyNames.Contains(nameof(State.AvailableModules)))
        {
            AvailableModuleSelection.Clear();
            stateHasChanged = true;
        }

        if (stateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async void AvailableModuleSelectionChanged(GridItemSelectionChangedEventArgs<ModuleMetadataModel> args)
        => await InvokeAsync(StateHasChanged);

    private async void InstalledModuleSelectionChanged(GridItemSelectionChangedEventArgs<ModuleMetadataModel> args)
        => await InvokeAsync(StateHasChanged);

    private async Task LoadAvailableModuleVersions(bool forceReload = false)
    {
        InstalledModuleSelection.Clear();
        AvailableModuleSelection.Clear();

        State.BeginLoading();
        try
        {
            await State.CancelEditInOptionGrids();

            // Loads the metadata assets and jsons in one step.
            var response = await ManagementService.GetMetadata(forceReload);

            // Triggers StateChanged, which updates the queryables and clears the selections.
            State.UpdateModules(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load module metadata");
            State.RequestErrorCode = 1;
            State.RequestErrorMessage = "Failed to load module metadata. Please try again.";
        }
        finally
        {
            State.EndLoading();
        }
    }

    private async Task ConfirmRestart()
    {
        if (IsStandalone)
            await SuiteControlService.RestartInstance();
        else
            await SuiteControlService.RestartAllInstances();
    }

    private IQueryable<ModuleMetadataModel> FilterInstalledItems(IQueryable<ModuleMetadataModel> installedModules)
    {
        if (string.IsNullOrEmpty(State.InstalledFilterText))
            return installedModules.OrderBy(d => d.Name);

        return installedModules
                .Where(i => i.Name.Contains(State.InstalledFilterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name);
    }

    private IQueryable<ModuleMetadataModel> FilterAvailableItems(IQueryable<ModuleMetadataModel> availableModules)
    {
        if (string.IsNullOrEmpty(State.AvailableFilterText))
            return availableModules.OrderBy(d => d.Name);

        return availableModules
                .Where(i => i.Name.Contains(State.AvailableFilterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name);
    }

    private bool CanUninstallSelectedModules()
        => InstalledModuleSelection.Count != 0
        && InstalledModuleSelection.All(i => i.Installed && i.CanBeModified && !i.Bundle.IsDebugSource && i.Bundle.PendingOperation?.OperationKind != ModulePackageOperationKind.Uninstall);

    private async Task UninstallSelectedModules()
    {
        var operations = InstalledModuleSelection
            .GetUninstallOperations()
            .ToList();

        State.EnqueueOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanRevertInstalledSelectedModules()
        => InstalledModuleSelection.Count != 0
        && InstalledModuleSelection.All(k => k.PendingOperation != null);

    private async Task RevertInstalledSelectedModules()
    {
        var operations = InstalledModuleSelection
            .GetRevertOperations()
            .ToList();

        State.EnqueueOperations(operations);

        if (State.HasPendingChanges() || operations.Count > 0)
            await BeginEdit();
    }

    private bool CanUpdateInstalledSelectedModules()
        => InstalledModuleSelection.Count != 0
        && InstalledModuleSelection.All(i => i.CanUpdate && !i.Bundle.IsDebugSource && i.Bundle.PendingOperation == null);

    private async Task UpdateInstalledSelectedModules()
    {
        var operations = InstalledModuleSelection
            .GetUpdateOperations()
            .ToList();

        State.EnqueueOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanInstallSelectedAvailableModules()
        => AvailableModuleSelection.Count != 0
        && AvailableModuleSelection.All(i => i.CanBeModified && !i.Bundle.IsDebugSource && i.Bundle.PendingOperation == null);

    private async Task InstallSelectedAvailableModules()
    {
        var operations = AvailableModuleSelection
            .GetInstallOperations()
            .ToList();

        State.EnqueueOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanResetAvailableSelectedModules()
        => AvailableModuleSelection.Count != 0
        && AvailableModuleSelection.All(k => k.PendingOperation?.OperationKind == ModulePackageOperationKind.Install);

    private async Task RevertAvailableSelectedModules()
    {
        var operations = AvailableModuleSelection
            .GetRevertOperations()
            .ToList();

        State.EnqueueOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private async Task ShowDetailsAsync(ModuleMetadataModel model)
    {
        var _ = await ControlPanelRequest.Send<ModuleDetailsControlPanel, ModuleDetailsControlPanelState>(s =>
        {
            s.ModuleMetadata = model;
        });
    }
}
