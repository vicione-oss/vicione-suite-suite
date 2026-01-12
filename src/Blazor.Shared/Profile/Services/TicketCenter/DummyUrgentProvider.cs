using Blazor.Shared.Profile.Colors;
using Blazor.Shared.Profile.Enums;
using Blazor.Shared.Profile.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Profile.Services.TicketCenter;

internal sealed class DummyUrgentProvider : IUrgentProvider
{
    public TicketCenterEntry GetUrgent() => new()
    {
        DueDateLevel = CommonVocabulary.Urgent,
        Message = "3 Items (2 new)",
        Color = TicketCenterHtmlColor.From(TicketCenterColor.DarkRed)
    };
}
