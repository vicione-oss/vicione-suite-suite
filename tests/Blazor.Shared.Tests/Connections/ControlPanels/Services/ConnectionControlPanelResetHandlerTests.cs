using Blazor.Shared.Connections.ControlPanels.Connections.Services;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Tests.Connections.Extensions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.ControlPanels.Services;

public sealed class ConnectionControlPanelResetHandlerTests
{
    [Fact]
    public async Task Should_throw_if_connection_from_state_can_not_be_found()
    {
        // Arrange
        var connection = ConnectionFactory.SQLiteConnection;

        var services = new ServiceCollection()
            .AddScoped(_ => Substitute.For<ISuiteConnectionService>())
            .AddScoped(_ => Substitute.For<IConnectionTypeRegistry>().Setup())
            .AddScoped(_ => Substitute.For<IConnectionTypeUiRegistry>())
            .AddScoped<IControlPanelResetHandler<ConnectionControlPanelState>, ConnectionControlPanelResetHandler>();

        await using var serviceProvider = services.BuildServiceProvider();

        var state = new ConnectionControlPanelState { ConnectionId = connection.Id };

        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ConnectionControlPanelState>>();

        // Act
        var action = async () => await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }
}
