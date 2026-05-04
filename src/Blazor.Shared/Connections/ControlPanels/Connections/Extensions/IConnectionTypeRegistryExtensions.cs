using Sdk.Client.Connections;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Extensions;

internal static class IConnectionTypeRegistryExtensions
{
    internal static List<ComboBoxItem<ConnectionType, string>> GetConnectionTypeComboBoxItems(this IConnectionTypeRegistry connectionTypeRegistry, IConnectionTypeUiRegistry connectionTypeUiRegistry)
        => connectionTypeRegistry
            .GetConnectionTypes()
            .Select(e => new ComboBoxItem<ConnectionType, string>() { Text = connectionTypeUiRegistry.TryGetDisplayName(e, out var displayName) ? displayName : e, Value = new ConnectionType(e) })
            .ToList();
}
