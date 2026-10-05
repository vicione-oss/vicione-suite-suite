using Sdk.Client.Connections;

namespace Blazor.Shared.Tests.Connections.Extensions;

internal static class IConnectionTypeUiRegistryExtensions
{
    public static IConnectionTypeUiRegistry Setup(this IConnectionTypeUiRegistry uiRegistry, IReadOnlyDictionary<string, string> displayNames)
    {
        uiRegistry.TryGetDisplayName(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(c =>
            {
                c[1] = displayNames[c.ArgAt<string>(0)];
                return true;
            });

        return uiRegistry;
    }
}
