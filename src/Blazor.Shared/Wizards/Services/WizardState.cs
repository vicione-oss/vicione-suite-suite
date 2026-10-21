using System.Runtime.CompilerServices;
using Blazor.Shared.Wizards.Models;
using Sdk.Client.Models;
using Sdk.Client.Wizards.Models;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Wizards.Services;

[NotifyChanged]
internal sealed partial class WizardState : IWizardState
{
    private readonly Lock _concurrentLock = new();
    private readonly HashSet<string> _changedProperties = [];
    private int _updateLock;


    /// <inheritdoc/>
    public int UpdateLock => _updateLock;

    /// <inheritdoc/>
    public partial bool LoadingOverlayVisible { get; set; }

    /// <inheritdoc/>
    public partial IReadOnlyList<WizardStep> Steps { get; set; } = [];

    /// <inheritdoc/>
    public partial WizardStep? ActiveStep { get; set; }

    /// <inheritdoc/>
    public partial bool BackButtonEnabled { get; set; }

    /// <inheritdoc/>
    public partial bool NextButtonEnabled { get; set; }

    /// <inheritdoc/>
    public partial bool FinishButtonEnabled { get; set; }

    /// <inheritdoc/>
    public partial List<SaveErrorResult> LastSaveErrorResults { get; set; } = [];

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
            Changed?.Invoke(new PropertiesChangedEventArgs(this, new HashSet<string> { propertyName }));
        else
        {
            lock (_concurrentLock)
            {
                _changedProperties.Add(propertyName);
            }
        }
    }
}
