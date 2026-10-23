namespace Blazor.Shared.Settings.Models;

/// <summary>Identifies a category within its group the same way the settings service merges categories.</summary>
internal sealed record SettingsCategoryKey
{
    public required int GroupPosition { get; init; }
    public required string Title { get; init; }
}
