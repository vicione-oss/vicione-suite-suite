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

public sealed class ArtifactRepositoryControlPanelResetHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _mediator)
            .AddScoped<IControlPanelResetHandler<ArtifactRepositoryControlPanelState>, ArtifactRepositoryControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_create_new_repository_when_repository_id_is_not_set()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Repository.Should().NotBeNull();
        state.Repository!.Endpoint.Should().Be("https://");
        await _mediator.DidNotReceive().Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(
            Arg.Any<GetArtifactRepositories>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_load_repository_from_mediator_when_repository_id_is_set()
    {
        // Arrange
        var repository = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo.example.com", Name = "Test Repo" };
        _mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(
                Arg.Any<GetArtifactRepositories>(), Arg.Any<CancellationToken>())
            .Returns(new GetArtifactRepositoriesResponse([repository]));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState { RepositoryId = repository.Id };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Repository.Should().NotBeNull();
        state.Repository!.Id.Should().Be(repository.Id);
        state.Repository.Endpoint.Should().Be(repository.Endpoint);
    }

    [Fact]
    public async Task Should_set_repository_to_null_when_repository_not_found_in_mediator_response()
    {
        // Arrange
        _mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(
                Arg.Any<GetArtifactRepositories>(), Arg.Any<CancellationToken>())
            .Returns(new GetArtifactRepositoriesResponse([]));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState { RepositoryId = Guid.NewGuid() };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Repository.Should().BeNull();
    }

    [Fact]
    public async Task Should_end_loading_after_reset()
    {
        // Arrange
        _mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(
                Arg.Any<GetArtifactRepositories>(), Arg.Any<CancellationToken>())
            .Returns(new GetArtifactRepositoriesResponse([]));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState { RepositoryId = Guid.NewGuid() };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
    }
}
