using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Instance;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Module.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ControlPanelChild<ModuleManagementControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ModuleManagementBrowseControlPanel : ControlPanelBase<ModuleManagementBrowseControlPanelState>
{
    private readonly string _restartIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _reloadIconCssClasses = MonochromeIconName.Reload.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _installIconCssClasses = MonochromeIconName.Import.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _uninstallIconCssClasses = MonochromeIconName.UninstallLight.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();

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
    public ILogger<ModuleManagementBrowseControlPanel> Logger { get; set; } = default!;

    [Inject(Key = typeof(AvailableModuleManagementControlPanelServiceKey))]
    private IGridItemSelection<ModuleMetadataModel> AvailableModuleSelection { get; set; } = default!;

    private bool IsStandalone => InformationProvider.Local.Type == InstanceType.Standalone;

    protected override async ValueTask DisposeAsyncCore()
    {
        State.Changed -= StateChanged;

        AvailableModuleSelection.Changed -= AvailableModuleSelectionChanged;

        await base.DisposeAsyncCore();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        AvailableModuleSelection.Clear();

        State.Changed += StateChanged;

        AvailableModuleSelection.Changed += AvailableModuleSelectionChanged;
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(State.Modules)))
        {
            AvailableModuleSelection.Clear();

            await InvokeAsync(StateHasChanged);
        }
        else if (args.PropertyNames.Contains(ModuleManagementControlPanelStateBase.PendingOperationsChanged))
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async void AvailableModuleSelectionChanged(GridItemSelectionChangedEventArgs<ModuleMetadataModel> args)
        => await InvokeAsync(StateHasChanged);

    private bool CanRefreshAvailableModules()
        => !State.IsLoading && !State.HasPendingChanges();

    private async Task LoadAvailableModuleVersions(bool forceReload = false)
    {
        AvailableModuleSelection.Clear();

        await State.LoadAvailableModules(ManagementService, forceReload, Logger, CancellationToken.None);
    }

    private async Task ConfirmRestart()
    {
        if (IsStandalone)
            await SuiteControlService.RestartInstance();
        else
            await SuiteControlService.RestartAllInstances();
    }

    private IQueryable<ModuleMetadataModel> FilterAvailableItems(IQueryable<ModuleMetadataModel> availableModules)
    {
        if (string.IsNullOrEmpty(State.FilterText))
            return availableModules.OrderBy(d => d.Name);

        return availableModules
                .Where(i => i.Name.Contains(State.FilterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name);
    }

    private bool CanInstallSelectedAvailableModules()
        => State.AllowInstallation
        && AvailableModuleSelection.Count != 0
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
        => State.AllowInstallation
        && AvailableModuleSelection.Count != 0
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
