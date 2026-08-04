using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.Logging;

namespace Core.OS.Tests.Instance.Extensions;

public sealed class IArtifactRepositoryStoreExtensionsTests
{
    private readonly IArtifactRepositoryStore _repositoryStore = Substitute.For<IArtifactRepositoryStore>();
    private readonly IArtifactRepositoryTokenService _tokenService = Substitute.For<IArtifactRepositoryTokenService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private static ArtifactRepository CreateRepository(
        bool enabled = true,
        string? tokenEndpoint = "https://token.example.com",
        DateTimeOffset? tokenValidUntil = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Endpoint = "https://repo1.example.com",
            Name = "Repo 1",
            Enabled = enabled,
            TokenEndpoint = tokenEndpoint,
            TokenValidUntil = tokenValidUntil,
        };

    [Fact]
    public async Task Should_update_token_for_enabled_repository_with_no_prior_expiration()
    {
        // Arrange
        var repo = CreateRepository(tokenValidUntil: null);
        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).Returns([repo]);
        _tokenService.GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryTokenResponse { Token = "mytoken", ValidUntil = DateTime.UtcNow.AddHours(1) });

        // Act
        await _repositoryStore.UpdateRepositoryTokens(_tokenService, _logger, TestContext.Current.CancellationToken);

        // Assert
        await _tokenService.Received(1).GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        await _repositoryStore.Received(1).Store(Arg.Is<List<ArtifactRepository>>(l => l!.Single().Password == "mytoken"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_update_token_for_repository_with_expired_token()
    {
        // Arrange
        var repo = CreateRepository(tokenValidUntil: DateTimeOffset.UtcNow.AddMinutes(-5));
        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).Returns([repo]);
        _tokenService.GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryTokenResponse { Token = "mytoken", ValidUntil = DateTime.UtcNow.AddHours(1) });

        // Act
        await _repositoryStore.UpdateRepositoryTokens(_tokenService, _logger, TestContext.Current.CancellationToken);

        // Assert
        await _tokenService.Received(1).GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_update_token_for_repository_with_still_valid_token()
    {
        // Arrange
        var repo = CreateRepository(tokenValidUntil: DateTimeOffset.UtcNow.AddDays(1));
        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).Returns([repo]);

        // Act
        await _repositoryStore.UpdateRepositoryTokens(_tokenService, _logger, TestContext.Current.CancellationToken);

        // Assert
        await _tokenService.DidNotReceive().GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        await _repositoryStore.DidNotReceive().Store(Arg.Any<List<ArtifactRepository>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_update_token_for_disabled_repository()
    {
        // Arrange
        var repo = CreateRepository(enabled: false, tokenValidUntil: null);
        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).Returns([repo]);

        // Act
        await _repositoryStore.UpdateRepositoryTokens(_tokenService, _logger, TestContext.Current.CancellationToken);

        // Assert
        await _tokenService.DidNotReceive().GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        await _repositoryStore.DidNotReceive().Store(Arg.Any<List<ArtifactRepository>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Should_not_update_token_for_repository_without_token_endpoint(string? tokenEndpoint)
    {
        // Arrange
        var repo = CreateRepository(tokenEndpoint: tokenEndpoint, tokenValidUntil: null);
        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>()).Returns([repo]);

        // Act
        await _repositoryStore.UpdateRepositoryTokens(_tokenService, _logger, TestContext.Current.CancellationToken);

        // Assert
        await _tokenService.DidNotReceive().GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        await _repositoryStore.DidNotReceive().Store(Arg.Any<List<ArtifactRepository>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_only_update_eligible_repositories_among_mixed_set()
    {
        // Arrange
        var eligible = CreateRepository(tokenValidUntil: null);
        var stillValid = CreateRepository(tokenValidUntil: DateTimeOffset.UtcNow.AddDays(1));
        var disabled = CreateRepository(enabled: false, tokenValidUntil: null);
        var noEndpoint = CreateRepository(tokenEndpoint: null, tokenValidUntil: null);

        _repositoryStore.GetRepositories(null, Arg.Any<CancellationToken>())
            .Returns([eligible, stillValid, disabled, noEndpoint]);
        _tokenService.GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(new ArtifactRepositoryTokenResponse { Token = "mytoken", ValidUntil = DateTime.UtcNow.AddHours(1) });

        // Act
        await _repositoryStore.UpdateRepositoryTokens(_tokenService, _logger, TestContext.Current.CancellationToken);

        // Assert
        await _tokenService.Received(1).GetToken(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        eligible.Password.Should().Be("mytoken");
        stillValid.Password.Should().BeNull();
        disabled.Password.Should().BeNull();
        noEndpoint.Password.Should().BeNull();
    }
}
