using Blazor.Shared.Profile.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazor.Shared.Profile.Components;

public partial class TicketCenterComponent
{
    [Inject] private ILogger<TicketCenterComponent> Logger { get; set; } = default!;

    [Parameter, EditorRequired]
    public TicketCenterEntry Urgent { get; set; }
    [Parameter, EditorRequired]
    public TicketCenterEntry Today { get; set; }
    [Parameter, EditorRequired]
    public TicketCenterEntry ThisWeek { get; set; }
    [Parameter, EditorRequired]
    public TicketCenterEntry Soon { get; set; }

    private void OpenTicketCenter() => Logger.LogDebug("Open Ticket Center...todo");
}
