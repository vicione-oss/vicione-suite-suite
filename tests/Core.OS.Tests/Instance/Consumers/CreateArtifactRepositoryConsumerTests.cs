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

public class CreateArtifactRepositoryConsumerTests
{
    private readonly IArtifactRepositoryStore _repositoryStore = Substitute.For<IArtifactRepositoryStore>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public CreateArtifactRepositoryConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<CreateArtifactRepositoryConsumer>();
            cfg.AddSingleton(_repositoryStore);
            cfg.AddSingleton(Substitute.For<ILogger<CreateArtifactRepositoryConsumer>>());
        };

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };
        _repositoryStore.CreateOrUpdate(repo, Arg.Any<CancellationToken>())
            .Returns(CrudAction.Created);

        await using var tester = new MassTransitTester(_configureServices);
        var command = new CreateArtifactRepository(repo);

        // Act + Assert
        await tester.TestCommand<CreateArtifactRepository, CreateArtifactRepositoryConsumer>(command);
    }

    [Fact]
    public async Task Consume_should_publish_created_event_on_success()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };
        _repositoryStore.CreateOrUpdate(repo, Arg.Any<CancellationToken>())
            .Returns(CrudAction.Created);

        await using var tester = new MassTransitTester(_configureServices);
        var command = new CreateArtifactRepository(repo);

        // Act
        var response = await tester.TestCommand<CreateArtifactRepository, CreateArtifactRepositoryConsumer, ArtifactRepositoryChanged>(command);

        // Assert
        response.Should().NotBeNull();
        response.Repository.Should().BeEquivalentTo(repo);
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Consume_should_publish_created_event_with_error_on_failure()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };
        _repositoryStore.CreateOrUpdate(Arg.Any<ArtifactRepository>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("create failed"));

        await using var tester = new MassTransitTester(_configureServices);
        var command = new CreateArtifactRepository(repo);

        // Act
        var response = await tester.TestCommand<CreateArtifactRepository, CreateArtifactRepositoryConsumer, ArtifactRepositoryChanged>(command);

        // Assert
        response.Should().NotBeNull();
        response.Repository.Should().BeEquivalentTo(repo);
        response.Error.Should().BeEquivalentTo(new ErrorInfo(100, "create failed"));
    }
}
