using AwesomeAssertions;
using Blazor.Shared.Instance.ControlPanels.Repositories;
using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories;

public class ArtifactRepositoryControlPanelTests
{
    private readonly IArtifactRepositoryClientService _repositoryClientService = Substitute.For<IArtifactRepositoryClientService>();

    private BunitContext SetupContext()
    {
        var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupControlPanelServices();
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddSingleton(_repositoryClientService);

        ctx.Services.AddControlPanel<SharedClientModule, ArtifactRepositoryControlPanel, ArtifactRepositoryControlPanelState>()
               .WithAutoDiscovery<ArtifactRepositoryControlPanelDescriptor>()
               .WithSaveHandler<ArtifactRepositoryControlPanelSaveHandler>()
               .WithResetHandler<ArtifactRepositoryControlPanelResetHandler>();

        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var source = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://update.ifm.com" };
        var model = new ArtifactRepositoryModel(source);
        var state = new ArtifactRepositoryControlPanelState() { RepositoryId = model.Id, Repository = model };

        using var ctx = SetupContext();

        // Act
        var component = ctx.Render<ArtifactRepositoryControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        component.Should().NotBeNull();
    }
}
