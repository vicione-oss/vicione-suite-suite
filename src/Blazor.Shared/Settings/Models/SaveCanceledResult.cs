using Sdk.Client.ControlPanels.Models;

namespace Blazor.Shared.Settings.Models;

/// <summary>
/// A save canceled by disposal or by a superseding call; it counts as failure but is not displayed.
/// </summary>
internal sealed class SaveCanceledResult : ISaveResult
{
    public string? Message => null;
}
