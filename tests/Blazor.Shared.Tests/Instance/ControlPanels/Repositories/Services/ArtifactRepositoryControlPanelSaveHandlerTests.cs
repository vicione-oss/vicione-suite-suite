using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories.Services;

public sealed class ArtifactRepositoryControlPanelSaveHandlerTests
{
    private readonly IArtifactRepositoryClientService _repositoryService = Substitute.For<IArtifactRepositoryClientService>();

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _repositoryService)
            .AddScoped<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>, ArtifactRepositoryControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_throw_when_repository_is_null()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var action = async () => await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_return_error_when_endpoint_is_empty()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState
        {
            Repository = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://placeholder" })
        };
        state.Repository.Endpoint = string.Empty;
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_when_endpoint_is_not_https()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState
        {
            Repository = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "http://repo.example.com" })
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_when_endpoint_is_not_a_valid_url()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState
        {
            Repository = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://not a valid url" })
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_when_token_endpoint_is_not_an_absolute_uri()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState
        {
            Repository = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo.example.com" })
        };
        state.Repository.TokenEndpoint = "relative/path";
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_create_repository_and_return_success_when_repository_id_is_not_set()
    {
        // Arrange
        _repositoryService.CreateRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState
        {
            Repository = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo.example.com" })
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _repositoryService.Received(1).CreateRepository(state.Repository, Arg.Any<CancellationToken>());
        await _repositoryService.DidNotReceive().UpdateRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_update_repository_and_return_success_when_repository_id_is_set()
    {
        // Arrange
        _repositoryService.UpdateRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState
        {
            RepositoryId = Guid.NewGuid(),
            Repository = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo.example.com" })
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _repositoryService.Received(1).UpdateRepository(state.Repository, Arg.Any<CancellationToken>());
        await _repositoryService.DidNotReceive().CreateRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_return_error_when_service_returns_error()
    {
        // Arrange
        _repositoryService.CreateRepository(Arg.Any<ArtifactRepositoryModel>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryServiceErrorResult("service error", 500));

        await using var serviceProvider = SetupServiceProvider();

        var state = new ArtifactRepositoryControlPanelState
        {
            Repository = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo.example.com" })
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        var errorResult = result.Should().BeOfType<SaveErrorResult>().Subject;
        errorResult.Message.Should().Be("service error");
    }
}
