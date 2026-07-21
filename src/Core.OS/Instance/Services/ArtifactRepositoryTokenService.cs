using System.Security.Cryptography;
using System.Text;
using Core.OS.Instance.Contracts;

namespace Core.OS.Instance.Services;

internal class ArtifactRepositoryTokenService(IHttpClientFactory httpClientFactory) : IArtifactRepositoryTokenService
{
    public async Task<ArtifactRepositoryTokenResponse> GetToken(Uri tokenEndpoint, CancellationToken cancellationToken)
    {
        if (!string.Equals(tokenEndpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Token endpoint '{tokenEndpoint}' is not secure. Artifact repository token endpoints must use HTTPS.");

        using var httpClient = httpClientFactory.CreateClient();
        await using var stream = await httpClient.GetStreamAsync(tokenEndpoint, cancellationToken);

        return await DecodeBase64TokenStream(stream, cancellationToken);
    }

    private static async Task<ArtifactRepositoryTokenResponse> DecodeBase64TokenStream(Stream stream, CancellationToken cancellationToken)
    {
        using var base64Transform = new FromBase64Transform();
        await using var cryptoStream = new CryptoStream(stream, base64Transform, CryptoStreamMode.Read);
        using var reader = new StreamReader(cryptoStream, Encoding.UTF8);
        var decoded = await reader.ReadToEndAsync(cancellationToken);

        return System.Text.Json.JsonSerializer.Deserialize<ArtifactRepositoryTokenResponse>(decoded)
            ?? throw new InvalidOperationException("Failed to deserialize the token response.");
    }
}
