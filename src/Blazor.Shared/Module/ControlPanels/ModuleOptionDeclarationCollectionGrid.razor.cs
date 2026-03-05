using System.Globalization;
using Blazor.Shared.Module.ControlPanels.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Modules;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Module.ControlPanels;

public sealed partial class ModuleOptionDeclarationCollectionGrid : ComponentBase, IModuleOptionDeclarationCollectionGrid, IDisposable
{
    public const string PasswordDisplayDummy = "******";
    private static readonly MonochromeIconSize s_actionButtonIconSize = MonochromeIconSize.Small;

    private static readonly string s_editButtonIconCssClass = MonochromeIconName.Edit.GetCssClasses(s_actionButtonIconSize).ToSpaceSeparated();
    private static readonly string s_saveButtonIconCssClass = MonochromeIconName.Check.GetCssClasses(s_actionButtonIconSize).ToSpaceSeparated();
    private static readonly string s_cancelButtonIconCssClass = MonochromeIconName.Redo.GetCssClasses(s_actionButtonIconSize).ToSpaceSeparated();

    private string? _filterText;
    private bool _canDelete;

    private IReadOnlyCollection<ModuleOptionDeclaration> _items = [];
    private HashSet<ModuleOptionDeclaration> _itemsHashed = [];
    private IQueryable<ModuleOptionDeclaration>? _itemsQueryable;
    private IEditContext? _editContext;
    private bool _shouldRender;

    private interface IEditContext
    {
        ModuleOptionDeclaration ModuleOptionDeclaration { get; }
        string Key { get; set; }
        bool? KeyValid { get; set; }
    }

    private class EditContext<T> : IEditContext
    {
        private string? _key;

        public required ModuleOptionDeclaration ModuleOptionDeclaration { get; init; }

        public string Key
        {
            get => _key ?? ModuleOptionDeclaration.Key;
            set => _key = value;
        }

        public bool? KeyValid { get; set; }

        public required T Value { get; set; }
    }

    [Parameter, EditorRequired]
    public IReadOnlyCollection<ModuleOptionDeclaration> Items { get; set; }
    [Parameter]
    public EventCallback<ModuleOptionDeclaration> ItemChanged { get; set; }
    [Parameter]
    public EventCallback<IEnumerable<ModuleOptionDeclaration>> ItemsRemoved { get; set; }
    [Parameter, EditorRequired]
    public Func<ModuleOptionDeclaration, string, bool> ValidateKey { get; set; }
    [Parameter, EditorRequired]
    public Func<ModuleOptionDeclaration, bool> CanDelete { get; set; }
    [Parameter]
    public EventCallback<ModuleOptionType> AddItem { get; set; }

    [Inject(Key = typeof(ModuleOptionDeclarationCollectionGridServiceKey))]
    private IGridItemSelection<string> ItemSelection { get; set; } = default!;

