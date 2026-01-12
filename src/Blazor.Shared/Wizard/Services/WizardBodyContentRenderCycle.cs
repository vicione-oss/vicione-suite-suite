namespace Blazor.Shared.Wizard.Services;

internal sealed record WizardBodyContentRenderCycle(params IEnumerable<IWizardPageRegistryItem> items) : IWizardBodyContentRenderCycle
{
    private readonly Lock _concurrentLock = new();
    private readonly List<IWizardPageRegistryItem> _items = [.. items];
    private readonly HashSet<IWizardPageRegistryItem> _finishedItems = [];

    public List<IWizardPageRegistryItem> Items => _items;

    public bool Complete { get; private set; }

    public event Action? Completed;

    public void SetFinished(IWizardPageRegistryItem wizardPageRegistryItem)
    {
        lock (_concurrentLock)
        {
            if (_finishedItems.Add(wizardPageRegistryItem))
            {
                Complete = !_items.Except(_finishedItems).Any();

                if (Complete)
                    Completed?.Invoke();
            }
        }
    }
}
