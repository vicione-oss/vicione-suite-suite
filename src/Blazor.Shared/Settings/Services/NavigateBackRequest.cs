namespace Blazor.Shared.Settings.Services;

internal sealed class NavigateBackRequest : INavigateBackRequest
{
    public event Func<NavigateBackRequestedEventArgs, Task>? NavigateBackRequested;

    public async Task<bool> Send()
    {
        if (NavigateBackRequested is not null)
        {
            var args = new NavigateBackRequestedEventArgs();

            await NavigateBackRequested.Invoke(args);

            return !args.Cancel;
        }
        else
        {
            return false;
        }
    }
}
