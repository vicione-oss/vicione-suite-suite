using Blazor.Shared.Profile.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Profile.Components;

public partial class TicketCenterComponent
{
    [Parameter, EditorRequired]
    public TicketCenterEntry Urgent { get; set; }
    [Parameter, EditorRequired]
    public TicketCenterEntry Today { get; set; }
    [Parameter, EditorRequired]
    public TicketCenterEntry ThisWeek { get; set; }
    [Parameter, EditorRequired]
    public TicketCenterEntry Soon { get; set; }
}
