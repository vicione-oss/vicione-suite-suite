using System.Runtime.CompilerServices;
using Blazor.Shared.Models;
using Blazor.Shared.Wizard.Models;

namespace Blazor.Shared.Wizard.Services;

internal sealed class WizardState<TContext> : IWizardState<TContext>
{
    private readonly Lock _concurrentLock = new();
    private readonly HashSet<string> _changedProperties = [];
    private int _updateLock;

    private bool _loadingOverlayVisible;
    private IReadOnlyList<WizardStep> _steps = [];
    private WizardStep? _activeStep;
    private bool _backButtonEnabled;
    private bool _nextButtonEnabled;
    private bool _finishButtonEnabled;
    private WizardPageEditFinishResult? _lastSaveResult;

    /// <inheritdoc/>
    public int UpdateLock => _updateLock;

    /// <inheritdoc/>
    public bool LoadingOverlayVisible
    {
        get => _loadingOverlayVisible;
        set
        {
            if (value != _loadingOverlayVisible)
            {
                _loadingOverlayVisible = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<WizardStep> Steps
    {
        get => _steps;
        set
        {
            if (value != _steps)
            {
                _steps = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public WizardStep? ActiveStep
    {
        get => _activeStep;
        set
        {
            if (value != _activeStep)
            {
                _activeStep = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public bool BackButtonEnabled
    {
        get => _backButtonEnabled;
        set
        {
            if (value != _backButtonEnabled)
            {
                _backButtonEnabled = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public bool NextButtonEnabled
    {
        get => _nextButtonEnabled;
        set
        {
            if (value != _nextButtonEnabled)
            {
                _nextButtonEnabled = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public bool FinishButtonEnabled
    {
        get => _finishButtonEnabled;
        set
        {
            if (value != _finishButtonEnabled)
            {
                _finishButtonEnabled = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public WizardPageEditFinishResult? LastSaveResult
    {
        get => _lastSaveResult;
        set
        {
            if (value != _lastSaveResult)
            {
                _lastSaveResult = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public event Action<PropertiesChangedEventArgs>? Changed;

    /// <inheritdoc/>
    public void BeginUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock++;
        }
    }

    /// <inheritdoc/>
    public void EndUpdate()
    {
        HashSet<string> changedProperties;

        lock (_concurrentLock)
        {
            _updateLock--;

            if (_updateLock > 0)
                return;

            _updateLock = 0;

            if (_changedProperties.Count == 0)
                return;

            changedProperties = [.. _changedProperties];

            _changedProperties.Clear();
        }

        Changed?.Invoke(new PropertiesChangedEventArgs(this, changedProperties));
    }

    /// <summary>
    /// Raises a <see cref="Changed">Changed</see> event when no <see cref="BeginUpdate">update cycle</see> is running.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
            return;

        if (_updateLock == 0)
        {
            Changed?.Invoke(new PropertiesChangedEventArgs(this, new HashSet<string> { propertyName }));
        }
        else
        {
            lock (_concurrentLock)
            {
                _changedProperties.Add(propertyName);
            }
        }
    }
}
