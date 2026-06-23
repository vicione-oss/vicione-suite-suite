using AwesomeAssertions;
using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.ControlPanels.Connections.Services;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Tests.Connections.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using Xunit;

namespace Blazor.Shared.Tests.Connections.ControlPanels.Services;

public sealed class ConnectionControlPanelSaveHandlerTests
{
    private readonly ISuiteConnectionService _connectionService = Substitute.For<ISuiteConnectionService>();
    private readonly IConnectionTypeUiRegistry _connectionTypeUiRegistry = Substitute.For<IConnectionTypeUiRegistry>();
    private readonly IConnectionTypeRegistry _connectionTypeRegistry = Substitute.For<IConnectionTypeRegistry>().Setup();

    public ConnectionControlPanelSaveHandlerTests()
    {
        var itemValidator = Substitute.For<IItemValidator>();
        itemValidator.Validate(Arg.Any<IConnection>()).Returns(new Dictionary<string, List<string>>());

        _connectionTypeUiRegistry.TryGetItemValidator(Arg.Any<string>(), out Arg.Any<IItemValidator?>())
            .Returns(c =>
            {
                c[1] = itemValidator;
                return true;
            });
    }

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_ => _connectionService)
            .AddSingleton(_ => _connectionTypeUiRegistry)
            .AddSingleton(_ => _connectionTypeRegistry)
            .AddScoped<IControlPanelSaveHandler<ConnectionControlPanelState>, ConnectionControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_throw_if_edit_connection_model_is_null()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ConnectionControlPanelState>>();

        var state = new ConnectionControlPanelState();

        // Act
        var action = async () => await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_return_save_error_if_name_is_empty()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ConnectionControlPanelState>>();

        var connection = new Connection() { Name = "", Type = ConnectionType.SQLite };
        var model = new EditConnectionModel(connection, _connectionTypeRegistry);

        var state = new ConnectionControlPanelState { EditConnectionModel = model };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_save_error_if_name_exceeds_maximum_length()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ConnectionControlPanelState>>();

        var connection = new Connection() { Name = new string('A', 81), Type = ConnectionType.SQLite };
        var model = new EditConnectionModel(connection, _connectionTypeRegistry);

        var state = new ConnectionControlPanelState { EditConnectionModel = model };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_save_error_if_name_is_already_used()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ConnectionControlPanelState>>();

        var connection = new Connection() { Id = Guid.NewGuid(), Name = "Existing", Type = ConnectionType.SQLite };
        var model = new EditConnectionModel(connection, _connectionTypeRegistry);

        _connectionService.Connections.Returns([new Connection() { Id = Guid.NewGuid(), Name = "Existing" }]);

        var state = new ConnectionControlPanelState { EditConnectionModel = model };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_save_success_when_upsert_succeeds()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ConnectionControlPanelState>>();

        var connection = new Connection() { Id = Guid.NewGuid(), Name = "Valid", Type = ConnectionType.SQLite };
        var model = new EditConnectionModel(connection, _connectionTypeRegistry);

        _connectionService.Connections.Returns([]);
        _connectionService.UpsertConnection(Arg.Any<Connection>(), Arg.Any<CancellationToken>())
            .Returns(new SuiteConnectionServiceSuccessResult());

        var state = new ConnectionControlPanelState
        {
            EditConnectionModel = model,
            EditModelTagTexts = [],
            AvailableTags = []
        };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_return_save_error_when_upsert_fails()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ConnectionControlPanelState>>();

        var connection = new Connection() { Id = Guid.NewGuid(), Name = "Valid", Type = ConnectionType.SQLite };
        var model = new EditConnectionModel(connection, _connectionTypeRegistry);

        _connectionService.Connections.Returns([]);
        _connectionService.UpsertConnection(Arg.Any<Connection>(), Arg.Any<CancellationToken>())
            .Returns(new SuiteConnectionServiceErrorResult("Something went wrong"));

        var state = new ConnectionControlPanelState
        {
            EditConnectionModel = model,
            EditModelTagTexts = [],
            AvailableTags = []
        };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }
}
