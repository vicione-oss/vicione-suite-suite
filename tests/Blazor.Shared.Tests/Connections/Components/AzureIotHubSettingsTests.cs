using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Components;

public class AzureIotHubSettingsTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        var connection = ConnectionFactory.AzureIotConnection.GetAzureIotHubConnection();
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();
        ctx.SetupBlazorUiComponents();

        // Act
        var component = ctx.RenderComponent<AzureIotHubSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public void FieldsBindToModel()
    {
        // Arrange
        var connection = ConnectionFactory.AzureIotConnection.GetAzureIotHubConnection();
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();
        ctx.SetupBlazorUiComponents();

        // Act
        var component = ctx.RenderComponent<AzureIotHubSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        Assert.NotNull(connection);

        component.AssertSettingsFieldTextBox(TechnicalTerms.Hostname, connection.Hostname);
        component.AssertSettingsFieldTextBox($"{TechnicalTerms.SharedAccessSignatureKey} {CommonVocabulary.Name}", connection.SharedAccessSignatureKeyName);
        component.AssertSettingsFieldTextBox(TechnicalTerms.SharedAccessSignatureKey, connection.SharedAccessSignatureKey);
    }
}
