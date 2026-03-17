using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class DeleteArtifactRepositoryConsumerTests
{
    private readonly IArtifactRepositoryStore _repositoryStore = Substitute.For<IArtifactRepositoryStore>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public DeleteArtifactRepositoryConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DeleteArtifactRepositoryConsumer>();
            cfg.AddSingleton(_repositoryStore);
            cfg.AddSingleton(Substitute.For<ILogger<DeleteArtifactRepositoryConsumer>>());
        };

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        _repositoryStore.Delete(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<ArtifactRepository>>([]));

        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteArtifactRepository(repoId);

        // Act + Assert
        await tester.TestCommand<DeleteArtifactRepository, DeleteArtifactRepositoryConsumer>(command);
    }

    [Fact]
    public async Task Consume_should_publish_deleted_event_for_deleted_repository()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };
        _repositoryStore.Delete(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<ArtifactRepository>>([repo]));

        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteArtifactRepository(repo.Id);

        // Act
        var response = await tester.TestCommand<DeleteArtifactRepository, DeleteArtifactRepositoryConsumer, ArtifactRepositoryChanged>(command);

        // Assert
        response.Should().NotBeNull();
        response.Repository.Id.Should().Be(repo.Id);
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Consume_should_publish_deleted_event_with_error_on_failure()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        _repositoryStore.Delete(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("delete failed"));

        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteArtifactRepository(repoId);

        // Act
        var response = await tester.TestCommand<DeleteArtifactRepository, DeleteArtifactRepositoryConsumer, ArtifactRepositoryChanged>(command);

        // Assert
        response.Should().NotBeNull();
        response.Repository.Id.Should().Be(repoId);
        response.Error.Should().BeEquivalentTo(new ErrorInfo(100, "delete failed"));
    }
}
