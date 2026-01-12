using AwesomeAssertions;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.Extensions;

internal static class ConnectionExtensions
{
    public static void AssertEqual(this Connection expected, Connection? actual, bool includeId = true)
    {
        if (includeId)
            expected.Should().BeEquivalentTo(actual);
        else
            expected.Should().BeEquivalentTo(actual, config => config.Excluding(k => k!.Id));
    }
}
