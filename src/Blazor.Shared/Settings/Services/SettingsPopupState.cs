using System.Runtime.CompilerServices;
using Sdk.Client.Models;

namespace Blazor.Shared.Settings.Services;

internal sealed class SettingsPopupState : ISettingsPopupState
{
    private bool _visible;

    public bool Visible
    {
        get => _visible;
        set
        {
            if (value != _visible)
            {
                _visible = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public event Action<PropertiesChangedEventArgs>? Changed;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
            return;

        Changed?.Invoke(new PropertiesChangedEventArgs(this, new HashSet<string> { propertyName }));
    }
}
