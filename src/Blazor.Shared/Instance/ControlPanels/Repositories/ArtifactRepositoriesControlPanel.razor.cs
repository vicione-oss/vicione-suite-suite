using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Blazor.Shared.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.Instance.ControlPanels.Repositories;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ArtifactRepositoriesControlPanel : ControlPanelBase<ArtifactRepositoriesControlPanelState>
{
    private IQueryable<ArtifactRepositoryModel>? _repositoriesQueryable;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];
    private bool _isSingleItemSelected;
    private bool _deleteGridActionButtonEnabled;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject] private IArtifactRepositoryClientService RepositoryService { get; set; } = default!;

    [Inject(Key = typeof(ArtifactRepositoriesControlPanelServiceKey))] private IGridItemSelection<Guid> GridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        RepositoryService.RepositoryChanged += RepositoryChanged;

        GridItemSelection.Clear();
        GridItemSelection.Changed += RepositoryGridItemSelectionChanged;
        State.Changed += StateChanged;

        UpdateRepositoriesQueryable();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandle?.Dispose();

        RepositoryService.RepositoryChanged -= RepositoryChanged;
        GridItemSelection.Changed -= RepositoryGridItemSelectionChanged;
        State.Changed -= StateChanged;

        await base.DisposeAsyncCore();
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs obj)
    {
        GridItemSelection.BeginUpdate();
        var reload = false;

        try
        {
            if (obj.PropertyNames.Contains(nameof(State.Repositories)))
            {
                UpdateRepositoriesQueryable();
                UpdateStatesDependingOnSourceGridItemSelection();

                reload = true;
            }
        }
        finally
        {
            GridItemSelection.EndUpdate();
        }

        if (reload)
            await InvokeAsync(StateHasChanged);
    }

    private async void RepositoryGridItemSelectionChanged(GridItemSelectionChangedEventArgs<Guid> args)
    {
        if (UpdateStatesDependingOnSourceGridItemSelection())
            await InvokeAsync(StateHasChanged);
    }

    private bool UpdateStatesDependingOnSourceGridItemSelection()
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
            foreach (var sourceId in GridItemSelection)
            {
                deleteGridActionButtonEnabled = State.Repositories.Any(c => c.Id == sourceId);

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

    private async Task DeleteSelectedRepositories()
    {
        GridItemSelection.BeginUpdate();

        try
        {
            var hasChanges = false;

            foreach (var sourceId in GridItemSelection)
            {
                if (!State.DeleteRepository(sourceId))
                    continue;

                GridItemSelection.Remove(sourceId);
                hasChanges = true;
            }

            if (hasChanges)
            {
                UpdateRepositoriesQueryable();
                await BeginEdit();
            }
        }
        finally
        {
            GridItemSelection.EndUpdate();
        }
    }

    private async Task RepositoryChanged(ArtifactRepository source, CrudAction action)
    {
        var changed = State.UpdateRepository(source, action);
        if (!changed)
            return;

        if (action == CrudAction.Deleted)
        {
            GridItemSelection.Remove(source.Id);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void ApplyFilter()
        => UpdateRepositoriesQueryable();

    private void UpdateRepositoriesQueryable()
    {
        _repositoriesQueryable = State.Repositories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(State.FilterText))
        {
            _repositoriesQueryable = _repositoriesQueryable.Where(m => (!string.IsNullOrEmpty(m.Name) && m.Name.Contains(State.FilterText, StringComparison.CurrentCultureIgnoreCase))
                || m.Endpoint.Contains(State.FilterText, StringComparison.CurrentCultureIgnoreCase));
        }

        _repositoriesQueryable = _repositoriesQueryable.OrderBy(u => u.Name);
    }

    private async Task AddNewRepository()
        => await ControlPanelRequest.Send<ArtifactRepositoryControlPanel, ArtifactRepositoryControlPanelState>(
            s => s.RepositoryId = null);

    private async Task EditSelectedRepository()
        => await ControlPanelRequest.Send<ArtifactRepositoryControlPanel, ArtifactRepositoryControlPanelState>(
            s => s.RepositoryId = GridItemSelection.FirstOrDefault());

    private async Task EditRepository(ArtifactRepositoryModel model)
        => await ControlPanelRequest.Send<ArtifactRepositoryControlPanel, ArtifactRepositoryControlPanelState>(s => s.RepositoryId = model.Id);
}
