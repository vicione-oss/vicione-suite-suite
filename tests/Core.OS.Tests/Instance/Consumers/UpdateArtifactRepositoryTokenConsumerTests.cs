using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Contracts;
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

public class UpdateArtifactRepositoryTokenConsumerTests
{
    private readonly IArtifactRepositoryStore _repositoryStore = Substitute.For<IArtifactRepositoryStore>();
    private readonly IArtifactRepositoryTokenService _tokenService = Substitute.For<IArtifactRepositoryTokenService>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public UpdateArtifactRepositoryTokenConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<UpdateArtifactRepositoryTokenConsumer>();
            cfg.AddSingleton(_repositoryStore);
            cfg.AddSingleton(_tokenService);
            cfg.AddSingleton(Substitute.For<ILogger<UpdateArtifactRepositoryTokenConsumer>>());
        };

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1", TokenEndpoint = "https://token.example.com" };
        _tokenService.GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryTokenResponse { Token = "mytoken", ValidUntil = DateTime.UtcNow.AddHours(1) });
        _repositoryStore.CreateOrUpdate(Arg.Any<ArtifactRepository>(), Arg.Any<CancellationToken>()).Returns(CrudAction.Updated);

        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateArtifactRepositoryToken(repo);

        // Act + Assert
        await tester.TestCommand<UpdateArtifactRepositoryToken, UpdateArtifactRepositoryTokenConsumer>(command);
    }

    [Fact]
    public async Task Consume_should_publish_ArtifactRepositoryUpdated_with_updated_token_on_success()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1", TokenEndpoint = "https://token.example.com" };
        var tokenResponse = new ArtifactRepositoryTokenResponse
        {
            Token = "mytoken",
            ValidUntil = new DateTime(2026, 11, 23, 13, 0, 0, DateTimeKind.Utc)
        };
        _tokenService.GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(tokenResponse);
        _repositoryStore.CreateOrUpdate(Arg.Any<ArtifactRepository>(), Arg.Any<CancellationToken>()).Returns(CrudAction.Updated);

        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateArtifactRepositoryToken(repo);

        // Act
        var response = await tester.TestCommand<UpdateArtifactRepositoryToken, UpdateArtifactRepositoryTokenConsumer, ArtifactRepositoryChanged>(command);

        // Assert
        response.Should().NotBeNull();
        response.Repository.Id.Should().Be(repo.Id);
        response.Repository.Password.Should().Be(tokenResponse.Token);
        response.Repository.TokenValidUntil.Should().Be((DateTimeOffset)tokenResponse.ValidUntil);
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Consume_should_publish_ArtifactRepositoryUpdated_with_error_when_token_endpoint_is_missing()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1" };

        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateArtifactRepositoryToken(repo);

        // Act
        var response = await tester.TestCommand<UpdateArtifactRepositoryToken, UpdateArtifactRepositoryTokenConsumer, ArtifactRepositoryChanged>(command);

        // Assert
        response.Should().NotBeNull();
        response.Repository.Id.Should().Be(repo.Id);
        response.Error.Should().NotBeNull();
        response.Error!.ErrorCode.Should().Be(100);
    }

    [Fact]
    public async Task Consume_should_publish_ArtifactRepositoryUpdated_with_error_when_token_service_throws()
    {
        // Arrange
        var repo = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo1.example.com", Name = "Repo 1", TokenEndpoint = "https://token.example.com" };
        _tokenService.GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection failed"));

        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateArtifactRepositoryToken(repo);

        // Act
        var response = await tester.TestCommand<UpdateArtifactRepositoryToken, UpdateArtifactRepositoryTokenConsumer, ArtifactRepositoryChanged>(command);

        // Assert
        response.Should().NotBeNull();
        response.Repository.Id.Should().Be(repo.Id);
        response.Error.Should().BeEquivalentTo(new ErrorInfo(100, "connection failed"));
    }
}
