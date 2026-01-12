using Blazor.Shared.Profile.Colors;
using Blazor.Shared.Profile.Enums;
using Blazor.Shared.Profile.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Profile.Services.TicketCenter;

internal sealed class DummySoonProvider : ISoonProvider
{
    public TicketCenterEntry GetSoon() => new()
    {
        DueDateLevel = CommonVocabulary.Soon,
        Message = "19 Items",
        Color = TicketCenterHtmlColor.From(TicketCenterColor.LightBlue)
    };
}
