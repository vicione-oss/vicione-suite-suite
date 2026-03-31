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
using Sdk.Modules;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Module.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ModuleManagementControlPanel : ControlPanelBase<ModuleManagementControlPanelState>
{
    private readonly string _restartIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _reloadIconCssClasses = MonochromeIconName.Reload.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _installIconCssClasses = MonochromeIconName.Import.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _uninstallIconCssClasses = MonochromeIconName.UninstallLight.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];
    private bool _dialogVisible;

    [Inject]
    internal IModuleManagementService ManagementService { get; set; } = default!;

    [Inject]
    public ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject]
    private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject]
    public ILogger<ModuleManagementControlPanel> Logger { get; set; } = default!;

    [Inject(Key = typeof(InstalledModuleManagementControlPanelServiceKey))]
    private IGridItemSelection<ModuleMetadataModel> InstalledModuleSelection { get; set; } = default!;

    [Inject(Key = typeof(AvailableModuleManagementControlPanelServiceKey))]
    private IGridItemSelection<ModuleMetadataModel> AvailableModuleSelection { get; set; } = default!;

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        ManagementService.OperationsChanged -= ManagementService_OperationsChanged;
        State.Changed -= StateChanged;

        await base.DisposeAsyncCore();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        State.Changed += StateChanged;

        ManagementService.OperationsChanged += ManagementService_OperationsChanged;

        InstalledModuleSelection.Clear();
        InstalledModuleSelection.Changed -= InstalledModuleSelectionChanged;
        InstalledModuleSelection.Changed += InstalledModuleSelectionChanged;

        AvailableModuleSelection.Clear();
        AvailableModuleSelection.Changed -= AvailableModuleSelectionChanged;
        AvailableModuleSelection.Changed += AvailableModuleSelectionChanged;
    }

    private async Task ManagementService_OperationsChanged(ModulePackageOperationsChanged changes, CancellationToken token)
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

            // load metadata assets and jsons in one step
            var response = await ManagementService.GetMetadata(forceReload);

            // will trigger StateChanged which will update the queryables and clear the selections
            State.Initialize(response);
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

    private async Task RestartSuite()
        => await SuiteControlService.RestartSuite();

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
            .Distinct()
            .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
            {
                Name = item.Name,
                Version = item.Version,
            },
            ModulePackageOperationKind.Uninstall))
            .ToList();

        State.UpdateUninstallOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanResetInstalledSelectedModules()
        => InstalledModuleSelection.Count != 0
        && InstalledModuleSelection.All(k => k.PendingOperation?.OperationKind == ModulePackageOperationKind.Uninstall);

    private async Task ResetInstalledSelectedModules()
    {
        var operations = InstalledModuleSelection
            .Where(k => k.PendingOperation != null && k.PendingOperation.OperationKind == ModulePackageOperationKind.Uninstall)
            .Distinct()
            .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
            {
                Name = item.Name,
                Version = item.PendingOperation!.Package.Version,
            },
            ModulePackageOperationKind.Install))
            .ToList();

        State.UpdateUninstallOperations(operations);

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
            .Distinct()
            .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
            {
                Name = item.Name,
                Version = item.Version,
            },
            ModulePackageOperationKind.Install))
            .ToList();

        State.UpdateInstallOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanResetAvailableSelectedModules()
        => AvailableModuleSelection.Count != 0
        && AvailableModuleSelection.All(k => k.PendingOperation?.OperationKind == ModulePackageOperationKind.Install);

    private async Task ResetAvailableSelectedModules()
    {
        var operations = AvailableModuleSelection
            .Where(k => k.PendingOperation != null && k.PendingOperation.OperationKind == ModulePackageOperationKind.Install)
            .Distinct()
            .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
            {
                Name = item.Name,
                Version = item.PendingOperation!.Package.Version,
            },
            ModulePackageOperationKind.Uninstall))
            .ToList();

        State.UpdateUninstallOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private async Task ShowDetailsAsync(ModuleMetadataModel model)
    {
        var result = await ControlPanelRequest.Send<ModuleDetailsControlPanel, ModuleDetailsControlPanelState>(s =>
        {
            s.ModuleMetadata = model;
        });
    }
}
