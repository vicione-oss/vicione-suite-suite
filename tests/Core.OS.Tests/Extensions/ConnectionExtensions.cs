using Sdk.Connections.Contracts;
using Xunit;

namespace Core.OS.Tests.Extensions;

internal static class ConnectionExtensions
{
    public static bool IsEqualTo(this Connection connection, Connection other)
        => connection.IsEqualTo(other, true);

    public static bool IsEqualTo(this Connection connection, Connection other, bool compareId)
        => connection.Json == other.Json &&
           connection.Description == other.Description &&
           compareId
            ? connection.Id == other.Id
            : true &&
              connection.Name == other.Name &&
              connection.Type == other.Type;

    public static void AssertEqual(this Connection expected, Connection? actual, bool includeId = true)
    {
        Assert.NotNull(actual);

        if (includeId)
            Assert.Equal(expected.Id, actual.Id);

        Assert.Equal(expected.Json, actual.Json);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Type, actual.Type);
    }

    public static void AssertEqual(this MqttConnection expected, MqttConnection? actual)
    {
        Assert.NotNull(actual);

        Assert.Equal(expected.Address, actual.Address);
        Assert.Equal(expected.Protocol, actual.Protocol);
        Assert.Equal(expected.Port, actual.Port);
    }
}
