using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class GetArtifactRepositoriesConsumerTests
{
    private readonly IArtifactRepositoryStore _repositoryStore = Substitute.For<IArtifactRepositoryStore>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetArtifactRepositoriesConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetArtifactRepositoriesConsumer>();
            cfg.AddSingleton(_repositoryStore);
        };

    [Fact]
    public async Task Request_should_be_consumed()
    {
        // Arrange
        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).Returns([]);

        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetArtifactRepositories();

        // Act
        var response = await tester.TestRequest<GetArtifactRepositoriesResponse, GetArtifactRepositories>(request);

        // Assert
        response.Should().NotBeNull();
        response.Repositories.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_should_return_repository_sources()
    {
        // Arrange
        List<ArtifactRepository> repos =
        [
            new() {Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" },
            new() {Id = Guid.NewGuid(), Endpoint = "https://repo2.example.com", Name = "Repo 2", UserName = "user", Password = "pass" }
        ];

        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).Returns(repos);

        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetArtifactRepositories();

        // Act
        var response = await tester.TestRequest<GetArtifactRepositoriesResponse, GetArtifactRepositories>(request);

        // Assert
        response.Should().NotBeNull();
        response.Repositories.Should().BeEquivalentTo(repos);
    }

    [Fact]
    public async Task Consume_should_publish_response_on_failure()
    {
        // Arrange
        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).ThrowsAsync(new ArgumentException("test"));

        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetArtifactRepositories();

        // Act
        var response = await tester.TestRequest<GetArtifactRepositoriesResponse, GetArtifactRepositories>(request);

        // Assert
        response.Should().BeEquivalentTo(new GetArtifactRepositoriesResponse([], new(0, "test")));
    }
}
