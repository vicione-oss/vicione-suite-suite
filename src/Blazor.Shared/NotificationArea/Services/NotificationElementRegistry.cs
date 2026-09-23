using System.Collections;
using Microsoft.AspNetCore.Authorization;
using Sdk.Client.Modules;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;

namespace Blazor.Shared.NotificationArea.Services;

/// <inheritdoc cref="INotificationElementRegistry{TClientModule}"/>
internal sealed class NotificationElementRegistry<TClientModule> : INotificationElementRegistry<TClientModule>
    where TClientModule : class, IClientModule
{
    private readonly Lock _concurrentLock = new();
    private readonly Dictionary<Guid, INotificationElementRegistryItem<TClientModule>> _itemMap = [];
    private readonly List<INotificationElementRegistryItem<TClientModule>> _items = [];
    private readonly List<INotificationElementRegistryItem> _itemsAdded = [];
    private readonly List<INotificationElementRegistryItem> _itemsRemoved = [];

    public int UpdateLock { get; private set; }

    public event Action<RegistryChangedEventArgs<INotificationElementRegistryItem>>? Changed;

    /// <exception cref="ArgumentException">The given id is already registered.</exception>
    public INotificationElementRegistryItem Add<TElement, TState>(TState state, int? position = null, Guid? id = null, IAuthorizationRequirement? authorizationRequirement = null)
        where TElement : NotificationElementBase<TState>
        where TState : INotificationElementState
    {
        if (id.HasValue && _itemMap.ContainsKey(id.Value))
            throw new ArgumentException($"Given ID '{id}' is already registered.", nameof(id));

        var item = new NotificationElementRegistryItem<TClientModule>(id ?? Guid.NewGuid(), position ?? 0, typeof(TElement), state, authorizationRequirement);

        lock (_concurrentLock)
        {
            _items.Add(item);
            _itemMap.Add(item.Id, item);

            if (UpdateLock == 0)
                Changed?.Invoke(new RegistryChangedEventArgs<INotificationElementRegistryItem> { Sender = this, ItemsAdded = [item], ItemsRemoved = [] });
            else
                _itemsAdded.Add(item);
        }

        return item;
    }

    public bool Remove(Guid id)
    {
        lock (_concurrentLock)
        {
            if (_itemMap.TryGetValue(id, out var item))
            {
                if (_items.Remove(item))
                {
                    _itemMap.Remove(id);

                    if (UpdateLock == 0)
                        Changed?.Invoke(new RegistryChangedEventArgs<INotificationElementRegistryItem> { Sender = this, ItemsAdded = [], ItemsRemoved = [item] });
                    else
                        _itemsRemoved.Add(item);

                    return true;
                }
            }
        }

        return false;
    }

    public bool Remove(INotificationElementRegistryItem item)
        => Remove(i => i == item) > 0;

    public int Remove<TNotificationElement>() where TNotificationElement : INotificationElement
        => Remove(i => i.ComponentType == typeof(TNotificationElement));

    public int Remove(Predicate<INotificationElementRegistryItem> match)
    {
        var itemsRemoved = new List<INotificationElementRegistryItem>();

        lock (_concurrentLock)
        {
            var itemsToRemove = _items.Where(i => match(i)).ToList();
            foreach (var itemToRemove in itemsToRemove)
            {
                if (_items.Remove(itemToRemove))
                {
                    _itemMap.Remove(itemToRemove.Id);

                    itemsRemoved.Add(itemToRemove);
                }
            }

            if (itemsRemoved.Count > 0)
            {
                if (UpdateLock == 0)
                    Changed?.Invoke(new RegistryChangedEventArgs<INotificationElementRegistryItem> { Sender = this, ItemsAdded = [], ItemsRemoved = itemsRemoved });
                else
                    _itemsRemoved.AddRange(itemsRemoved);
            }
        }

        return itemsRemoved.Count;
    }

    public IEnumerator<INotificationElementRegistryItem> GetEnumerator()
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
                    Changed?.Invoke(new RegistryChangedEventArgs<INotificationElementRegistryItem> { Sender = this, ItemsAdded = _itemsAdded, ItemsRemoved = _itemsRemoved });

                    _itemsAdded.Clear();
                    _itemsRemoved.Clear();
                }
            }
        }
    }
}
