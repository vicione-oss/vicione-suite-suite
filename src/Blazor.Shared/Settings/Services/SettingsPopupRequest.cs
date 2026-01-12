namespace Blazor.Shared.Settings.Services;

internal sealed class SettingsPopupRequest : ISettingsPopupRequest
{
    public event Func<Task>? SettingsPopupRequested;

    public async Task Send()
    {
        if (SettingsPopupRequested is not null)
            await SettingsPopupRequested.Invoke();
    }
}
