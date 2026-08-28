using Blazor.Shared.EnvironmentOverrides.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;

public sealed class EnvironmentOverridesControlPanelState : ControlPanelState
{
    internal List<EnvironmentOverrideEntry> Entries { get; set; } = [];

    /// <summary>
    /// Set when the stored overrides could not be read. The panel then shows the failure instead of
    /// an editable grid, because an empty grid would be saved back over the real file contents.
    /// </summary>
    internal string? LoadError { get; set; }

    /// <summary>
    /// Set once overrides were stored, so the in-panel restart action only appears when there is
    /// something to restart for. Offering it while edits are unsaved would discard them.
    /// </summary>
    internal bool RestartRequired { get; set; }
}