    public async Task CancelEdit()
    {
        if (_editContext is not null)
        {
            _editContext = null;
            _shouldRender = true;

            await InvokeAsync(StateHasChanged);
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        ItemSelection.Changed += ItemSelectionChanged;
    }

    public void Dispose()
        => ItemSelection.Changed -= ItemSelectionChanged;

    protected override void OnParametersSet()
    {
        var resetStates = false;

        if (Items != _items)
            resetStates = true;
        else if (Items.Count != _items.Count)
            resetStates = true;
        else if (_itemsHashed.Except(Items).Any() || _itemsHashed.Intersect(Items).Count() != Items.Count)
            resetStates = true;

        if (resetStates)
        {
            _items = Items;
            _itemsHashed = [.. Items];
            _itemsQueryable = _itemsHashed.AsQueryable();

            _editContext = null;
        }

        _shouldRender = true;
    }

    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    private async Task SaveButtonClick()
    {
        if (_editContext is null)
            return;

        if (!ValidateKey.Invoke(_editContext.ModuleOptionDeclaration, _editContext.Key))
        {
            SetEditContextKeyValid(_editContext, false);

            return;
        }
        else
        {
            SetEditContextKeyValid(_editContext, true);
        }

        var notifyItemChanged = false;

        if (_editContext.Key != _editContext.ModuleOptionDeclaration.Key)
        {
            _editContext.ModuleOptionDeclaration.Key = _editContext.Key;

            notifyItemChanged = true;
        }

        string? newValue;

        if (_editContext is EditContext<bool> booleanEditContext)
            newValue = booleanEditContext.Value.ToString();
        else if (_editContext is EditContext<float> floatEditContext)
            newValue = floatEditContext.Value.ToString(CultureInfo.InvariantCulture);
        else if (_editContext is EditContext<int> integerEditContext)
            newValue = integerEditContext.Value.ToString(CultureInfo.InvariantCulture);
        else if (_editContext is EditContext<string?> stringEditContext)
            newValue = stringEditContext.Value;
        else
            return;

        if (!string.Equals(newValue, _editContext.ModuleOptionDeclaration.Value, StringComparison.Ordinal))
        {
            _editContext.ModuleOptionDeclaration.Value = newValue;

            notifyItemChanged = true;
        }

        if (notifyItemChanged && ItemChanged.HasDelegate)
            await ItemChanged.InvokeAsync(_editContext.ModuleOptionDeclaration);

        _editContext = null;
        _shouldRender = true;
    }

    private void CancelButtonClick()
    {
        _editContext = null;
        _shouldRender = true;
    }

    private void EditButtonClick(ModuleOptionDeclaration moduleOptionDeclaration)
    {
        _editContext = CreateEditContext(moduleOptionDeclaration);

        _shouldRender = true;
    }

    private static IEditContext? CreateEditContext(ModuleOptionDeclaration moduleOptionDeclaration)
    {
        if (moduleOptionDeclaration.OptionType == ModuleOptionType.Boolean)
        {
            if (bool.TryParse(moduleOptionDeclaration.Value, out var booleanValue))
                return new EditContext<bool> { ModuleOptionDeclaration = moduleOptionDeclaration, Value = booleanValue };
        }
        else if (moduleOptionDeclaration.OptionType == ModuleOptionType.Number)
        {
            if (float.TryParse(moduleOptionDeclaration.Value, out var floatValue))
                return new EditContext<float> { ModuleOptionDeclaration = moduleOptionDeclaration, Value = floatValue };

            if (int.TryParse(moduleOptionDeclaration.Value, out var intValue))
                return new EditContext<int> { ModuleOptionDeclaration = moduleOptionDeclaration, Value = intValue };
        }
        else if (moduleOptionDeclaration.OptionType == ModuleOptionType.Text)
        {
            return new EditContext<string?> { ModuleOptionDeclaration = moduleOptionDeclaration, Value = moduleOptionDeclaration.Value };
        }

        return null;
    }

    private void SetEditContextKeyValid(IEditContext editContext, bool value)
    {
        if (editContext.KeyValid != value)
        {
            editContext.KeyValid = value;

            _shouldRender = true;
        }
    }

    private async Task AddButtonClickAsync(ModuleOptionType moduleOptionType)
    {
        _filterText = "";

        if (AddItem.HasDelegate)
            await AddItem.InvokeAsync(moduleOptionType);
    }

    private void ApplyFilter()
    {
        _itemsQueryable = _itemsHashed.AsQueryable();

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            _itemsQueryable = _itemsQueryable.Where(i => i.Key.Contains(_filterText) ||
                (i.Value != null && i.Value.Contains(_filterText)));
        }

        _shouldRender = true;
    }

    private async void ItemSelectionChanged(GridItemSelectionChangedEventArgs<string> args)
    {
        var canDelete = false;

        try
        {
            if (_itemsQueryable is null)
                return;

            var selectedItems = _itemsQueryable.Where(i => args.Sender.Contains(i.Key)).ToList();
            var deletableItems = selectedItems.Where(CanDelete).ToList();

            canDelete = selectedItems.Count > 0 && selectedItems.Count == deletableItems.Count;
        }
        finally
        {
            if (canDelete != _canDelete)
            {
                _canDelete = canDelete;
                _shouldRender = true;

                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task DeleteGridActionButtonClick()
    {
        if (_itemsQueryable is null)
            return;

        var removableItems = _itemsQueryable.Where(i => ItemSelection.Contains(i.Key)).Where(CanDelete).ToList();

        var itemsRemoved = new List<ModuleOptionDeclaration>();
        foreach (var removeableItem in removableItems)
        {
            if (_itemsHashed.Remove(removeableItem))
            {
                _items = _itemsHashed;
                _editContext = null;
                _shouldRender = true;

                itemsRemoved.Add(removeableItem);
                ItemSelection.Remove(removeableItem.Key);
            }
        }

        if (itemsRemoved.Count > 0)
        {
            ItemSelection.BeginUpdate();
            try
            {
                foreach (var itemRemoved in itemsRemoved)
                    ItemSelection.Remove(itemRemoved.Key);
            }
            finally
            {
                ItemSelection.EndUpdate();
            }

            if (ItemsRemoved.HasDelegate)
                await ItemsRemoved.InvokeAsync(itemsRemoved);
        }
    }
}
