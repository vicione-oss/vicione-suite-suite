using Blazor.Shared.Connections.Contracts;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Factories;

internal static class EditConnectionModelFactory
{
    public static EditConnectionModel CreateNew(IConnectionTypeRegistry connectionTypeRegistry)
        => new(new Connection()
        {
            Id = Guid.NewGuid(),
            Type = new(connectionTypeRegistry.GetConnectionTypes().First()),
        }, connectionTypeRegistry);
}
