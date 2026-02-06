using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Components;

public class HttpSettingsTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        var connection = ConnectionFactory.HttpConnection.GetHttpConnection();
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var component = ctx.Render<HttpSettings>(parameters =>
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
        var connection = ConnectionFactory.HttpConnection.GetHttpConnection();
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var component = ctx.Render<HttpSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        Assert.NotNull(connection);

        component.AssertSettingsFieldTextBox(CommonVocabulary.Address, connection.BaseAddress);
        component.AssertSettingsFieldTextBox(TechnicalTerms.ApiKey, connection.ApiKey);
    }
}
