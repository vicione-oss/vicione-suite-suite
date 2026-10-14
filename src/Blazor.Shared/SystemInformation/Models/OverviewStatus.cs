namespace Blazor.Shared.SystemInformation.Models;

/// <remarks>
/// Ordered from the most to the least severe, so sorting by the value puts problems first.
/// </remarks>
public enum OverviewStatus
{
    Failed,
    Warning,
    Normal,
    Inactive,
}
