namespace Blazor.Shared.Settings.Services;

internal sealed class SettingsPopupRequest : ISettingsPopupRequest
{
    public event Action? SettingsPopupRequested;

    public void Send()
        => SettingsPopupRequested?.Invoke();
}
