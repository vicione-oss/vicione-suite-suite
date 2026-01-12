using Sdk.Client.Colors;

namespace Blazor.Shared.Profile.Models;

public sealed class TicketCenterEntry
{
    public string? DueDateLevel { get; set; }
    public string? Message { get; set; }
    public IHtmlColor? Color { get; set; }
}
