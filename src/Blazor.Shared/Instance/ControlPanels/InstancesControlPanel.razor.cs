using Blazor.Shared.Instance.Contracts;
using Blazor.Shared.Instance.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Instance.ControlPanels;

public sealed partial class InstancesControlPanel : ControlPanelBase<InstancesControlPanelState>, IEventConsumer<InstanceAdministrated>
{
    private readonly string _refreshIconCssClass =
        MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();

    private readonly string _reloadIconCssClass =
        MonochromeIconName.Reload.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();

    private IQueryable<InstanceInformationModel>? _instancesQueryable;
    private IDisposable? _subscriptionHandle;
    private string? _filterText;
    private bool _isSingleInstanceSelected;
    private bool _deleteGridActionButtonEnabled;

    [Inject] private IUiMediator Mediator { get; set; } = default!;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    // todo - activate on next sdk v0.23.0
    //[Inject] private IInstanceInformationProvider InformationProvider { get; set; } = default!;

    [Inject(Key = typeof(InstancesControlPanelServiceKey))] private IGridItemSelection<Guid> InstanceGridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _subscriptionHandle = Mediator.Register(this);

        InstanceGridItemSelection.Clear();
        InstanceGridItemSelection.Changed += InstanceGridItemSelectionChanged;

        State.ResetSelectedInstances = false;
        State.Changed += StateChanged;

        UpdateInstancesQueryable();

        // todo - activate on next sdk v0.23.0
        //InformationProvider.HealthStatusChanged += OnHealthStatusChanged;
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs obj)
    {
        InstanceGridItemSelection.BeginUpdate();
        var reload = true;

        try
        {
            if (obj.PropertyNames.Contains(nameof(State.ResetSelectedInstances)) && State.ResetSelectedInstances)
            {
                foreach (var tag in State.DeletingInstances)
                    InstanceGridItemSelection.Add(tag.Id);

                State.ResetSelectedInstances = false;

                reload = true;
            }
        }
        finally
        {
            InstanceGridItemSelection.EndUpdate();
        }

        if (obj.PropertyNames.Contains(nameof(State.Instances)))
        {
            UpdateInstancesQueryable();
            UpdateStatesDependingOnInstanceGridItemSelection();

            reload = true;
        }

        if (reload)
            await InvokeAsync(StateHasChanged);
    }

    private async void InstanceGridItemSelectionChanged(GridItemSelectionChangedEventArgs<Guid> args)
    {
        if (UpdateStatesDependingOnInstanceGridItemSelection())
            await InvokeAsync(StateHasChanged);
    }

    private bool UpdateStatesDependingOnInstanceGridItemSelection()
    {
        var somethingChanged = false;

        var isSingleInstanceSelected = InstanceGridItemSelection.Count == 1;
        if (isSingleInstanceSelected != _isSingleInstanceSelected)
        {
            _isSingleInstanceSelected = isSingleInstanceSelected;
            somethingChanged = true;
        }

        var deleteGridActionButtonEnabled = false;

        if (InstanceGridItemSelection.Count > 0)
        {
            foreach (var instanceId in InstanceGridItemSelection)
            {
                deleteGridActionButtonEnabled = State.Instances.Any(c => c.Id == instanceId);

                if (!deleteGridActionButtonEnabled)
                    break;
            }
        }

        if (deleteGridActionButtonEnabled != _deleteGridActionButtonEnabled)
        {
            _deleteGridActionButtonEnabled = deleteGridActionButtonEnabled;
            somethingChanged = true;
        }

        return somethingChanged;
    }

    private async Task SynchronizeSelectedInstances()
    {
        if (InstanceGridItemSelection.Count == 0)
            return;

        var commands = InstanceGridItemSelection
            .Select(id => new AdministrateInstance
            {
                Action = AdministrateInstanceAction.Synchronize,
                InstanceId = id
            }).ToList();

        foreach (var command in commands)
        {
            var instance = State.Instances.FirstOrDefault(k => k.Id == command.InstanceId);
            if (instance != null)
                instance.IsSynchronizing = true;
        }

        await Task.WhenAll(commands.Select(c => Mediator.Send(c)));
        await InvokeAsync(StateHasChanged);
    }

    private async Task DeleteSelectedInstances()
    {
        InstanceGridItemSelection.BeginUpdate();

        try
        {
            foreach (var instanceId in InstanceGridItemSelection)
            {
                if (!State.Instances.Any(i => i.Id == instanceId))
                    continue;

                State.DeletingInstances.Add(State.Instances.First(c => c.Id == instanceId));
                InstanceGridItemSelection.Remove(instanceId);
                State.Instances.Remove(State.Instances.First(c => c.Id == instanceId));
            }

            UpdateInstancesQueryable();
            await BeginEdit();
        }
        finally
        {
            InstanceGridItemSelection.EndUpdate();
        }
    }

    public async Task Consume(ClientContext<InstanceAdministrated> context, CancellationToken cancellationToken)
    {
        switch (context.Message.Action)
        {
            case AdministrateInstanceAction.Delete:
                var instanceDeleted = State.DeletingInstances.FirstOrDefault(k => k.Id == context.Message.InstanceId);
                if (instanceDeleted is null)
                    return;

                if (context.Message.Success)
                    State.DeletingInstances.RemoveAll(k => k.Id == context.Message.InstanceId);
                else
                    instanceDeleted.HasFailed = true;

                UpdateInstancesQueryable();
                await InvokeAsync(StateHasChanged);

                break;

            case AdministrateInstanceAction.Synchronize:
                var instance = State.Instances.FirstOrDefault(k => k.Id == context.Message.InstanceId);
                if (instance is not null)
                {
                    instance.IsSynchronizing = false;
                    instance.HasFailed = !context.Message.Success;
                    await InvokeAsync(StateHasChanged);
                }
                break;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandle?.Dispose();

        State.Changed -= StateChanged;
        InstanceGridItemSelection.Changed -= InstanceGridItemSelectionChanged;

        // todo - activate on next sdk v0.23.0
        //InformationProvider.HealthStatusChanged -= OnHealthStatusChanged;

        await base.DisposeAsyncCore();
    }

    private void ApplyFilter()
        => UpdateInstancesQueryable();

    private void UpdateInstancesQueryable()
    {
        _instancesQueryable = State.Instances.AsQueryable();

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            _instancesQueryable = _instancesQueryable.Where(m => (!string.IsNullOrEmpty(m.Name) && m.Name.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase))
                || m.FormattedName.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase));
        }

        _instancesQueryable = _instancesQueryable.OrderBy(u => u.Name);
    }

    private async Task EditSelectedInstance()
        => await ControlPanelRequest.Send<InstanceControlPanel, InstanceControlPanelState>(
            s => s.InstanceId = InstanceGridItemSelection.FirstOrDefault());

    private async Task EditInstance(InstanceInformationModel model)
        => await ControlPanelRequest.Send<InstanceControlPanel, InstanceControlPanelState>(s => s.InstanceId = model.Id);

    // todo - activate on next sdk v0.23.0
    //private async Task OnHealthStatusChanged(Guid id, HealthStatus status, DateTime lastSend)
    //{
    //    var instance = _instances.FirstOrDefault(i => i.Id == id);
    //    if (instance is null)
    //        return;

    //    instance.HealthStatus = status;
    //    instance.LastSent = lastSend;

    //    await InvokeAsync(StateHasChanged);
    //}
}
