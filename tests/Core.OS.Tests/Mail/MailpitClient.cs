using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Core.OS.Tests.Mail;

/// <summary>
/// Minimal client for the <see href="https://mailpit.axllent.org/docs/api-v1/">mailpit API</see>,
/// used by integration tests to assert on the mail the system under test actually delivered.
/// There is no official .NET client, so this wraps the few endpoints we need over HTTP.
/// </summary>
internal sealed class MailpitClient(Uri baseUri)
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>Returns the most recently received message (full content); throws if none exist.</summary>
    public async Task<MailpitMessage> GetLatestMessage(CancellationToken cancellationToken = default)
        => await HttpClient.GetFromJsonAsync<MailpitMessage>(
               new Uri(baseUri, "api/v1/message/latest"),
               cancellationToken)
           ?? throw new InvalidOperationException("mailpit returned no latest message.");

    /// <summary>
    /// Returns all messages matching mailpit's full-text search <paramref name="query"/>
    /// (subject, body, addresses, ...). Pass a unique token to locate a specific message.
    /// </summary>
    public async Task<IReadOnlyList<MailpitMessage>> SearchMessages(string query,
        CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<MailpitSearchResult>(
            new Uri(baseUri, $"api/v1/search?query={Uri.EscapeDataString(query)}"),
            cancellationToken);
        return result?.Messages ?? [];
    }

    /// <summary>Deletes all stored messages so a test can assert against only the mail it sends.</summary>
    public async Task DeleteAllMessages(CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.DeleteAsync(new Uri(baseUri, "api/v1/messages"), cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>A message as returned by the mailpit API (only the fields the tests assert on).</summary>
internal sealed record MailpitMessage
{
    public MailpitAddress? From { get; init; }

    public IReadOnlyList<MailpitAddress> To { get; init; } = [];

    public string? Subject { get; init; }

    public string? Text { get; init; }

    [JsonPropertyName("HTML")] public string? Html { get; init; }
}

internal sealed record MailpitAddress
{
    public string? Name { get; init; }

    public string? Address { get; init; }
}

/// <summary>The result of a mailpit search query (the matching messages).</summary>
internal sealed record MailpitSearchResult
{
    public IReadOnlyList<MailpitMessage> Messages { get; init; } = [];
}
