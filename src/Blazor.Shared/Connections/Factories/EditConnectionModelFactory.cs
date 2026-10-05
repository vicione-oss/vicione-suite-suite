using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.ControlPanels.Connections.Extensions;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Factories;

internal static class EditConnectionModelFactory
{
    public static EditConnectionModel CreateNew(IConnectionTypeRegistry connectionTypeRegistry, IConnectionTypeUiRegistry connectionTypeUiRegistry)
        => new(new Connection()
        {
            Id = Guid.NewGuid(),
            Type = connectionTypeRegistry.GetConnectionTypeComboBoxItems(connectionTypeUiRegistry).First().Value,
        }, connectionTypeRegistry);
}
