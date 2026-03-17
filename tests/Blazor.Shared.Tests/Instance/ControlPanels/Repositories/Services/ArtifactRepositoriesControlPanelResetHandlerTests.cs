using AwesomeAssertions;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Xunit;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories.Services;

public sealed class ArtifactRepositoriesControlPanelResetHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _mediator)
            .AddScoped<IControlPanelResetHandler<ArtifactRepositoriesControlPanelState>, ArtifactRepositoriesControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_populate_repositories_from_mediator_response()
    {
        // Arrange
        var repositories = new List<ArtifactRepository>
        {
            new() { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" },
            new() { Id = Guid.NewGuid(), Endpoint = "https://repo2.example.com", Name = "Repo 2" }
        };

        _mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(
                Arg.Any<GetArtifactRepositories>(), Arg.Any<CancellationToken>())
            .Returns(new GetArtifactRepositoriesResponse(repositories));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoriesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ArtifactRepositoriesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Repositories.Should().HaveCount(2);
        state.Repositories.Select(r => r.Id).Should().BeEquivalentTo(repositories.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_leave_repositories_empty_when_mediator_returns_no_repositories()
    {
        // Arrange
        _mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(
                Arg.Any<GetArtifactRepositories>(), Arg.Any<CancellationToken>())
            .Returns(new GetArtifactRepositoriesResponse([]));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoriesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ArtifactRepositoriesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Repositories.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_end_loading_after_reset()
    {
        // Arrange
        _mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(
                Arg.Any<GetArtifactRepositories>(), Arg.Any<CancellationToken>())
            .Returns(new GetArtifactRepositoriesResponse([]));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoriesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ArtifactRepositoriesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
    }
}
