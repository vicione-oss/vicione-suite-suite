using Blazor.Shared.Profile.Colors;
using Blazor.Shared.Profile.Enums;
using Blazor.Shared.Profile.Models;

namespace Blazor.Shared.Profile.Services.TicketCenter;

internal sealed class DummyThisWeekProvider : IThisWeekProvider
{
    public TicketCenterEntry GetThisWeek() => new()
    {
        DueDateLevel = Localization.TicketCenterComponent.ThisWeek,
        Message = $"11 Items (6 new)",
        Color = TicketCenterHtmlColor.From(TicketCenterColor.Orange)
    };
}
