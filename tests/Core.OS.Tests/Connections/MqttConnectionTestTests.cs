using Core.OS.Connections;
using Core.Tests.Tools;
using Sdk.Connections.Contracts;

namespace Core.OS.Tests.Connections;

public class MqttConnectionTestTests
{
    public sealed class Test : MqttConnectionTestTests
    {
        [Fact]
        public async Task Should_return_failure_when_connection_is_not_mqtt_connection()
        {
            // Arrange
            var connection = Substitute.For<IConnection>();
            var sut = new MqttConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorInfo.Should().NotBeNull();
            result.ErrorInfo.Message.Should().Contain("Invalid connection type");
        }

        [Fact]
        public async Task Should_return_failure_when_no_broker_listens_on_the_port()
        {
            // Arrange
            var connection = new MqttConnection
            {
                Address = "localhost",
                Port = 1,
                Protocol = MqttConnectionType.TCP,
                ConnectTimeoutSeconds = 5,
            };
            var sut = new MqttConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorInfo.Should().NotBeNull();
            result.ErrorInfo.Message.Should().StartWith("MQTT connection test failed");
        }
    }

    /// <summary>
    /// Runs against a real broker with a TLS listener, configured through the environment: <c>MQTT_TLS_HOST</c>
    /// (default <c>localhost</c>, must match the broker certificate), <c>MQTT_TLS_PORT</c> (default 8883) and, for a
    /// listener with <c>require_certificate true</c>, <c>MQTT_CLIENT_CERTIFICATE</c> and <c>MQTT_CLIENT_CERTIFICATE_KEY</c>
    /// (absolute paths or PEM content). The CA of the broker certificate must be trusted by the host running the test,
    /// and its revocation status must be checkable online, since MQTTnet rejects a certificate without a reachable CRL.
    /// </summary>
    public sealed class Tls : MqttConnectionTestTests
    {
        private static MqttConnection CreateTlsConnection() => new()
        {
            Address = IntegrationServiceSettings.GetHost("MQTT_TLS_HOST"),
            Port = IntegrationServiceSettings.GetPort("MQTT_TLS_PORT", 8883),
            Protocol = MqttConnectionType.TCP,
            SslProtocol = MqttSslProtocol.Tls12,
            ClientCertificate = Environment.GetEnvironmentVariable("MQTT_CLIENT_CERTIFICATE"),
            ClientCertificateKey = Environment.GetEnvironmentVariable("MQTT_CLIENT_CERTIFICATE_KEY"),
            ConnectTimeoutSeconds = 10,
        };

        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_connect_with_tls_1_2_to_a_broker_with_a_trusted_certificate()
        {
            // Arrange
            var sut = new MqttConnectionTest();

            // Act
            var result = await sut.Test(CreateTlsConnection(), TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue(result.ErrorInfo?.Message);
        }

        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_connect_with_tls_1_2_when_untrusted_certificates_are_allowed()
        {
            // Arrange
            var connection = CreateTlsConnection();
            connection.AllowUntrustedCertificates = true;
            var sut = new MqttConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue(result.ErrorInfo?.Message);
        }

        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_fail_without_tls_against_a_tls_listener()
        {
            // Arrange
            var connection = CreateTlsConnection();
            connection.SslProtocol = null;
            var sut = new MqttConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeFalse();
        }
    }
}
