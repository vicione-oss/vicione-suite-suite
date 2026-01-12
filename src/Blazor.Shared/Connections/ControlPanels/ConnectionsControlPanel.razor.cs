using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Connections.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ConnectionsControlPanel : ControlPanelBase<ConnectionsControlPanelState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.Connectivity.GetCssClasses().ToSpaceSeparated();

    private IQueryable<EditConnectionModel>? _connectionsQueryable;
    private string? _filterText;
    private bool _isSingleItemSelected;
    private bool _deleteGridActionButtonEnabled;

    [Inject] private ISuiteConnectionService ConnectionService { get; set; } = default!;

    [Inject] private IConnectionTypeRegistry ConnectionTypeRegistry { get; set; } = default!;

    [Inject] private IConnectionTypeUiRegistry ConnectionTypeUiRegistry { get; set; } = default!;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject(Key = typeof(ConnectionsControlPanelServiceKey))] private IGridItemSelection<Guid> GridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ConnectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        ConnectionService.ConnectionStateChanged += OnConnectionStateChanged;

        GridItemSelection.Clear();
        GridItemSelection.Changed -= GridItemSelectionChanged;
        GridItemSelection.Changed += GridItemSelectionChanged;

        State.Changed += StateChanged;

        State.ResetSelectedConnections = false;

        UpdateConnectionsQueryable();
        UpdateGridSelection();
        UpdateStatesDependingOnGridItemSelection();
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs obj)
    {
        var stateHasChanged = false;

        if (obj.PropertyNames.Contains(nameof(State.ResetSelectedConnections)) && State.ResetSelectedConnections)
        {
            GridItemSelection.BeginUpdate();

            try
            {
                UpdateConnectionsQueryable();
                UpdateGridSelection();

                foreach (var connection in State.DeletingConnections)
                    GridItemSelection.Add(connection.Id);

                stateHasChanged = true;
                State.ResetSelectedConnections = false;
            }
            finally
            {
                GridItemSelection.EndUpdate();
            }
        }

        if (stateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _connectionsQueryable = Enumerable.Empty<EditConnectionModel>().AsQueryable();

        GridItemSelection.Changed -= GridItemSelectionChanged;
        ConnectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        State.Changed -= StateChanged;

        await base.DisposeAsyncCore();
    }

    private void ApplyFilter()
        => UpdateConnectionsQueryable();

    private async void DeleteSelectedItems()
    {
        GridItemSelection.BeginUpdate();

        try
        {
            foreach (var connectionId in GridItemSelection)
            {
                if (State.Connections.All(c => c.Id != connectionId))
                    continue;

                State.DeletingConnections.Add(State.Connections.First(c => c.Id == connectionId));

                GridItemSelection.Remove(connectionId);
                State.Connections.Remove(State.Connections.First(c => c.Id == connectionId));
            }

            UpdateConnectionsQueryable();
            await BeginEdit();
        }
        finally
        {
            GridItemSelection.EndUpdate();
        }
    }

    private async Task AddNewItem()
        => await ControlPanelRequest.Send<ConnectionControlPanel, ConnectionControlPanelState>(
            s => s.ConnectionId = null);

    private async Task EditSelectedItem()
        => await ControlPanelRequest.Send<ConnectionControlPanel, ConnectionControlPanelState>(
            s => s.ConnectionId = GridItemSelection.First());

    private async Task EditItem(EditConnectionModel model)
        => await ControlPanelRequest.Send<ConnectionControlPanel, ConnectionControlPanelState>(
            s => s.ConnectionId = model.Connection.Id);

    private static IEnumerable<string> GetTags(EditConnectionModel conn)
        => conn.Tags.Select(t => t.Text);

    private async void GridItemSelectionChanged(GridItemSelectionChangedEventArgs<Guid> args)
    {
        if (UpdateStatesDependingOnGridItemSelection())
            await InvokeAsync(StateHasChanged);
    }

    private bool UpdateStatesDependingOnGridItemSelection()
    {
        var somethingChanged = false;

        var isSingleItemSelected = GridItemSelection.Count == 1;
        if (isSingleItemSelected != _isSingleItemSelected)
        {
            _isSingleItemSelected = isSingleItemSelected;
            somethingChanged = true;
        }

        var deleteGridActionButtonEnabled = false;

        if (GridItemSelection.Count > 0)
        {
            foreach (var connectionId in GridItemSelection)
            {
                deleteGridActionButtonEnabled = State.Connections
                    .Any(c => !c.Connection.Managed
                              && c.Id == connectionId);

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

    private void UpdateConnectionsQueryable()
    {
        _connectionsQueryable = State.Connections.AsQueryable();

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            _connectionsQueryable = _connectionsQueryable
                .Where(m => !string.IsNullOrEmpty(m.Name) && m.Name.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase));
        }

        _connectionsQueryable = _connectionsQueryable.OrderBy(u => u.Name);
    }

    private async Task OnConnectionStateChanged(IReadOnlyList<Connection> connections)
    {
        var editConnections = connections.Select(c => new EditConnectionModel(c, ConnectionTypeRegistry));

        if (State.ResetSelectedConnections)
        {
            State.Connections = [.. editConnections];

            State.ResetSelectedConnections = false;
        }
        else
        {
            State.Connections = [.. editConnections.ExceptBy(State.DeletingConnections.Select(d => d.Id), c => c.Id)];
        }

        UpdateConnectionsQueryable();
        UpdateGridSelection();
        UpdateStatesDependingOnGridItemSelection();
        await InvokeAsync(StateHasChanged);
    }

    private void UpdateGridSelection()
    {
        // remove items from selection that are not contained in connections anymore (e.g. connection was deleted) 
        var removed = GridItemSelection.Where(c => State.Connections.All(k => k.Connection.Id != c));
        foreach (var connectionId in removed)
        {
            GridItemSelection.Remove(connectionId);
        }
    }
}
