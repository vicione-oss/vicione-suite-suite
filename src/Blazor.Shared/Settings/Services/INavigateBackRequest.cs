namespace Blazor.Shared.Settings.Services;

internal interface INavigateBackRequest
{
    /// <summary>
    /// Raised when <see cref="Send"/> was called
    /// </summary>
    event Func<NavigateBackRequestedEventArgs, Task>? NavigateBackRequested;

    /// <summary>
    /// Raises event <see cref="NavigateBackRequested"/> to notifiy about
    /// the request to navigate to the previous control panel
    /// </summary>
    /// <returns>
    /// True if the request was canceled via <see cref="NavigateBackRequestedEventArgs.Cancel"/>, otherwise false.
    /// </returns>
    Task<bool> Send();
}
