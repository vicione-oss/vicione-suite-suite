using Blazor.Shared.Profile.Colors;
using Blazor.Shared.Profile.Enums;
using Blazor.Shared.Profile.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Profile.Services.TicketCenter;

internal sealed class DummyTodayProvider : ITodayProvider
{
    public TicketCenterEntry GetToday() => new()
    {
        DueDateLevel = CommonVocabulary.Today,
        Message = "8 Items (4 new)",
        Color = TicketCenterHtmlColor.From(TicketCenterColor.Red)
    };
}
