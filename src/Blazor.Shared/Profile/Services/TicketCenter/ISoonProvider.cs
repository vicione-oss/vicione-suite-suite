using Blazor.Shared.Profile.Models;

namespace Blazor.Shared.Profile.Services.TicketCenter;

internal interface ISoonProvider
{
    TicketCenterEntry GetSoon();
}
