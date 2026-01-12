using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Components;

public class DatabaseSettingsTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        var connection = ConnectionFactory.DatabaseConnection.GetDatabaseConnection();
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var component = ctx.RenderComponent<DatabaseSettings>(parameters =>
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
        var connection = ConnectionFactory.DatabaseConnection.GetDatabaseConnection();
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var component = ctx.RenderComponent<DatabaseSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        Assert.NotNull(connection);

        component.AssertSettingsFieldComboBoxWithItem(TechnicalTerms.DatabaseType, connection.DatabaseType);
        component.AssertSettingsFieldTextBox(TechnicalTerms.ConnectionString, connection.ConnectionString);
    }
}
