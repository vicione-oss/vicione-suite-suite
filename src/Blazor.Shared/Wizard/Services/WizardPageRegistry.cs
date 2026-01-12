using System.Collections;
using Blazor.Shared.Wizard.Components;
using Sdk.Client.Services;

namespace Blazor.Shared.Wizard.Services;

internal sealed class WizardPageRegistry<TContext> : IWizardPageRegistry<TContext>
{
    private readonly Lock _concurrentLock = new();
    private readonly List<IWizardPageRegistryItem> _items = [];
    private readonly List<IWizardPageRegistryItem> _itemsAdded = [];
    private readonly List<IWizardPageRegistryItem> _itemsRemoved = [];

    public int UpdateLock { get; private set; }

    public event Action<RegistryChangedEventArgs<IWizardPageRegistryItem>>? Changed;

    public IWizardPageRegistryItem Add<TComponent, TState>(IWizardPageDescriptor descriptor, TState state)
        where TComponent : WizardPage<TState>
        where TState : IWizardPageState
    {
        var item = new WizardPageRegistryItem(typeof(TComponent), descriptor, state);

        lock (_concurrentLock)
        {
            _items.Add(item);

            if (UpdateLock == 0)
                Changed?.Invoke(new RegistryChangedEventArgs<IWizardPageRegistryItem> { Sender = this, ItemsAdded = [item], ItemsRemoved = [] });
            else
                _itemsAdded.Add(item);
        }

        return item;
    }

    public bool Remove(IWizardPageRegistryItem item)
        => Remove(i => i == item) > 0;

    public int Remove<TComponent>() where TComponent : IWizardPage
        => Remove(i => i.ComponentType == typeof(TComponent));

    public int Remove(Predicate<IWizardPageRegistryItem> match)
    {
        var itemsRemoved = new List<IWizardPageRegistryItem>();

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
                    Changed?.Invoke(new RegistryChangedEventArgs<IWizardPageRegistryItem> { Sender = this, ItemsAdded = [], ItemsRemoved = itemsRemoved });
                else
                    _itemsRemoved.AddRange(itemsRemoved);
            }
        }

        return itemsRemoved.Count;
    }

    public IEnumerator<IWizardPageRegistryItem> GetEnumerator()
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
                    Changed?.Invoke(new RegistryChangedEventArgs<IWizardPageRegistryItem> { Sender = this, ItemsAdded = _itemsAdded, ItemsRemoved = _itemsRemoved });

                    _itemsAdded.Clear();
                    _itemsRemoved.Clear();
                }
            }
        }
    }
}
