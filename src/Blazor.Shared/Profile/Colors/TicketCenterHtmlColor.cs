using Blazor.Shared.Profile.Enums;
using Sdk.Client.Colors;
using Sdk.Client.Extensions;

namespace Blazor.Shared.Profile.Colors;

public sealed class TicketCenterHtmlColor : IHtmlColor
{
    private readonly TicketCenterColor _ticketCenterColor;
    private readonly string _cssCustomVariable;

    private TicketCenterHtmlColor(TicketCenterColor color)
    {
        _ticketCenterColor = color;
        _cssCustomVariable = $"var(--ticket-center-color-{_ticketCenterColor.ToString().ToHyphenSeparated()})";
    }

    public static TicketCenterHtmlColor From(TicketCenterColor color) => new(color);

    public override string ToString() => _cssCustomVariable;
}
