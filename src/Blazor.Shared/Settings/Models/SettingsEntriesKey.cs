namespace Blazor.Shared.Settings.Models;

internal sealed record SettingsEntriesKey
{
    public required SettingsGroup SettingsGroup { get; init; }
    public required SettingsCategory SettingsCategory { get; init; }
}
