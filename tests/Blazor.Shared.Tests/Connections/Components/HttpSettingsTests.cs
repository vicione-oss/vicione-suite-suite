using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests.Connections.Components;

public class HttpSettingsTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        var connection = ConnectionFactory.HttpConnection.GetHttpConnection();
        await using var ctx = new BunitContext();
        ctx.SetupBlazorUiComponents();

        // Act
        var component = ctx.Render<HttpSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public void Should_bind_fields_to_model()
    {
        // Arrange
        var connection = ConnectionFactory.HttpConnection.GetHttpConnection();
        using var ctx = new BunitContext();
        ctx.SetupBlazorUiComponents();

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
