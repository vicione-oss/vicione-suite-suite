using Blazor.Shared.Connections.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.Connections.ControlPanels;

public sealed partial class TagsControlPanelPageContent : ComponentBase, IDisposable
{
    private IQueryable<Tag>? _tagsQueryable;
    private string? _filterText;
    private bool _isSingleTagSelected;
    private bool _deleteGridActionButtonEnabled;

    [Inject]
    private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject(Key = typeof(TagsControlPanelPageContentServiceKey))]
    private IGridItemSelection<Guid> TagGridItemSelection { get; set; } = default!;

    [Parameter, EditorRequired]
    public ConnectionsControlPanelState State { get; set; } = default!;
    [Parameter, EditorRequired]
    public Func<Task> BeginEdit { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

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
            UpdateTagsQueryable();
            UpdateStatesDependingOnTagGridItemSelection();

            reload = true;
        }

        if (reload)
            await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TagGridItemSelection.Changed -= TagGridItemSelectionChanged;
        State.Changed -= StateChanged;
    }

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

            if (BeginEdit is not null)
                await BeginEdit.Invoke();
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
