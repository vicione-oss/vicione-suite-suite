using Core.OS.Instance.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Core.OS.Tests.Instance.Services;

public sealed class ArtifactRepositoryTokenServiceTests
{
    private readonly Uri _baseAddress = new Uri("https://system.update.ifm");

    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Should_fetch_token_from_api_endpoint()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var tokenUri = CreateTokenUri(_baseAddress);

        // Act
        var base64 = await httpClient.GetStringAsync(tokenUri, TestContext.Current.CancellationToken);

        // Assert
        var clearText = Convert.FromBase64String(base64);
        clearText.Should().NotBeEmpty();
    }

    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Should_decode_token_from_api_endpoint()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddHttpClient()
            .AddSingleton<ArtifactRepositoryTokenService>()
            .BuildServiceProvider();

        var tokenService = services.GetRequiredService<ArtifactRepositoryTokenService>();
        var tokenUri = CreateTokenUri(_baseAddress);

        // Act
        var tokenResponse = await tokenService.GetToken(tokenUri, TestContext.Current.CancellationToken);

        // Assert - {"token":"XXX","valid_until":"2026-11-23T13:00:00Z"}
        tokenResponse.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_reject_insecure_http_token_endpoint()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddHttpClient()
            .AddSingleton<ArtifactRepositoryTokenService>()
            .BuildServiceProvider();

        var tokenService = services.GetRequiredService<ArtifactRepositoryTokenService>();
        var tokenUri = new Uri("http://system.update.ifm/artifactory/vicione-token/token.json");

        // Act
        var act = () => tokenService.GetToken(tokenUri, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not secure*");
    }

    private Uri CreateTokenUri(Uri baseAddress)
    {
        var uriBuilder = new UriBuilder(baseAddress)
        {
            Path = "artifactory/vicione-token/token.json"
        };

        return uriBuilder.Uri;
    }
}
