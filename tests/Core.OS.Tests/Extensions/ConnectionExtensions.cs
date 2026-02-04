using Sdk.Connections.Contracts;
using Xunit;

namespace Core.OS.Tests.Extensions;

internal static class ConnectionExtensions
{
    extension(Connection connection)
    {
        public bool IsEqualTo(Connection other)
            => connection.IsEqualTo(other, true);

        public bool IsEqualTo(Connection other, bool compareId)
            => connection.Json == other.Json &&
               connection.Description == other.Description &&
               compareId
                ? connection.Id == other.Id
                : true &&
                  connection.Name == other.Name &&
                  connection.Type == other.Type;

        public void AssertEqual(Connection? actual, bool includeId = true)
        {
            Assert.NotNull(actual);

            if (includeId)
                Assert.Equal(connection.Id, actual.Id);

            Assert.Equal(connection.Json, actual.Json);
            Assert.Equal(connection.Description, actual.Description);
            Assert.Equal(connection.Name, actual.Name);
            Assert.Equal(connection.Type, actual.Type);
        }
    }

    public static void AssertEqual(this MqttConnection expected, MqttConnection? actual)
    {
        Assert.NotNull(actual);

        Assert.Equal(expected.Address, actual.Address);
        Assert.Equal(expected.Protocol, actual.Protocol);
        Assert.Equal(expected.Port, actual.Port);
    }
}
