using System.Runtime.CompilerServices;
using Blazor.Shared.Models;
using Blazor.Shared.Wizard.Models;

namespace Blazor.Shared.Wizard.Services;

/// <summary>
/// Default implementation of <see cref="IWizardPageState"/>
/// </summary>
public class WizardPageState : IWizardPageState
{
    private readonly Lock _concurrentLock = new();
    private int _operationCounter;
    private IWizardOperation? _currentOperation;

    public IWizardOperation? CurrentOperation
    {
        get => _currentOperation;
        private set
        {
            if (value != _currentOperation)
            {
                _currentOperation = value;

                OnPropertyChanged();
            }
        }
    }

    public event Action<PropertiesChangedEventArgs>? Changed;

    public void BeginOperation(IWizardOperation operation)
    {
        lock (_concurrentLock)
        {
            _operationCounter++;

            if (_operationCounter == 1)
                CurrentOperation = operation;
        }
    }

    public void EndOperation()
    {
        lock (_concurrentLock)
        {
            if (_operationCounter > 0)
                _operationCounter--;

            if (_operationCounter == 0)
                CurrentOperation = null;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
            return;

        Changed?.Invoke(new PropertiesChangedEventArgs(this, new HashSet<string> { propertyName }));
    }
}
