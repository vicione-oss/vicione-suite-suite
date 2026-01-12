namespace Blazor.Shared.Settings.Services;

internal interface ISettingsPopupRequest
{
    event Func<Task>? SettingsPopupRequested;

    Task Send();
}
