namespace Blazor.Shared.Settings.Services;

internal sealed class NavigateBackRequestedEventArgs
{
    /// <summary>
    /// True if the action should be canceled, otherwise false.
    /// </summary>
    public bool Cancel { get; set; }
}
