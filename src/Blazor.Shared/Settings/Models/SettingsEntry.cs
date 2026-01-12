using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Models;

internal sealed class SettingsEntry
{
    public required string Title { get; init; }
    public required string? IconPath { get; init; }
    public required IControlPanelRegistryItem ControlPanelRegistryItem { get; init; }
    public int? Position { get; init; }
}
