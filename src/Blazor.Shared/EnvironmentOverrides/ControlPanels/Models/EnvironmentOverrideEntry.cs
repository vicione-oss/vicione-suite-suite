namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Models;

internal sealed record EnvironmentOverrideEntry
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
