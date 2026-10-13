using Sdk.Client.Wizards.Models;

namespace Blazor.Shared.Wizards.Models;

/// <summary>
/// A save canceled by disposal or by a superseding call; it counts as failure but is not displayed.
/// </summary>
internal sealed class SaveCanceledResult : ISaveResult
{
    public string? Message => null;
}
