using Blazor.Shared.Connections.Validators;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.Validators;

public sealed class MqttItemValidatorTests
{
    private static MqttConnection CreateValidConnection() => new()
    {
        Address = "broker.local",
        Port = 1883,
        Protocol = MqttConnectionType.TCP,
    };

    private static IDictionary<string, List<string>> Validate(Action<MqttConnection> configure)
    {
        var connection = CreateValidConnection();
        configure(connection);

        return new MqttItemValidator().Validate(connection);
    }

    [Fact]
    public void Should_return_no_error_for_a_valid_connection()
    {
        // Act
        var errors = new MqttItemValidator().Validate(CreateValidConnection());

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("mqtt://broker.local")]
    [InlineData("broker.local:1883")]
    [InlineData("broker local")]
    public void Should_reject_a_tcp_address_that_is_not_a_host(string address)
    {
        // Act
        var errors = Validate(c => c.Address = address);

        // Assert
        errors.Should().ContainKey(nameof(MqttConnection.Address));
    }

    [Theory]
    [InlineData("broker.local", true)]
    [InlineData("ws://broker.local/mqtt", true)]
    [InlineData("https://broker.local/mqtt", true)]
    [InlineData("[::1]", true)]
    [InlineData("mqtt://broker.local", false)]
    [InlineData("broker local", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    public void Should_accept_a_host_or_a_websocket_uri_for_websocket(string address, bool isValid)
    {
        // Act
        var errors = Validate(c =>
        {
            c.Protocol = MqttConnectionType.WebSocket;
            c.Address = address;
        });

        // Assert
        errors.ContainsKey(nameof(MqttConnection.Address)).Should().Be(!isValid);
    }

    [Theory]
    [InlineData("ws://broker.local:8083/mqtt", false)]
    [InlineData("ws://broker.local:9001/mqtt", true)]
    [InlineData("wss://broker.local/mqtt", true)]
    public void Should_reject_a_websocket_address_whose_port_differs_from_the_port(string address, bool isValid)
    {
        // Act
        var errors = Validate(c =>
        {
            c.Protocol = MqttConnectionType.WebSocket;
            c.Address = address;
            c.Port = 9001;
        });

        // Assert
        errors.ContainsKey(nameof(MqttConnection.Port)).Should().Be(!isValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Should_reject_a_port_out_of_range(int port)
    {
        // Act
        var errors = Validate(c => c.Port = port);

        // Assert
        errors.Should().ContainKey(nameof(MqttConnection.Port));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public void Should_accept_keep_alive_from_0_to_65535(int keepAliveSeconds, bool isValid)
    {
        // Act
        var errors = Validate(c => c.KeepAliveSeconds = keepAliveSeconds);

        // Assert
        errors.ContainsKey(nameof(MqttConnection.KeepAliveSeconds)).Should().Be(!isValid);
    }

    [Fact]
    public void Should_reject_a_connect_timeout_of_0()
    {
        // Act
        var errors = Validate(c => c.ConnectTimeoutSeconds = 0);

        // Assert
        errors.Should().ContainKey(nameof(MqttConnection.ConnectTimeoutSeconds));
    }

    [Fact]
    public void Should_reject_a_password_without_a_username()
    {
        // Act
        var errors = Validate(c => c.Password = "secret");

        // Assert
        errors.Should().ContainKey(nameof(MqttConnection.Username));
    }

    [Theory]
    [InlineData(MqttProtocolVersion.V311, false)]
    [InlineData(MqttProtocolVersion.V500, true)]
    public void Should_limit_the_client_id_to_23_characters_for_mqtt_3_1_1(MqttProtocolVersion protocolVersion, bool isValid)
    {
        // Act
        var errors = Validate(c =>
        {
            c.ProtocolVersion = protocolVersion;
            c.ClientId = new string('c', 24);
        });

        // Assert
        errors.ContainsKey(nameof(MqttConnection.ClientId)).Should().Be(!isValid);
    }

    [Theory]
    [InlineData(null, "offline")]
    [InlineData("status/#", null)]
    [InlineData("status/+/offline", "offline")]
    public void Should_reject_a_missing_will_topic_or_one_with_wildcards(string? willTopic, string? willMessage)
    {
        // Act
        var errors = Validate(c =>
        {
            c.WillTopic = willTopic;
            c.WillMessage = willMessage;
        });

        // Assert
        errors.Should().ContainKey(nameof(MqttConnection.WillTopic));
    }

    [Fact]
    public void Should_accept_a_will_topic_with_an_empty_message()
    {
        // Act
        var errors = Validate(c => c.WillTopic = "status/offline");

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, "inline PEM content", nameof(MqttConnection.ClientCertificate))]
    [InlineData("/etc/certs/client.pem", "inline PEM content", nameof(MqttConnection.ClientCertificateKey))]
    public void Should_reject_inconsistent_client_certificate_settings(string? certificate, string key, string expectedKey)
    {
        // Act
        var errors = Validate(c =>
        {
            c.SslProtocol = MqttSslProtocol.Tls12;
            c.ClientCertificate = certificate;
            c.ClientCertificateKey = key;
        });

        // Assert
        errors.Should().ContainKey(expectedKey);
    }

    [Fact]
    public void Should_ignore_client_certificate_settings_without_tls()
    {
        // Act
        var errors = Validate(c => c.ClientCertificateKey = "left over from a previous TLS setting");

        // Assert
        errors.Should().BeEmpty();
    }
}
