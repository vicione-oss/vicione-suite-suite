using System.Collections;
using Blazor.Shared.Settings.Models;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Services;

/// <inheritdoc cref="IControlPanelPageRegistry"/>
internal sealed class ControlPanelPageRegistry : IControlPanelPageRegistry
{
    private readonly Lock _concurrentLock = new();
    private readonly List<IControlPanelPageRegistryItem> _items = [];
    private readonly List<IControlPanelPageRegistryItem> _itemsAdded = [];
    private readonly List<IControlPanelPageRegistryItem> _itemsRemoved = [];
    private int _updateLock;

    public int UpdateLock => _updateLock;

    public event Action<RegistryChangedEventArgs<IControlPanelPageRegistryItem>>? Changed;

    public void Add(IControlPanelPage controlPanelPage, IControlPanelRegistryItem controlPanelRegistryItem)
    {
        lock (_concurrentLock)
        {
            var itemsAdded = new List<IControlPanelPageRegistryItem>();
            var itemsRemoved = new List<IControlPanelPageRegistryItem>();

            var existingItem = _items.FirstOrDefault(i => i.ControlPanelPage == controlPanelPage);
            if (existingItem is not null)
            {
                if (existingItem.ControlPanelRegistryItem != controlPanelRegistryItem)
                {
                    _items.Remove(existingItem);
                    itemsRemoved.Add(existingItem);

                    var newItem = new ControlPanelPageRegistryItem(controlPanelPage, controlPanelRegistryItem);
                    _items.Add(newItem);
                    itemsAdded.Add(newItem);
                }
            }
            else
            {
                var newItem = new ControlPanelPageRegistryItem(controlPanelPage, controlPanelRegistryItem);
                _items.Add(newItem);
                itemsAdded.Add(newItem);
            }

            if (itemsAdded.Count != 0 || itemsRemoved.Count != 0)
            {
                if (_updateLock == 0)
                    Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelPageRegistryItem> { Sender = this, ItemsAdded = itemsAdded, ItemsRemoved = itemsRemoved });
                else
                {
                    _itemsAdded.AddRange(itemsAdded);
                    _itemsRemoved.AddRange(itemsRemoved);
                }
            }
        }
    }

    public bool Remove(IControlPanelPage controlPanelPage)
        => Remove(i => i.ControlPanelPage == controlPanelPage) > 0;

    public bool Remove(IControlPanelPageRegistryItem item)
    => Remove(i => i == item) > 0;

    public int Remove(Predicate<IControlPanelPageRegistryItem> match)
    {
        var itemsRemoved = new List<IControlPanelPageRegistryItem>();

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
                if (_updateLock == 0)
                    Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelPageRegistryItem> { Sender = this, ItemsAdded = [], ItemsRemoved = itemsRemoved });
                else
                    _itemsRemoved.AddRange(itemsRemoved);
            }
        }

        return itemsRemoved.Count;
    }

    public IEnumerator<IControlPanelPageRegistryItem> GetEnumerator()
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
            _updateLock++;
        }
    }

    public void EndUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock--;

            if (_updateLock <= 0)
            {
                _updateLock = 0;

                if (_itemsAdded.Count > 0 || _itemsRemoved.Count > 0)
                {
                    Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelPageRegistryItem> { Sender = this, ItemsAdded = _itemsAdded, ItemsRemoved = _itemsRemoved });

                    _itemsAdded.Clear();
                    _itemsRemoved.Clear();
                }
            }
        }
    }
}
