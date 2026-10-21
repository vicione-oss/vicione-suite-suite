using System.Runtime.CompilerServices;
using Sdk.Client.Models;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Settings.Services;

[NotifyChanged]
internal sealed partial class SettingsPopupState : ISettingsPopupState
{
    public partial bool Visible { get; set; }

    /// <inheritdoc/>
    public event Action<PropertiesChangedEventArgs>? Changed;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
            return;

        Changed?.Invoke(new PropertiesChangedEventArgs(this, new HashSet<string> { propertyName }));
    }
}
