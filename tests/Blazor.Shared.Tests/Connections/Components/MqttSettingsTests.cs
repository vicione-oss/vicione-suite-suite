using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Components;

public class MqttSettingsTests
{
    [Fact]
    public void ComponentGetsRendered()
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
    public void FieldsBindToModel()
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

        component.AssertSettingsFieldCheckBox(Shared.Connections.Components.Localization.MqttSettings.CleanSession, connection.CleanSession);
        component.AssertSettingsFieldSpinEditInt(TechnicalTerms.Port, connection.Port);
        component.AssertSettingsFieldComboBoxWithItem($"{TechnicalAcronyms.Mqtt} {TechnicalTerms.Protocol}", connection.Protocol);

        component.AssertSettingsFieldTextBox(CommonVocabulary.Address, connection.Address);
        component.AssertSettingsFieldTextBox(TechnicalTerms.Username, connection.Username);
        component.AssertSettingsFieldTextBox(CommonVocabulary.Password, connection.Password);
        component.AssertSettingsFieldTextBox(Shared.Connections.Components.Localization.MqttSettings.ClientId, connection.ClientId);
        component.AssertSettingsFieldTextBox(Shared.Connections.Components.Localization.MqttSettings.WillTopic, connection.WillTopic);
        component.AssertSettingsFieldTextBox(Shared.Connections.Components.Localization.MqttSettings.WillMessage, connection.WillMessage);
        component.AssertSettingsFieldCheckBox(Shared.Connections.Components.Localization.MqttSettings.WillRetain, connection.WillRetain);
    }
}
