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

    private void OpenTicketCenter() => LogOpenTicketCenter(Logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Open Ticket Center...todo")]
    private static partial void LogOpenTicketCenter(ILogger logger);
}
