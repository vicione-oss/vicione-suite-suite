using AwesomeAssertions;
using Blazor.Shared.Instance.ControlPanels.Repositories;
using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories;

public class ArtifactRepositoriesControlPanelTests
{
    private readonly IArtifactRepositoryClientService _repositoryClientService = Substitute.For<IArtifactRepositoryClientService>();

    private BunitContext SetupContext()
    {
        var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupControlPanelServices();
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddCheckBox();
        ctx.Services.AddSingleton(_repositoryClientService);
        ctx.JSInterop.ConfigureQuickGridJSInterop();

        ctx.Services.AddControlPanel<SharedClientModule, ArtifactRepositoriesControlPanel, ArtifactRepositoriesControlPanelState>()
                .WithAutoDiscovery<ArtifactRepositoriesControlPanelDescriptor>()
                .WithSaveHandler<ArtifactRepositoriesControlPanelSaveHandler>()
                .WithResetHandler<ArtifactRepositoriesControlPanelResetHandler>();

        ctx.Services.AddGridItemSelection<Guid>(typeof(ArtifactRepositoriesControlPanelServiceKey));
        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var source = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://update.ifm.com" };
        var model = new ArtifactRepositoryModel(source);
        var state = new ArtifactRepositoriesControlPanelState() { Repositories = [model] };

        using var ctx = SetupContext();

        // Act
        var component = ctx.Render<ArtifactRepositoriesControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        component.Should().NotBeNull();
    }
}
