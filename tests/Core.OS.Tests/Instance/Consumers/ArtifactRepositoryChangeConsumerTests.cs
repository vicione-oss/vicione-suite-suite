using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Modules;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class ArtifactRepositoryChangeConsumerTests
{
    private readonly IArtifactRepositoryStore _repositoryStore = Substitute.For<IArtifactRepositoryStore>();
    private readonly IArtifactRepositoryOptionsCache _optionsCache = Substitute.For<IArtifactRepositoryOptionsCache>();
    private readonly IModuleArtifactCache _artifactsCache = Substitute.For<IModuleArtifactCache>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public ArtifactRepositoryChangeConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<ArtifactRepositoryChangeConsumer>();
            cfg.AddSingleton(_repositoryStore);
            cfg.AddSingleton(_optionsCache);
            cfg.AddSingleton(_artifactsCache);
            cfg.AddSingleton(Substitute.For<ILogger<ArtifactRepositoryChangeConsumer>>());
        };

    [Fact]
    public async Task ArtifactRepositoryChanged_event_should_be_consumed()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };
        await using var tester = new MassTransitTester(_configureServices);
        var @event = new ArtifactRepositoryChanged(repo, CrudAction.Deleted);

        // Act + Assert
        await tester.TestEvent<ArtifactRepositoryChanged, ArtifactRepositoryChangeConsumer>(@event);
    }

    [Fact]
    public async Task ArtifactRepositoryChanged_should_invalidate_caches_on_success()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };
        await using var tester = new MassTransitTester(_configureServices);
        var @event = new ArtifactRepositoryChanged(repo, CrudAction.Updated);

        // Act
        await tester.TestEvent<ArtifactRepositoryChanged, ArtifactRepositoryChangeConsumer>(@event);

        // Assert
        _artifactsCache.Received(1).Invalidate();
        await _optionsCache.Received(1).ReloadOptions(_repositoryStore, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArtifactRepositoryChanged_should_not_invalidate_caches_when_event_has_error()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };
        await using var tester = new MassTransitTester(_configureServices);
        var @event = new ArtifactRepositoryChanged(repo, CrudAction.Created, new ErrorInfo(100, "create failed"));

        // Act
        await tester.TestEvent<ArtifactRepositoryChanged, ArtifactRepositoryChangeConsumer>(@event);

        // Assert
        _artifactsCache.Received(0).Invalidate();
        await _optionsCache.DidNotReceive().ReloadOptions(Arg.Any<IArtifactRepositoryStore>(), Arg.Any<CancellationToken>());
    }
}
