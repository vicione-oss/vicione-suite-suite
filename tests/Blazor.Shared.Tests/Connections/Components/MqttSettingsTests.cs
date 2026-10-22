using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Client.Components.Settings;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Localization.Resources;
using MqttLocalization = Blazor.Shared.Connections.Components.Localization;

namespace Blazor.Shared.Tests.Connections.Components;

public class MqttSettingsTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection();
        using var ctx = new BunitContext();
        ctx.SetupBlazorUiComponents();

        // Act + Assert
        _ = ctx.Render<MqttSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });
    }

    [Fact]
    public void Should_bind_default_fields_to_model()
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection();
        using var ctx = new BunitContext();
        ctx.SetupBlazorUiComponents();

        // Act
        var component = ctx.Render<MqttSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        Assert.NotNull(connection);

        component.AssertSettingsFieldCheckBox(MqttLocalization.MqttSettings.CleanSession, connection.CleanSession);
        component.AssertSettingsFieldSpinEditInt(TechnicalTerms.Port, connection.Port);
        component.AssertSettingsFieldComboBoxWithItem(MqttLocalization.MqttSettings.TransportProtocol, connection.Protocol);
        component.AssertSettingsFieldComboBoxWithItem(MqttLocalization.MqttSettings.SslProtocol, connection.SslProtocol);
        component.AssertSettingsFieldSpinEditInt(MqttLocalization.MqttSettings.ConnectionTimeout, connection.ConnectTimeoutSeconds);
        component.AssertSettingsFieldSpinEditInt(MqttLocalization.MqttSettings.KeepAlive, connection.KeepAliveSeconds);

        component.AssertSettingsFieldTextBox(CommonVocabulary.Address, connection.Address);
        component.AssertSettingsFieldTextBox(TechnicalTerms.Username, connection.Username);
        component.AssertSettingsFieldTextBox(CommonVocabulary.Password, connection.Password);
        component.AssertSettingsFieldTextBox(MqttLocalization.MqttSettings.ClientId, connection.ClientId);
    }

    [Fact]
    public void Should_bind_certificate_fields_to_model_with_ssl()
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection();
        Assert.NotNull(connection);

        connection.SslProtocol = MqttSslProtocol.Tls12;
        connection.ClientCertificate = "ClientCertificate";
        connection.ClientCertificateKey = "ClientCertificateKey";
        connection.ClientCertificateKeyPassword = "ClientCertificateKeyPassword";

        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupBlazorUiComponents();

        // Act
        var component = ctx.Render<MqttSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        component.AssertSettingsFieldTextBox(MqttLocalization.MqttSettings.ClientCertificate, connection.ClientCertificate);
        component.AssertSettingsFieldTextBox(MqttLocalization.MqttSettings.ClientKey, connection.ClientCertificateKey);
        component.AssertSettingsFieldTextBox(MqttLocalization.MqttSettings.ClientKeyPassword, connection.ClientCertificateKeyPassword);
    }

    [Fact]
    public void Should_bind_will_fields_to_model()
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection();
        Assert.NotNull(connection);

        connection.WillTopic = "topic";
        connection.WillMessage = "a message";
        connection.WillRetain = true;
        connection.QualityOfService = MqttQualityOfServiceLevel.AtMostOnce;

        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupBlazorUiComponents();

        // Act
        var component = ctx.Render<MqttSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        component.AssertSettingsFieldTextBox(MqttLocalization.MqttSettings.WillTopic, connection.WillTopic);
        component.AssertSettingsFieldTextBox(MqttLocalization.MqttSettings.WillMessage, connection.WillMessage);
        component.AssertSettingsFieldCheckBox(MqttLocalization.MqttSettings.WillRetain, connection.WillRetain);
        component.AssertSettingsFieldComboBoxWithItem(MqttLocalization.MqttSettings.QualityOfServiceLevels, connection.QualityOfService);
    }

    [Fact]
    public async Task Should_notify_a_change_of_the_tls_version()
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection()!;
        connection.SslProtocol = MqttSslProtocol.Tls12;
        var changedCount = 0;

        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupBlazorUiComponents();

        var component = ctx.Render<MqttSettings>(parameters => parameters
            .Add(c => c.Connection, connection)
            .Add(c => c.Changed, () => changedCount++));

        var comboBox = component.GetSettingsFieldComboBox<ComboBoxItem<MqttSslProtocol?, string>, MqttSslProtocol?>(MqttLocalization.MqttSettings.SslProtocol);

        // Act
        await component.InvokeAsync(() => comboBox.Instance.ValueChanged.InvokeAsync(MqttSslProtocol.Tls13));

        // Assert
        connection.SslProtocol.Should().Be(MqttSslProtocol.Tls13);
        changedCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_clear_the_will_and_notify_when_the_will_is_switched_off()
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection()!;
        connection.WillTopic = "status/offline";
        connection.WillMessage = "offline";
        connection.WillRetain = true;
        var changedCount = 0;

        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupBlazorUiComponents();

        var component = ctx.Render<MqttSettings>(parameters => parameters
            .Add(c => c.Connection, connection)
            .Add(c => c.Changed, () => changedCount++));

        var willSwitch = component.GetSettingsFieldChild<SettingsFieldSwitch>(MqttLocalization.MqttSettings.WillOptions);

        // Act
        await component.InvokeAsync(() => willSwitch.Instance.ValueChanged.InvokeAsync(false));

        // Assert
        connection.WillTopic.Should().BeEmpty();
        connection.WillMessage.Should().BeEmpty();
        connection.WillRetain.Should().BeFalse();
        changedCount.Should().Be(1);
    }

    [Theory]
    [InlineData(1883, 9001)]
    [InlineData(8883, 8883)]
    public async Task Should_keep_the_port_editable_and_switch_only_the_default_port_to_websocket(int port, int expectedPort)
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection()!;
        connection.Protocol = MqttConnectionType.TCP;
        connection.Port = port;

        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupBlazorUiComponents();

        var component = ctx.Render<MqttSettings>(parameters => parameters.Add(c => c.Connection, connection));
        var comboBox = component.GetSettingsFieldComboBox<ComboBoxItem<MqttConnectionType, string>, MqttConnectionType>(MqttLocalization.MqttSettings.TransportProtocol);

        // Act
        await component.InvokeAsync(() => comboBox.Instance.ValueChanged.InvokeAsync(MqttConnectionType.WebSocket));

        // Assert
        connection.Port.Should().Be(expectedPort);
        component.GetSettingsFieldSpinEdit<int, int, int>(TechnicalTerms.Port).Instance.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Should_show_a_tcp_with_tls_connection_as_tcp_with_tls_1_2_without_notifying()
    {
        // Arrange
        var connection = ConnectionFactory.MqttConnection.GetMqttConnection()!;
#pragma warning disable CS0618 // Connections stored before SslProtocol existed still carry TCPWithTLS.
        connection.Protocol = MqttConnectionType.TCPWithTLS;
#pragma warning restore CS0618
        connection.SslProtocol = null;
        var changedCount = 0;

        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupBlazorUiComponents();

        // Act
        var component = ctx.Render<MqttSettings>(parameters => parameters
            .Add(c => c.Connection, connection)
            .Add(c => c.Changed, () => changedCount++));

        // Assert
        component.AssertSettingsFieldComboBoxWithItem(MqttLocalization.MqttSettings.TransportProtocol, MqttConnectionType.TCP);
        component.AssertSettingsFieldComboBoxWithItem(MqttLocalization.MqttSettings.SslProtocol, (MqttSslProtocol?)MqttSslProtocol.Tls12);
        changedCount.Should().Be(0);
    }
}
