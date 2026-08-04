using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories.Services;

public sealed class ArtifactRepositoriesControlPanelSaveHandlerTests
{
    private readonly IArtifactRepositoryClientService _repositoryService = Substitute.For<IArtifactRepositoryClientService>();

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _repositoryService)
            .AddScoped<IControlPanelSaveHandler<ArtifactRepositoriesControlPanelState>, ArtifactRepositoriesControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_return_success_when_no_repositories_are_marked_for_deletion()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoriesControlPanelState();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoriesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _repositoryService.DidNotReceive().DeleteRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_delete_marked_repositories_and_return_success()
    {
        // Arrange
        var repo = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com" });
        _repositoryService.DeleteRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoriesControlPanelState();
        state.RepositoriesMarkedForDeletion.Add(repo);
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoriesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _repositoryService.Received(1).DeleteRepository(repo, Arg.Any<CancellationToken>());
        state.RepositoriesMarkedForDeletion.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_return_error_when_delete_fails()
    {
        // Arrange
        var repo = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com" });
        _repositoryService.DeleteRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryServiceErrorResult("delete failed", 100));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoriesControlPanelState();
        state.RepositoriesMarkedForDeletion.Add(repo);
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoriesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var errorResult = result.Should().BeOfType<SaveErrorResult>().Subject;
        errorResult.Message.Should().Be("delete failed");
        errorResult.ErrorCode.Should().Be(100);
    }
}
