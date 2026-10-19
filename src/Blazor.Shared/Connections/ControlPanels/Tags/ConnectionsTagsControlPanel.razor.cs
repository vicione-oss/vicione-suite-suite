using Blazor.Shared.Connections.ControlPanels.Connections.Services;
using Blazor.Shared.Connections.ControlPanels.Tags.Extensions;
using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.Connections.ControlPanels.Tags;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ControlPanelChild<ConnectionsControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ConnectionsTagsControlPanel : ControlPanelBase<ConnectionsTagsControlPanelState>
{
    private IQueryable<Tag>? _tagsQueryable;
    private string? _filterText;
    private bool _isSingleTagSelected;
    private bool _deleteGridActionButtonEnabled;

    [Inject]
    private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject]
    private ISuiteConnectionService ConnectionService { get; set; } = default!;

    [Inject(Key = typeof(ConnectionsTagsControlPanelServiceKey))]
    private IGridItemSelection<Guid> TagGridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ConnectionService.TagsChanged += ConnectionServiceTagsChanged;

        State.Changed += StateChanged;
        State.ResetSelectedTags = false;

        TagGridItemSelection.Changed += TagGridItemSelectionChanged;

        UpdateTagsQueryable();

        UpdateStatesDependingOnTagGridItemSelection();
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs obj)
    {
        TagGridItemSelection.BeginUpdate();
        var reload = false;

        try
        {
            if (obj.PropertyNames.Contains(nameof(State.ResetSelectedTags)) && State.ResetSelectedTags)
            {
                foreach (var tag in State.DeletingTags)
                    TagGridItemSelection.Add(tag.Id);

                State.ResetSelectedTags = false;

                reload = true;
            }
        }
        finally
        {
            TagGridItemSelection.EndUpdate();
        }

        if (obj.PropertyNames.Contains(nameof(State.Tags)))
        {
            UpdateTagGridSelection();
            UpdateTagsQueryable();
            UpdateStatesDependingOnTagGridItemSelection();

            reload = true;
        }

        if (reload)
            await InvokeAsync(StateHasChanged);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _tagsQueryable = Enumerable.Empty<Tag>().AsQueryable();

        ConnectionService.TagsChanged -= ConnectionServiceTagsChanged;
        TagGridItemSelection.Changed -= TagGridItemSelectionChanged;
        State.Changed -= StateChanged;

        await base.DisposeAsyncCore();
    }

    private Task ConnectionServiceTagsChanged(TagsChanged changeEvent)
        => InvokeAsync(() => State.UpdateTags(changeEvent));

    private async Task AddTag()
        => await ControlPanelRequest.Send<TagControlPanel, TagControlPanelState>(s => s.TagId = null);

    private async Task EditSelectedTag()
        => await ControlPanelRequest.Send<TagControlPanel, TagControlPanelState>(s => s.TagId = TagGridItemSelection.FirstOrDefault());

    private async Task EditTag(Tag model)
        => await ControlPanelRequest.Send<TagControlPanel, TagControlPanelState>(t => t.TagId = model.Id);

    private async void DeleteSelectedTags()
    {
        TagGridItemSelection.BeginUpdate();

        try
        {
            foreach (var tagId in TagGridItemSelection)
            {
                if (State.Tags.TryGetValue(tagId, out var tag) && !tag.Protected)
                    State.DeletingTags.Add(tag);
                else
                    continue;

                TagGridItemSelection.Remove(tagId);
                State.Tags.Remove(State.Tags.Keys.First(id => id == tagId));
            }

            UpdateTagsQueryable();

            await BeginEdit();
        }
        finally
        {
            TagGridItemSelection.EndUpdate();
        }
    }

    private async void TagGridItemSelectionChanged(GridItemSelectionChangedEventArgs<Guid> args)
    {
        if (UpdateStatesDependingOnTagGridItemSelection())
            await InvokeAsync(StateHasChanged);
    }

    private bool UpdateStatesDependingOnTagGridItemSelection()
    {
        var somethingChanged = false;

        var isSingleTagSelected = TagGridItemSelection.Count == 1;
        if (isSingleTagSelected != _isSingleTagSelected)
        {
            _isSingleTagSelected = isSingleTagSelected;
            somethingChanged = true;
        }

        var deleteGridActionButtonEnabled = false;
        if (TagGridItemSelection.Count > 0)
        {
            foreach (var tagId in TagGridItemSelection)
            {
                deleteGridActionButtonEnabled = State.Tags.TryGetValue(tagId, out var tag) && !tag.Protected;

                if (deleteGridActionButtonEnabled)
                {
                    break;
                }
            }
        }

        if (deleteGridActionButtonEnabled != _deleteGridActionButtonEnabled)
        {
            _deleteGridActionButtonEnabled = deleteGridActionButtonEnabled;
            somethingChanged = true;
        }

        return somethingChanged;
    }

    private void ApplyFilter()
        => UpdateTagsQueryable();

    private void UpdateTagGridSelection()
    {
        var removed = TagGridItemSelection.Where(id => !State.Tags.ContainsKey(id)).ToList();
        if (removed.Count == 0)
            return;

        TagGridItemSelection.BeginUpdate();

        try
        {
            foreach (var tagId in removed)
                TagGridItemSelection.Remove(tagId);
        }
        finally
        {
            TagGridItemSelection.EndUpdate();
        }
    }

    private void UpdateTagsQueryable()
    {
        _tagsQueryable = State.Tags.Values.AsQueryable();

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            _tagsQueryable = _tagsQueryable.Where(t => t.Text.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase));
        }

        _tagsQueryable = _tagsQueryable.OrderBy(t => t.Text);
    }
}
