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
using Sdk.Client.Infrastructure;
using Sdk.Modules;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.Module.ControlPanels;

/// <summary>
/// todo - changing the available version will can lead to changed dependencies, options etc
/// where to store the options later if we enter passwords etc.
/// </summary>

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ModuleManagementControlPanel : ControlPanelBase<ModuleManagementControlPanelState>,
    IEventConsumer<ModulePackageOperationsChanged>,
    IEventConsumer<ModulePackageOperationsFailed>
{
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];
    private bool _dialogVisible;
    private IQueryable<ModuleMetadataModel> _installedModulesQueryable = Enumerable.Empty<ModuleMetadataModel>().AsQueryable();
    private IQueryable<ModuleMetadataModel> _availableModulesQueryable = Enumerable.Empty<ModuleMetadataModel>().AsQueryable();
    private string _installedModulesFilterText = string.Empty;
    private string _availableModulesFilterText = string.Empty;

    [Inject]
    internal IModuleManagementService ManagementService { get; set; } = default!;

    [Inject]
    internal IUiMediator Mediator { get; set; } = default!;

    [Inject]
    public ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject]
    public ILogger<ModuleManagementControlPanel> Logger { get; set; } = default!;

    [Inject(Key = typeof(ModuleManagementControlPanelServiceKey))] private IGridItemSelection<ModuleMetadataModel> InstalledModuleSelection { get; set; } = default!;

    [Inject(Key = typeof(ModuleManagementControlPanelServiceKey))] private IGridItemSelection<ModuleMetadataModel> AvailableModuleSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        State.Changed += StateChanged;

        _installedModulesQueryable = State.InstalledModules.AsQueryable();
        _availableModulesQueryable = State.AvailableModules.AsQueryable();

        InstalledModuleSelection.Clear();
        InstalledModuleSelection.Changed -= InstalledModuleSelectionChanged;
        InstalledModuleSelection.Changed += InstalledModuleSelectionChanged;

        AvailableModuleSelection.Clear();
        AvailableModuleSelection.Changed -= AvailableModuleSelectionChanged;
        AvailableModuleSelection.Changed += AvailableModuleSelectionChanged;

        _subscriptionHandles.Add(Mediator.Register<ModulePackageOperationsChanged>(this));
        _subscriptionHandles.Add(Mediator.Register<ModulePackageOperationsFailed>(this));
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs args)
    {
        var stateHasChanged = false;

        if (args.PropertyNames.Contains(nameof(State.InstalledModules)))
        {
            _installedModulesQueryable = State.InstalledModules.AsQueryable();
            stateHasChanged = true;
        }
        else if (args.PropertyNames.Contains(nameof(State.AvailableModules)))
        {
            _availableModulesQueryable = State.AvailableModules.AsQueryable();
            stateHasChanged = true;
        }

        if (stateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async void AvailableModuleSelectionChanged(GridItemSelectionChangedEventArgs<ModuleMetadataModel> args)
        => await InvokeAsync(StateHasChanged);

    private async void InstalledModuleSelectionChanged(GridItemSelectionChangedEventArgs<ModuleMetadataModel> args)
        => await InvokeAsync(StateHasChanged);

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        await base.DisposeAsyncCore();
    }

    private async Task LoadModuleVersions(bool forceReload = false)
    {
        InstalledModuleSelection.Clear();
        AvailableModuleSelection.Clear();

        State.BeginLoading();
        try
        {
            // load metadata assets and jsons in one step
            var response = await ManagementService.GetModuleMetadata(forceReload);

            State.Initialize(response);
        }
        finally
        {
            State.EndLoading();
        }
    }

    private async Task RestartSuite()
    {
        await SuiteControlService.RestartSuite();
        _dialogVisible = false;
    }

    private IQueryable<ModuleMetadataModel> FilterInstalledItems(IQueryable<ModuleMetadataModel> installedModules)
        => installedModules
            .Where(i => i.Name.Contains(_installedModulesFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

    private IQueryable<ModuleMetadataModel> FilterAvailableItems(IQueryable<ModuleMetadataModel> installedModules)
        => installedModules
            .Where(i => i.Name.Contains(_availableModulesFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

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

        await ManagementService.SendUpdateModulePackages(operations);
    }

    private bool CanResetSelectedModules()
        => AvailableModuleSelection.Count != 0
        && AvailableModuleSelection.All(k => k.PendingOperation?.OperationKind == ModulePackageOperationKind.Install);

    private async Task ResetPendingInstallation()
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

        await ManagementService.SendUpdateModulePackages(operations);
    }

    private async Task ShowDetailsAsync(ModuleMetadataModel model)
    {
        var result = await ControlPanelRequest.Send<ModuleDetailsControlPanel, ModuleDetailsControlPanelState>(s =>
        {
            s.ModuleMetadata = model;
        });
    }

    public async Task Consume(ClientContext<ModulePackageOperationsChanged> context, CancellationToken cancellationToken)
    {
        var hasChanged = false;

        foreach (var change in context.Message.Changes)
        {
            var installed = State.InstalledModules.FirstOrDefault(k => k.Name == change.Operation.Package.Name);
            if (installed != null)
            {
                installed.PendingOperation = change.Operation;
                hasChanged = true;
            }


            var available = State.AvailableModules.FirstOrDefault(k => k.Name == change.Operation.Package.Name);
            if (available != null)
            {
                available.PendingOperation = change.Operation;
                hasChanged = true;
            }
        }

        if (hasChanged)
            await InvokeAsync(StateHasChanged);
    }

    public Task Consume(ClientContext<ModulePackageOperationsFailed> context, CancellationToken cancellationToken)
    {
        // how to treat the errors?

        return Task.CompletedTask;
    }
}
