namespace Blazor.Shared.Settings.Services;

internal interface ISettingsPopupRequest
{
    event Action? SettingsPopupRequested;

    void Send();
}
