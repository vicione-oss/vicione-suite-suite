using System.IO.Abstractions.TestingHelpers;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Core.Shared.Extensions;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace Core.Shared.Tests.Extensions;

public class MqttClientOptionsBuilderExtensionsTests
{
    private static MqttConnection CreateTcpConnection() => new()
    {
        Address = "broker.local",
        Port = 1883,
        Protocol = MqttConnectionType.TCP,
        ProtocolVersion = MqttProtocolVersion.V500,
    };

    private static MqttClientOptions Build(MqttConnection connection) =>
        new MqttClientOptionsBuilder()
            .WithSuiteConnection(connection, new MockFileSystem())
            .Build();

    public sealed class WithCredentials : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_username_and_password_when_both_are_provided()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Username = "user";
            connection.Password = "secret";

            // Act
            var options = Build(connection);

            // Assert
            options.Credentials.Should().NotBeNull();
            options.Credentials!.GetUserName(options).Should().Be("user");
            Encoding.UTF8.GetString(options.Credentials.GetPassword(options)).Should().Be("secret");
        }

        [Fact]
        public void Should_apply_username_only_when_password_is_empty()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Username = "user";
            connection.Password = string.Empty;

            // Act
            var options = Build(connection);

            // Assert
            options.Credentials.Should().NotBeNull();
            options.Credentials!.GetUserName(options).Should().Be("user");
        }

        [Fact]
        public void Should_not_apply_credentials_when_username_is_empty()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Username = string.Empty;

            // Act
            var options = Build(connection);

            // Assert
            options.Credentials.Should().BeNull();
        }
    }

    public sealed class WithClientId : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_client_id_when_provided()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.ClientId = "my-client";

            // Act
            var options = Build(connection);

            // Assert
            options.ClientId.Should().Be("my-client");
        }
    }

    public sealed class WithProtocolAndVersion : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_configure_tcp_server_for_tcp_protocol()
        {
            // Arrange
            var connection = CreateTcpConnection();

            // Act
            var options = Build(connection);

            // Assert
            var tcpOptions = options.ChannelOptions.Should().BeOfType<MqttClientTcpOptions>().Subject;
            tcpOptions.RemoteEndpoint.ToString().Should().Contain("broker.local");
        }

        [Fact]
        public void Should_configure_websocket_server_for_websocket_protocol()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Protocol = MqttConnectionType.WebSocket;

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.Should().BeOfType<MqttClientWebSocketOptions>();
        }

        [Fact]
        public void Should_map_protocol_version()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.ProtocolVersion = MqttProtocolVersion.V311;

            // Act
            var options = Build(connection);

            // Assert
            options.ProtocolVersion.Should().Be(MQTTnet.Formatter.MqttProtocolVersion.V311);
        }
    }

    public sealed class WithCleanSessionSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_clean_session_flag()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.CleanSession = false;

            // Act
            var options = Build(connection);

            // Assert
            options.CleanSession.Should().BeFalse();
        }
    }

    public sealed class WithWillOptions : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_will_options_when_topic_and_message_are_provided()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.WillTopic = "status/offline";
            connection.WillMessage = "offline";
            connection.WillRetain = true;
            connection.QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce;

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().Be("status/offline");
            Encoding.UTF8.GetString(options.WillPayload).Should().Be("offline");
            options.WillRetain.Should().BeTrue();
            options.WillQualityOfServiceLevel.Should().Be(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        }

        [Fact]
        public void Should_not_apply_will_options_when_topic_is_missing()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.WillMessage = "offline";

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().BeNull();
        }

        [Fact]
        public void Should_apply_will_options_with_an_empty_message()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.WillTopic = "status/offline";

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().Be("status/offline");
            (options.WillPayload ?? []).Should().BeEmpty();
        }
    }

    public sealed class WithTimingSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_keep_alive_and_connect_timeout()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.KeepAliveSeconds = 45;
            connection.ConnectTimeoutSeconds = 12;

            // Act
            var options = Build(connection);

            // Assert
            options.KeepAlivePeriod.Should().Be(TimeSpan.FromSeconds(45));
            options.Timeout.Should().Be(TimeSpan.FromSeconds(12));
        }
    }

    public sealed class WithTlsSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_not_use_tls_without_ssl_protocol()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.ClientCertificate = "left over from a previous TLS setting";

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.UseTls.Should().BeFalse();
        }

        [Fact]
        public void Should_apply_the_selected_ssl_protocol()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.SslProtocol = MqttSslProtocol.Tls13;

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.UseTls.Should().BeTrue();
            options.ChannelOptions.TlsOptions.SslProtocol.Should().Be(SslProtocols.Tls13);
            options.ChannelOptions.TlsOptions.CertificateValidationHandler.Should().BeNull();
        }

        [Fact]
        public void Should_accept_untrusted_certificates_without_a_client_certificate()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.SslProtocol = MqttSslProtocol.Tls12;
            connection.AllowUntrustedCertificates = true;

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.AllowUntrustedCertificates.Should().BeTrue();
            options.ChannelOptions.TlsOptions.CertificateValidationHandler.Should().NotBeNull();
        }

        [Fact]
        public void Should_apply_the_ssl_protocol_and_the_client_certificate()
        {
            // Arrange
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            var connection = CreateTcpConnection();
            connection.SslProtocol = MqttSslProtocol.Tls12;
            connection.ClientCertificate = certificate.ExportCertificatePem();
            connection.ClientCertificateKey = rsa.ExportPkcs8PrivateKeyPem();

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.SslProtocol.Should().Be(SslProtocols.Tls12);
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider.Should().NotBeNull();
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates().Count.Should().Be(1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Should_load_an_encrypted_client_key(bool fromFiles)
        {
            // Arrange
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            var encryptedKey = rsa.ExportEncryptedPkcs8PrivateKeyPem("secret",
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000));

            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(@"/certs/client.pem", new MockFileData(certificate.ExportCertificatePem()));
            fileSystem.AddFile(@"/certs/client.key", new MockFileData(encryptedKey));

            var connection = CreateTcpConnection();
            connection.SslProtocol = MqttSslProtocol.Tls12;
            connection.ClientCertificate = fromFiles ? @"/certs/client.pem" : certificate.ExportCertificatePem();
            connection.ClientCertificateKey = fromFiles ? @"/certs/client.key" : encryptedKey;
            connection.ClientCertificateKeyPassword = "secret";

            // Act
            var options = new MqttClientOptionsBuilder().WithSuiteConnection(connection, fileSystem).Build();

            // Assert
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates().Count.Should().Be(1);
        }

        [Fact]
        public void Should_ignore_an_empty_client_certificate()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.SslProtocol = MqttSslProtocol.Tls12;
            connection.ClientCertificate = string.Empty;

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.UseTls.Should().BeTrue();
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider.Should().BeNull();
        }

        [Fact]
        public void Should_connect_a_tcp_with_tls_connection_over_tcp_with_tls()
        {
            // Arrange
            var connection = CreateTcpConnection();
#pragma warning disable CS0618 // Connections stored before SslProtocol existed still carry TCPWithTLS.
            connection.Protocol = MqttConnectionType.TCPWithTLS;
#pragma warning restore CS0618

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.Should().BeOfType<MqttClientTcpOptions>();
            options.ChannelOptions.TlsOptions.UseTls.Should().BeTrue();
        }
    }

    public sealed class WithWebSocketSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Theory]
        [InlineData("broker.local", null, "ws://broker.local:9001/")]
        [InlineData("http://broker.local/mqtt", null, "ws://broker.local:9001/mqtt")]
        [InlineData("https://broker.local/mqtt", null, "wss://broker.local:9001/mqtt")]
        [InlineData("ws://broker.local:8080/mqtt", null, "ws://broker.local:9001/mqtt")]
        [InlineData("ws://broker.local/mqtt", MqttSslProtocol.Tls12, "wss://broker.local:9001/mqtt")]
        public void Should_build_the_websocket_uri_from_address_port_and_tls(string address, MqttSslProtocol? sslProtocol, string expected)
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Protocol = MqttConnectionType.WebSocket;
            connection.Address = address;
            connection.Port = 9001;
            connection.SslProtocol = sslProtocol;

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.Should().BeOfType<MqttClientWebSocketOptions>().Which.Uri.Should().Be(expected);
        }

        [Fact]
        public void Should_pass_the_tls_options_to_the_websocket_channel()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Protocol = MqttConnectionType.WebSocket;
            connection.SslProtocol = MqttSslProtocol.Tls12;
            connection.AllowUntrustedCertificates = true;

            // Act
            var options = Build(connection);

            // Assert
            var tlsOptions = options.ChannelOptions.Should().BeOfType<MqttClientWebSocketOptions>().Which.TlsOptions;
            tlsOptions.UseTls.Should().BeTrue();
            tlsOptions.CertificateValidationHandler.Should().NotBeNull();
        }
    }
}
