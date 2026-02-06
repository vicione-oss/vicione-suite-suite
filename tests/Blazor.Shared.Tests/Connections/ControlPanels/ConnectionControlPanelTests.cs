using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.ControlPanels;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Factories;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.ControlPanels;

public class ConnectionControlPanelTests
{
    private static BunitContext SetupTestContext(Connection? connection = null)
    {
        var ctx = new BunitContext();

        var suiteConnectionService = Substitute.For<ISuiteConnectionService>();

        ctx.Services.AddScoped(_ => Substitute.For<IActiveControlPanelDescriptorProvider>())
            .AddScoped(_ => suiteConnectionService)
            .AddSingleton(_ => Substitute.For<IConnectionTypeRegistry>().Setup())
            .AddSingleton(_ => Substitute.For<IConnectionTypeUiRegistry>())
            .AddCheckBox()
            .AddControlPanelInfrastructure()
            .AddConnectionControlPanel();

        ctx.SetupSuiteServicesWithBlazorDx(setup =>
        {
            setup.ClientMediator.Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
                .Returns(new GetConnectionsResponse(connection is not null ? [connection] : []));

            setup.ClientMediator.Request<GetTags, GetTagsResponse>(Arg.Any<GetTags>(), Arg.Any<CancellationToken>())
                .Returns(new GetTagsResponse([]));

            suiteConnectionService.GetConnection(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(connection);

            suiteConnectionService.GetTags(Arg.Any<CancellationToken>()).Returns([new Tag() { Id = Guid.NewGuid(), Text = "Tag A" }]);

            suiteConnectionService.UpsertConnection(Arg.Any<Connection>(), Arg.Any<CancellationToken>())
                .Returns(new SuiteConnectionServiceSuccessResult());
        });

        return ctx;
    }

    public class OnInitializedAsync : ConnectionControlPanelTests
    {
        [Fact]
        public async Task Should_init_empty_connection_if_state_connection_id_is_not_set()
        {
            // Arrange
            await using var ctx = SetupTestContext();

            var registry = ctx.Services.GetRequiredService<IConnectionTypeRegistry>();
            var connection = EditConnectionModelFactory.CreateNew(registry);

            var state = new ConnectionControlPanelState();

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<ConnectionControlPanelState>>();
            await resetHandler.Reset(state, CancellationToken.None);

            // Act
            var component = ctx.Render<ConnectionControlPanel>(parameters =>
            {
                parameters.Add(c => c.State, state);
            });

            // Assert
            component.AssertSettingsFieldTextBox(CommonVocabulary.Name, connection.Name);
            component.AssertSettingsFieldTextBox(CommonVocabulary.Description, connection.Description);
        }

        [Fact]
        public async Task Should_bind_connection_from_state_id()
        {
            // Arrange
            var connection = ConnectionFactory.SQLiteConnection;

            await using var ctx = SetupTestContext(connection);

            var state = new ConnectionControlPanelState { ConnectionId = connection.Id };

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<ConnectionControlPanelState>>();
            await resetHandler.Reset(state, CancellationToken.None);

            // Act
            var component = ctx.Render<ConnectionControlPanel>(parameters =>
            {
                parameters.Add(c => c.State, state);
            });

            // Assert
            component.AssertSettingsFieldTextBox(CommonVocabulary.Name, connection.Name);
            component.AssertSettingsFieldTextBox(CommonVocabulary.Description, connection.Description);
        }
    }
}
