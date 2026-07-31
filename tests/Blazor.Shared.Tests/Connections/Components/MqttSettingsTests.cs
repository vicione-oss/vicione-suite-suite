using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;
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
        ctx.SetupSuiteServicesWithBlazorDx();
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
        ctx.SetupSuiteServicesWithBlazorDx();
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
        ctx.SetupSuiteServicesWithBlazorDx();
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
        ctx.SetupSuiteServicesWithBlazorDx();
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
}
