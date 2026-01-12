using System.Collections;
using Blazor.Shared.Settings.Models;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed partial class ControlPanelEditRegistry : IControlPanelEditRegistry
{
    private readonly Lock _concurrentLock = new();
    private readonly List<IControlPanelEdit> _items = [];
    private readonly List<IControlPanelEdit> _itemsAdded = [];
    private readonly List<IControlPanelEdit> _itemsRemoved = [];

    public int UpdateLock { get; private set; }

    public event Action<RegistryChangedEventArgs<IControlPanelEdit>>? Changed;

    public void Add(IControlPanelEdit item)
    {
        lock (_concurrentLock)
        {
            _items.Add(item);

            if (UpdateLock == 0)
                Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelEdit> { Sender = this, ItemsAdded = [item], ItemsRemoved = [] });
            else
                _itemsAdded.Add(item);
        }
    }

    public bool Remove(IControlPanelEdit item)
        => Remove(i => i == item) > 0;

    public int Remove(Predicate<IControlPanelEdit> match)
    {
        var itemsRemoved = new List<IControlPanelEdit>();

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
                    Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelEdit> { Sender = this, ItemsAdded = [], ItemsRemoved = itemsRemoved });
                else
                    _itemsRemoved.AddRange(itemsRemoved);
            }
        }

        return itemsRemoved.Count;
    }

    public IEnumerator<IControlPanelEdit> GetEnumerator()
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
                    Changed?.Invoke(new RegistryChangedEventArgs<IControlPanelEdit> { Sender = this, ItemsAdded = _itemsAdded, ItemsRemoved = _itemsRemoved });

                    _itemsAdded.Clear();
                    _itemsRemoved.Clear();
                }
            }
        }
    }
}
