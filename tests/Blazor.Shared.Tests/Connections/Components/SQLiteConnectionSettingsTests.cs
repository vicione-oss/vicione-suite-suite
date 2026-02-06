using Blazor.Shared.Connections.Components;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Sdk.Connections.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Components;

public class SQLiteConnectionSettingsTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        var connection = ConnectionFactory.SQLiteConnection.GetSQLiteConnection();
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var component = ctx.Render<SQLiteConnectionSettings>(parameters =>
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
        var connection = ConnectionFactory.SQLiteConnection.GetSQLiteConnection();
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var component = ctx.Render<SQLiteConnectionSettings>(parameters =>
        {
            parameters.Add(c => c.Connection, connection);
        });

        // Assert
        Assert.NotNull(connection);

        component.AssertSettingsFieldTextBox(TechnicalTerms.ConnectionString, connection.ConnectionString);
    }
}
