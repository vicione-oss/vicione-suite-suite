using System.Collections;
using Blazor.Shared.Settings.Models;
using Microsoft.AspNetCore.Authorization;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Services;

/// <inheritdoc cref="IControlPanelRegistry{TClientModule}"/>
internal sealed class ControlPanelRegistry<TClientModule>(IDefaultControlPanelGroupDescriptor defaultControlPanelGroupDescriptor)
    : IControlPanelRegistry<TClientModule>
        where TClientModule : class, IClientModule
{
    private readonly Lock _concurrentLock = new();
    private readonly List<IControlPanelRegistryItem<TClientModule>> _items = [];
    private readonly List<IControlPanelRegistryItem> _itemsAdded = [];
    private readonly List<IControlPanelRegistryItem> _itemsRemoved = [];

    public int UpdateLock { get; private set; }

    public event Action<RegistryChangedEventArgs<IControlPanelRegistryItem>>? Changed;

    public IControlPanelRegistryItem Add<TComponent, TState>(IControlPanelDescriptor descriptor, TState state,
        IControlPanelCategoryDescriptor categoryDescriptor, IControlPanelGroupDescriptor? groupDescriptor = null,
        IAuthorizationRequirement? authorizationRequirement = null)
            where TComponent : ControlPanelBase<TState>
            where TState : IControlPanelState
    {
        var item = new ControlPanelRegistryItem<TClientModule>(typeof(TComponent), descriptor, state, categoryDescriptor,
            groupDescriptor ?? defaultControlPanelGroupDescriptor, authorizationRequirement);

        lock (_concurrentLock)
        {
            _items.Add(item);

            if (UpdateLock == 0)
                Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelRegistryItem> { Sender = this, ItemsAdded = [item], ItemsRemoved = [] });
            else
                _itemsAdded.Add(item);
        }

        return item;
    }

    public bool Remove(IControlPanelRegistryItem item)
        => Remove(i => i == item) > 0;

    public int Remove<TControlPanel>() where TControlPanel : IControlPanel
        => Remove(i => i.ComponentType == typeof(TControlPanel));

    public int Remove(Predicate<IControlPanelRegistryItem> match)
    {
        var itemsRemoved = new List<IControlPanelRegistryItem>();

        lock (_concurrentLock)
        {
            var itemsToRemove = _items.Where(i => match(i)).ToList();
            foreach (var itemToRemove in itemsToRemove)
            {
                if (_items.Remove(itemToRemove))
                {
                    itemsRemoved.Add(itemToRemove);
                }
            }

            if (itemsRemoved.Count > 0)
            {
                if (UpdateLock == 0)
                    Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelRegistryItem> { Sender = this, ItemsAdded = [], ItemsRemoved = itemsRemoved });
                else
                    _itemsRemoved.AddRange(itemsRemoved);
            }
        }

        return itemsRemoved.Count;
    }

    public IEnumerator<IControlPanelRegistryItem> GetEnumerator()
    {
        lock (_concurrentLock)
        {
            return _items.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        lock (_concurrentLock)
        {
            return _items.ToList().GetEnumerator();
        }
    }

    public void BeginUpdate()
    {
        lock (_concurrentLock)
        {
            UpdateLock++;
        }
    }

    public void EndUpdate()
    {
        lock (_concurrentLock)
        {
            UpdateLock--;

            if (UpdateLock <= 0)
            {
                UpdateLock = 0;

                if (_itemsAdded.Count > 0 || _itemsRemoved.Count > 0)
                {
                    Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelRegistryItem> { Sender = this, ItemsAdded = _itemsAdded, ItemsRemoved = _itemsRemoved });

                    _itemsAdded.Clear();
                    _itemsRemoved.Clear();
                }
            }
        }
    }
}
