using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Instance;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Module.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ControlPanelChild<ModuleManagementControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ModuleManagementInstalledControlPanel : ControlPanelBase<ModuleManagementInstalledControlPanelState>
{
    private readonly string _restartIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _uninstallIconCssClasses = MonochromeIconName.UninstallLight.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _updateIconCssClasses = MonochromeIconName.InstallPendingLight.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();

    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];
    private bool _dialogVisible;

    [Inject]
    public ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject]
    private IInstanceInformationProvider InformationProvider { get; set; } = default!;

    [Inject]
    private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject(Key = typeof(InstalledModuleManagementControlPanelServiceKey))]
    private IGridItemSelection<ModuleMetadataModel> InstalledModuleSelection { get; set; } = default!;

    private bool IsStandalone => InformationProvider.Local.Type == InstanceType.Standalone;

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        State.Changed -= StateChanged;

        InstalledModuleSelection.Changed -= InstalledModuleSelectionChanged;

        await base.DisposeAsyncCore();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        InstalledModuleSelection.Clear();

        State.Changed += StateChanged;

        InstalledModuleSelection.Changed += InstalledModuleSelectionChanged;
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(State.Modules)))
        {
            InstalledModuleSelection.Clear();

            await InvokeAsync(StateHasChanged);
        }
        else if (args.PropertyNames.Contains(ModuleManagementControlPanelStateBase.PendingOperationsChanged))
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async void InstalledModuleSelectionChanged(GridItemSelectionChangedEventArgs<ModuleMetadataModel> args)
        => await InvokeAsync(StateHasChanged);

    private async Task ConfirmRestart()
    {
        if (IsStandalone)
            await SuiteControlService.RestartInstance();
        else
            await SuiteControlService.RestartAllInstances();
    }

    private IQueryable<ModuleMetadataModel> FilterInstalledItems(IQueryable<ModuleMetadataModel> installedModules)
    {
        if (string.IsNullOrEmpty(State.FilterText))
            return installedModules.OrderBy(d => d.Name);

        return installedModules
                .Where(i => i.Name.Contains(State.FilterText, StringComparison.OrdinalIgnoreCase))
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

    private async Task ShowDetailsAsync(ModuleMetadataModel model)
    {
        var _ = await ControlPanelRequest.Send<ModuleDetailsControlPanel, ModuleDetailsControlPanelState>(s =>
        {
            s.ModuleMetadata = model;
        });
    }
}
