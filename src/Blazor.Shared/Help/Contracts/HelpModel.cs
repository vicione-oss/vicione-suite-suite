using Sdk.Client.Components.Cards.Contracts;

namespace Blazor.Shared.Help.Contracts;

public sealed class HelpModel : ICardModel
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public required string TeaserText { get; set; }
    public required string Text { get; set; }
    public Uri? TeaserImageUrl { get; set; }
}
