using System.Text.RegularExpressions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Models;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Components;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class EnvironmentOverridesControlPanel : ControlPanelBase<EnvironmentOverridesControlPanelState>
{
    private static readonly string EditButtonIconCssClass
        = MonochromeIconName.Edit.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private static readonly string SaveButtonIconCssClass
        = MonochromeIconName.Check.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private static readonly string CancelButtonIconCssClass
        = MonochromeIconName.Redo.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private bool _dialogVisible;
    private string? _filterText;
    private bool _deleteEnabled;
    private List<EnvironmentOverrideEntry>? _renderedEntries;
    private IQueryable<EnvironmentOverrideEntry>? _entriesQueryable;
    private EntryEditContext? _editContext;

    [Inject] public ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject(Key = typeof(EnvironmentOverridesControlPanelServiceKey))]
    private IGridItemSelection<Guid> GridItemSelection { get; set; } = default!;

    private sealed class EntryEditContext
    {
        public required EnvironmentOverrideEntry Entry { get; init; }

        public required string Name { get; set; }

        public required string Value { get; set; }

        /// <summary>
        /// Set for a row that the add button put into <see cref="EnvironmentOverridesControlPanelState.Entries"/>
        /// but that was never committed, so abandoning its editor has to take the row with it.
        /// </summary>
        public required bool IsNewEntry { get; init; }

        public bool? NameValid { get; set; }
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        GridItemSelection.Clear();
        GridItemSelection.Changed += GridItemSelectionChanged;
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (ReferenceEquals(_renderedEntries, State.Entries))
            return;

        _renderedEntries = State.Entries;
        _editContext = null;

        UpdateEntriesQueryable();
        UpdateDeleteEnabled();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        GridItemSelection.Changed -= GridItemSelectionChanged;

        await base.DisposeAsyncCore();
    }

    private async Task AddEntry()
    {
        CancelEntryEdit();

        var entry = new EnvironmentOverrideEntry();

        State.Entries.Add(entry);

        _editContext = CreateEditContext(entry, isNewEntry: true);

        UpdateEntriesQueryable();

        await BeginEdit();
    }

    private async Task DeleteSelectedEntries()
    {
        try
        {
            GridItemSelection.BeginUpdate();

            var removedIds = State.Entries
                .Where(e => GridItemSelection.Contains(e.Id))
                .Select(e => e.Id)
                .ToList();

            if (removedIds.Count == 0)
                return;

            State.Entries.RemoveAll(e => removedIds.Contains(e.Id));

            foreach (var removedId in removedIds)
                GridItemSelection.Remove(removedId);

            if (_editContext is not null && removedIds.Contains(_editContext.Entry.Id))
                _editContext = null;

            UpdateEntriesQueryable();

            await BeginEdit();
        }
        finally
        {
            GridItemSelection.EndUpdate();
        }
    }

    private void EditEntry(EnvironmentOverrideEntry entry)
    {
        CancelEntryEdit();

        _editContext = CreateEditContext(entry, isNewEntry: false);
    }

    private void CancelEntryEdit()
    {
        if (_editContext is null)
            return;

        var abandonedEntry = _editContext.IsNewEntry ? _editContext.Entry : null;

        _editContext = null;

        if (abandonedEntry is null)
            return;

        State.Entries.Remove(abandonedEntry);

        UpdateEntriesQueryable();
    }

    private static EntryEditContext CreateEditContext(EnvironmentOverrideEntry entry, bool isNewEntry)
        => new()
        {
            Entry = entry,
            Name = entry.Name,
            Value = entry.Value,
            IsNewEntry = isNewEntry
        };

    private async Task SaveEditedEntry()
    {
        if (_editContext is null)
            return;

        var name = _editContext.Name.Trim();

        if (!KeyRegex().IsMatch(name))
        {
            _editContext.NameValid = false;

            return;
        }

        _editContext.Entry.Name = name;
        _editContext.Entry.Value = _editContext.Value;
        _editContext = null;

        await BeginEdit();
    }

    private void ApplyFilter()
        => UpdateEntriesQueryable();

    private void UpdateEntriesQueryable()
    {
        var entries = State.Entries.AsQueryable();
        var filterText = _filterText;

        if (!string.IsNullOrWhiteSpace(filterText))
        {
            entries = entries.Where(e => e.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase)
                || e.Value.Contains(filterText, StringComparison.CurrentCultureIgnoreCase));
        }

        // Ordered for display rather than in the state: the queryable stays deferred over the
        // live list, so a row only moves into place once its editor is committed and never while
        // the name is being typed.
        _entriesQueryable = entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.Ordinal);
    }

    private async void GridItemSelectionChanged(GridItemSelectionChangedEventArgs<Guid> args)
    {
        if (UpdateDeleteEnabled())
            await InvokeAsync(StateHasChanged);
    }

    private bool UpdateDeleteEnabled()
    {
        var oneOrMoreItemsAreSelected = GridItemSelection.Count > 0;
        var allSelectedItemsAreAlsoRegisteredInOurState
            = GridItemSelection.All(id => State.Entries.Any(e => e.Id == id));
        var deleteCanBeEnabled = oneOrMoreItemsAreSelected
                            && allSelectedItemsAreAlsoRegisteredInOurState;

        if (deleteCanBeEnabled == _deleteEnabled)
            return false;

        _deleteEnabled = deleteCanBeEnabled;

        return true;
    }

    private async Task RestartSuite() => await SuiteControlService.RestartInstance();

    [GeneratedRegex(Core.Shared.EnvironmentOverrides.Constants.KeyPattern)]
    private static partial Regex KeyRegex();
}
