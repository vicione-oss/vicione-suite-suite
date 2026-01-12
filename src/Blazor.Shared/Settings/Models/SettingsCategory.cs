namespace Blazor.Shared.Settings.Models;

internal sealed record SettingsCategory
{
    public required string Title { get; init; }
    public string? IconCssClass { get; init; }
    public Uri? IconUrl { get; init; }
    public int? Position { get; init; }
    public int GroupPosition { get; init; }
}
