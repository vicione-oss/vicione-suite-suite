using Blazor.Shared.EnvironmentOverrides.ControlPanels.Models;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Extensions;

internal static class EnvironmentOverridesControlPanelStateExtensions
{
    public static void Initialize(this EnvironmentOverridesControlPanelState state,
        IReadOnlyDictionary<string, string> overrides)
    {
        state.Entries =
        [
            .. overrides.Select(o => new EnvironmentOverrideEntry
            {
                Name = o.Key,
                Value = o.Value
            })
        ];
    }
}
