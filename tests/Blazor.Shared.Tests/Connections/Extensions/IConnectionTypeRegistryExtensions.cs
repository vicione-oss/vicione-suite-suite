using NSubstitute;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.Extensions;

internal static class IConnectionTypeRegistryExtensions
{
    public static IConnectionTypeRegistry Setup(this IConnectionTypeRegistry registry)
    {
        registry.GetConnectionTypes().Returns([ConnectionType.Mqtt]);
        registry.TryCreateConnection(Arg.Any<string>(), out Arg.Any<IConnection?>())
            .Returns(c =>
            {
                c[1] = Substitute.For<IConnection>();
                return true;
            });
        registry.TryGetConnectionSerializer(Arg.Any<string>(), out Arg.Any<IConnectionSerializer?>())
            .Returns(c =>
            {
                c[1] = Substitute.For<IConnectionSerializer>();
                return true;
            });
        registry.TryGetConnectionTest(Arg.Any<string>(), out Arg.Any<IConnectionTest?>())
            .Returns(c =>
            {
                c[1] = Substitute.For<IConnectionTest>();
                return true;
            });

        return registry;
    }
}
